-- PermaLocke: los fantasmas. Cuando a alguien se le muere un Pokémon, su app lo apunta aquí al momento, y las de los
-- demás lo leen cada pocos segundos para enseñarlo encima de su emulador.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 10 antes. Se puede ejecutar más de una vez.
--
-- Cada jugador solo escribe los suyos (jugador = auth.uid()) y todos los de la lista leen los de todos. No se borra
-- nada desde la app: es un aviso, no el historial, que sigue siendo el de cada run.

create table if not exists public.fantasmas (
    id       bigint generated always as identity primary key,
    jugador  uuid not null default auth.uid(),
    nombre   text not null,
    pokemon  text not null,
    especie  int not null,
    forma    int not null default 0,
    shiny    boolean not null default false,
    nivel    int,
    zona     text,
    creado   timestamptz not null default now()
);
alter table public.fantasmas enable row level security;
revoke all on public.fantasmas from anon;

drop policy if exists "leer si estas en la lista" on public.fantasmas;
create policy "leer si estas en la lista" on public.fantasmas
    for select to authenticated using (public.permitido() or public.es_organizador());

drop policy if exists "apuntar los tuyos" on public.fantasmas;
create policy "apuntar los tuyos" on public.fantasmas
    for insert to authenticated with check (jugador = auth.uid() and public.permitido());
