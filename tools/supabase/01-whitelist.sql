-- PermaLocke: whitelist del torneo. Se pega entero en Supabase > SQL Editor > New query > Run.
-- Se puede ejecutar más de una vez sin romper nada.

-- La lista. Nadie la puede leer ni tocar con la clave pública: RLS activado y sin políticas.
-- Se rellena desde el panel (Table Editor > whitelist > Insert row).
create table if not exists public.whitelist (
    discord_id text primary key,
    nombre     text,
    alta       timestamptz not null default now()
);
alter table public.whitelist enable row level security;

-- La única pregunta que la app puede hacer: "¿estoy yo en la lista?". Responde solo por la cuenta que pregunta.
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
        where u.id = auth.uid()
    );
$$;

revoke execute on function public.permitido() from public, anon;
grant execute on function public.permitido() to authenticated;

-- Sin sesión, ni siquiera se puede preguntar por la tabla.
revoke all on public.whitelist from anon;
