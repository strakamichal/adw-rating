# Deployment

ADW Rating runs as two Docker containers (API + Web) on a Hetzner VPS with Coolify. MSSQL runs separately in its own container. GitHub Actions builds Docker images and pushes them to GitHub Container Registry (ghcr.io). Coolify pulls the images and manages TLS, reverse proxy (Traefik), and container lifecycle.

## Architecture

```
Internet
  │
  ├── adwrating.example.com ──► Traefik (Coolify) ──► adwrating-web:8080 (Blazor SSR)
  │                                                        │
  └── api.adwrating.example.com ──► Traefik ──► adwrating-api:8080 (ASP.NET API)
                                                           │
                                                    MSSQL :1433
```

- **TLS terminates at Traefik** — containers receive plain HTTP
- **Web calls API** over internal Docker network (not via public URL)
- **MSSQL** is accessible via Docker bridge IP (runs outside Coolify project, port 1433 published on host). Find the IP with `ip addr show docker0 | grep inet` on the VPS (typically `172.17.0.1` or `10.0.0.1`)

## Docker images

Two Dockerfiles in repo root, both multi-stage (SDK build → ASP.NET runtime):

| Dockerfile | Image | Entrypoint |
|---|---|---|
| `Dockerfile.api` | `ghcr.io/<owner>/adw-rating/api:latest` | `AdwRating.Api.dll` |
| `Dockerfile.web` | `ghcr.io/<owner>/adw-rating/web:latest` | `AdwRating.Web.dll` |

Both containers expose port **8080**.

## CI/CD pipeline

**File:** `.github/workflows/deploy.yml`

Triggers on push to `main`. Builds both images in parallel and pushes to ghcr.io using the built-in `GITHUB_TOKEN` (no manual secrets needed).

```
push to main → GitHub Actions → build Dockerfile.api → ghcr.io/.../api:latest
                               → build Dockerfile.web → ghcr.io/.../web:latest
```

Coolify does **not** automatically detect new images in the registry. To trigger deploys after CI pushes new images, add webhook calls to the GitHub Actions workflow. Each Coolify service has a webhook URL (found in the service's "Webhooks" tab):

```yaml
- name: Deploy API
  run: curl -s "${{ secrets.COOLIFY_WEBHOOK_API }}"

- name: Deploy Web
  run: curl -s "${{ secrets.COOLIFY_WEBHOOK_WEB }}"
```

Store the webhook URLs as GitHub repository secrets (`COOLIFY_WEBHOOK_API`, `COOLIFY_WEBHOOK_WEB`). Alternatively, deploy manually from the Coolify UI.

## Environment variables

### API (`adwrating-api`)

| Variable | Required | Description |
|---|---|---|
| `ADW_RATING_CONNECTION` | Yes | App connection string. Example: `Server=DOCKER_BRIDGE_IP,1433;Database=AdwRating;User Id=adwrating;Password=...;TrustServerCertificate=True` |
| `ADW_RATING_ADMIN_CONNECTION` | No | SA connection string for initial bootstrap. When set, API creates the login, database, and user from `ADW_RATING_CONNECTION` automatically. Can be removed after first successful deploy. Example: `Server=DOCKER_BRIDGE_IP,1433;Database=master;User Id=sa;Password=...;TrustServerCertificate=True` |
| `ASPNETCORE_ENVIRONMENT` | No | Set to `Production` (default in container). |

### Web (`adwrating-web`)

| Variable | Required | Description |
|---|---|---|
| `ApiBaseUrl` | Yes | Internal URL of the API container. Example: `http://adwrating-api:8080` (use the Docker container name/hostname visible in Coolify) |
| `ASPNETCORE_ENVIRONMENT` | No | Set to `Production`. |

## Database bootstrap

On startup, the API performs two steps:

### 1. Bootstrap (optional, when `ADW_RATING_ADMIN_CONNECTION` is set)

Connects as `sa` and creates:
- SQL Server login (from `ADW_RATING_CONNECTION` User Id)
- Database (from `ADW_RATING_CONNECTION` Initial Catalog)
- Database user with `db_owner` role

All operations are idempotent — safe to run on every startup. Once the login/database/user exist, you can remove `ADW_RATING_ADMIN_CONNECTION` and the bootstrap is skipped.

### 2. Migrations (always)

Connects as the app user and runs `Database.MigrateAsync()`. This:
- Creates the database if it doesn't exist (but won't have permissions without bootstrap or manual setup)
- Applies all pending EF Core migrations
- Is idempotent — safe to run on every startup

## Coolify setup (step by step)

### Prerequisites

1. MSSQL running in a container on the VPS (port 1433)
2. Docker logged into ghcr.io on the VPS:
   ```bash
   ssh root@your-vps
   docker login ghcr.io -u <github-username>
   # Password: GitHub PAT with read:packages scope
   ```

### Create services

1. Create a **Project** in Coolify (e.g., "ADW Rating")
2. Add resource → **Docker Image** for API:
   - Image: `ghcr.io/<owner>/adw-rating/api:latest`
   - Domain: `api.adwrating.example.com`
   - Ports Exposes: `8080`
   - **Container Name**: `adwrating-api` (General tab — this becomes the stable hostname on the Docker network)
   - Health check: HTTP GET `/health`
   - Set environment variables (see table above)
3. **Deploy API first** — wait until healthy (migrations create all tables)
4. Verify: `curl https://api.adwrating.example.com/health` → `{"status":"healthy"}`
5. Add resource → **Docker Image** for Web:
   - Image: `ghcr.io/<owner>/adw-rating/web:latest`
   - Domain: `adwrating.example.com`
   - Ports Exposes: `8080`
   - **Container Name**: `adwrating-web`
   - Set environment variables (see table above)
   - `ApiBaseUrl` = `http://adwrating-api:8080` (uses the container name set above)
6. **Deploy Web**
7. Verify: open `https://adwrating.example.com` in browser

Both services **must be in the same Coolify project** to share a Docker network (`coolify`). Without explicit container names, Coolify generates random hostnames that change on every redeploy — always set a fixed container name.

## Seeding data

After first deploy the database is empty. Import competition data using the CLI:

Option A — **SSH tunnel** from local machine:
```bash
ssh -L 1433:localhost:1433 root@your-vps
# In another terminal, run CLI locally against localhost:1433
```

Option B — copy CSV data to VPS and run a one-off container.

## Reverse proxy notes

- **Forwarded headers** middleware is enabled in both API and Web so the app sees the real client IP and HTTPS scheme through Traefik
- **HTTPS redirect is removed** from Web — Traefik handles TLS termination, adding redirect in the app would cause a loop
- **CORS** is set to allow any origin (MVP setting)
