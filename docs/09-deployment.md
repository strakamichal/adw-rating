# Deployment

ADW Rating runs on a Hetzner VPS managed by [Coolify](https://coolify.io). The stack consists of three Docker containers: API (ASP.NET), Web (Blazor SSR), and MSSQL. Coolify handles TLS certificates (Let's Encrypt), reverse proxy (Traefik), and container lifecycle. GitHub Actions builds and pushes Docker images, then triggers Coolify to redeploy.

## Architecture

```
Internet
  │
  ├── rating.agilitydogsworld.com ──► Traefik (Coolify) ──► adwrating-web:8080
  │                                                              │
  └── api.rating.agilitydogsworld.com ──► Traefik ──► adwrating-api:8080
                                                           │
                                                    MSSQL :1433
```

- **TLS terminates at Traefik** — Coolify auto-provisions Let's Encrypt certificates. Containers receive plain HTTP.
- **Web → API** communication goes over the internal Docker network (`coolify`), not via the public URL. Web calls `http://adwrating-api:8080`.
- **MSSQL** runs in a separate container outside the Coolify project, accessible via Docker bridge IP (typically `172.17.0.1`, find with `ip addr show docker0 | grep inet`).

## Infrastructure files

All deployment-related files live in `infra/`:

| File | Purpose |
|---|---|
| `infra/Dockerfile.api` | Multi-stage build for API image |
| `infra/Dockerfile.web` | Multi-stage build for Web image |
| `infra/coolify-setup.sh` | Automated Coolify setup via API (creates project, services, env vars, deploys) |
| `infra/coolify-setup.env.example` | Template for setup script configuration |
| `.dockerignore` | Docker build exclusions (must stay in repo root) |

Both Dockerfiles use the repo root as build context — only the `file:` path changes.

## CI/CD pipeline

**File:** `.github/workflows/deploy.yml`

Every push to `main` triggers the full pipeline:

```
push to main
  └─► GitHub Actions
        ├─► build-api (parallel) ──► ghcr.io/.../api:latest
        ├─► build-web (parallel) ──► ghcr.io/.../web:latest
        └─► deploy (after both builds)
              ├─► Coolify API: redeploy API service
              └─► Coolify API: redeploy Web service
```

Coolify does **not** poll the registry for new images. The `deploy` job calls the Coolify deploy API with a bearer token after both images are pushed.

### GitHub repository secrets

| Secret | Description |
|---|---|
| `COOLIFY_TOKEN` | Coolify API token (Settings → API → Generate Token, Read & Write permissions) |
| `COOLIFY_API_UUID` | UUID of the API service (output by setup script or found in Coolify URL) |
| `COOLIFY_WEB_UUID` | UUID of the Web service |

`GITHUB_TOKEN` is used automatically for pushing images to ghcr.io — no manual setup needed.

## Initial setup from scratch

### 1. VPS prerequisites

- MSSQL container running on port 1433
- Docker logged into ghcr.io:
  ```bash
  docker login ghcr.io -u <github-username>
  # Password: GitHub PAT with read:packages scope
  ```
- Firewall open for HTTP/HTTPS:
  ```bash
  ufw allow 80/tcp && ufw allow 443/tcp
  ```
- Coolify API enabled with a generated token (Settings → API)
- DNS A records for `rating.agilitydogsworld.com` and `api.rating.agilitydogsworld.com` pointing to the VPS IP

### 2. Run setup script

The script creates a Coolify project, both services with environment variables, health checks, and triggers the first deployment.

```bash
cp infra/coolify-setup.env.example infra/coolify-setup.env
# Fill in: COOLIFY_URL, COOLIFY_TOKEN, SERVER_UUID, ENVIRONMENT_NAME,
#          GHCR_OWNER, domains, DB connection strings
vim infra/coolify-setup.env
bash infra/coolify-setup.sh
```

The script will:
- Create a Coolify project (if `PROJECT_UUID` is not set)
- Create API service with domain, health check, and env vars
- Create Web service with domain and env vars
- Deploy both services

Required env vars are documented in `infra/coolify-setup.env.example`. The `coolify-setup.env` file is gitignored (contains secrets).

### 3. Manual steps after script

Container names cannot be set via the Coolify API. After the script completes:

1. In Coolify UI → API service → General tab → set **Container Name** to `adwrating-api`
2. In Coolify UI → Web service → General tab → set **Container Name** to `adwrating-web`
3. Redeploy both services

This gives containers stable hostnames on the Docker network. Without this, Coolify generates random names that change on every redeploy, breaking Web → API communication.

### 4. Set GitHub secrets

Use the UUIDs from the script output:

```bash
# In GitHub repo → Settings → Secrets and variables → Actions
COOLIFY_TOKEN    = <your Coolify API token>
COOLIFY_API_UUID = <UUID from script output>
COOLIFY_WEB_UUID = <UUID from script output>
```

### 5. Verify

```bash
curl https://api.rating.agilitydogsworld.com/health
# → {"status":"healthy"}

open https://rating.agilitydogsworld.com
```

## Environment variables

### API container (`adwrating-api`)

| Variable | Required | Description |
|---|---|---|
| `ADW_RATING_CONNECTION` | Yes | App connection string. Example: `Server=172.17.0.1,1433;Database=AdwRating;User Id=adwrating;Password=...;TrustServerCertificate=True` |
| `ADW_RATING_ADMIN_CONNECTION` | No | SA connection string for initial bootstrap (creates login, database, user). Remove after first successful deploy. |
| `ASPNETCORE_ENVIRONMENT` | No | Defaults to `Production` in the container. |

### Web container (`adwrating-web`)

| Variable | Required | Description |
|---|---|---|
| `ApiBaseUrl` | Yes | `http://adwrating-api:8080` — internal Docker hostname of the API container |
| `ASPNETCORE_ENVIRONMENT` | No | Defaults to `Production` in the container. |

## Database bootstrap

On startup, the API performs two steps:

**1. Bootstrap** (only when `ADW_RATING_ADMIN_CONNECTION` is set): connects as `sa` and creates the SQL Server login, database, and user with `db_owner` role. All operations are idempotent. Remove the env var after first successful deploy.

**2. Migrations** (always): connects as the app user and runs `Database.MigrateAsync()` to apply all pending EF Core migrations. Idempotent — safe on every startup.

## Seeding data

After first deploy the database is empty. Import competition data using the CLI via SSH tunnel:

```bash
ssh -L 1433:localhost:1433 root@your-vps
# In another terminal, run CLI locally against localhost:1433
```

## Reverse proxy notes

- **Forwarded headers** middleware is enabled in both API and Web so the app sees the real client IP and HTTPS scheme through Traefik
- **HTTPS redirect is not in the app** — Traefik handles TLS termination and HTTP→HTTPS redirect
- **CORS** is set to allow any origin (MVP setting)
- **Container names matter** — Web resolves `adwrating-api` via Docker DNS on the `coolify` network. Both services must be in the same Coolify project to share this network.
