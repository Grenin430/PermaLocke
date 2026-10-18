-- Pruebas del 21: registro del organizador, mandar de una vez, notas, versión y la lista de jugadores. Van tras 01-21.
\set ON_ERROR_STOP 1
truncate public.whitelist, public.organizadores, public.runs, public.subidas, public.presencia, public.regalos,
    public.anuncios, public.reglas, public.reinicios, public.admin_registro, public.admin_notas, public.sugerencias cascade;
delete from auth.users;
insert into auth.users values
    ('aaaaaaaa-0000-0000-0000-000000000001', '{"provider_id":"111","full_name":"Orga"}'),
    ('bbbbbbbb-0000-0000-0000-000000000002', '{"provider_id":"222","full_name":"Amigo"}'),
    ('cccccccc-0000-0000-0000-000000000003', '{"provider_id":"333","full_name":"Expulsado"}');
insert into public.whitelist (discord_id, nombre) values ('111','Yo'), ('222','Amigo'), ('444','Nunca entró');
insert into public.organizadores values ('111','Yo');
-- El expulsado tiene run pero ya no está en la lista.
insert into public.runs (run_id, user_id, snapshot, history) values
    ('33333333-3333-3333-3333-333333333333', 'cccccccc-0000-0000-0000-000000000003', '{"points":5,"playerName":"Expulsado","eventCount":2}', '{"events":[]}');

set role authenticated;

-- ---------------------------------------------------------------- EL JUGADOR
select set_config('test.uid','bbbbbbbb-0000-0000-0000-000000000002', false);
select public.subir_eventos('22222222-2222-2222-2222-222222222222',
    '{"eventCount":4,"chainHead":"b4","points":40,"playerName":"Amigo","alive":3,"dead":1,"stagesCleared":2,"roleName":"Normal","playedHours":3.5,"runCreatedAt":"2026-10-01T10:00:00Z"}', -1, '[]');
-- Una subida que retrocede (restaurada): 4 eventos y luego 3.
select public.subir_eventos('22222222-2222-2222-2222-222222222222', '{"eventCount":3,"chainHead":"b3","points":30,"playerName":"Amigo"}', -1, '[]');
insert into public.presencia (nombre, estado) values ('Amigo', 2);
select public.anotar_version('1.0.16');
\echo 'el jugador no puede mandar regalos, ni de uno en uno ni de una vez:'
do $$ begin insert into public.regalos (id, para, regalo) values (gen_random_uuid(), 'todos', '{}'); raise notice 'MAL';
exception when others then raise notice 'bien: %', sqlerrm; end $$;
do $$ begin perform public.mandar_regalos('[{"id":"00000000-0000-0000-0000-000000000009","para":"todos","regalo":{"id":"00000000-0000-0000-0000-000000000009","reason":"x"}}]'); raise notice 'MAL';
exception when others then raise notice 'bien: %', sqlerrm; end $$;
\echo 'ni ver el registro, ni escribir en él, ni las notas, ni la lista de jugadores:'
select count(*) as registro_que_ve from public.admin_registro;
do $$ begin insert into public.admin_registro (accion) values ('falsa'); raise notice 'MAL';
exception when others then raise notice 'bien: %', sqlerrm; end $$;
select count(*) as notas_que_ve from public.admin_notas;
do $$ begin perform public.admin_jugadores(); raise notice 'MAL'; exception when others then raise notice 'bien: %', sqlerrm; end $$;

-- ---------------------------------------------------------------- EL ORGANIZADOR
select set_config('test.uid','aaaaaaaa-0000-0000-0000-000000000001', false);

\echo 'mandar dos de una vez: 2 nuevos; repetir el mismo envío: 0 (no duplica)'
select public.mandar_regalos('[
 {"id":"10000000-0000-0000-0000-000000000001","para":"bbbbbbbb-0000-0000-0000-000000000002","regalo":{"id":"10000000-0000-0000-0000-000000000001","reason":"premio","points":50,"from":"Inventado"}},
 {"id":"10000000-0000-0000-0000-000000000002","para":"bbbbbbbb-0000-0000-0000-000000000002","regalo":{"id":"10000000-0000-0000-0000-000000000002","reason":"fallo","order":{"kind":"revivir","args":{"pokemon":"x"},"summary":"Revivir a Pika"}}}]');
select public.mandar_regalos('[
 {"id":"10000000-0000-0000-0000-000000000001","para":"bbbbbbbb-0000-0000-0000-000000000002","regalo":{"id":"10000000-0000-0000-0000-000000000001","reason":"premio","points":50}}]');
\echo 'uno mal formado (sin motivo) tumba el envío entero: siguen 2'
do $$ begin perform public.mandar_regalos('[
 {"id":"10000000-0000-0000-0000-000000000003","para":"todos","regalo":{"id":"10000000-0000-0000-0000-000000000003","reason":"ok"}},
 {"id":"10000000-0000-0000-0000-000000000004","para":"todos","regalo":{"id":"10000000-0000-0000-0000-000000000004","reason":" "}}]'); raise notice 'MAL';
exception when others then raise notice 'bien: %', sqlerrm; end $$;
select count(*) as regalos, count(*) filter (where por = 'aaaaaaaa-0000-0000-0000-000000000001') as con_por_del_servidor from public.regalos;

\echo 'retirar, suspender, reactivar, anuncio, reglas (dos veces), nota, borrar del buzón'
delete from public.regalos where id = '10000000-0000-0000-0000-000000000001';
update public.whitelist set suspendido = true where discord_id = '222';
update public.whitelist set suspendido = false where discord_id = '222';
insert into public.anuncios (texto) values ('Hola a todos');
insert into public.reglas (fichero, contenido) values ('roles.json', '{"v":1}');
insert into public.reglas (fichero, contenido) values ('roles.json', '{"v":2}')
    on conflict (fichero) do update set contenido = excluded.contenido;
insert into public.admin_notas (discord_id, texto) values ('222', 'Dice que se le cerró Azahar en la Prueba 3');
\echo 'una nota a nombre de otro organizador no entra:'
do $$ begin insert into public.admin_notas (discord_id, texto, por) values ('222', 'x', 'bbbbbbbb-0000-0000-0000-000000000002'); raise notice 'MAL';
exception when others then raise notice 'bien: %', sqlerrm; end $$;

\echo 'EL REGISTRO (del más viejo al más nuevo):'
select por_nombre, accion, jugador is not null as con_jugador, objetivo, motivo, detalle ->> 'antes' as antes
from public.admin_registro order by id;

\echo 'antes de reiniciar: el amigo tiene 1 retroceso (4 eventos y luego 3) y su versión'
select nombre, puntos, retrocesos, version from public.admin_jugadores() where discord_id = '222';

\echo 'borrar del buzón queda apuntado con el texto:'
reset role;
insert into public.sugerencias (jugador, nombre, texto) values ('bbbbbbbb-0000-0000-0000-000000000002', 'Amigo', 'Más Poké Balls');
set role authenticated;
delete from public.sugerencias;
select accion, detalle ->> 'texto' as texto from public.admin_registro where accion = 'buzon:borrar';

\echo 'reiniciar la run del amigo queda apuntado con sus puntos:'
select public.reiniciar_run('bbbbbbbb-0000-0000-0000-000000000002');
select accion, detalle from public.admin_registro where accion like 'run:%';

\echo 'LIMPIEZA: contar no apunta; limpiar apunta UNA línea aunque borre varias cosas'
insert into public.anuncios (texto) values ('otro'), ('y otro');
select count(*) as antes from public.admin_registro;
select public.limpiar(false) is not null as contado;
select public.limpiar(true) is not null as limpiado;
select accion, count(*) from public.admin_registro where id > (select max(id) - 3 from public.admin_registro) group by accion order by accion;

\echo 'LOS JUGADORES: la lista, el que nunca entró y el expulsado con run'
select discord_id, en_lista, suspendido, nombre, estado, version, puntos, archivadas, notas, retrocesos
from public.admin_jugadores() order by discord_id;
