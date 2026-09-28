-- PermaLocke: los motes votados (2026-09-28). Quien captura decide si los demás le ponen el mote; si dice que sí, su app
-- abre aquí una votación con dos tramos: unos segundos para proponer (cada uno escribe el suyo) y otros para votar entre
-- lo propuesto. Al acabar, la app del que capturó pone el ganador en su Pokémon (en el momento si está en el equipo).
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 10 antes. Se puede ejecutar más de una vez.
-- Solo AÑADE dos tablas: no toca nada de lo que usan las apps que ya están jugando.

create table if not exists public.motes (
    id                bigint generated always as identity primary key,
    jugador           uuid not null default auth.uid(),
    nombre            text not null,
    pokemon           text not null,
    especie           int not null,
    forma             int not null default 0,
    shiny             boolean not null default false,
    pid               text not null,
    creado            timestamptz not null default now(),
    propuestas_hasta  timestamptz not null default now() + interval '20 seconds',
    cierra            timestamptz not null default now() + interval '40 seconds',
    aplicado          boolean not null default false,
    ganador           text
);
alter table public.motes enable row level security;
revoke all on public.motes from anon;

drop policy if exists "leer si estas en la lista" on public.motes;
create policy "leer si estas en la lista" on public.motes
    for select to authenticated using (public.permitido() or public.es_organizador());

drop policy if exists "abrir las tuyas" on public.motes;
create policy "abrir las tuyas" on public.motes
    for insert to authenticated with check (jugador = auth.uid() and public.permitido());

drop policy if exists "cerrar las tuyas" on public.motes;
create policy "cerrar las tuyas" on public.motes
    for update to authenticated using (jugador = auth.uid()) with check (jugador = auth.uid());

-- Una fila por jugador y votación: su propuesta (solo en el primer tramo) y su voto (hasta el cierre). Nunca en la tuya.
create table if not exists public.votos_mote (
    mote       bigint not null references public.motes(id) on delete cascade,
    jugador    uuid not null default auth.uid(),
    nombre     text not null,
    propuesta  text check (propuesta is null or char_length(propuesta) between 1 and 12),
    voto       text check (voto is null or char_length(voto) between 1 and 12),
    creado     timestamptz not null default now(),
    primary key (mote, jugador)
);
alter table public.votos_mote enable row level security;
revoke all on public.votos_mote from anon;

drop policy if exists "leer si estas en la lista" on public.votos_mote;
create policy "leer si estas en la lista" on public.votos_mote
    for select to authenticated using (public.permitido() or public.es_organizador());

drop policy if exists "proponer en las abiertas de otros" on public.votos_mote;
create policy "proponer en las abiertas de otros" on public.votos_mote
    for insert to authenticated with check (
        jugador = auth.uid() and public.permitido()
        and exists (select 1 from public.motes m where m.id = mote and m.jugador <> auth.uid() and m.cierra > now()));

drop policy if exists "votar hasta el cierre" on public.votos_mote;
create policy "votar hasta el cierre" on public.votos_mote
    for update to authenticated
    using (jugador = auth.uid())
    with check (jugador = auth.uid()
        and exists (select 1 from public.motes m where m.id = mote and m.jugador <> auth.uid() and m.cierra > now()));

-- El organizador puede anular una votación desde Admin > MOTES.
drop policy if exists "anular si organizas" on public.motes;
create policy "anular si organizas" on public.motes
    for delete to authenticated using (public.es_organizador());

-- LA COLA (2026-09-28): si varios piden votación a la vez, cada una empieza cuando acaba la anterior (con su resultado
-- en pantalla). La hora la pone el servidor, así que todas las apps la ven igual. Tramos: 3 s para que todos la lean,
-- 15 s para proponer, 15 s para votar y 8 s de resultado.
alter table public.motes add column if not exists empieza timestamptz;

create or replace function public.motes_en_cola() returns trigger
language plpgsql security definer set search_path = public as $$
declare
    libre timestamptz;
begin
    perform pg_advisory_xact_lock(20260928);
    select max(cierra) + interval '9 seconds' into libre from public.motes where cierra > now() - interval '9 seconds';
    new.creado := now();
    new.empieza := greatest(now() + interval '3 seconds', coalesce(libre, now()));
    new.propuestas_hasta := new.empieza + interval '15 seconds';
    new.cierra := new.propuestas_hasta + interval '15 seconds';
    return new;
end $$;

drop trigger if exists motes_en_cola on public.motes;
create trigger motes_en_cola before insert on public.motes for each row execute function public.motes_en_cola();

-- UN VOTO Y NO SE CAMBIA: la fila solo se puede tocar mientras no tiene voto (proponer primero y votar después, sí).
drop policy if exists "votar hasta el cierre" on public.votos_mote;
create policy "votar hasta el cierre" on public.votos_mote
    for update to authenticated
    using (jugador = auth.uid() and voto is null)
    with check (jugador = auth.uid()
        and exists (select 1 from public.motes m where m.id = mote and m.jugador <> auth.uid() and m.cierra > now()));
