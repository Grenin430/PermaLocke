-- PermaLocke: los regalos del organizador, por el servidor en vez de la carpeta compartida.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 07 antes. Se puede ejecutar más de una vez.
--
-- "para" es 'todos' o el user_id del jugador. Cada jugador solo ve los suyos y los de todos; solo el organizador
-- manda o retira. Recogerlo sigue siendo un evento en la run del jugador (AdminGiftClaimed), que sube con su historial.

create table if not exists public.regalos (
    id      uuid primary key,
    para    text not null,
    regalo  jsonb not null,
    creado  timestamptz not null default now()
);
alter table public.regalos enable row level security;
revoke all on public.regalos from anon;

drop policy if exists "ver los tuyos" on public.regalos;
create policy "ver los tuyos" on public.regalos
    for select to authenticated
    using (public.es_organizador()
           or (public.permitido() and (para = 'todos' or para = auth.uid()::text)));

drop policy if exists "mandar si organizas" on public.regalos;
create policy "mandar si organizas" on public.regalos
    for insert to authenticated with check (public.es_organizador());

drop policy if exists "retirar si organizas" on public.regalos;
create policy "retirar si organizas" on public.regalos
    for delete to authenticated using (public.es_organizador());
