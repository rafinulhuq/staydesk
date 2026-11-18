# StayDesk

Hotel booking platform with a public booking site and a staff back-office. ASP.NET Core 10 + Angular 22 + PostgreSQL.

**Status:** early build. Phases 1 to 4 are done: login and registration, admin pages for properties, rooms and price rules, the public site to search free rooms with a per-night price breakdown, and booking: a 3-step booking flow with mock payment, a confirmation email, My bookings with cancellation, and staff walk-in bookings through the API. The back-office bookings table with filters and CSV export is in; booking detail actions and the audit log come next.

## What it will do

- Guests search rooms by city, dates and guest count, see a per-night price breakdown, and book with a mock payment step.
- Staff manage bookings, check guests in and out, create walk-in bookings, and watch an occupancy calendar that updates live.
- Admins manage properties, rooms, price rules and users, and read an append-only audit log.
- UI in English and German.

## Technical points

Built:

- **Deterministic pricing.** A pure pricing engine with one strategy class per rule type, multiplicative stacking, and rounding once per night, so the total always equals the sum of the nights. Admins can preview an unsaved rule against a room before saving it.
- **Search state in the URL.** City, dates, filters, sort and page live in the query string, so a search survives reload and can be shared. Known limit: totals depend on price rules, so price filtering, sorting and paging run in memory after the SQL availability filter.
- **No double bookings.** Booking creation locks the room row (`SELECT … FOR UPDATE`), and a PostgreSQL exclusion constraint backs it up; both outcomes map to 409, never 500. An integration test fires 20 parallel requests for the same room and dates and gets 1 × 201 and 19 × 409.
- **Unpaid holds expire without relying on a job.** Availability ignores Pending bookings past their 15 minutes, and booking creation cancels expired holds it meets. A once-a-minute job only tidies up the rest.
- **Idempotent booking.** `POST /bookings` requires an `Idempotency-Key`; a retry returns the original booking instead of a second one.
- **Price snapshot.** The per-night breakdown is stored on the booking and never recalculated. If the price moved since the guest saw it, the API answers 409 with the new quote.
- **Cancellation fee in real hours.** Free until 48 hours before the check-in instant in the property's time zone, then the first night is kept. Unit tests cover the boundary minute and the October clock change.

Specified in [`docs/SRS.md`](docs/SRS.md), not yet built:

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

- [x] 1. Foundation: solution, Docker Compose, data model, seed data, CI
- [x] 2. Auth and inventory
- [x] 3. Search and pricing
- [x] 4. Booking core
- [ ] 5. Back-office and audit log
- [ ] 6. Live calendar and dashboard
- [ ] 7. Translations, accessibility, deployment

## Running locally

Requires Docker.

```bash
docker compose up -d --build --wait
```

This starts PostgreSQL, Mailpit (http://localhost:8025) and the API (http://localhost:8080). The API applies migrations and seeds demo data on startup. `GET /health` reports database status.

Demo accounts (local only): `admin@example.com` / `Admin123`, `staff@example.com` / `Staff123`, `guest@example.com` / `Guest123`. Swagger UI is at http://localhost:8080/swagger: call `POST /api/v1/auth/login`, then paste the `accessToken` into Authorize.

The web app needs Node 24:

```bash
cd client/staydesk-web && npm ci && npm start   # http://localhost:4200, proxies /api to :8080
```

Running tests needs the .NET 10 SDK and Docker:

```bash
dotnet test
```
