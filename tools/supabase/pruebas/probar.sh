#!/bin/sh
# Aplica 01..NN sobre un Postgres de usar y tirar que imita a Supabase y ejecuta las pruebas de esta carpeta.
#   PGHOST=/ruta/al/socket PGPORT=5433 sh tools/supabase/pruebas/probar.sh
# Nunca contra el Supabase del torneo: crea y borra la base «permalocke_pruebas».
set -e
cd "$(dirname "$0")"
P="psql -U ${PGUSER:-postgres} -v ON_ERROR_STOP=1 -q"
$P -c "drop database if exists permalocke_pruebas" -c "create database permalocke_pruebas" >/dev/null
P="$P -d permalocke_pruebas"
$P -f supabase-falso.sql
for f in ../[0-9]*.sql; do $P -f "$f" >/dev/null 2>&1 || { echo "FALLA al aplicar $f"; exit 1; }; done
echo "SQL 01 al último aplicados"
for t in [0-9]*.sql; do echo "== $t"; $P -f "$t"; done
