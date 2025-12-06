#!/bin/sh
echo "Waiting for Postgres..."
until pg_isready -h postgres -p 5432 -U elijah; do
  sleep 2
done
echo "Postgres is ready — starting app"
exec dotnet Elijah.Host.dll

