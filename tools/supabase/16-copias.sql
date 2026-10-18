-- PermaLocke: copias de seguridad en el servidor (§198, plan del próximo torneo, paso 5).
-- Se pega entero en Supabase > SQL Editor > Run. Necesita 01 a 15 antes. Se puede ejecutar más de una vez.
--
-- Solo AÑADE: un bucket privado de Storage («copias»), sus permisos y una función para la LIMPIEZA. No toca nada de lo
-- que usan las apps que ya están jugando.
--
-- Cada app sube de vez en cuando un zip con su run y su partida de Ultra Luna a copias/<su id>/<fecha>.zip. Cada jugador
-- solo puede subir a su carpeta y leer la suya; no puede borrar ni cambiar nada (una copia no se pisa). El organizador
-- las lee todas, para recuperar la de quien la pierda, y las retira desde la LIMPIEZA de Admin (se queda con las 5
-- últimas de cada uno). Va a Storage (1 GB en el plan gratis) y no a la base de datos (500 MB).

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('copias', 'copias', false, 26214400, array['application/zip'])
on conflict (id) do nothing;

-- Subir: solo a tu carpeta, y solo si estás en la lista.
drop policy if exists "copias: subir la tuya" on storage.objects;
create policy "copias: subir la tuya" on storage.objects
    for insert to authenticated
    with check (bucket_id = 'copias'
                and (storage.foldername(name))[1] = auth.uid()::text
                and public.permitido());

-- Leer: las tuyas; el organizador, todas.
drop policy if exists "copias: leer la tuya o si organizas" on storage.objects;
create policy "copias: leer la tuya o si organizas" on storage.objects
    for select to authenticated
    using (bucket_id = 'copias'
           and ((storage.foldername(name))[1] = auth.uid()::text or public.es_organizador()));

-- Retirar: solo el organizador (la LIMPIEZA de Admin, por la API de Storage).
drop policy if exists "copias: retirar si organizas" on storage.objects;
create policy "copias: retirar si organizas" on storage.objects
    for delete to authenticated
    using (bucket_id = 'copias' and public.es_organizador());

-- Las copias que sobran: de cada jugador, todas menos las p_guardar más recientes. Solo el organizador. La LIMPIEZA las
-- enseña con su tamaño y, con un sí, las retira por la API de Storage (Supabase no deja borrar ficheros con SQL).
create or replace function public.copias_sobrantes(p_guardar int default 5)
returns table (nombre text, bytes bigint)
language plpgsql
stable
security definer
set search_path = ''
as $$
begin
    if not public.es_organizador() then
        raise exception 'Solo el organizador puede ver las copias que sobran';
    end if;

    return query
    select o.name, coalesce((o.metadata ->> 'size')::bigint, 0)
    from (
        select s.name, s.metadata,
               row_number() over (partition by (storage.foldername(s.name))[1] order by s.created_at desc, s.name desc) as orden
        from storage.objects s
        where s.bucket_id = 'copias'
    ) o
    where o.orden > greatest(p_guardar, 1);
end;
$$;

revoke execute on function public.copias_sobrantes(int) from public, anon;
grant execute on function public.copias_sobrantes(int) to authenticated;
