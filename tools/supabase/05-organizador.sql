-- PermaLocke, fase 3: el organizador puede leer el registro de subidas para auditar.
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 04 antes. Se puede ejecutar más de una vez.
--
-- Después, en Table Editor > organizadores > Insert row, pon tu ID de Discord.

create table if not exists public.organizadores (
    discord_id text primary key,
    nombre     text
);
alter table public.organizadores enable row level security;
revoke all on public.organizadores from anon;

create or replace function public.es_organizador()
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.organizadores o
        join auth.users u on u.raw_user_meta_data ->> 'provider_id' = o.discord_id
        where u.id = auth.uid()
    );
$$;

revoke execute on function public.es_organizador() from public, anon;
grant execute on function public.es_organizador() to authenticated;

-- El registro de subidas lo lee solo el organizador; los jugadores siguen sin poder verlo ni tocarlo.
drop policy if exists "leer si organizas" on public.subidas;
create policy "leer si organizas" on public.subidas
    for select to authenticated using (public.es_organizador());
