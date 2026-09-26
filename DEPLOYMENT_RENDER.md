# KrishiLink Render Deployment & Docker Guide

This guide provides step-by-step instructions for containerizing and deploying **KrishiLink** to [Render](https://render.com/) with automated CI/CD via GitHub Actions.

---

## Architecture Overview

```
                          +------------------------+
                          |   Internet / Clients   |
                          +-----------+------------+
                                      |
                                      v HTTPS
                     +----------------------------------+
                     |  Render Edge Reverse Proxy / CDN |
                     +----------------+-----------------+
                                      |
                                      v HTTP ($PORT)
                      +---------------+---------------+
                      |   KrishiLink Web Container    |
                      |   (.NET 8 Linux / Non-root)   |
                      +-------+---------------+-------+
                              |               |
             +----------------+               +----------------+
             |                                                 |
             v                                                 v
  +----------------------+                           +--------------------+
  | Render Persistent    |                           | Supabase / Cloud   |
  | Disk (/App_Data/keys)|                           | Services           |
  +----------------------+                           +--------------------+
                                                     | • PostgreSQL (5432)|
                                                     | • Supabase Storage |
                                                     | • Supabase Auth    |
                                                     | • Groq / Gemini AI |
                                                     +--------------------+
```

---

## Option 1: One-Click Deploy via Render Blueprint (`render.yaml`)

The repository includes a ready-to-use [`render.yaml`](./render.yaml) file for Render Infrastructure-as-Code.

1. **Push your code to GitHub**:
   Ensure your repository on GitHub contains `Dockerfile`, `render.yaml`, and the `.github` workflows.

2. **Open Render Dashboard**:
   - Navigate to [dashboard.render.com](https://dashboard.render.com/).
   - Click **New +** > **Blueprint**.
   - Connect your GitHub repository (`KrishiLink`).

3. **Configure Environment Secrets**:
   Render will parse `render.yaml` and create the Web Service and Persistent Disk. Enter the secret environment variables when prompted:
   - `DATABASE_URL`: PostgreSQL connection string (Supabase Session Pooler or Direct connection on port 5432).
   - `DIRECT_URL`: Direct PostgreSQL connection string for migration locks.
   - `SUPABASE_URL`: e.g. `https://your-ref.supabase.co`
   - `SUPABASE_PUBLISHABLE_KEY`: Supabase anon/publishable key.
   - `SUPABASE_SECRET_KEY`: Supabase service_role / secret key.
   - `GROQ_API_KEY`: Groq API key for AI assistant.
   - `GEMINI_API_KEY`: Gemini API key for fallback AI assistant.

4. **Deploy**:
   Click **Apply**. Render will automatically build the Docker image, attach the persistent disk at `/app/App_Data/keys`, run database migrations, and launch the service.

---

## Option 2: Manual Web Service Setup on Render

If you prefer configuring the service manually through the Render Dashboard:

1. **Create Web Service**:
   - Go to [dashboard.render.com](https://dashboard.render.com/) > **New +** > **Web Service**.
   - Select **Build and deploy from a Git repository** and connect your repository.

2. **Configure General Settings**:
   - **Name**: `krishilink`
   - **Region**: `Singapore` (or region closest to your PostgreSQL database)
   - **Branch**: `main`
   - **Runtime**: `Docker`
   - **Dockerfile Path**: `./Dockerfile`
   - **Instance Type**: `Starter` (recommended for persistent disk support) or `Free`

3. **Configure Health Check**:
   - **Health Check Path**: `/healthz`

4. **Attach Persistent Disk (Crucial for Data Protection)**:
   - In the **Disks** section, click **Add Disk**.
   - **Name**: `dataprotection-keys`
   - **Mount Path**: `/app/App_Data/keys`
   - **Size**: `1 GB`
   *(This ensures Data Protection encryption keys persist across deploys and container restarts, so users are not signed out and encrypted NIDs remain decryptable).*

5. **Set Environment Variables**:
   Add the following environment variables in **Environment**:

   | Variable | Value | Description |
   |---|---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` | Runs app in Production mode |
   | `Database__ApplyMigrationsOnStartup` | `true` | Applies EF Core migrations on boot |
   | `App__PublicBaseUrl` | `https://krishilink.onrender.com` | Public HTTPS origin (replace with your domain) |
   | `AllowedHosts` | `krishilink.onrender.com;localhost` | Allowed host headers (semicolon-separated) |
   | `ForwardedHeaders__KnownNetworks__0` | `0.0.0.0/0` | Trust Render reverse proxy for HTTPS redirection & IP headers |
   | `ForwardedHeaders__KnownNetworks__1` | `::/0` | Trust IPv6 proxy headers |
   | `DataProtection__KeyPath` | `/app/App_Data/keys` | Path to persistent Data Protection key ring |
   | `DATABASE_URL` | *`postgresql://...`* | Supabase PostgreSQL URI (port 5432) |
   | `DIRECT_URL` | *`postgresql://...`* | Supabase direct PostgreSQL URI |
   | `SUPABASE_URL` | *`https://xyz.supabase.co`* | Supabase Project URL |
   | `SUPABASE_PUBLISHABLE_KEY` | *`sb_publishable_...`* | Supabase Publishable Key |
   | `SUPABASE_SECRET_KEY` | *`sb_secret_...`* | Supabase Service Secret Key |
   | `GROQ_API_KEY` | *`gsk_...`* | Groq API Key |
   | `GEMINI_API_KEY` | *`AIza...`* | Google Gemini API Key |

6. Click **Create Web Service**.

---

## GitHub Actions CI/CD Pipeline

The project includes two GitHub Actions workflows:

### 1. Continuous Integration (`.github/workflows/ci.yml`)
Runs on every push and pull request to `main` or `develop`:
- Validates code formatting (`dotnet format`)
- Checks view CSS utility classes, localized TempData, CSP-safe handlers, and Service Worker routing
- Runs security audits on NuGet dependencies and secret scans
- Compiles the solution in `Release` mode
- Executes unit and integration test suites
- Validates EF Core migration scripts and model changes
- **Verifies Docker image build** to prevent container regressions

### 2. Continuous Deployment (`.github/workflows/deploy.yml`)
Runs automatically on merge to `main`:
- Publishes the production bundle
- Generates idempotent PostgreSQL migration SQL (`migrations.sql`)
- Builds and verifies the release container image
- **Triggers automated Render deployment via Deploy Hook**

### Setting Up Zero-Touch Deployments with Render Deploy Hooks

1. In Render Dashboard, go to your Web Service > **Settings**.
2. Scroll to **Deploy Hook** and copy the unique URL (`https://api.render.com/deploy/srv-xxxx?key=yyyy`).
3. In GitHub, go to your repository > **Settings** > **Secrets and variables** > **Actions**.
4. Click **New repository secret**:
   - **Name**: `RENDER_DEPLOY_HOOK_URL`
   - **Secret**: *Paste the copied Render Deploy Hook URL*
5. Every time you push or merge to `main`, GitHub Actions will automatically test, build, and trigger a fresh deployment on Render!

---

## Local Testing with Docker & Docker Compose

### Using Docker Compose (Full Stack)
Run the application and a dedicated PostgreSQL database container locally:

```bash
# Build and start services in background
docker compose up -d --build

# View logs
docker compose logs -f app

# Open in browser
# http://localhost:8080

# Stop services
docker compose down
```

### Running the Docker Image Directly
```bash
# Build image
docker build -t krishilink:local .

# Run container
docker run -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e DATABASE_URL="Host=host.docker.internal;Port=5432;Database=krishilink;Username=postgres;Password=postgres;SSL Mode=Disable" \
  krishilink:local
```

---

## Health Checks & Diagnostic Probes

KrishiLink provides standard Kubernetes/Cloud health check endpoints:
- `GET /healthz` - **Liveness Probe**: Returns `200 OK` (Healthy) as long as the process is alive.
- `GET /readyz` - **Readiness Probe**: Verifies that PostgreSQL database connectivity and Supabase Auth services are reachable and functional.
