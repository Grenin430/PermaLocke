-- PermaLocke: la clasificación con más datos de cada run (equipo, caídos, etapas, logros) y si está conectado.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 05 antes. Se puede ejecutar más de una vez.

create or replace view public.clasificacion with (security_invoker = true) as
    select u.user_id,
           coalesce(p.nombre, u.snapshot ->> 'playerName')                    as jugador,
           (u.snapshot ->> 'points')::int                                     as puntos,
           (u.snapshot ->> 'avatarSpecies')::int                              as avatar,
           u.user_id = auth.uid()                                             as es_mio,
           p.avatar_url,
           (u.snapshot ->> 'registered')::int                                 as capturados,
           (u.snapshot ->> 'alive')::int                                      as vivos,
           (u.snapshot ->> 'dead')::int                                       as caidos,
           (u.snapshot ->> 'stagesCleared')::int                              as etapas,
           (u.snapshot ->> 'levelCap')::int                                   as nivel_maximo,
           (u.snapshot ->> 'achievementsUnlocked')::int                       as logros,
           (u.snapshot ->> 'achievementsTotal')::int                          as logros_total,
           case when p.latido > now() - interval '3 minutes' then p.estado else 0 end as estado
    from public.ultima_run u
    left join public.presencia p on p.user_id = u.user_id
    order by puntos desc;

revoke all on public.clasificacion from anon;
