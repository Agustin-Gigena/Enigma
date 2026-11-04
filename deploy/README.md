# Deploy de producción (VPS)

Stack: `api` (imagen GHCR `enigma-server` desde `src/Enigma.Api/Dockerfile`) + `db` (MySQL 8.0) + `caddy` (TLS automático). Deploy manual: `docker compose pull && docker compose up -d` desde `deploy/`. Refrescar la imagen sin SSH se puede con el workflow **Deploy API** (dispatch de GitHub Actions); la tag `latest` solo se pisa desde `production`.

## 1. Variables de entorno requeridas

`docker-compose.yml` las interpola desde un archivo `.env` junto al compose (o desde el entorno). Sin ellas, `docker compose up` falla:

| Variable | Consumidor | Descripción |
|---|---|---|
| `MYSQL_DATABASE` | `db`, `api` | Nombre de la base (se crea al primer arranque de MySQL). |
| `MYSQL_USER` | `db`, `api` | Usuario de aplicación (el mysql:8.0 lo crea y le otorga permisos sobre `MYSQL_DATABASE`). |
| `MYSQL_PASSWORD` | `db`, `api` | Contraseña de `MYSQL_USER`. |
| `MYSQL_ROOT_PASSWORD` | `db` | Password de root de MySQL. |
| `ENIGMA_JWT_SECRET` | `api` | Secret de firma de JWT. Debe ser fuerte (el arranque en Production rechaza secrets débiles). |
| `ENIGMA_CORS_ORIGINS` | `api` | Orígenes CORS, **separados por coma** (sin espacios). Debe incluir el dominio de Cloudflare Pages (el front) y, si aplica, el dominio de la API. |
| `ENIGMA_API_DOMAIN` | `caddy` | Dominio público de la API; lo consume el `Caddyfile` (`{$ENIGMA_API_DOMAIN}`). Ese **DNS debe apuntar a la VPS** antes de arrancar, para que Caddy emita el certificado. |

`MYSQL_HOST` (`db`) y `MYSQL_PORT` (`3306`) ya están fijados en el compose.

## 2. Migraciones (la Api en Production NO auto-migra)

El auto-migrate y el seed solo corren en `Development`. En producción las migraciones se aplican **antes** del primer arranque con un *bundle* de EF Core, generado una vez desde una máquina con SDK .NET 10:

```bash
# Desde una máquina con SDK (no necesita MySQL vivo: la cadena NO se abre
# en design-time, las MYSQL_* solo arman la config; la cadena real se pasa
# al bundle en runtime).
dotnet tool install -g dotnet-ef   # si no está instalado
dotnet ef migrations bundle --project src/Enigma.Api -o efbundle
```

Copiar `efbundle` a la VPS (a la carpeta `deploy/` o la que se use) y ejecutarlo **dentro de la red del compose**, usando **la imagen de la propia API** (ya trae las deps runtime de .NET; una imagen genérica tipo `debian:bookworm-slim` NO sirve porque al bundle le faltan librerías):

```bash
cd deploy
chmod +x efbundle

# compose run hereda el `environment` del servicio api (MYSQL_HOST=db, etc.).
# --entrypoint es necesario: la imagen tiene ENTRYPOINT ["dotnet", "Enigma.Api.dll"]
# y sin él se ejecutaría `dotnet Enigma.Api.dll /efbundle`.
docker compose run --rm --no-deps --entrypoint /efbundle \
    -v "$PWD/efbundle:/efbundle" api
```

> ⚠️ **Verificar en el primer deploy real**: la combinación exacta de `compose run` + `--entrypoint` + volumen no se ha ejercitado contra producción todavía. Alternativa documentada pero no recomendada: `docker compose exec api /bin/bash -c ...` NO funciona — la imagen runtime no garantiza shell tools.

## 3. Primer usuario (bootstrap interim)

La seed (`admin`/`admin123` + las 2 instituciones de ejemplo, sobrescribibles con `ENIGMA_SEED_ADMIN_USER`/`ENIGMA_SEED_ADMIN_PASSWORD`) es **Development-only**. Como no existe aún un mecanismo de bootstrap de producción (decisión pendiente en `PRODUCT.md`), el bootstrap interim es:

```bash
cd deploy

# 1. Arrancar la api UNA VEZ en Development: auto-migra y siembra
#    admin/admin123 + las 2 instituciones. Corre en primer plano;
#    cortar con Ctrl+C cuando el log muestre el seed aplicado.
docker compose run --rm -e ASPNETCORE_ENVIRONMENT=Development api

# 2. Levantar la api definitiva en Production.
docker compose up -d api
```

> ⚠️ **Verificar en el primer deploy real** (los datos sembrados quedan en la base: si el paso 1 corrió dos veces, la seed es idempotente y no duplica). Un mecanismo de bootstrap proper (admin de producción, password único, sin instituciones de ejemplo) es decisión pendiente de `PRODUCT.md`.

## 4. Arranque y verificación

```bash
docker compose pull
docker compose up -d
curl -fsS https://"$ENIGMA_API_DOMAIN"/health
```
