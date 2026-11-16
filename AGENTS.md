# StayDesk

Accommodation booking platform (public site + staff back-office). Portfolio project for an Associate Software Engineer (.NET / Angular) role.

**Source of truth: `docs/SRS.md`.** Follow it exactly. If code and the SRS disagree, the SRS wins. Propose an SRS change instead of silently diverging from it.

**Start here: `HANDOFF.md`** (current state and how to resume). Update it at the end of every session or sub-feature.

**Open work: `TRACKER.md`.** It holds open work, unverified items and pending decisions. Read it at session start, and update it in the same step as any new finding or decision.

## Stack (do not substitute)

- **Backend:** .NET 10, ASP.NET Core Web API (controllers), Clean Architecture (Domain → Application → Infrastructure, Api).
- **Data access:** EF Core 10 + Npgsql + PostgreSQL 16+ (`btree_gist`), snake_case naming via `EFCore.NamingConventions`.
- **Backend libraries:** Identity + JWT + rotating refresh tokens, FluentValidation, SignalR, Serilog, OpenAPI + Swagger UI.
- **Frontend:** Angular 22, standalone components, OnPush, Angular Material, RxJS, ngx-translate v18 (en/de), ng2-charts.
- **Tests:** xUnit + Shouldly + Testcontainers (PostgreSQL); Vitest for Angular. No in-memory or SQLite providers in integration tests.
- **Ops:** Docker Compose, GitHub Actions.

Before adding any package: confirm it is free for this use, then name it and say what it's for.

## Layout

```
src/StayDesk.{Domain,Application,Infrastructure,Api}
tests/StayDesk.{UnitTests,IntegrationTests}
client/staydesk-web/src/app/{core,shared,public,account,admin}
docs/SRS.md
```

**Dependency rule:**
- Domain depends on nothing.
- Application depends on Domain.
- Infrastructure depends on Application.
- Api depends on Application, and references Infrastructure only in `Program.cs` for DI.

Controllers stay thin: validate, call one service, return a DTO. SignalR events, audit entries and emails are triggered from Application, never from controllers.

## Critical logic (see SRS for full text)

- **Active booking:** Confirmed or CheckedIn, or Pending with `ExpiresAt > now`. Overlap: `existing.CheckIn < req.CheckOut AND existing.CheckOut > req.CheckIn`. (SRCH-3)
- **Booking creation:** transaction, then `SELECT … FOR UPDATE` on the room row, then lazily cancel expired Pending bookings, then re-check, then insert. An exclusion constraint is the safety net. `23P01` and other conflicts map to **409, never 500**. The `Idempotency-Key` is unique per (CreatedById, key). (BOOK-2, BOOK-8)
- **Pricing:** Strategy pattern, one class per rule type. Signed `PercentChange`, multiplicative stacking, no priority. Each night is rounded to 2 decimals with `AwayFromZero`; total = sum of the rounded nights; nightly floor 1.00 EUR. The engine takes `today` as input. The snapshot is stored on the booking. (PRICE-*)
- **Status transitions:** enforced in the Booking entity. Booking has a concurrency token (`xmin`). (BOOK-3, BOOK-4)
- **Cancellation:** free until 48 h before the check-in instant (`CheckIn` + `Property.CheckInTime` in `Property.TimeZone`); after that, the first night is charged. (BOOK-5)
- **Audit:** an EF Core `SaveChangesInterceptor`, append-only. GUID v7 IDs. Never record secrets. (AUD-*)
- **Time:** inject `TimeProvider` everywhere; never call `DateTime.Now` or `DateTimeOffset.UtcNow` directly.
- **Correctness must never depend on a background job running on time.**

## Conventions

- Money: `decimal` / `numeric(10,2)`. Dates: `DateOnly` / `date`. Timestamps: `DateTimeOffset` UTC. Enums are stored as strings.
- Async all the way, `AsNoTracking` + `Select` projections for reads, DTOs only at the API boundary.
- Nullable reference types on. **Zero build warnings** (warnings are errors in CI).
- Errors are RFC 9457 Problem Details with a `code` extension (`room_unavailable`, `price_changed`, …).
- Angular: typed reactive forms; interceptors and guards in `core/`; every list has loading, empty and error states; every UI string goes in `en.json` and `de.json`.
- **Secrets:** env vars or user-secrets only. Never commit `.env`. Keep `.env.example` up to date.
- **Git:** one branch and PR per sub-feature, Conventional Commits, requirement IDs in PR descriptions.

## No AI slop

This repo must read as work by one developer who understands every line. These rules are hard rules, not style suggestions.

**Code**
- Write only what a requirement in `docs/SRS.md` needs. No speculative features, options, flags or extension points.
- No abstraction without a second use or a test seam the SRS requires.
  - No generic `IRepository<T>` over EF Core.
  - No interface with one implementation unless Application needs it to reach Infrastructure.
  - No `Helper`, `Utils`, `Manager`, `Common` or `Base*` dumping-ground classes.
- Comments explain *why*, never *what*. Delete any comment that restates the code. No XML doc comments on private or obvious members. No banner or section comments.
- No `try/catch` that only logs and rethrows, swallows, or wraps without adding information. Let the global exception middleware do its job.
- No defensive null checks that nullable reference types already rule out. No `!` to silence the compiler without a comment saying why it's safe.
- Names come from the SRS vocabulary (Booking, Room, PriceRule, check-in instant). No `Data`, `Info`, `Item`, `Obj`, `Temp`, `Result2`.
- Match the patterns already in the repo. Don't introduce a second way of doing something that already has one.
- No dead code, commented-out code, unused usings or imports, or unused parameters. No `TODO` without an issue reference.
- Keep diffs minimal: no reformatting, renaming or "cleanup" of code the task doesn't touch.

**Tests**
- Each test checks a behaviour from the SRS and is named after it, e.g. `Cancel_inside_48h_charges_first_night`. Put the requirement ID in a trait or in the test class name.
- Don't test getters, constructors, framework behaviour or mocks returning what they were told to return.
- Never weaken an assertion or skip a test to make it pass. Fix the code, or stop and report.

**UI**
- Follow the SRS visual spec (Material, deep blue + amber, 8 px spacing). No purple-gradient hero, glassmorphism, floating blobs, or emoji in UI.
- No lorem ipsum or "John Doe". Seed data uses believable German names, addresses and prices.
- Copy is short and specific: "Room no longer available for 12–14 March", not "Oops! Something went wrong 😕".

**Docs, commits, PRs**
- Plain, specific English. No emoji, no marketing words. Banned: *seamless, robust, powerful, leverage, cutting-edge, comprehensive, blazing-fast, elevate, empower, delve, unlock, game-changer, "In today's…"*.
- Never claim what isn't true *now*: no "production-ready", no badges for things that don't exist, no checked boxes for unbuilt features. Planned work goes under Roadmap.
- Use real numbers when you have them (measured p95, test count). Leave a number out rather than invent one.
- Commit messages say what changed and why in a sentence, e.g. `feat(booking): lock room row before availability re-check (BOOK-2)`. Never "update files" or "improvements".
- **No AI attribution anywhere.** No `Co-Authored-By` trailers, no "Generated with…" lines, no AI mentions in code, docs, commits or PRs. Commits go out under the owner's git identity only.

**Before finishing any change**, re-read the diff and delete anything that isn't needed. If the owner couldn't explain a piece in an interview, simplify it or explain it in the end-of-phase notes.

## Workflow

- Build one sub-feature from SRS §10 at a time (1a, 1b, …). Stop and wait for "continue".
- At the start of each sub-feature: list the requirement IDs and the files you will touch.
- In the same sub-feature, write the tests SRS §9 lists for it. Run them and report the actual results. Never say something works without running it.
- At the end of each sub-feature, give:
  - The run and test commands.
  - What to check in the browser or Swagger.
  - 2–3 interview talking points.
  - A Conventional Commit message.
- If something is ambiguous and touches the data model, the API contract or concurrency: **stop and ask.** Otherwise pick the simplest option that satisfies SRS §11, and state the assumption in one line.
- Never edit a migration that has been applied or committed. A new migration may get hand-added `migrationBuilder.Sql(...)`, but only for things EF Core can't model (e.g. the BOOK-2 exclusion constraint). It needs a matching `Down`, a comment saying why, and must be added before the migration is first applied (owner-approved 2026-11-16).
- Never touch `bin/`, `obj/`, `dist/` or `node_modules/`.

## Commands

Verified in 1b. The api and web compose services arrive in 1c, lint in 1d.

```bash
docker compose up -d --wait    # db (PostgreSQL 16) + mail (Mailpit UI on http://localhost:8025)
dotnet tool restore            # dotnet-ef from dotnet-tools.json
dotnet build -warnaserror      # backend, from repo root
dotnet test                    # unit + integration (Testcontainers needs Docker)

# API needs ConnectionStrings:Default (once per machine):
dotnet user-secrets set ConnectionStrings:Default "Host=localhost;Port=5432;Database=staydesk;Username=staydesk;Password=staydesk_dev" --project src/StayDesk.Api
dotnet run --project src/StayDesk.Api   # http://localhost:5095, OpenAPI at /openapi/v1.json

# Migrations (the startup project reads the connection string from user-secrets):
dotnet ef migrations add <Name> --project src/StayDesk.Infrastructure --startup-project src/StayDesk.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/StayDesk.Infrastructure --startup-project src/StayDesk.Api

cd client/staydesk-web
npm test -- --watch=false      # Vitest
npm run build
npm start                      # http://localhost:4200
```
