-- PermaLocke: los cementerios de todos, con sus killcams (1.0.5.5).
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 10 antes. Se puede ejecutar más de una vez.
--
-- Solo AÑADE: una tabla «caidos» (una fila por Pokémon muerto: su lápida, en JSON) y un bucket privado de Storage
-- («killcams») con la repetición de cada muerte en vídeo, unos 250 KB. Cada jugador escribe solo los suyos; todos los de
-- la lista leen los de todos, para ver el cementerio de los demás. Solo el organizador borra (LIMPIEZA).

create table if not exists public.caidos (
    pokemon_id  uuid primary key,
    jugador     uuid not null default auth.uid(),
    nombre      text not null,
    run_id      uuid not null,
    datos       jsonb not null,
    killcam     boolean not null default false,
    creado      timestamptz not null default now()
);
create index if not exists caidos_jugador on public.caidos (jugador, creado desc);
alter table public.caidos enable row level security;
revoke all on public.caidos from anon;

drop policy if exists "leer si estas en la lista" on public.caidos;
create policy "leer si estas en la lista" on public.caidos
    for select to authenticated using (public.permitido() or public.es_organizador());

drop policy if exists "apuntar los tuyos" on public.caidos;
create policy "apuntar los tuyos" on public.caidos
    for insert to authenticated with check (jugador = auth.uid() and public.permitido());

drop policy if exists "corregir los tuyos" on public.caidos;
create policy "corregir los tuyos" on public.caidos
    for update to authenticated using (jugador = auth.uid()) with check (jugador = auth.uid() and public.permitido());

drop policy if exists "borrar si organizas" on public.caidos;
create policy "borrar si organizas" on public.caidos
    for delete to authenticated using (public.es_organizador());

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('killcams', 'killcams', false, 3145728, array['application/octet-stream'])
on conflict (id) do nothing;

drop policy if exists "killcams: subir la tuya" on storage.objects;
create policy "killcams: subir la tuya" on storage.objects
    for insert to authenticated
    with check (bucket_id = 'killcams'
                and (storage.foldername(name))[1] = auth.uid()::text
                and public.permitido());

drop policy if exists "killcams: leer si estas en la lista" on storage.objects;
create policy "killcams: leer si estas en la lista" on storage.objects
    for select to authenticated
    using (bucket_id = 'killcams' and (public.permitido() or public.es_organizador()));

drop policy if exists "killcams: retirar si organizas" on storage.objects;
create policy "killcams: retirar si organizas" on storage.objects
    for delete to authenticated
    using (bucket_id = 'killcams' and public.es_organizador());
