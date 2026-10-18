-- Pruebas del 15: subir solo lo nuevo, la cadena, los retrocesos, los permisos y la LIMPIEZA. Van tras aplicar 01-15.
\set ON_ERROR_STOP 1
insert into auth.users values ('aaaaaaaa-0000-0000-0000-000000000001', '{"provider_id":"111"}'), ('bbbbbbbb-0000-0000-0000-000000000002', '{"provider_id":"222"}');
insert into public.whitelist (discord_id, nombre) values ('111','Yo'), ('222','Amigo');
insert into public.organizadores values ('111','Yo');

set role authenticated;
select set_config('test.uid','aaaaaaaa-0000-0000-0000-000000000001', false);

-- Una run subida a la antigua, con 3 eventos (uno es un logro).
insert into public.runs (run_id, snapshot, history) values ('11111111-1111-1111-1111-111111111111',
 '{"eventCount":3,"chainHead":"h3","points":10,"playerName":"Yo"}',
 '{"events":[{"hash":"h1","previousHash":"","type":"RunCreated","timestamp":"2026-09-26T10:00:00Z"},
             {"hash":"h2","previousHash":"h1","type":"AchievementUnlocked","data":{"logro":"x","nombre":"Primera","puntos":"10"},"timestamp":"2026-09-26T10:01:00Z"},
             {"hash":"h3","previousHash":"h2","type":"PointsAdjusted","timestamp":"2026-09-26T10:02:00Z"}]}');

\echo 'sin saber por donde va: pasa los 3 a eventos y responde 3'
select public.subir_eventos('11111111-1111-1111-1111-111111111111', '{"eventCount":3,"chainHead":"h3","points":10,"playerName":"Yo"}', -1, '[]');
select jsonb_array_length(history->'events') as en_history, history->>'enEventos' as en_eventos from public.runs;

\echo 'dos nuevos que siguen la cadena: 5'
select public.subir_eventos('11111111-1111-1111-1111-111111111111', '{"eventCount":5,"chainHead":"h5","points":10,"playerName":"Yo"}', 3,
 '[{"hash":"h4","previousHash":"h3","type":"X","timestamp":"2026-09-26T10:03:00Z"},{"hash":"h5","previousHash":"h4","type":"AchievementUnlocked","data":{"logro":"y","nombre":"Segunda","puntos":"20"},"timestamp":"2026-09-26T10:04:00Z"}]');

\echo 'uno que NO sigue la cadena: se queda en 5'
select public.subir_eventos('11111111-1111-1111-1111-111111111111', '{"eventCount":6,"chainHead":"zz","points":10,"playerName":"Yo"}', 5,
 '[{"hash":"zz","previousHash":"otra","type":"X","timestamp":"2026-09-26T10:05:00Z"}]');

\echo 'restaurada (dice tener 4): se queda en 5 y el resumen queda anotado'
select public.subir_eventos('11111111-1111-1111-1111-111111111111', '{"eventCount":4,"chainHead":"h4","points":10,"playerName":"Yo"}', 4,
 '[{"hash":"q","previousHash":"h4","type":"X","timestamp":"2026-09-26T10:06:00Z"}]');
select eventos, huella from public.subidas order by id;

\echo 'logros_todos:'
select jugador, logro, nombre, puntos from public.logros_todos;

\echo 'una run nueva por eventos, de 0'
select set_config('test.uid','bbbbbbbb-0000-0000-0000-000000000002', false);
select public.subir_eventos('22222222-2222-2222-2222-222222222222', '{"eventCount":1,"chainHead":"b1","points":0,"playerName":"Amigo"}', -1, '[]');
select public.subir_eventos('22222222-2222-2222-2222-222222222222', '{"eventCount":1,"chainHead":"b1","points":0,"playerName":"Amigo"}', 0,
 '[{"hash":"b1","previousHash":"","type":"RunCreated","timestamp":"2026-09-26T11:00:00Z"}]');

\echo 'el amigo no puede subir a la mía:'
do $$ begin perform public.subir_eventos('11111111-1111-1111-1111-111111111111', '{}', -1, '[]'); raise notice 'MAL: la aceptó';
exception when others then raise notice 'bien: %', sqlerrm; end $$;
\echo 'ni limpiar:'
do $$ begin perform public.limpieza(false); raise notice 'MAL'; exception when others then raise notice 'bien: %', sqlerrm; end $$;
\echo 'ni escribir en eventos a mano:'
do $$ begin insert into public.eventos values ('22222222-2222-2222-2222-222222222222', 9, '{}'); raise notice 'MAL'; exception when others then raise notice 'bien: %', sqlerrm; end $$;

select set_config('test.uid','aaaaaaaa-0000-0000-0000-000000000001', false);
\echo 'LIMPIEZA: contar'
insert into public.anuncios (texto) values ('a1'),('a2'),('a3');
select public.limpieza(false);
select public.limpieza(true);
select count(*) as subidas_que_quedan from public.subidas;
select count(*) as anuncios_que_quedan from public.anuncios;

\echo 'con historiales: archivo la run del amigo y la vacío'
select public.reiniciar_run('bbbbbbbb-0000-0000-0000-000000000002');
select public.limpieza(false, true);
select public.limpieza(true, true);
select run_id, history from public.runs order by run_id;
select run_id, count(*) from public.eventos group by run_id;

set role authenticated;
select set_config('test.uid','aaaaaaaa-0000-0000-0000-000000000001', false);
-- Regalos: uno recogido (evento en eventos) y viejo, uno sin recoger, uno a todos, uno recogido pero reciente.
insert into public.regalos (id, para, regalo, creado) values
 ('00000000-0000-0000-0000-00000000000a', 'aaaaaaaa-0000-0000-0000-000000000001', '{}', now() - interval '10 days'),
 ('00000000-0000-0000-0000-00000000000b', 'aaaaaaaa-0000-0000-0000-000000000001', '{}', now() - interval '10 days'),
 ('00000000-0000-0000-0000-00000000000c', 'todos', '{}', now() - interval '10 days'),
 ('00000000-0000-0000-0000-00000000000d', 'aaaaaaaa-0000-0000-0000-000000000001', '{}', now() - interval '1 day');
select public.subir_eventos('11111111-1111-1111-1111-111111111111', '{"eventCount":7,"chainHead":"h7","points":10,"playerName":"Yo"}', 5,
 '[{"hash":"h6","previousHash":"h5","type":"AdminGiftClaimed","data":{"regalo":"00000000-0000-0000-0000-00000000000a"},"timestamp":"2026-09-26T12:00:00Z"},
   {"hash":"h7","previousHash":"h6","type":"AdminGiftClaimed","data":{"regalo":"00000000-0000-0000-0000-00000000000d"},"timestamp":"2026-09-26T12:01:00Z"}]');
insert into public.fantasmas (nombre, pokemon, especie, creado) values ('Yo','A',1, now() - interval '3 days'), ('Yo','B',1, now() - interval '2 days'), ('Yo','C',1, now() - interval '2 days');
insert into public.lluvias (nombre, creado) values ('Yo', now() - interval '3 days');
select public.limpieza(true);
select id from public.regalos order by id;
select pokemon from public.fantasmas;
select count(*) lluvias from public.lluvias;
