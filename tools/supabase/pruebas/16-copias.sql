-- Pruebas del 16: el bucket de copias, quién sube, quién lee y las que sobran. Van tras aplicar 01-16.
\set ON_ERROR_STOP 1
insert into auth.users values ('aaaaaaaa-0000-0000-0000-000000000001', '{"provider_id":"111"}'), ('bbbbbbbb-0000-0000-0000-000000000002', '{"provider_id":"222"}')
on conflict do nothing;
insert into public.whitelist (discord_id, nombre) values ('111','Yo'), ('222','Amigo') on conflict do nothing;
insert into public.organizadores values ('111','Yo') on conflict do nothing;
select id, public from storage.buckets where id = 'copias';

set role authenticated;
select set_config('test.uid','bbbbbbbb-0000-0000-0000-000000000002', false);
\echo 'el amigo sube 7 copias a su carpeta'
insert into storage.objects (bucket_id, name, metadata, created_at)
select 'copias', 'bbbbbbbb-0000-0000-0000-000000000002/2026092' || g || '.zip', jsonb_build_object('size', 1000 * g), now() - (g || ' hours')::interval
from generate_series(1, 7) g;
\echo 'y no puede subir a la de otro:'
do $$ begin insert into storage.objects (bucket_id, name) values ('copias', 'aaaaaaaa-0000-0000-0000-000000000001/x.zip'); raise notice 'MAL';
exception when others then raise notice 'bien: %', sqlerrm; end $$;
\echo 'ni borrar las suyas:'
delete from storage.objects where bucket_id = 'copias';
select count(*) as siguen from storage.objects;
\echo 'ni ver las que sobran:'
do $$ begin perform * from public.copias_sobrantes(); raise notice 'MAL'; exception when others then raise notice 'bien: %', sqlerrm; end $$;

select set_config('test.uid','aaaaaaaa-0000-0000-0000-000000000001', false);
\echo 'el organizador ve las 7 y sobran las 2 más viejas'
select count(*) as ve from storage.objects;
select nombre, bytes from public.copias_sobrantes(5);
