# Design decisions

One entry per decision that shaped the SRS. Add new entries at the bottom; don't rewrite old ones. When a decision changes, add a new entry that supersedes the old one.

---

## D1. PostgreSQL instead of SQL Server

**Date:** 2026-11-16 · **Status:** accepted

**Context.** Revision 1 of the SRS named SQL Server but also allowed PostgreSQL. The concurrency design depends on the engine, so one had to be picked.

**Decision.** PostgreSQL 16+.

**Why.**
- An exclusion constraint on `daterange(check_in, check_out)` makes overlapping bookings impossible at the database level. SQL Server has no equivalent constraint and needs locking hints instead.
- Free managed hosting with `btree_gist` support exists (e.g. Neon).
- The official image runs on both x64 and ARM64. The SQL Server image is x64-only.

**Cost.** Many .NET job ads list SQL Server. EF Core keeps most code engine-neutral; the engine-specific parts are the constraint, `FOR UPDATE` and `xmin`.

---

## D2. .NET 10 and Angular 22

**Date:** 2026-11-16 · **Status:** accepted

**Context.** Revision 1 named .NET 8 and Angular 17.

**Decision.** .NET 10 (LTS) and Angular 22.

**Why.** .NET 8 and 9 reached end of support on 10 Nov 2026. Angular 17 has long been out of support. A portfolio project on unsupported runtimes reads badly.

**Cost.** None at project start.

---

## D3. Refresh token in localStorage

**Date:** 2026-11-16 · **Status:** accepted

**Decision.** Access token in memory; refresh token in localStorage.

**Why.** The SPA and API will likely be on different hosts on free tiers. An httpOnly cookie across sites needs `SameSite=None`, CSRF protection and third-party-cookie workarounds.

**Cost.** Any XSS can read the refresh token. Mitigations: CSP header, no `innerHTML` with user data, rotation with reuse detection (AUTH-2). Revisit if the SPA and API end up on one domain.

---

## D4. No priority on price rules

**Date:** 2026-11-16 · **Status:** accepted

**Context.** Revision 1 said rules "stack multiplicatively in priority order".

**Decision.** Remove priority. All matching rules multiply.

**Why.** Multiplication is order-independent, so priority changed nothing except rounding, and rounding is now per night after all rules apply.

**Cost.** No way for one rule to override another. If that's needed later, add an `IsExclusive` flag as a new decision.

---

## D5. Protecting the public demo

**Date:** 2026-11-16 · **Status:** accepted

**Decision.** A nightly job re-seeds demo data. Demo accounts are exempt from lockout.

**Why.** The demo admin can change everything, and anyone can lock an account with 5 wrong passwords. Without a reset, the demo breaks the first time a stranger plays with it.

**Cost.** Changes made by visitors disappear overnight. The README says so.

---

## D6. Expiry checked in the query, not only by a job

**Date:** 2026-11-16 · **Status:** accepted

**Decision.** Availability treats a Pending booking as active only while `ExpiresAt > now`. The booking transaction cancels expired holds for the room before inserting. The background job only tidies up.

**Why.** Free hosts sleep, so a job can't be trusted to run on time. Correctness that depends on it would fail silently on the demo.

**Cost.** Expired holds stay in Pending status until the job runs or someone books the room, so dashboard counts must also apply the expiry filter.

---

## D7. xUnit v3 on Microsoft.Testing.Platform

**Date:** 2026-11-16 · **Status:** accepted

**Context.** The `xunit` template on SDK 10.0.113 installs xUnit v2 (2.9.3, the latest v2 on NuGet). v3 is the actively developed line (4.0.x shipped August 2026).

**Decision.** Use `xunit.v3` and run tests through Microsoft.Testing.Platform (`"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`). The VSTest packages (`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector`) are removed.

**Why.** New features and fixes land only in v3. On the .NET 10 SDK, running v3 through the old VSTest path is an error, and MTP is the supported route.

**Cost.**
- Test projects are executables.
- Async test calls must pass `TestContext.Current.CancellationToken`, or the xUnit1051 analyzer fails the build.
- Code coverage needs an MTP extension instead of coverlet's VSTest collector (decided in 1d).
