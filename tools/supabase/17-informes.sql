-- PermaLocke: informes de fallo directos a Admin (§199, plan del próximo torneo, paso 6).
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 16 antes. Se puede ejecutar más de una vez.
--
-- Solo AÑADE: un bucket privado de Storage («informes»), sus permisos y dos funciones para Admin. No toca nada de lo
-- que usan las apps que ya están jugando.
--
-- Cuando Azahar se cierra solo, PermaLocke escribe un zip con los dos logs, las últimas peticiones al emulador y el
-- equipo (§168; sin ROM, partida ni run) y ahora lo sube a informes/<su id>/<nombre>.zip. Cada jugador solo puede subir
-- a su carpeta; solo el organizador los lee y los retira (la LIMPIEZA quita los de más de 30 días).

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('informes', 'informes', false, 5242880, array['application/zip'])
on conflict (id) do nothing;

drop policy if exists "informes: subir el tuyo" on storage.objects;
create policy "informes: subir el tuyo" on storage.objects
    for insert to authenticated
    with check (bucket_id = 'informes'
                and (storage.foldername(name))[1] = auth.uid()::text
                and public.permitido());

drop policy if exists "informes: leer si organizas" on storage.objects;
create policy "informes: leer si organizas" on storage.objects
    for select to authenticated
    using (bucket_id = 'informes' and public.es_organizador());

drop policy if exists "informes: retirar si organizas" on storage.objects;
create policy "informes: retirar si organizas" on storage.objects
    for delete to authenticated
    using (bucket_id = 'informes' and public.es_organizador());

-- Los últimos informes de todos, para la ventana INFORMES de Admin: de quién (su id) y cuándo. Solo el organizador.
create or replace function public.informes_lista(p_limite int default 200)
returns table (nombre text, jugador uuid, bytes bigint, creado timestamptz)
language plpgsql
stable
security definer
set search_path = ''
as $$
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede ver los informes';
    end if;

    return query
    select s.name, ((storage.foldername(s.name))[1])::uuid, coalesce((s.metadata ->> 'size')::bigint, 0), s.created_at
    from storage.objects s
    where s.bucket_id = 'informes'
    order by s.created_at desc
    limit greatest(p_limite, 1);
end;
$$;

-- Los que sobran para la LIMPIEZA: los de más de p_dias días. Solo el organizador; se retiran por la API de Storage.
create or replace function public.informes_viejos(p_dias int default 30)
returns table (nombre text, bytes bigint)
language plpgsql
stable
security definer
set search_path = ''
as $$
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede ver los informes que sobran';
    end if;

    return query
    select s.name, coalesce((s.metadata ->> 'size')::bigint, 0)
    from storage.objects s
    where s.bucket_id = 'informes' and s.created_at < now() - make_interval(days => greatest(p_dias, 1));
end;
$$;

revoke execute on function public.informes_lista(int) from public, anon;
revoke execute on function public.informes_viejos(int) from public, anon;
grant execute on function public.informes_lista(int) to authenticated;
grant execute on function public.informes_viejos(int) to authenticated;
