# StayDesk Tracker

Every open issue, todo, unverified item and pending decision, in one file. Update it in the same step as the finding, not later. Never write secret values here.

**Last updated:** 2026-11-17 +06

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
| T-004 | 2026-11-16 | P2 | DECIDE | repo | Licence: MIT (reusable) or none (all rights reserved). | Owner decides |
| T-005 | 2026-11-16 | P2 | DECIDE | spec | D1–D5 were applied with the recommended options without explicit owner sign-off (see `docs/decisions.md`). | Owner confirms or changes each one |
| T-006 | 2026-11-16 | P2 | OPEN | spec | The PDF layout was never checked: tables seemed to run past the next heading, and the architecture diagram on page 3 was never viewed. Only matters if the PDF is still shared. | Open the PDF and check pages 3, 11–14 and 17–18 |
| T-014 | 2026-11-16 | P2 | OPEN | frontend | npm 11 blocked install scripts for esbuild, lmdb, msgpackr-extract and @parcel/watcher. Build and tests pass without them. | Recheck if a build or watch issue appears; approve individually with `npm approve-scripts <pkg>` only if needed |
| T-018 | 2026-11-16 | P3 | OPEN | frontend | The Angular CLI's default `favicon.ico` is still in `public/`. | Replace with a real icon in 7a |
| T-019 | 2026-11-16 | P3 | OPEN | backend | Foreign key names on the renamed Identity tables still say `asp_net_users` (e.g. `fk_bookings_asp_net_users_guest_id`). Cosmetic only. | Leave; rename only if it confuses anyone |
| T-020 | 2026-11-16 | P2 | OPEN | backend | `Booking.price_breakdown` is a nullable jsonb column: EF can't mark an owned JSON collection as required. The constructor always sets it. | Seeder and tests must never create a booking without nights |
| T-022 | 2026-11-16 | P1 | OPEN | seed | The ~40 demo bookings (SRS §5) and the load seed (`Seed__LoadTest`) are not seeded yet. Real price snapshots and statuses need the pricing engine (3a) and the booking status machine (4a); faking them in the seeder would duplicate that logic. | Add both to `DbSeeder` in 4a |
| T-023 | 2026-11-16 | P3 | OPEN | ops | Docker images use floating minor tags `mcr.microsoft.com/dotnet/sdk:10.0` / `aspnet:10.0`, so patch updates arrive automatically. | Pin exact digests or patch tags before deploy (7d) if reproducibility matters |
| T-025 | 2026-11-17 | P3 | OPEN | ci | Actions are pinned to major tags (`actions/checkout@v7` etc.) and ReportGenerator to `5.6.0`, not to commit SHAs. | Pin to SHAs or add Dependabot for actions before deploy (7d) if supply-chain hardening matters |
| T-026 | 2026-11-17 | P2 | OPEN | ci | SRS §9 sets an 80% coverage target for Application, but CI only reports coverage; it doesn't fail below the target. Application has no code yet. | Add a threshold once Application has services (2a/3a) |
| T-028 | 2026-11-17 | P1 | DECIDE | spec | AUTH-3 role change, AUTH-6 deactivation (incl. revoking refresh tokens), AUTH-7 `PUT /users/me` and `GET /users` are in §4/§6 but in no §10 sub-feature. 2a only enforces the IsActive check on login/refresh. | Recommended: add them to 2b (admin API) and the account page to 2c; owner confirms, then amend §10 |
| T-029 | 2026-11-17 | P1 | OPEN | security | NFR-3 ForwardedHeaders not configured. The auth rate limit partitions on the direct peer IP, so behind nginx or a host's proxy every client would share one limit. | Configure `ForwardedHeaders` with the known proxy when the `web` nginx service (2c/3d) or deploy (7d) lands; add a test |
| T-031 | 2026-11-17 | P3 | OPEN | api | A malformed JSON body gets MVC's automatic 400 without the `code` extension (validation errors and known failures have it). | Customise `InvalidModelStateResponseFactory` if the Angular client needs it |
| T-032 | 2026-11-17 | P3 | DECIDE | tests | SRS §3 and AGENTS.md name Shouldly, but every test so far uses xUnit `Assert`. Mixing both would be two ways of doing one thing. | Owner picks: add Shouldly and convert, or drop it from the SRS |
| T-033 | 2026-11-17 | P3 | OPEN | security | Access tokens are stateless: after logout or deactivation an already issued access token still works until it expires (max 15 min). Accepted tradeoff of short-lived JWTs. | Mention in README security notes (7e) |
| T-035 | 2026-11-17 | P3 | OPEN | ops | The `api` compose service has no healthcheck, so `docker compose up --wait` returns before the API listens. The aspnet image has no curl. | Add a healthcheck (e.g. a tiny `--health` mode or wget in the image) when the `web` service is added |
| T-036 | 2026-11-17 | P3 | OPEN | api | Two admins adding the same room number at the same instant: the pre-check passes for both, the unique index rejects one, and it surfaces as a 500, not a 400. | Map unique violations (23505) in Infrastructure when 4b adds the 23P01 mapping |
| T-037 | 2026-11-17 | P3 | OPEN | spec | PROP-5's warning covers rooms only. Deactivating a property with active bookings returns 204 without listing them. | Ask owner whether property deactivation should warn too; extend PROP-5 if yes |
| T-038 | 2026-11-17 | P3 | OPEN | ops | API container logs `Cannot load library libgssapi_krb5.so.2` at startup: Npgsql probing for Kerberos, which isn't used. Harmless. | Silence later if it confuses anyone (install the lib or ignore) |
| T-039 | 2026-11-17 | P1 | OPEN | frontend | The SPA calls the API with relative `/api/...` URLs. That works behind the dev proxy and the planned nginx `web` service, but not if SPA and API are deployed on different hosts (D3 assumes they might be). | Add an API base URL setting when the deploy target is chosen (T-010, 7d) |
| T-041 | 2026-11-17 | P3 | OPEN | tests | The error-snackbar and loading interceptors have no tests (SRS §9 lists only the auth interceptor and role guard). | Add small specs if either grows logic |
| T-042 | 2026-11-17 | P3 | OPEN | api | `/auth/refresh` shares the 10/min auth rate limit, and every full page load refreshes once. An admin reloading 11 times in a minute gets a 429 on the restore and starts logged out (the token is kept since 2d, so the next reload works). | Give refresh its own, higher limit (e.g. 30/min) when NFR-3 is revisited with T-029 |
| T-043 | 2026-11-17 | P3 | OPEN | dev | The 2d live check created a property "Pension am Dom" (Würzburg) in the local dev database. The seeder skips inventory when any property exists, so it stays until the DB volume is recreated. | Deactivate it in /admin/properties, or recreate the local DB volume (owner's call) |
| T-044 | 2026-11-17 | P3 | OPEN | i18n | Amenity names ("Breakfast", "City view") are seed data in English and show untranslated in the German UI. §5 has no translated name field. | Decide in 7a: translation keys per amenity icon, or German seed names |
| T-045 | 2026-11-17 | P3 | OPEN | seed | Seasonal seed rules use the current year (Oktoberfest 19 Sep–4 Oct, Christmas markets 24 Nov–23 Dec). Once a season has passed, the demo can't show it until next year's reseed. | Decide with the nightly reset (7d): seed the next occurrence instead of this year's |
| T-046 | 2026-11-17 | P3 | OPEN | api | Search loads every available room in the city and prices, filters, sorts and pages them in memory (SRCH-4 accepts this). Invalid `sort`/`dir`/`type` values get MVC's automatic 400 without the `code` extension (as T-031). | Measure on the load seed in 7c; state the limit in the README (7e) |
| T-047 | 2026-11-17 | P3 | OPEN | frontend | At 360 px the search filters (10 amenity checkboxes) sit above the results, so the first room card is about 1000 px down. | Collapse the filters behind a button on narrow screens in the 7a responsive pass |
| T-048 | 2026-11-17 | P3 | OPEN | frontend | The room page has no Book button: `/book/:roomId` arrives in 4e. Booked dates on the 60-day calendar (crossed out and disabled) are untested in a browser because the seed has no bookings yet (T-022). | Add the button in 4e; check the calendar once 4a seeds bookings |
| T-049 | 2026-11-17 | P3 | OPEN | frontend | Initial bundle grew from 526 to 540 kB in 3d although the new pages are in the lazy public chunk; not investigated. Still under the 600 kB warning. | Check with `ng build --stats-json` if it grows again |
| T-027 | 2026-11-17 | P2 | OPEN | career | Owner asked for a full interview question bank once the project is complete. Talking points per sub-feature are collected in `notes/interview-prep.md` (git-ignored). | After 7e: write questions + model answers from that file and the final code |
| T-010 | 2026-11-16 | P2 | OPEN | ops | The free hosting choice for the API isn't made. Render was checked; Fly.io and Azure were not. | Decide before Phase 7d |

## Done log

| ID | Closed | Item | Commit |
|----|--------|------|--------|
| — | 2026-11-17 | Phase 3d: home (hero search, property cards), search (filters, sort and page in the URL; cards with stay total; pager), property (description, times, map placeholder, rooms) and room pages (amenities, 60-day availability calendar, live quote with per-night breakdown). New `GET /rooms/{id}/availability` (SRCH-5, max 90 days, dates only). Shared models moved to `shared/`; date pickers follow the language switch. 3 integration tests (123 backend) and 5 Vitest (33 total). Live check in Chrome at 1280 px (EN and DE) and Firefox at 360 px | Phase 3d commit |
| — | 2026-11-17 | Phase 3c: `GET /rooms/search` (SRCH-1..4). Availability (`Booking.IsActiveAt` + new `Booking.Overlaps`), capacity, type and all-amenities filters in SQL; totals from `PricingEngine`, `maxPrice`, price/capacity sort and paging in memory; past check-in checked against every property in the city. Paged shape `{ items, page, pageSize, totalCount, totalPages }`. 6 integration + 23 unit tests; 120 backend tests total. Live: 6 Berlin rooms, 67 ms on the dev stack | Phase 3c commit |
| T-003 | 2026-11-17 | Original SRS PDF and `SRS-revision-plan.md` stay local only, git-ignored; `docs/SRS.md` replaces both | `chore: ignore the original SRS sources` |
| — | 2026-11-17 | Phase 3b: `POST /pricing/quote` (public, active rooms only) and `POST /pricing/preview` (Admin, draft rule built in memory, returns current and with-rule quotes). SRCH-2 date rules (check-in not before today at the property, 1–30 nights), PRICE-2 rule validation (−90..+300, type-specific fields only); nested validation fields camelCased per segment (`rule.percentChange`). 13 integration + 11 unit tests; 91 backend tests total | Phase 3b commit |
| — | 2026-11-17 | Phase 3a: `PricingEngine` (Domain, pure, D9) with one strategy per rule type (Seasonal inclusive dates, Weekend Fri/Sat nights, LengthOfStay, LastMinute against today at the property); multiplicative stacking, one AwayFromZero rounding per night, 1.00 floor, total = sum of nights; property and room rules both apply. 15 unit tests (19 cases), mutation-checked; 70 backend tests total | Phase 3a commit |
| — | 2026-11-17 | Phase 2d: admin properties and rooms pages (Admin-only child routes, back-office tab bar), list tables with show-inactive toggle, deactivate with confirm dialog, reactivate, PROP-5 warning dialog, create/edit forms with server field errors, room version conflict (409) with reload; locale-aware EUR and date pipes. Header language switch is plain buttons now (first load 601 → 526 kB). Fixed: a 429 or network error on refresh no longer drops the session. 28 Vitest; live check in Chrome over CDP at 1280 and 900 px | Phase 2d commit |
| T-034 | 2026-11-17 | Admin rooms table sorts room numbers naturally (`Intl.Collator` numeric); the API still orders as text, the public pages (3d) must sort the same way | Phase 2d commit |
| T-040 | 2026-11-17 | `eur` and `localDate` pipes format for `de-DE` / `en-GB` following the language switch; both locales registered in `LanguageService` | Phase 2d commit |
| — | 2026-11-17 | Phase 2c: Angular Material theme (deep blue/amber, system fonts), ngx-translate en/de with persisted switcher, AuthService (access token in memory, refresh token in localStorage), auth interceptor with one shared refresh and one retry, error snackbar and loading interceptors, `roleGuard` on canMatch, login/register pages with server field errors, dev proxy; API CORS for the configured origin. 15 Vitest + 53 .NET tests; checked in a real browser at 1280 and 360 px | Phase 2c commit |
| T-015 | 2026-11-17 | Page headings moved to `en.json`/`de.json` | Phase 2c commit |
| T-030 | 2026-11-17 | CORS default policy from `Cors:AllowedOrigins` (Development: `http://localhost:4200`), preflight tests for allowed and foreign origins | Phase 2c commit |
| — | 2026-11-17 | Phase 2b: property/room/amenity API (list, detail, create, edit, deactivate, reactivate) with Admin-only writes and `includeInactive`, room edits checked against the client's `xmin` version (409), PROP-5 active-booking warning, IANA time-zone validation, `IStayDeskDbContext` (D8). 51 tests | Phase 2b commit |
| T-017 | 2026-11-17 | Application now has real services; `DependencyRuleTests` checks something meaningful | Phase 2b commit |
| — | 2026-11-17 | Phase 2a: register/login/refresh/logout/me under `/api/v1/auth`, JWT (15 min) + rotating refresh tokens (7 days, hashed) with reuse detection and an atomic claim, Guest role on register, lockout with demo exemption, IsActive check, 10/min auth rate limit, Problem Details with `code`, FluentValidation filter, Swagger Authorize. 32 tests | Phase 2a commit |
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
