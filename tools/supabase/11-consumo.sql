-- PermaLocke: lo que ocupa el torneo en el servidor, para el panel CONSUMO de Admin.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 10 antes. Se puede ejecutar más de una vez.
--
-- Solo mide la base de datos (el límite de 500 MB del plan gratis). Lo descargado en el mes (5 GB) no se puede leer
-- desde aquí: está en el panel de Supabase, Project Settings > Usage.

create or replace function public.consumo()
returns json
language plpgsql
stable
security definer
set search_path = ''
as $$
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede ver el consumo';
    end if;

    return json_build_object(
        'baseDeDatos', pg_catalog.pg_database_size(pg_catalog.current_database()),
        'tablas', (
            select coalesce(json_agg(t order by t.bytes desc), '[]'::json)
            from (
                select c.relname as nombre,
                       greatest(c.reltuples, 0)::bigint as filas,  -- estimado por Postgres; exacto tras su limpieza automatica
                       pg_catalog.pg_total_relation_size(c.oid) as bytes
                from pg_catalog.pg_class c
                join pg_catalog.pg_namespace n on n.oid = c.relnamespace
                where n.nspname = 'public' and c.relkind = 'r'
            ) t
        ),
        'runs', (
            select coalesce(json_agg(r order by r.bytes desc), '[]'::json)
            from (
                select coalesce(p.nombre, u.snapshot ->> 'playerName') as jugador,
                       u.activa,
                       jsonb_array_length(coalesce(u.history -> 'events', '[]'::jsonb)) as eventos,
                       pg_catalog.pg_column_size(u.snapshot) + pg_catalog.pg_column_size(u.history) as bytes
                from public.runs u
                left join public.presencia p on p.user_id = u.user_id
            ) r
        )
    );
end;
$$;

revoke execute on function public.consumo() from public, anon;
grant execute on function public.consumo() to authenticated;
