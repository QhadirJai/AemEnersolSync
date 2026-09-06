# AemEnersolSync

An ASP.NET Core 8 Web API that pulls Platform and Well data from the AEM Enersol API and
stores it in a local SQL Server (LocalDB) database using **EF Core Code-First**.

One HTTP request drives the whole chain — log in for a JWT, call the platform endpoint
with it, upsert the results — with no tokens to copy by hand in between.

## How it works

```
POST /api/sync/actual
      │
      ├─ POST api/Account/Login          → JWT (returned as a bare JSON string)
      ├─ GET  api/PlatformWell/GetPlatformWellActual   (Authorization: Bearer <jwt>)
      └─ upsert platforms + nested wells into SQL Server, matched on the API's Id
```

`POST /api/sync/dummy` runs the same chain against `GetPlatformWellDummy`, whose payload
deliberately differs in shape — that endpoint is what demonstrates the sync tolerates a
changing contract.

## Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/sync/actual` | Login → `GetPlatformWellActual` → upsert |
| `POST` | `/api/sync/dummy` | Login → `GetPlatformWellDummy` → upsert |
| `GET` | `/api/platforms` | Stored platforms |
| `GET` | `/api/platforms/{id}` | One platform with its wells |
| `GET` | `/api/wells?platformId=` | Stored wells, optionally filtered |
| `GET` | `/api/wells/{id}` | One well |

Both sync endpoints return a tally of what happened:

```json
{
  "dataset": "Actual",
  "startedAt": "2026-09-06T14:25:02.09Z",
  "completedAt": "2026-09-06T14:25:02.76Z",
  "platforms": { "fetched": 2, "inserted": 2, "updated": 0, "skipped": 0 },
  "wells":     { "fetched": 1, "inserted": 1, "updated": 0, "skipped": 0 }
}
```

## Running it

Prerequisites: .NET 8 SDK and SQL Server LocalDB (installed with Visual Studio, or via
the SQL Server Express installer).

```powershell
# 1. Set the API base URL in appsettings.json (AemEnersolApi:BaseUrl)

# 2. Create the database from the Code-First migrations
dotnet tool restore
dotnet ef database update

# 3. Run
dotnet run
```

Swagger opens at `https://localhost:7171/swagger`; `AemEnersolSync.http` has the same
requests for VS Code / Visual Studio.

## Configuration

`appsettings.json`:

```json
"AemEnersolApi": {
  "BaseUrl": "",
  "Username": "user@aemenersol.com",
  "Password": "Test@123",
  "LoginPath": "api/Account/Login",
  "ActualPath": "api/PlatformWell/GetPlatformWellActual",
  "DummyPath": "api/PlatformWell/GetPlatformWellDummy",
  "TimeoutSeconds": 60
}
```

The credentials are the ones supplied with the assessment, so they live in
`appsettings.json` to keep the repo runnable as cloned. Anything real would go in user
secrets or environment variables instead (`AemEnersolApi__Password=...`), which the
standard configuration providers already support without a code change.

## Design notes

**Code-First.** `Platform` and `Well` in `Models/` plus `AppDbContext` define the schema;
`Migrations/` is generated from them. Nothing is scaffolded from an existing database.

**Ids come from the API.** Both keys are configured `ValueGeneratedNever()`, so SQL Server
never substitutes its own identity value for the one the API supplied. `Well.PlatformId`
arrives in the payload and is used directly as the foreign key.

**Upsert by Id.** Each run loads the rows matching the incoming Ids in one query, updates
those that exist and inserts the rest, then saves once — so the whole run is a single
transaction and re-running it is idempotent.

**Surviving a changed payload** (the `Dummy` requirement) rests on three things:

- Every DTO property is nullable, so a key the payload omits deserialises to `null`
  instead of throwing.
- `System.Text.Json` ignores unmapped keys by default, so the extra `lastUpdate` field
  needs no handling at all.
- The upsert writes only the fields that actually arrived. A missing `createdAt` leaves
  the stored value alone rather than blanking good data with `null` — verified by syncing
  Actual and then Dummy, after which the timestamps are still present and the names have
  been updated.

**Failures are reported, not swallowed.** An unreachable API, a rejected login or a
malformed response returns `502 Bad Gateway` with the upstream body in the detail, rather
than a bare 500. A well whose `platformId` matches no known platform is skipped and
counted rather than failing the entire run on a foreign-key violation.

**The sync is additive.** Rows the API stops returning are left in place, so a partial
upstream response can never wipe local data.

## Time spent

_<!-- TODO: fill in the honest total before submitting, e.g. "About 6 hours: ~1h reading
the brief and exploring the API in Swagger, ~1h on the Code-First model and migrations,
~2h on the login/fetch/upsert pipeline, ~1h on the resilience behaviour and testing both
datasets, ~1h on documentation." -->_
