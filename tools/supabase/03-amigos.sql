-- PermaLocke: amigos, logros compartidos y clasificación. Se pega entero en Supabase > SQL Editor > Run.
-- Necesita 01-whitelist.sql y 02-runs.sql antes. Se puede ejecutar más de una vez sin romper nada.

-- La presencia: una fila por jugador, que su app renueva cada 45 s. La hora la pone el servidor, no el PC
-- del jugador, así un reloj mal puesto no deja a nadie "jugando" para siempre.
create table if not exists public.presencia (
    user_id        uuid primary key default auth.uid() references auth.users (id) on delete cascade,
    nombre         text not null,
    estado         int  not null default 0,   -- 0 desconectado, 1 en la app, 2 jugando
    jugando_desde  timestamptz,
    latido         timestamptz not null default now()
);
alter table public.presencia enable row level security;

drop policy if exists "leer si estas en la lista" on public.presencia;
create policy "leer si estas en la lista" on public.presencia
    for select to authenticated using (public.permitido());

drop policy if exists "escribir la tuya" on public.presencia;
create policy "escribir la tuya" on public.presencia
    for insert to authenticated with check (user_id = auth.uid() and public.permitido());

drop policy if exists "actualizar la tuya" on public.presencia;
create policy "actualizar la tuya" on public.presencia
    for update to authenticated using (user_id = auth.uid() and public.permitido()) with check (user_id = auth.uid());

create or replace function public.sellar_latido()
returns trigger language plpgsql set search_path = '' as $$
begin
    new.latido := now();
    return new;
end;
$$;

drop trigger if exists sellar_latido on public.presencia;
create trigger sellar_latido before insert or update on public.presencia
    for each row execute function public.sellar_latido();

revoke all on public.presencia from anon;

-- La última run subida de cada jugador (una por jugador: la más reciente).
create or replace view public.ultima_run with (security_invoker = true) as
    select distinct on (r.user_id) r.user_id, r.run_id, r.snapshot, r.history
    from public.runs r
    order by r.user_id, r.subida desc;

-- Amigos: presencia con su antigüedad medida por el reloj del servidor, y el avatar Pokémon de su run.
create or replace view public.amigos with (security_invoker = true) as
    select p.user_id,
           p.nombre,
           p.estado,
           extract(epoch from now() - p.latido)::int                          as segundos,
           extract(epoch from now() - p.jugando_desde)::int                   as jugando_segundos,
           (u.snapshot ->> 'avatarSpecies')::int                              as avatar,
           p.user_id = auth.uid()                                             as es_mio
    from public.presencia p
    left join public.ultima_run u on u.user_id = p.user_id;

-- Logros compartidos: los que hay en el historial subido de cada run, los más recientes primero.
create or replace view public.logros with (security_invoker = true) as
    select u.user_id,
           coalesce(p.nombre, u.snapshot ->> 'playerName')                    as jugador,
           e -> 'data' ->> 'logro'                                            as logro,
           e -> 'data' ->> 'nombre'                                           as nombre,
           coalesce((e -> 'data' ->> 'puntos')::int, (e ->> 'pointsDelta')::int, 0) as puntos,
           (e ->> 'timestamp')::timestamptz                                   as cuando
    from public.ultima_run u
    left join public.presencia p on p.user_id = u.user_id
    cross join lateral jsonb_array_elements(u.history -> 'events') e
    where e ->> 'type' = 'AchievementUnlocked'
    order by cuando desc
    limit 60;

-- Clasificación: los puntos de la última run de cada jugador.
create or replace view public.clasificacion with (security_invoker = true) as
    select u.user_id,
           coalesce(p.nombre, u.snapshot ->> 'playerName')                    as jugador,
           (u.snapshot ->> 'points')::int                                     as puntos,
           (u.snapshot ->> 'avatarSpecies')::int                              as avatar,
           u.user_id = auth.uid()                                             as es_mio
    from public.ultima_run u
    left join public.presencia p on p.user_id = u.user_id
    order by puntos desc;

revoke all on public.ultima_run, public.amigos, public.logros, public.clasificacion from anon;
