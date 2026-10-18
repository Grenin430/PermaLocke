-- PermaLocke: suspender sin quitar de la lista, deshacer un reinicio y anuncios del organizador.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 09 antes. Se puede ejecutar más de una vez.

-- ============================================================ SUSPENDER
-- Un suspendido sigue en la lista (con todo lo suyo) pero no entra ni sube nada hasta que se le reactive.
alter table public.whitelist add column if not exists suspendido boolean not null default false;

create or replace function public.permitido()
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.whitelist w
        join auth.users u on u.raw_user_meta_data ->> 'provider_id' = w.discord_id
        where u.id = auth.uid() and not w.suspendido
    );
$$;

-- ============================================================ DESHACER UN REINICIO
alter table public.reinicios add column if not exists accion text not null default 'reinicio';

-- Vuelve a activar una run archivada. Solo si su jugador no tiene ya otra activa (si la tiene, reiníciala antes).
create or replace function public.reactivar_run(run uuid)
returns int
language plpgsql
security definer
set search_path = ''
as $$
declare
    jugador uuid;
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede reactivar runs';
    end if;

    select r.user_id into jugador from public.runs r where r.run_id = run and not r.activa;
    if jugador is null then
        return 0;
    end if;

    if exists (select 1 from public.runs r where r.user_id = jugador and r.activa) then
        raise exception 'Ese jugador ya tiene otra run activa: reiníciala antes';
    end if;

    update public.runs set activa = true where run_id = run;
    insert into public.reinicios (user_id, run_id, por, accion) values (jugador, run, auth.uid(), 'reactivada');
    return 1;
end;
$$;

revoke execute on function public.reactivar_run(uuid) from public, anon;
grant execute on function public.reactivar_run(uuid) to authenticated;

-- ============================================================ ANUNCIOS
create table if not exists public.anuncios (
    id      bigint generated always as identity primary key,
    texto   text not null,
    creado  timestamptz not null default now()
);
alter table public.anuncios enable row level security;
revoke all on public.anuncios from anon;

drop policy if exists "leer si estas en la lista" on public.anuncios;
create policy "leer si estas en la lista" on public.anuncios
    for select to authenticated using (public.permitido() or public.es_organizador());

drop policy if exists "publicar si organizas" on public.anuncios;
create policy "publicar si organizas" on public.anuncios
    for insert to authenticated with check (public.es_organizador());

drop policy if exists "retirar si organizas" on public.anuncios;
create policy "retirar si organizas" on public.anuncios
    for delete to authenticated using (public.es_organizador());
