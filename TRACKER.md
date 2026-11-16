# StayDesk Tracker

Every open issue, todo, unverified item and pending decision, in one file. Update it in the same step as the finding, not later. Never write secret values here.

**Last updated:** 2026-11-17 01:10 +06

**Status values:**
- **OPEN:** needs doing.
- **DECIDE:** needs the owner's decision.
- **WAITING:** blocked on something external.
- **DEFERRED:** parked on purpose.
- **RECURRING:** repeats.
- **DONE:** finished; moves to the Done log.

## Open

| ID | Opened | Priority | Status | Area | Item | Next step |
|----|--------|----------|--------|------|------|-----------|
| T-003 | 2026-11-16 | P2 | DECIDE | git | Should the original SRS PDF and `SRS-revision-plan.md` go into the repo? The PDF has the author's name in it. `docs/SRS.md` replaces both. | Owner decides: commit, move outside the repo, or add to `.gitignore` |
| T-004 | 2026-11-16 | P2 | DECIDE | repo | Licence: MIT (reusable) or none (all rights reserved). | Owner decides |
| T-005 | 2026-11-16 | P2 | DECIDE | spec | D1–D5 were applied with the recommended options without explicit owner sign-off (see `docs/decisions.md`). | Owner confirms or changes each one |
| T-006 | 2026-11-16 | P2 | OPEN | spec | The PDF layout was never checked: tables seemed to run past the next heading, and the architecture diagram on page 3 was never viewed. Only matters if the PDF is still shared. | Open the PDF and check pages 3, 11–14 and 17–18 |
| T-014 | 2026-11-16 | P2 | OPEN | frontend | npm 11 blocked install scripts for esbuild, lmdb, msgpackr-extract and @parcel/watcher. Build and tests pass without them. | Recheck if a build or watch issue appears; approve individually with `npm approve-scripts <pkg>` only if needed |
| T-015 | 2026-11-16 | P2 | OPEN | frontend | The skeleton pages have hard-coded headings (StayDesk, My bookings, Dashboard). NFR-10 requires translation files. | Move them to en.json/de.json when ngx-translate is added (2c) |
| T-018 | 2026-11-16 | P3 | OPEN | frontend | The Angular CLI's default `favicon.ico` is still in `public/`. | Replace with a real icon in 7a |
| T-017 | 2026-11-16 | P3 | OPEN | tests | `DependencyRuleTests` now has Domain types to check, but Application is still empty, so its test passes trivially. | Recheck when Application gets its first service (2a) |
| T-019 | 2026-11-16 | P3 | OPEN | backend | Foreign key names on the renamed Identity tables still say `asp_net_users` (e.g. `fk_bookings_asp_net_users_guest_id`). Cosmetic only. | Leave; rename only if it confuses anyone |
| T-020 | 2026-11-16 | P2 | OPEN | backend | `Booking.price_breakdown` is a nullable jsonb column: EF can't mark an owned JSON collection as required. The constructor always sets it. | Seeder and tests must never create a booking without nights |
| T-022 | 2026-11-16 | P1 | OPEN | seed | The ~40 demo bookings (SRS §5) and the load seed (`Seed__LoadTest`) are not seeded yet. Real price snapshots and statuses need the pricing engine (3a) and the booking status machine (4a); faking them in the seeder would duplicate that logic. | Add both to `DbSeeder` in 4a |
| T-023 | 2026-11-16 | P3 | OPEN | ops | Docker images use floating minor tags `mcr.microsoft.com/dotnet/sdk:10.0` / `aspnet:10.0`, so patch updates arrive automatically. | Pin exact digests or patch tags before deploy (7d) if reproducibility matters |
| T-025 | 2026-11-17 | P3 | OPEN | ci | Actions are pinned to major tags (`actions/checkout@v7` etc.) and ReportGenerator to `5.6.0`, not to commit SHAs. | Pin to SHAs or add Dependabot for actions before deploy (7d) if supply-chain hardening matters |
| T-026 | 2026-11-17 | P2 | OPEN | ci | SRS §9 sets an 80% coverage target for Application, but CI only reports coverage; it doesn't fail below the target. Application has no code yet. | Add a threshold once Application has services (2a/3a) |
| T-010 | 2026-11-16 | P2 | OPEN | ops | The free hosting choice for the API isn't made. Render was checked; Fly.io and Azure were not. | Decide before Phase 7d |

## Done log

| ID | Closed | Item | Commit |
|----|--------|------|--------|
| — | 2026-11-17 | Phase 1d: GitHub Actions CI (api job: restore, build `-warnaserror`, tests with coverage, ReportGenerator summary + artifact; web job: lint, Prettier check, Vitest, production build). angular-eslint 22.7.0 added; 4 generated files reformatted with Prettier | Phase 1d commit |
| T-024 | 2026-11-17 | First CI run green on GitHub (run 43934822555): web 23 s, api 47 s, 12/12 tests incl. Testcontainers, coverage artifact uploaded | Phase 1d commit |
| T-021 | 2026-11-17 | Coverage via coverlet.MTP 10.1.1 (MIT), the MTP extension from the coverlet project | Phase 1d commit |
| — | 2026-11-16 | Phase 1c: idempotent DbSeeder (roles, 3 demo users, 3 properties, 21 rooms, 10 amenities, 8 price rules), Identity core, Serilog + correlation IDs, Swagger UI, `/health` with DB check, migrate/seed on startup, API Dockerfile + compose service, 7 startup integration tests | Phase 1c commit |
| T-016 | 2026-11-16 | Owner chose xUnit v3: xunit.v3 4.0.3 on Microsoft.Testing.Platform (D7) | xUnit v3 commit |
| T-007 | 2026-11-16 | Confirmed by the integration test: an overlap surfaces as `DbUpdateException` → `PostgresException` with SqlState `23P01` and ConstraintName `ex_bookings_no_overlap` | Phase 1b commit |
| T-008 | 2026-11-16 | `uint Version` + `IsRowVersion()` appears as `xmin` in the generated migration, but Npgsql creates no column (checked with `\d bookings`) | Phase 1b commit |
| — | 2026-11-16 | Phase 1b: Docker Compose (db + mail, healthchecks), all §5 entities, DbContext, InitialCreate migration with exclusion constraint, 3 Testcontainers tests | Phase 1b commit |
| T-013 | 2026-11-16 | Owner installed the .NET SDK on the host via dnf (10.0.113) | Phase 1a commit |
| T-011 | 2026-11-16 | Phase 1a: .NET solution (4 projects + 2 test projects, layer references, shared props, zero warnings) + Angular 22 skeleton (3 lazy areas) | Phase 1a commit |
| T-012 | 2026-11-16 | Real commands written into `AGENTS.md` | Phase 1a commit |
| T-009 | 2026-11-16 | `npm view` shows @angular/material 22.2.7 peer-depends on @angular/core ^22 \|\| ^23 | — |
| T-001 | 2026-11-16 | Removed the AI co-author trailer from the first commit (amend + force-push by owner) | f9870a2 |
| T-002 | 2026-11-16 | Committed SRS rev 2, decisions, agent rules, tracker, README, `.gitignore` | `docs: add SRS rev 2, design decisions and agent rules` |
