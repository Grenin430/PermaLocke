-- PermaLocke: el organizador gestiona la whitelist desde Admin (ver, añadir y quitar).
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 08 antes. Se puede ejecutar más de una vez.
-- Los jugadores siguen sin poder ver ni tocar la lista: solo preguntan por sí mismos con permitido().

drop policy if exists "ver si organizas" on public.whitelist;
create policy "ver si organizas" on public.whitelist
    for select to authenticated using (public.es_organizador());

drop policy if exists "añadir si organizas" on public.whitelist;
create policy "añadir si organizas" on public.whitelist
    for insert to authenticated with check (public.es_organizador());

drop policy if exists "cambiar si organizas" on public.whitelist;
create policy "cambiar si organizas" on public.whitelist
    for update to authenticated using (public.es_organizador()) with check (public.es_organizador());

drop policy if exists "quitar si organizas" on public.whitelist;
create policy "quitar si organizas" on public.whitelist
    for delete to authenticated using (public.es_organizador());

revoke all on public.whitelist from anon;
