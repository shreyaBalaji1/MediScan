# MediScan

A full-stack medication lookup app. Search real FDA drug label data, save favorites, keep personal notes on each medication, and check whether two drugs' label text mention each other — all behind per-user authentication.

**Live demo:** _TODO: add link once deployed_

## Features

- **Drug search** — looks up real medication data (purpose, warnings) from the [openFDA](https://open.fda.gov/) drug label API.
- **Accounts** — register/login with a hashed password (ASP.NET Core `PasswordHasher`) and a JWT-based session. Every user's favorites and notes are private to them.
- **Favorites** — save drugs you've looked up for quick reference later.
- **Notes** — attach and edit personal notes on any drug, from the search result or from a saved favorite.
- **Interaction checker** — enter two drug names and see whether either drug's FDA label text mentions the other, as a quick sanity check (not a clinical interaction database — see the in-app disclaimer).

## Tech stack

| Layer     | Tech                                                            |
| --------- | ---------------------------------------------------------------|
| Frontend  | Angular 21 (standalone components), TypeScript, RxJS           |
| Backend   | ASP.NET Core Web API (.NET 10)                                  |
| Database  | SQLite via Entity Framework Core (migrations included)          |
| Auth      | JWT bearer tokens, ASP.NET Core Identity password hashing        |
| External  | [openFDA](https://open.fda.gov/apis/drug/label/) drug label API |

## Architecture

```
mediscan-ui (Angular, :4200)
   │  HTTP + JWT bearer token (via an HTTP interceptor)
   ▼
MediScanApi (ASP.NET Core, :5173)
   │
   ├── AuthController      → register/login, issues JWTs
   ├── DrugsController      → proxies openFDA search + interaction check
   ├── FavoritesController  → CRUD, scoped to the logged-in user
   └── NotesController      → CRUD, scoped to the logged-in user
   │
   ▼
SQLite (MediScan.db) via EF Core
```

Auth is stateless: the server never stores sessions. A JWT issued at login carries the user's ID as a claim; the API verifies its signature on every request and reads the user ID back out of it to scope favorites/notes queries.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20+ and npm
- The [EF Core CLI tool](https://learn.microsoft.com/ef/core/cli/dotnet) (`dotnet tool install --global dotnet-ef`), only needed if you change the data models

### Backend (`MediScanApi/`)

```bash
cd MediScanApi
dotnet restore
dotnet ef database update   # creates MediScan.db with the latest schema
dotnet run --urls http://localhost:5173
```

Before running for real (not just local dev), replace the placeholder `Jwt:Key` in `appsettings.json` with a strong secret — it's what signs and verifies login tokens.

Swagger UI is available at `http://localhost:5173/swagger` in development.

### Frontend (`mediscan-ui/`)

```bash
cd mediscan-ui
npm install
npm start   # ng serve, runs on http://localhost:4200
```

The frontend expects the API at `http://localhost:5173` (see the `apiUrl` in `src/app/services/*.ts`).

### Running tests

```bash
cd mediscan-ui && npm test
```

The backend doesn't have a test project yet (`MediScanApi.Tests`) — that's a planned addition, not currently wired up.

## Deployment

The API deploys to [Render](https://render.com) (Docker) and the frontend to [Vercel](https://vercel.com), both free tier.

### API → Render

1. Push this repo to GitHub, then in Render: **New → Blueprint**, point it at the repo. It picks up [`render.yaml`](render.yaml), which builds [`MediScanApi/Dockerfile`](MediScanApi/Dockerfile) and generates a random `Jwt__Key` for you.
2. On the free tier there's no persistent disk or shell access, so:
   - Migrations run automatically on every startup (see `db.Database.Migrate()` in [Program.cs](MediScanApi/Program.cs)), so the schema always exists.
   - The SQLite file lives on the container's ephemeral disk — data resets on every redeploy/restart. Fine for a portfolio demo; if you need real persistence, swap in a managed Postgres and update `ConnectionStrings__DefaultConnection` + the `UseSqlite`/`UseNpgsql` call in `Program.cs`.
3. Once deployed, note the Render URL (e.g. `https://mediscan-api.onrender.com`) — you'll need it for the frontend.

### Frontend → Vercel

1. Import the repo in Vercel and set **Root Directory** to `mediscan-ui` (this is a monorepo).
2. Before deploying, update `apiBaseUrl` in [`src/environments/environment.prod.ts`](mediscan-ui/src/environments/environment.prod.ts) to your actual Render URL + `/api`.
3. Vercel will use [`vercel.json`](mediscan-ui/vercel.json), which builds with `npm run build` (production config, which picks up `environment.prod.ts` via the `fileReplacements` in `angular.json`) and rewrites all routes to `index.html` so client-side routing works.
4. Back on Render, update the `Cors__AllowedOrigins__0` env var to your Vercel URL (e.g. `https://mediscan-ui.vercel.app`) so the API's CORS policy allows it — otherwise the deployed frontend's requests will be blocked.

## Project structure

```
MediScanApi/       ASP.NET Core Web API
  Controllers/      Auth, Drugs, Favorites, Notes
  Models/           EF Core entities + DTOs
  Data/             AppDbContext
  Migrations/       EF Core migrations

mediscan-ui/        Angular app
  src/app/pages/     login, register, dashboard
  src/app/services/  drug, favorite, note, auth (HTTP clients)
  src/app/guards/    route guard for authenticated pages
  src/app/interceptors/  attaches the JWT to outgoing requests
```
