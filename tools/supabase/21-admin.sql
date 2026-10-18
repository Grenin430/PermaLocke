-- PermaLocke: el centro de control de Admin (2026-10-08). Registro de lo que hace el organizador, envíos de una vez, notas
-- sobre los jugadores, la lista de jugadores completa y la versión de la app de cada uno.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 20 antes. Se puede ejecutar más de una vez.
--
-- Solo AÑADE: dos tablas (admin_registro, admin_notas), una columna en regalos (por) y otra en presencia (version),
-- disparadores que apuntan en el registro y funciones nuevas. No cambia ni quita nada de lo que usan las apps que ya
-- están jugando: un Admin o una app de antes siguen funcionando igual, y lo que hagan también queda registrado.
--
-- Lo que hay en el registro empieza el día que se ejecuta esto: lo de antes no se apuntó y no se inventa.

-- ============================================================================================ EL REGISTRO
-- Una fila por cada cosa que cambia el organizador. Quién lo pone el servidor (auth.uid()), no Admin. Nadie la
-- cambia ni la borra desde fuera: no hay políticas de escribir y solo entran los disparadores de abajo.
create table if not exists public.admin_registro (
    id          bigint generated always as identity primary key,
    cuando      timestamptz not null default now(),
    por         uuid,
    por_nombre  text,
    accion      text not null,
    jugador     uuid,        -- la cuenta del jugador afectado, si la hay
    objetivo    text,        -- lo que se tocó: id del regalo, discord_id, fichero de reglas...
    motivo      text,
    detalle     jsonb not null default '{}'::jsonb
);
alter table public.admin_registro enable row level security;
revoke all on public.admin_registro from anon;
revoke insert, update, delete, truncate on public.admin_registro from authenticated;
create index if not exists admin_registro_cuando on public.admin_registro (cuando desc);
create index if not exists admin_registro_jugador on public.admin_registro (jugador, cuando desc) where jugador is not null;

drop policy if exists "leer si organizas" on public.admin_registro;
create policy "leer si organizas" on public.admin_registro
    for select to authenticated using (public.es_organizador());

-- La cuenta de un discord_id, si ha entrado alguna vez.
create or replace function public.cuenta_de(p_discord text)
returns uuid
language sql
stable
security definer
set search_path = ''
as $$
    select u.id from auth.users u where u.raw_user_meta_data ->> 'provider_id' = p_discord limit 1;
$$;
revoke execute on function public.cuenta_de(text) from public, anon, authenticated;

-- Apunta una acción. Solo la llaman los disparadores y funciones de aquí (no se da a nadie).
create or replace function public.anotar_admin(p_accion text, p_jugador uuid, p_objetivo text, p_motivo text, p_detalle jsonb)
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
    -- La LIMPIEZA se apunta una vez entera, no fila a fila.
    if coalesce(current_setting('permalocke.limpiando', true), '') = 'si' then
        return;
    end if;

    insert into public.admin_registro (por, por_nombre, accion, jugador, objetivo, motivo, detalle)
    values (auth.uid(),
            (select coalesce(o.nombre, u.raw_user_meta_data ->> 'full_name', u.raw_user_meta_data ->> 'name')
             from auth.users u left join public.organizadores o on o.discord_id = u.raw_user_meta_data ->> 'provider_id'
             where u.id = auth.uid()),
            p_accion, p_jugador, p_objetivo, nullif(p_motivo, ''), coalesce(p_detalle, '{}'::jsonb));
end;
$$;
revoke execute on function public.anotar_admin(text, uuid, text, text, jsonb) from public, anon, authenticated;

-- ============================================================================================ LOS DISPARADORES
-- REGALOS: mandar (regalo, ajuste u orden) y retirar. El quién de verdad va en la columna por.
alter table public.regalos add column if not exists por uuid default auth.uid();
create index if not exists regalos_para on public.regalos (para, creado desc);

create or replace function public.registrar_regalo()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    fila public.regalos;
    tipo text;
begin
    fila := case when tg_op = 'DELETE' then old else new end;
    tipo := case
        when fila.regalo ? 'order' and jsonb_typeof(fila.regalo -> 'order') = 'object' then 'orden:' || (fila.regalo -> 'order' ->> 'kind')
        when coalesce((fila.regalo ->> 'adjustment')::boolean, false) then 'ajuste'
        else 'regalo' end;

    perform public.anotar_admin(
        case when tg_op = 'DELETE' then 'retirar:' || tipo else tipo end,
        case when fila.para ~* '^[0-9a-f-]{36}$' then fila.para::uuid end,
        fila.id::text,
        fila.regalo ->> 'reason',
        jsonb_strip_nulls(jsonb_build_object(
            'para', fila.para,
            'resumen', fila.regalo -> 'order' ->> 'summary',
            'orden', fila.regalo -> 'order' -> 'args',
            'puntos', nullif((fila.regalo ->> 'points')::int, 0),
            'tiradas', nullif(fila.regalo -> 'rolls', '{}'::jsonb),
            'wonderTrades', nullif((fila.regalo ->> 'wonderTrades')::int, 0))));
    return null;
end;
$$;
drop trigger if exists registrar_regalo on public.regalos;
create trigger registrar_regalo after insert or delete on public.regalos
    for each row when (public.es_organizador()) execute function public.registrar_regalo();

-- LISTA: altas, bajas, suspender, reactivar y cambios de nombre.
create or replace function public.registrar_lista()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    accion text;
    discord text;
begin
    discord := case when tg_op = 'DELETE' then old.discord_id else new.discord_id end;
    accion := case
        when tg_op = 'INSERT' then 'lista:alta'
        when tg_op = 'DELETE' then 'lista:baja'
        when new.suspendido and not old.suspendido then 'lista:suspender'
        when old.suspendido and not new.suspendido then 'lista:reactivar'
        else 'lista:cambio' end;

    perform public.anotar_admin(accion, public.cuenta_de(discord), discord, null,
        jsonb_strip_nulls(jsonb_build_object(
            'antes', case when tg_op <> 'INSERT' then jsonb_build_object('nombre', old.nombre, 'suspendido', old.suspendido) end,
            'despues', case when tg_op <> 'DELETE' then jsonb_build_object('nombre', new.nombre, 'suspendido', new.suspendido) end)));
    return null;
end;
$$;
drop trigger if exists registrar_lista on public.whitelist;
create trigger registrar_lista after insert or update or delete on public.whitelist
    for each row when (public.es_organizador()) execute function public.registrar_lista();

-- ANUNCIOS: publicar y retirar, con su texto.
create or replace function public.registrar_anuncio()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    if tg_op = 'DELETE' then
        perform public.anotar_admin('anuncio:retirar', null, old.id::text, null, jsonb_build_object('texto', old.texto));
    else
        perform public.anotar_admin('anuncio:publicar', null, new.id::text, null, jsonb_build_object('texto', new.texto));
    end if;
    return null;
end;
$$;
drop trigger if exists registrar_anuncio on public.anuncios;
create trigger registrar_anuncio after insert or delete on public.anuncios
    for each row when (public.es_organizador()) execute function public.registrar_anuncio();

-- REGLAS: publicar, cambiar y quitar. Guarda el contenido de antes, para poder volver a él desde Admin.
create or replace function public.registrar_reglas()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    perform public.anotar_admin(
        case tg_op when 'INSERT' then 'reglas:publicar' when 'UPDATE' then 'reglas:cambiar' else 'reglas:quitar' end,
        null,
        case when tg_op = 'DELETE' then old.fichero else new.fichero end,
        null,
        jsonb_strip_nulls(jsonb_build_object(
            'antes', case when tg_op <> 'INSERT' then old.contenido end,
            'bytesAntes', case when tg_op <> 'INSERT' then octet_length(old.contenido) end,
            'bytesDespues', case when tg_op <> 'DELETE' then octet_length(new.contenido) end)));
    return null;
end;
$$;
drop trigger if exists registrar_reglas on public.reglas;
create trigger registrar_reglas after insert or update or delete on public.reglas
    for each row when (public.es_organizador()) execute function public.registrar_reglas();

-- MOTES y BUZÓN: lo que el organizador anula o borra (lo que crean los jugadores no se apunta).
create or replace function public.registrar_borrado_comunidad()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    if tg_table_name = 'motes' then
        perform public.anotar_admin('mote:anular', old.jugador, old.id::text, null,
            jsonb_build_object('pokemon', old.pokemon, 'jugadorNombre', old.nombre, 'ganador', old.ganador));
    else
        perform public.anotar_admin('buzon:borrar', old.jugador, old.id::text, null,
            jsonb_build_object('texto', old.texto, 'jugadorNombre', old.nombre));
    end if;
    return null;
end;
$$;
drop trigger if exists registrar_borrado on public.motes;
create trigger registrar_borrado after delete on public.motes
    for each row when (public.es_organizador() and old.jugador is distinct from auth.uid())
    execute function public.registrar_borrado_comunidad();
drop trigger if exists registrar_borrado on public.sugerencias;
create trigger registrar_borrado after delete on public.sugerencias
    for each row when (public.es_organizador()) execute function public.registrar_borrado_comunidad();

-- RUNS: reiniciar y deshacer un reinicio (ya tenían su tabla, reinicios; aquí entran en el mismo registro).
create or replace function public.registrar_reinicio()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    perform public.anotar_admin('run:' || new.accion, new.user_id, new.run_id::text, null,
        (select jsonb_build_object('puntos', (r.snapshot ->> 'points')::int, 'eventos', (r.snapshot ->> 'eventCount')::int)
         from public.runs r where r.run_id = new.run_id));
    return null;
end;
$$;
drop trigger if exists registrar_reinicio on public.reinicios;
create trigger registrar_reinicio after insert on public.reinicios
    for each row execute function public.registrar_reinicio();

-- ============================================================================================ LIMPIEZA, APUNTADA
-- La misma limpieza() de siempre, pero cuando de verdad borra queda una sola línea con lo que quitó.
create or replace function public.limpiar(p_ejecutar boolean, p_historiales boolean default false)
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

    perform set_config('permalocke.limpiando', 'si', true);
    resultado := public.limpieza(p_ejecutar, p_historiales);
    perform set_config('permalocke.limpiando', '', true);

    if p_ejecutar then
        perform public.anotar_admin('servidor:limpieza', null, null, null,
            jsonb_build_object('historiales', p_historiales, 'resultado', resultado::jsonb));
    end if;
    return resultado;
end;
$$;
revoke execute on function public.limpiar(boolean, boolean) from public, anon;
grant execute on function public.limpiar(boolean, boolean) to authenticated;

-- ============================================================================================ MANDAR DE UNA VEZ
-- Varios regalos, ajustes u órdenes en una sola transacción: o entran todos o ninguno. Cada uno trae su id, así que
-- repetir el envío (un reintento tras un corte) no duplica nada: los que ya estaban se saltan.
-- p_regalos: [{"id": uuid, "para": "todos" | uuid, "regalo": {...}}, ...]. Devuelve cuántos entraron nuevos.
create or replace function public.mandar_regalos(p_regalos jsonb)
returns int
language plpgsql
security definer
set search_path = ''
as $$
declare
    nuevos int;
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede mandar regalos';
    end if;
    if jsonb_typeof(p_regalos) <> 'array' or jsonb_array_length(p_regalos) = 0 then
        raise exception 'No hay nada que mandar';
    end if;
    if jsonb_array_length(p_regalos) > 200 then
        raise exception 'Demasiados de una vez (máximo 200)';
    end if;
    if exists (select 1 from jsonb_array_elements(p_regalos) r
               where (r ->> 'id') is null
                  or not ((r ->> 'para') = 'todos' or (r ->> 'para') ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')
                  or jsonb_typeof(r -> 'regalo') <> 'object'
                  or (r -> 'regalo' ->> 'id') is distinct from (r ->> 'id')
                  or length(trim(coalesce(r -> 'regalo' ->> 'reason', ''))) = 0) then
        raise exception 'Hay un envío mal formado (id, destinatario o motivo)';
    end if;

    insert into public.regalos (id, para, regalo, por)
    select (r ->> 'id')::uuid, r ->> 'para', r -> 'regalo', auth.uid()
    from jsonb_array_elements(p_regalos) r
    on conflict (id) do nothing;
    get diagnostics nuevos = row_count;
    return nuevos;
end;
$$;
revoke execute on function public.mandar_regalos(jsonb) from public, anon;
grant execute on function public.mandar_regalos(jsonb) to authenticated;

-- ============================================================================================ NOTAS
-- Notas internas del organizador sobre un jugador (una incidencia, lo que se habló). Van por discord_id para valer
-- también con quien aún no ha entrado nunca. Los jugadores no las ven.
create table if not exists public.admin_notas (
    id          bigint generated always as identity primary key,
    discord_id  text not null,
    texto       text not null check (char_length(texto) between 1 and 2000),
    por         uuid not null default auth.uid(),
    por_nombre  text,
    creado      timestamptz not null default now()
);
alter table public.admin_notas enable row level security;
revoke all on public.admin_notas from anon;
create index if not exists admin_notas_discord on public.admin_notas (discord_id, creado desc);

drop policy if exists "leer si organizas" on public.admin_notas;
create policy "leer si organizas" on public.admin_notas
    for select to authenticated using (public.es_organizador());
drop policy if exists "escribir si organizas" on public.admin_notas;
create policy "escribir si organizas" on public.admin_notas
    for insert to authenticated with check (public.es_organizador() and por = auth.uid());
drop policy if exists "borrar si organizas" on public.admin_notas;
create policy "borrar si organizas" on public.admin_notas
    for delete to authenticated using (public.es_organizador());

create or replace function public.registrar_nota()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    perform public.anotar_admin(case when tg_op = 'DELETE' then 'nota:borrar' else 'nota:escribir' end,
        public.cuenta_de(case when tg_op = 'DELETE' then old.discord_id else new.discord_id end),
        case when tg_op = 'DELETE' then old.discord_id else new.discord_id end, null,
        jsonb_build_object('texto', case when tg_op = 'DELETE' then old.texto else new.texto end));
    return null;
end;
$$;
drop trigger if exists registrar_nota on public.admin_notas;
create trigger registrar_nota after insert or delete on public.admin_notas
    for each row when (public.es_organizador()) execute function public.registrar_nota();

-- ============================================================================================ LA VERSIÓN DE CADA APP
-- La app la anota junto a su presencia desde la 1.0.16. Antes no se guardaba: null = no se sabe.
alter table public.presencia add column if not exists version text;

create or replace function public.anotar_version(p_version text)
returns void
language sql
security definer
set search_path = ''
as $$
    update public.presencia set version = left(p_version, 32) where user_id = auth.uid() and public.permitido();
$$;
revoke execute on function public.anotar_version(text) from public, anon;
grant execute on function public.anotar_version(text) to authenticated;

-- ============================================================================================ LOS JUGADORES
-- Todos: los de la lista (hayan entrado o no) y los que tienen run sin estar ya en la lista. Una fila cada uno, con su
-- run activa, su presencia y lo que pide atención. Solo el organizador.
create or replace function public.admin_jugadores()
returns table (
    discord_id     text,
    en_lista       boolean,
    suspendido     boolean,
    alta           timestamptz,
    user_id        uuid,
    nombre         text,
    avatar_url     text,
    estado         int,
    latido         timestamptz,
    version        text,
    run_id         uuid,
    subida         timestamptz,
    creada         timestamptz,
    puntos         int,
    vivos          int,
    caidos         int,
    capturados     int,
    etapas         int,
    nivel_maximo   int,
    logros         int,
    logros_total   int,
    rol            text,
    horas          double precision,
    eventos        int,
    archivadas     int,
    notas          int,
    retrocesos     int
)
language plpgsql
stable
security definer
set search_path = ''
as $$
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede ver la lista de jugadores';
    end if;

    return query
    with cuentas as (
        select u.id, u.raw_user_meta_data ->> 'provider_id' as discord,
               coalesce(u.raw_user_meta_data ->> 'full_name', u.raw_user_meta_data ->> 'name') as nombre_discord
        from auth.users u
    ),
    personas as (
        select w.discord_id as discord, true as en_lista, w.suspendido, w.alta, w.nombre as nombre_lista
        from public.whitelist w
        union all
        select c.discord, false, false, null::timestamptz, null::text
        from cuentas c
        where exists (select 1 from public.runs r where r.user_id = c.id)
          and not exists (select 1 from public.whitelist w where w.discord_id = c.discord)
    ),
    activa as (
        select distinct on (r.user_id) r.user_id, r.run_id, r.snapshot, r.subida
        from public.runs r where r.activa
        order by r.user_id, r.subida desc
    ),
    -- Retrocesos de la run activa: subidas con menos eventos que la anterior, o los mismos con otra huella.
    vueltas as (
        select s.run_id, count(*)::int as n
        from (select s.run_id, s.eventos, s.huella,
                     lag(s.eventos) over w as antes_eventos, lag(s.huella) over w as antes_huella
              from public.subidas s
              where s.run_id in (select a.run_id from activa a)
              window w as (partition by s.run_id order by s.llegada, s.id)) s
        where s.eventos < s.antes_eventos or (s.eventos = s.antes_eventos and s.huella is distinct from s.antes_huella)
        group by s.run_id
    )
    select pe.discord,
           pe.en_lista,
           pe.suspendido,
           pe.alta,
           c.id,
           coalesce(p.nombre, a.snapshot ->> 'playerName', pe.nombre_lista, c.nombre_discord, pe.discord),
           p.avatar_url,
           case when p.latido > now() - interval '3 minutes' then p.estado else 0 end,
           p.latido,
           p.version,
           a.run_id,
           a.subida,
           (a.snapshot ->> 'runCreatedAt')::timestamptz,
           (a.snapshot ->> 'points')::int,
           (a.snapshot ->> 'alive')::int,
           (a.snapshot ->> 'dead')::int,
           (a.snapshot ->> 'registered')::int,
           (a.snapshot ->> 'stagesCleared')::int,
           (a.snapshot ->> 'levelCap')::int,
           (a.snapshot ->> 'achievementsUnlocked')::int,
           (a.snapshot ->> 'achievementsTotal')::int,
           nullif(a.snapshot ->> 'roleName', ''),
           (a.snapshot ->> 'playedHours')::double precision,
           (a.snapshot ->> 'eventCount')::int,
           (select count(*)::int from public.runs r where r.user_id = c.id and not r.activa),
           (select count(*)::int from public.admin_notas n where n.discord_id = pe.discord),
           coalesce(v.n, 0)
    from personas pe
    left join cuentas c on c.discord = pe.discord
    left join public.presencia p on p.user_id = c.id
    left join activa a on a.user_id = c.id
    left join vueltas v on v.run_id = a.run_id;
end;
$$;
revoke execute on function public.admin_jugadores() from public, anon;
grant execute on function public.admin_jugadores() to authenticated;
