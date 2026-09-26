-- PermaLocke: las reglas oficiales del torneo, editadas desde Admin (2026-09-26).
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 13 antes. Se puede ejecutar más de una vez.
--
-- Una fila por fichero de Data/ (shop.json, gacha.json...). La app de cada jugador los descarga al abrirse, guarda
-- una copia de los suyos y los pone en su Data/; se aplican al reiniciar PermaLocke. Sin fila, cada uno usa el
-- fichero que venía con su PermaLocke. Solo el organizador escribe; los de la lista leen.
create table if not exists public.reglas (
    fichero     text primary key,
    contenido   text not null,
    actualizado timestamptz not null default now()
);
alter table public.reglas enable row level security;
revoke all on public.reglas from anon;

drop policy if exists "leer si estas en la lista" on public.reglas;
create policy "leer si estas en la lista" on public.reglas
    for select to authenticated using (public.permitido() or public.es_organizador());

drop policy if exists "escribir si organizas" on public.reglas;
create policy "escribir si organizas" on public.reglas
    for insert to authenticated with check (public.es_organizador());

drop policy if exists "cambiar si organizas" on public.reglas;
create policy "cambiar si organizas" on public.reglas
    for update to authenticated using (public.es_organizador()) with check (public.es_organizador());

drop policy if exists "quitar si organizas" on public.reglas;
create policy "quitar si organizas" on public.reglas
    for delete to authenticated using (public.es_organizador());
