# StayDesk

Hotel booking platform with a public booking site and a staff back-office. ASP.NET Core 10 + Angular 22 + PostgreSQL.

**Status:** early build. Phase 1 (foundation) is in progress; there are no features to use yet.

## What it will do

- Guests search rooms by city, dates and guest count, see a per-night price breakdown, and book with a mock payment step.
- Staff manage bookings, check guests in and out, create walk-in bookings, and watch an occupancy calendar that updates live.
- Admins manage properties, rooms, price rules and users, and read an append-only audit log.
- UI in English and German.

## Technical points

These are specified in [`docs/SRS.md`](docs/SRS.md), not yet built:

- **No double bookings.** A row lock plus a PostgreSQL exclusion constraint. An integration test fires 20 parallel requests and expects 1 success and 19 conflicts.
- **Unpaid holds expire without a background job.** Availability ignores Pending bookings past their expiry, so a sleeping free-tier host can't block rooms.
- **Deterministic pricing.** One strategy class per rule type, per-night rounding, and a price snapshot stored on the booking.
- **Audit log** written by an EF Core `SaveChangesInterceptor`, not by hand in each service.

## Stack

| Area | Choice |
|------|--------|
| API | ASP.NET Core 10, Clean Architecture, EF Core 10 + Npgsql, Identity + JWT, SignalR, FluentValidation, Serilog |
| Database | PostgreSQL 16+ |
| Frontend | Angular 22, Angular Material, RxJS, ngx-translate, ng2-charts |
| Tests | xUnit v3, Shouldly, Testcontainers, Vitest |
| Ops | Docker Compose, GitHub Actions |

Design decisions and their tradeoffs: [`docs/decisions.md`](docs/decisions.md).

## Roadmap

Build order from SRS §10:

- [ ] 1. Foundation: solution, Docker Compose, data model, seed data, CI (CI added, first run pending)
- [ ] 2. Auth and inventory
- [ ] 3. Search and pricing
- [ ] 4. Booking core
- [ ] 5. Back-office and audit log
- [ ] 6. Live calendar and dashboard
- [ ] 7. Translations, accessibility, deployment

## Running locally

Requires Docker.

```bash
docker compose up -d --build --wait
```

This starts PostgreSQL, Mailpit (http://localhost:8025) and the API (http://localhost:8080). The API applies migrations and seeds demo data on startup. `GET /health` reports database status.

Demo accounts (local only): `admin@example.com` / `Admin123`, `staff@example.com` / `Staff123`, `guest@example.com` / `Guest123`. There's no login endpoint yet.

Running tests needs the .NET 10 SDK and Docker:

```bash
dotnet test
```
