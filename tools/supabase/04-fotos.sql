-- PermaLocke: la foto de Discord de cada jugador en amigos, logros y clasificación.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01, 02 y 03 antes. Se puede ejecutar más de una vez.

alter table public.presencia add column if not exists avatar_url text;

-- Las vistas se rehacen con la foto al final (añadir columnas al final es lo que create or replace permite).
create or replace view public.amigos with (security_invoker = true) as
    select p.user_id,
           p.nombre,
           p.estado,
           extract(epoch from now() - p.latido)::int                          as segundos,
           extract(epoch from now() - p.jugando_desde)::int                   as jugando_segundos,
           (u.snapshot ->> 'avatarSpecies')::int                              as avatar,
           p.user_id = auth.uid()                                             as es_mio,
           p.avatar_url
    from public.presencia p
    left join public.ultima_run u on u.user_id = p.user_id;

create or replace view public.clasificacion with (security_invoker = true) as
    select u.user_id,
           coalesce(p.nombre, u.snapshot ->> 'playerName')                    as jugador,
           (u.snapshot ->> 'points')::int                                     as puntos,
           (u.snapshot ->> 'avatarSpecies')::int                              as avatar,
           u.user_id = auth.uid()                                             as es_mio,
           p.avatar_url
    from public.ultima_run u
    left join public.presencia p on p.user_id = u.user_id
    order by puntos desc;

revoke all on public.amigos, public.clasificacion from anon;
