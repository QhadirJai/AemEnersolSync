# AemEnersolSync

An ASP.NET Core 8 Web API that pulls Platform and Well data from the AEM Enersol test API
(<http://test-demo.aemenersol.com>) and stores it in a local SQL Server (LocalDB) database
using **EF Core Code-First**.

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
  "platforms": { "fetched": 10, "inserted": 10, "updated": 0, "skipped": 0 },
  "wells":     { "fetched": 15, "inserted": 15, "updated": 0, "skipped": 0 }
}
```

## Running it

Prerequisites: .NET 8 SDK and SQL Server LocalDB (installed with Visual Studio, or via
the SQL Server Express installer).

```powershell
# 1. Create the database from the Code-First migrations
dotnet tool restore
dotnet ef database update

# 2. Run
dotnet run

# 3. Trigger a sync (or click Execute in Swagger)
curl -X POST http://localhost:5237/api/sync/actual
```

The API base URL and credentials are already set in `appsettings.json`, so a fresh clone
runs against the test API without further configuration.

Swagger opens at `/swagger` on whichever launch profile you use (`http://localhost:5237/swagger`
by default); `AemEnersolSync.http` has the same requests for VS Code / Visual Studio.

## Configuration

`appsettings.json`:

```json
"AemEnersolApi": {
  "BaseUrl": "http://test-demo.aemenersol.com",
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

## Verified against the live API

Run against <http://test-demo.aemenersol.com>, which returns 10 platforms and 15 wells:

| Check | Result |
| --- | --- |
| `POST /api/sync/actual` on an empty database | 10 platforms + 15 wells inserted |
| Same call again | 0 inserted, 25 rows **updated** — the upsert is idempotent |
| `POST /api/sync/dummy` after Actual | 200 OK, 25 updated, and `createdAt`/`updatedAt` still hold their Actual values |
| `POST /api/sync/dummy` on an empty database | 25 rows inserted with null timestamps — the insert path survives the missing keys too |
| `POST /api/sync/actual` after that | the same rows updated, timestamps backfilled |

The difference between the two payloads, confirmed from the live responses:

| | Actual | Dummy |
| --- | --- | --- |
| `id`, `uniqueName`, `latitude`, `longitude` | present | present |
| `createdAt`, `updatedAt` | present | **absent** |
| `lastUpdate` | absent | **present** (unmapped, ignored) |

## Part 2 — SQL query

`SqlQueries/LastUpdatedWellPerPlatform.sql` returns the last updated well for each
platform: `ROW_NUMBER()` ranks each platform's wells newest-first and only rank 1 is kept,
with `Id` as a tie-breaker so wells sharing an `UpdatedAt` still yield one stable row. The
file also carries a `CROSS APPLY` variant, which can be the better plan on a large table.

Run against the synced data it reproduces the expected result exactly:

| PlatformName | Id | PlatformId | UniqueName | Latitude | Longitude | CreatedAt | UpdatedAt |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Platform1 | 1 | 11 | Well11 | 37.06257 | 18.406885 | 2017-11-01 02:41:00 | 2018-08-04 02:16:42 |
| Platform2 | 5 | 12 | Well22 | 181.01989 | 92.034426 | 2018-11-24 06:12:26 | 2018-08-20 14:59:48 |
| Platform3 | 8 | 13 | Well32 | 289.631824 | 147.255081 | 2015-11-18 23:08:44 | 2016-10-20 12:20:45 |
| Platform4 | 10 | 14 | Well41 | 357.894286 | 184.068851 | 2017-04-16 09:55:14 | 2018-09-16 19:26:11 |
| Platform5 | 14 | 15 | Well52 | 489.834418 | 257.696391 | 2017-10-29 07:48:12 | 2016-01-28 18:30:25 |

## Time spent

**About 3 hours.**

Most of the time was used before I wrote any code. I spent it reading the API in Swagger, to
check two things that are easy to get wrong. First, the login endpoint gives back the JWT as
a plain JSON string, not inside an object, so I need to read it as a plain string. Second,
the Authorization header needs the word "Bearer" in front of the token, or the API replies
with 401. I also checked the data shape: platforms come as an array, and each platform has
its wells inside a field called "well" (not "wells"), and each well already has "platformId"
inside it, so I can use that directly as the foreign key without extra logic.

After I understood these things, the coding part was simple: I made Code-First entities and
a migration, an HttpClient for login and fetching data, and an upsert based on the "id" from
the API.

The rest of the time was for the requirement that the app must not break when the data shape
changes. This part needed more care, not more time. I used three things together: DTO
properties are nullable, so if a key is missing, it becomes null instead of causing an error.
System.Text.Json ignores any key it doesn't know, so the extra "lastUpdate" field in Dummy is
simply ignored. And the upsert only writes fields that are actually present, so if
"createdAt" is missing in Dummy, the old value in the database stays instead of becoming
null. This last point is easy to miss — you only notice it if you sync Actual, then sync
Dummy, and check if the timestamps are still there. So I tested both cases: Dummy into an
empty database, and Dummy after Actual already filled the database.
