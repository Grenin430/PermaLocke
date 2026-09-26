-- PermaLocke: subir solo lo nuevo y la LIMPIEZA de Admin (§194, plan del próximo torneo, paso 1).
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 14 antes. Se puede ejecutar más de una vez.
--
-- Solo AÑADE: una tabla (eventos), tres funciones (subir_eventos, limpieza) y una vista (logros_todos). No cambia nada
-- de lo que usan las apps que ya están jugando: siguen subiendo su run entera a runs como siempre, y las vistas
-- ultima_run, logros, amigos y clasificacion siguen igual.
--
-- Cómo sube una app nueva: en vez de reescribir el historial entero en runs.history cada dos minutos, manda solo los
-- eventos que el servidor no tiene. Van a eventos, una fila por evento, y no se reescriben nunca: nada de copias muertas
-- de un JSON grande esperando al vacuum. En runs se queda el resumen (snapshot) de siempre, así que el trigger de
-- subidas sigue anotando cada envío con su número de eventos y su huella, y la AUDITORÍA sigue viendo los retrocesos.
-- runs.history de esas runs se queda en un esqueleto con "enEventos": true.

-- ============================================================================================ LA TABLA

create table if not exists public.eventos (
    run_id   uuid not null,
    n        int not null,        -- posición en la cadena, desde 0
    evento   jsonb not null,      -- el GameEvent tal cual lo guarda la app (camelCase, tipos como texto)
    llegada  timestamptz not null default now(),
    primary key (run_id, n)
);
alter table public.eventos enable row level security;
revoke all on public.eventos from anon;

-- Leer: los de la lista (la actividad de los amigos) y el organizador (la auditoría).
drop policy if exists "leer si estas en la lista" on public.eventos;
create policy "leer si estas en la lista" on public.eventos
    for select to authenticated using (public.permitido() or public.es_organizador());

-- Sin políticas de escribir: solo se entra por subir_eventos(), que comprueba dueño, lista y cadena.

-- Para la actividad: los logros de cada run sin recorrer todos sus eventos.
create index if not exists eventos_logros on public.eventos (run_id)
    where evento ->> 'type' = 'AchievementUnlocked';

-- ============================================================================================ SUBIR

-- Sube el resumen y los eventos nuevos de una run. Devuelve cuántos eventos tiene el servidor después.
--   p_desde: cuántos cree la app que tiene ya el servidor (-1 si no lo sabe: entonces no se añade nada y solo se
--            actualiza el resumen, y la respuesta le dice por dónde seguir).
--   p_eventos: los que faltan, en orden (un array JSON).
-- Solo se añaden si p_desde coincide con lo guardado y el primero sigue la cadena (su previousHash es el hash del
-- último guardado). Si no coincide —una partida restaurada, una cadena reescrita— no se añade ni se borra nada: el
-- resumen se actualiza igual, y la diferencia queda a la vista de la auditoría.
create or replace function public.subir_eventos(p_run uuid, p_snapshot jsonb, p_desde int, p_eventos jsonb)
returns int
language plpgsql
security definer
set search_path = ''
as $$
declare
    dueno     uuid;
    sigue     boolean;
    guardados int;
    ultimo    text;
    viejo     jsonb;
    nueva     boolean := false;
begin
    if not public.permitido() then
        raise exception 'No estás en la lista del torneo';
    end if;

    select r.user_id, r.activa, r.history into dueno, sigue, viejo from public.runs r where r.run_id = p_run;

    if dueno is null then
        -- Una run nueva: la crea como el insert de siempre. El índice una_run_activa sigue impidiendo tener dos.
        insert into public.runs (run_id, user_id, snapshot, history)
        values (p_run, auth.uid(), p_snapshot,
                jsonb_build_object('schema', 1, 'runId', p_run, 'events', '[]'::jsonb, 'enEventos', true));
        nueva := true;
    elsif dueno <> auth.uid() then
        raise exception 'Esa run no es tuya';
    elsif not sigue then
        raise exception 'Esa run está archivada';
    end if;

    select count(*) into guardados from public.eventos e where e.run_id = p_run;

    -- Una run que subía entera (una versión anterior de la app) pasa sus eventos a la tabla la primera vez, tal cual.
    if guardados = 0 and viejo is not null and coalesce((viejo ->> 'enEventos')::boolean, false) = false
       and jsonb_array_length(coalesce(viejo -> 'events', '[]'::jsonb)) > 0 then
        insert into public.eventos (run_id, n, evento)
        select p_run, (t.ord - 1)::int, t.e
        from jsonb_array_elements(viejo -> 'events') with ordinality as t(e, ord);
        get diagnostics guardados = row_count;
    end if;

    if p_desde = guardados and jsonb_typeof(p_eventos) = 'array' and jsonb_array_length(p_eventos) > 0 then
        if guardados > 0 then
            select e.evento ->> 'hash' into ultimo from public.eventos e where e.run_id = p_run and e.n = guardados - 1;
        end if;

        if guardados = 0 or (p_eventos -> 0 ->> 'previousHash') is not distinct from ultimo then
            insert into public.eventos (run_id, n, evento)
            select p_run, guardados + (t.ord - 1)::int, t.e
            from jsonb_array_elements(p_eventos) with ordinality as t(e, ord);
            guardados := guardados + jsonb_array_length(p_eventos);
        end if;
    end if;

    -- El resumen, siempre (una run nueva ya lo lleva): el trigger anotar_subida lo apunta en subidas con su número de
    -- eventos y su huella.
    if nueva then
        return guardados;
    end if;

    update public.runs r
    set snapshot = p_snapshot,
        history = jsonb_build_object('schema', 1, 'runId', p_run, 'events', '[]'::jsonb, 'enEventos', true)
    where r.run_id = p_run;

    return guardados;
end;
$$;

revoke execute on function public.subir_eventos(uuid, jsonb, int, jsonb) from public, anon;
grant execute on function public.subir_eventos(uuid, jsonb, int, jsonb) to authenticated;

-- ============================================================================================ LA ACTIVIDAD

-- Los logros de la última run de cada jugador, estén en runs.history (apps de antes) o en eventos (apps nuevas).
-- La vista logros de siempre no se toca: las apps que están jugando la siguen leyendo.
create or replace view public.logros_todos with (security_invoker = true) as
    select * from (
        select u.user_id,
               coalesce(p.nombre, u.snapshot ->> 'playerName')                    as jugador,
               e -> 'data' ->> 'logro'                                            as logro,
               e -> 'data' ->> 'nombre'                                           as nombre,
               coalesce((e -> 'data' ->> 'puntos')::int, (e ->> 'pointsDelta')::int, 0) as puntos,
               (e ->> 'timestamp')::timestamptz                                   as cuando
        from public.ultima_run u
        left join public.presencia p on p.user_id = u.user_id
        cross join lateral jsonb_array_elements(coalesce(u.history -> 'events', '[]'::jsonb)) e
        where e ->> 'type' = 'AchievementUnlocked'
        union all
        select u.user_id,
               coalesce(p.nombre, u.snapshot ->> 'playerName'),
               v.evento -> 'data' ->> 'logro',
               v.evento -> 'data' ->> 'nombre',
               coalesce((v.evento -> 'data' ->> 'puntos')::int, (v.evento ->> 'pointsDelta')::int, 0),
               (v.evento ->> 'timestamp')::timestamptz
        from public.ultima_run u
        left join public.presencia p on p.user_id = u.user_id
        join public.eventos v on v.run_id = u.run_id and v.evento ->> 'type' = 'AchievementUnlocked'
    ) todos
    order by cuando desc
    limit 60;

-- ============================================================================================ LA LIMPIEZA

-- Lo que se puede quitar sin que nada deje de funcionar, contado (p_ejecutar = false) o quitado (true). Solo el
-- organizador. Devuelve cuántas filas de cada cosa y cuántos bytes ocupan.
--   * fantasmas y lluvias de más de 1 día, salvo la última de cada tabla (la app solo pinta las recientes y lee desde
--     la última que ve).
--   * anuncios, salvo el último (la app solo lee el último).
--   * regalos a una persona, de más de 7 días, que esa persona ya ha recogido en su run activa (la recogida vive en el
--     historial de su run). Los de «todos» nunca: puede llegar alguien que aún no lo tenga.
--   * subidas: por run, se quedan la primera, la última y las de un retroceso con su anterior (menos eventos que la
--     anterior, o los mismos con otra huella). Esas son la prueba y no se tocan.
--   * con p_historiales = true, además: el historial de las runs ARCHIVADAS (runs.history y sus filas de eventos). El
--     resumen se queda. La auditoría de esas runs deja de poder comprobar su cadena, por eso va aparte y se pregunta.
-- Nunca: runs activas, whitelist, organizadores, reglas, reinicios, presencia.
create or replace function public.limpieza(p_ejecutar boolean, p_historiales boolean default false)
returns json
language plpgsql
security definer
set search_path = ''
as $$
declare
    resultado json;
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede limpiar el servidor';
    end if;

    create temporary table if not exists _limpiar (cosa text, id text, bytes bigint) on commit drop;
    truncate _limpiar;

    insert into _limpiar
    select 'fantasmas', f.id::text, pg_catalog.pg_column_size(f.*)
    from public.fantasmas f
    where f.creado < now() - interval '1 day'
      and f.id <> (select max(x.id) from public.fantasmas x);

    insert into _limpiar
    select 'lluvias', l.id::text, pg_catalog.pg_column_size(l.*)
    from public.lluvias l
    where l.creado < now() - interval '1 day'
      and l.id <> (select max(x.id) from public.lluvias x);

    insert into _limpiar
    select 'anuncios', a.id::text, pg_catalog.pg_column_size(a.*)
    from public.anuncios a
    where a.id <> (select max(x.id) from public.anuncios x);

    insert into _limpiar
    select 'regalos', g.id::text, pg_catalog.pg_column_size(g.*)
    from public.regalos g
    join public.runs r on r.activa and r.user_id::text = g.para
    where g.para <> 'todos'
      and g.creado < now() - interval '7 days'
      and (exists (select 1 from jsonb_array_elements(coalesce(r.history -> 'events', '[]'::jsonb)) e
                   where e ->> 'type' = 'AdminGiftClaimed' and e -> 'data' ->> 'regalo' = g.id::text)
           or exists (select 1 from public.eventos v
                      where v.run_id = r.run_id and v.evento ->> 'type' = 'AdminGiftClaimed'
                        and v.evento -> 'data' ->> 'regalo' = g.id::text));

    insert into _limpiar
    select 'subidas', s.id::text, pg_catalog.pg_column_size(s.*)
    from (
        select s.*,
               row_number() over w                       as orden,
               count(*) over (partition by s.run_id)     as total,
               lag(s.eventos) over w                     as antes_eventos,
               lag(s.huella) over w                      as antes_huella,
               lead(s.eventos) over w                    as despues_eventos,
               lead(s.huella) over w                     as despues_huella
        from public.subidas s
        window w as (partition by s.run_id order by s.llegada, s.id)
    ) s
    where s.orden > 1 and s.orden < s.total
      -- esta es un retroceso...
      and not (s.eventos < s.antes_eventos or (s.eventos = s.antes_eventos and s.huella is distinct from s.antes_huella))
      -- ...ni la anterior a uno.
      and not (s.despues_eventos < s.eventos or (s.despues_eventos = s.eventos and s.despues_huella is distinct from s.huella));

    if p_historiales then
        insert into _limpiar
        select 'historiales', r.run_id::text, pg_catalog.pg_column_size(r.history)
        from public.runs r
        where not r.activa
          and (jsonb_array_length(coalesce(r.history -> 'events', '[]'::jsonb)) > 0
               or exists (select 1 from public.eventos v where v.run_id = r.run_id));

        insert into _limpiar
        select 'eventos', v.run_id::text || ':' || v.n::text, pg_catalog.pg_column_size(v.*)
        from public.eventos v
        join public.runs r on r.run_id = v.run_id
        where not r.activa;
    end if;

    select json_build_object(
        'cosas', coalesce(json_object_agg(t.cosa, json_build_object('filas', t.filas, 'bytes', t.bytes)), '{}'::json),
        'bytes', coalesce(sum(t.bytes), 0))
    into resultado
    from (select l.cosa, count(*) as filas, sum(l.bytes) as bytes from _limpiar l group by l.cosa) t;

    if p_ejecutar then
        delete from public.fantasmas f using _limpiar l where l.cosa = 'fantasmas' and l.id = f.id::text;
        delete from public.lluvias x using _limpiar l where l.cosa = 'lluvias' and l.id = x.id::text;
        delete from public.anuncios a using _limpiar l where l.cosa = 'anuncios' and l.id = a.id::text;
        delete from public.regalos g using _limpiar l where l.cosa = 'regalos' and l.id = g.id::text;
        delete from public.subidas s using _limpiar l where l.cosa = 'subidas' and l.id = s.id::text;

        if p_historiales then
            delete from public.eventos v using public.runs r
            where r.run_id = v.run_id and not r.activa;

            -- Sin disparar anotar_subida: vaciar un historial archivado no es una subida del jugador.
            alter table public.runs disable trigger anotar_subida;
            update public.runs r
            set history = jsonb_build_object('schema', 1, 'runId', r.run_id, 'events', '[]'::jsonb, 'vaciado', true)
            from _limpiar l
            where l.cosa = 'historiales' and l.id = r.run_id::text;
            alter table public.runs enable trigger anotar_subida;
        end if;
    end if;

    return resultado;
end;
$$;

revoke execute on function public.limpieza(boolean, boolean) from public, anon;
grant execute on function public.limpieza(boolean, boolean) to authenticated;
