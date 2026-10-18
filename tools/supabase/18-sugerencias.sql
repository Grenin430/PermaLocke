-- PermaLocke: el buzón de sugerencias (1.0.5.5). Lo que un jugador escribe en el buzón de arriba de su app llega aquí,
-- y solo el organizador lo lee (y lo borra) desde Admin > SUGERENCIAS.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 10 antes. Se puede ejecutar más de una vez.
-- Solo AÑADE una tabla: no toca nada de lo que usan las apps que ya están jugando.

create table if not exists public.sugerencias (
    id       bigint generated always as identity primary key,
    jugador  uuid not null default auth.uid(),
    nombre   text not null,
    texto    text not null check (char_length(texto) between 1 and 1000),
    version  text,
    creado   timestamptz not null default now()
);
alter table public.sugerencias enable row level security;
revoke all on public.sugerencias from anon;

drop policy if exists "mandar la tuya" on public.sugerencias;
create policy "mandar la tuya" on public.sugerencias
    for insert to authenticated with check (jugador = auth.uid() and public.permitido());

drop policy if exists "leer si organizas" on public.sugerencias;
create policy "leer si organizas" on public.sugerencias
    for select to authenticated using (public.es_organizador());

drop policy if exists "borrar si organizas" on public.sugerencias;
create policy "borrar si organizas" on public.sugerencias
    for delete to authenticated using (public.es_organizador());
