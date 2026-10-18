-- Pruebas del 17: el bucket de informes, quién sube, quién lee y los viejos. Van tras aplicar 01-17 (y las del 16).
\set ON_ERROR_STOP 1
select id, public, file_size_limit from storage.buckets where id = 'informes';

set role authenticated;
select set_config('test.uid','bbbbbbbb-0000-0000-0000-000000000002', false);
\echo 'el amigo sube dos informes a su carpeta, uno de hace 40 días'
insert into storage.objects (bucket_id, name, metadata, created_at) values
    ('informes', 'bbbbbbbb-0000-0000-0000-000000000002/cierre-azahar-viejo.zip', '{"size": 900}', now() - interval '40 days'),
    ('informes', 'bbbbbbbb-0000-0000-0000-000000000002/cierre-azahar-nuevo.zip', '{"size": 800}', now() - interval '1 hour');
\echo 'no puede subir a la carpeta de otro:'
do $$ begin insert into storage.objects (bucket_id, name) values ('informes', 'aaaaaaaa-0000-0000-0000-000000000001/x.zip'); raise notice 'MAL';
exception when others then raise notice 'bien: %', sqlerrm; end $$;
\echo 'no ve los informes (ni los suyos) y no puede listarlos:'
select count(*) as ve_informes from storage.objects where bucket_id = 'informes';
do $$ begin perform * from public.informes_lista(); raise notice 'MAL'; exception when others then raise notice 'bien: %', sqlerrm; end $$;
do $$ begin perform * from public.informes_viejos(); raise notice 'MAL'; exception when others then raise notice 'bien: %', sqlerrm; end $$;

select set_config('test.uid','aaaaaaaa-0000-0000-0000-000000000001', false);
\echo 'el organizador los lista (el nuevo primero) y sobra solo el de hace 40 días'
select nombre, jugador, bytes from public.informes_lista();
select nombre, bytes from public.informes_viejos(30);
