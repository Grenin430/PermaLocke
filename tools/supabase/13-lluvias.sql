-- PermaLocke: la lluvia de sangre. Cuando alguien pierde el equipo entero, su app lo apunta aquí al momento, y las de
-- los demás lo leen cada pocos segundos para que llueva sangre medio minuto encima de su emulador (§184).
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 12 antes. Se puede ejecutar más de una vez.
--
-- Tabla aparte de fantasmas a propósito: una app de antes que leyera una fila de wipe en fantasmas enseñaría un
-- fantasma vacío. Cada jugador solo escribe las suyas y todos los de la lista leen las de todos. Es un aviso, no el
-- historial: el wipe sigue siendo el TeamWiped de la run de quien lo sufrió.

create table if not exists public.lluvias (
    id       bigint generated always as identity primary key,
    jugador  uuid not null default auth.uid(),
    nombre   text not null,
    creado   timestamptz not null default now()
);
alter table public.lluvias enable row level security;
revoke all on public.lluvias from anon;

drop policy if exists "leer si estas en la lista" on public.lluvias;
create policy "leer si estas en la lista" on public.lluvias
    for select to authenticated using (public.permitido() or public.es_organizador());

drop policy if exists "apuntar las tuyas" on public.lluvias;
create policy "apuntar las tuyas" on public.lluvias
    for insert to authenticated with check (jugador = auth.uid() and public.permitido());
