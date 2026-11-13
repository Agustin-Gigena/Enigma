# Deploy de producción (VPS)

Stack: `api` (imagen GHCR `enigma-server` desde `infra/api.Dockerfile`) contra **Aiven** (MySQL gestionado externo — la DB NO vive en la VPS) + `caddy` (TLS automático). Deploy manual: `docker compose pull && docker compose up -d` desde `infra/deploy/`. Refrescar la imagen sin SSH se puede con el workflow **Deploy API** (dispatch de GitHub Actions); la tag `latest` solo se pisa desde `production`.

## 1. Variables de entorno requeridas

`docker-compose.yml` las interpola desde un archivo `.env` junto al compose (o desde el entorno). Sin ellas, `docker compose up` falla:

| Variable | Consumidor | Descripción |
|---|---|---|
| `MYSQL_HOST` | `api` | Host del servicio Aiven (ej. `mysql-xxxx.aivencloud.com`). |
| `MYSQL_PORT` | `api` | Puerto Aiven (usualmente `3306`). |
| `MYSQL_DATABASE` | `api` | Nombre de la base, creado desde la consola de Aiven. |
| `MYSQL_USER` | `api` | Usuario Aiven (el default es `avnadmin`). |
| `MYSQL_PASSWORD` | `api` | Password Aiven. |
| `MYSQL_SSL_MODE` | `api` | **`REQUIRED`** — Aiven exige TLS (default del compose). |
| `ENIGMA_JWT_SECRET` | `api` | Secret de firma de JWT. Debe ser fuerte (el arranque en Production rechaza secrets débiles). |
| `ENIGMA_CORS_ORIGINS` | `api` | Orígenes CORS, **separados por coma** (sin espacios). Debe incluir el dominio de Cloudflare Pages (el front) y, si aplica, el dominio de la API. |
| `ENIGMA_API_DOMAIN` | `caddy` | Dominio público de la API; lo consume el `Caddyfile` (`{$ENIGMA_API_DOMAIN}`). Ese **DNS debe apuntar a la VPS** antes de arrancar, para que Caddy emita el certificado. |

`MYSQL_HOST`/`MYSQL_PORT`/`MYSQL_SSL_MODE` NO están fijados en el compose: salen del `.env` (Aiven).

## 2. Migraciones (la Api en Production NO auto-migra)

El auto-migrate y el seed solo corren en `Development`. En producción las migraciones se aplican **antes** del primer arranque con un *bundle* de EF Core, generado una vez desde una máquina con SDK .NET 10:

```bash
# Desde una máquina con SDK (no necesita MySQL vivo: la cadena NO se abre
# en design-time, las MYSQL_* solo arman la config; la cadena real se pasa
# al bundle en runtime).
dotnet tool install -g dotnet-ef   # si no está instalado
dotnet ef migrations bundle --project src/Enigma.Api -o efbundle
```

Copiar `efbundle` a la VPS (a la carpeta `infra/deploy/` o la que se use) y ejecutarlo **dentro de la red del compose**, usando **la imagen de la propia API** (ya trae las deps runtime de .NET; una imagen genérica tipo `debian:bookworm-slim` NO sirve porque al bundle le faltan librerías):

```bash
cd infra/deploy
chmod +x efbundle

# compose run hereda el `environment` del servicio api (host y credenciales
# Aiven del .env; el bundle abre TLS contra Aiven en runtime).
# --entrypoint es necesario: la imagen tiene ENTRYPOINT ["dotnet", "Enigma.Api.dll"]
# y sin él se ejecutaría `dotnet Enigma.Api.dll /efbundle`.
docker compose run --rm --no-deps --entrypoint /efbundle \
    -v "$PWD/efbundle:/efbundle" api
```

> ⚠️ **Verificar en el primer deploy real**: la combinación exacta de `compose run` + `--entrypoint` + volumen no se ha ejercitado contra producción todavía. Alternativa documentada pero no recomendada: `docker compose exec api /bin/bash -c ...` NO funciona — la imagen runtime no garantiza shell tools.

## 3. Primer usuario (bootstrap interim)

La seed (`admin`/`admin123` + las 2 instituciones de ejemplo, sobrescribibles con `ENIGMA_SEED_ADMIN_USER`/`ENIGMA_SEED_ADMIN_PASSWORD`) es **Development-only**. Como no existe aún un mecanismo de bootstrap de producción (decisión pendiente en `PRODUCT.md`), el bootstrap interim es:

```bash
cd infra/deploy

# 1. Arrancar la api UNA VEZ en Development: auto-migra y siembra
#    admin/admin123 + las 2 instituciones. Corre en primer plano;
#    cortar con Ctrl+C cuando el log muestre el seed aplicado.
docker compose run --rm -e ASPNETCORE_ENVIRONMENT=Development api

# 2. Levantar la api definitiva en Production.
docker compose up -d api
```

> ⚠️ **OJO — esto escribe en la base REAL de Aiven**: el paso 1 siembra `admin`/`admin123` + 2 instituciones de ejemplo **en producción**. Antes de correrlo, exportá `ENIGMA_SEED_ADMIN_USER`/`ENIGMA_SEED_ADMIN_PASSWORD` con las credenciales reales que quieras (NO dejes admin/admin123 vivo). La seed es idempotente (si corrió dos veces, no duplica). Un mecanismo de bootstrap proper (admin de producción, sin instituciones de ejemplo) es decisión pendiente de `PRODUCT.md`.

## 4. Arranque y verificación

```bash
docker compose pull
docker compose up -d
curl -fsS https://"$ENIGMA_API_DOMAIN"/health
```
