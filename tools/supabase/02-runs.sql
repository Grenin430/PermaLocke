-- PermaLocke, fase 2: las runs del torneo. Se pega entero en Supabase > SQL Editor > New query > Run.
-- Necesita 01-whitelist.sql antes. Se puede ejecutar más de una vez sin romper nada.

-- Una fila por run: el resumen (RunSnapshot) y el historial entero (RunHistory), tal como los construye la app.
-- El dueño lo pone el servidor (auth.uid()), no la app: nadie puede subir una run a nombre de otro.
create table if not exists public.runs (
    run_id     uuid primary key,
    user_id    uuid not null default auth.uid() references auth.users (id) on delete cascade,
    snapshot   jsonb not null,
    history    jsonb not null,
    subida     timestamptz not null default now()
);
alter table public.runs enable row level security;

-- Leer: cualquiera de la whitelist (para la clasificación de la fase 3).
drop policy if exists "leer si estas en la lista" on public.runs;
create policy "leer si estas en la lista" on public.runs
    for select to authenticated
    using (public.permitido());

-- Subir: solo tu run, y solo si estás en la lista.
drop policy if exists "subir la tuya" on public.runs;
create policy "subir la tuya" on public.runs
    for insert to authenticated
    with check (user_id = auth.uid() and public.permitido());

-- Actualizar: solo la tuya, y no puedes pasársela a otro.
drop policy if exists "actualizar la tuya" on public.runs;
create policy "actualizar la tuya" on public.runs
    for update to authenticated
    using (user_id = auth.uid() and public.permitido())
    with check (user_id = auth.uid());

-- Sin política de borrar: una run subida no la quita el jugador.

-- El registro de subidas: una fila por cada vez que llega algo, que nadie puede tocar desde la app.
-- Es lo que delata una run restaurada desde una copia (el número de eventos baja) o reescrita (cambia la
-- huella de la cadena sin que suba el número).
create table if not exists public.subidas (
    id         bigint generated always as identity primary key,
    run_id     uuid not null,
    user_id    uuid not null,
    eventos    int,
    huella     text,
    puntos     int,
    llegada    timestamptz not null default now()
);
alter table public.subidas enable row level security;

create or replace function public.anotar_subida()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    new.subida := now();
    insert into public.subidas (run_id, user_id, eventos, huella, puntos)
    values (new.run_id, new.user_id,
            (new.snapshot ->> 'eventCount')::int,
            new.snapshot ->> 'chainHead',
            (new.snapshot ->> 'points')::int);
    return new;
end;
$$;

drop trigger if exists anotar_subida on public.runs;
create trigger anotar_subida
    before insert or update on public.runs
    for each row execute function public.anotar_subida();

-- Sin sesión no se toca nada: ni se llega a los permisos por fila ni al trigger.
revoke all on public.runs, public.subidas from anon;
