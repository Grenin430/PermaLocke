-- PermaLocke: una sola run por jugador. Solo el organizador la reinicia.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 06 antes. Se puede ejecutar más de una vez.
--
-- Reiniciar no borra: la run pasa a "archivada" (activa = false) y se queda para auditar. El jugador puede entonces
-- crear una nueva desde cero; mientras tenga una activa, el servidor no le acepta otra.

alter table public.runs add column if not exists activa boolean not null default true;

-- La regla, en la base de datos: como mucho una run activa por jugador.
create unique index if not exists una_run_activa on public.runs (user_id) where activa;

-- Una run archivada ya no se puede actualizar desde la app.
drop policy if exists "actualizar la tuya" on public.runs;
create policy "actualizar la tuya" on public.runs
    for update to authenticated
    using (user_id = auth.uid() and public.permitido() and activa)
    with check (user_id = auth.uid() and activa);

-- La clasificación, los amigos y los logros miran solo la run activa.
create or replace view public.ultima_run with (security_invoker = true) as
    select distinct on (r.user_id) r.user_id, r.run_id, r.snapshot, r.history
    from public.runs r
    where r.activa
    order by r.user_id, r.subida desc;

-- La pregunta de la app antes de crear una run: "¿puedo?". Solo si no tienes ninguna activa en el torneo.
create or replace function public.puedo_crear_run()
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select not exists (select 1 from public.runs r where r.user_id = auth.uid() and r.activa);
$$;

revoke execute on function public.puedo_crear_run() from public, anon;
grant execute on function public.puedo_crear_run() to authenticated;

-- El registro de reinicios: quién, qué run y cuándo. Nadie lo toca desde la app.
create table if not exists public.reinicios (
    id         bigint generated always as identity primary key,
    user_id    uuid not null,
    run_id     uuid not null,
    por        uuid not null,
    cuando     timestamptz not null default now()
);
alter table public.reinicios enable row level security;
revoke all on public.reinicios from anon;

drop policy if exists "leer si organizas" on public.reinicios;
create policy "leer si organizas" on public.reinicios
    for select to authenticated using (public.es_organizador());

-- Reiniciar la run de un jugador. Solo un organizador.
create or replace function public.reiniciar_run(jugador uuid)
returns int
language plpgsql
security definer
set search_path = ''
as $$
declare
    archivadas int;
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede reiniciar runs';
    end if;

    insert into public.reinicios (user_id, run_id, por)
    select r.user_id, r.run_id, auth.uid() from public.runs r where r.user_id = jugador and r.activa;

    update public.runs set activa = false where user_id = jugador and activa;
    get diagnostics archivadas = row_count;
    return archivadas;
end;
$$;

revoke execute on function public.reiniciar_run(uuid) from public, anon;
grant execute on function public.reiniciar_run(uuid) to authenticated;
