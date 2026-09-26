-- Lo mínimo de Supabase para probar estos SQL en un Postgres cualquiera: los roles, auth.users y auth.uid().
-- auth.uid() lee el ajuste test.uid: select set_config('test.uid', '<uuid>', false) para «entrar» como alguien.
do $$ begin create role anon nologin; exception when others then null; end $$; do $$ begin create role authenticated nologin; exception when others then null; end $$;
create schema auth;
create table auth.users (id uuid primary key, raw_user_meta_data jsonb);
create function auth.uid() returns uuid language sql stable as $$ select nullif(current_setting('test.uid', true), '')::uuid $$;
grant usage on schema auth to anon, authenticated;
grant usage on schema public to anon, authenticated;
alter default privileges in schema public grant all on tables to anon, authenticated;
alter default privileges in schema public grant all on functions to anon, authenticated;
