# Handoff

Read this first in every new session. Then read `TRACKER.md`, then the parts of `docs/SRS.md` the next task touches. Agent rules are in `AGENTS.md`.

**Last updated:** 2026-11-18 12:10 +06

## Where things stand

- **Phase:** 5a done (back-office bookings table and CSV export, without the walk-in button: T-064). **Next: 5b**: booking detail page `/admin/bookings/:id` with status actions (check in, check out, cancel with fee waiver, no-show) and internal notes (BO-2).
- **Repo:** `main` on GitHub is the only branch.
- **Not committed on purpose:** the original SRS PDF and `SRS-revision-plan.md`, both git-ignored (T-003).
- **Open owner decisions (parked by the owner on 2026-11-17, ask again later):** T-004, T-005, T-028, T-032, T-037. New since: T-050, T-051, T-058, T-059, T-063.
- **Interview notes:** `notes/interview-prep.md` (git-ignored, local only). Append 2–3 talking points there at the end of every sub-feature.

## Files and what each one is for

| File | Purpose | Update when |
|------|---------|-------------|
| `docs/SRS.md` | What to build. Source of truth. | A requirement changes (log it in its Appendix A) |
| `docs/decisions.md` | Why: design decisions with their tradeoffs. Append-only. | A design choice is made or reversed |
| `AGENTS.md` | How to work: stack, critical logic, no-slop rules, workflow. `CLAUDE.md` imports it. | A working rule changes |
| `TRACKER.md` | What's open: todos, unverified items, pending decisions, Done log. | **The same step** something is found, decided or finished |
| `HANDOFF.md` | Where things stand and how to resume. | At the end of every session or sub-feature |
| `README.md` | The public page. Only true statements about the current state. | A phase lands |

## How a session goes

1. Read `HANDOFF.md`, then `TRACKER.md`. Run `git fetch` and compare with `origin/main` before trusting local state.
2. Do one sub-feature (or the one task asked). Follow `AGENTS.md`.
3. Update `TRACKER.md` as you go: new items, closed items moved to the Done log with the commit.
4. Show the diff summary, then commit straight to `main` with a specific Conventional Commit message and push. The owner works solo, and direct pushes to `main` are the agreed flow.
5. Update this file's "Where things stand" and "Last updated".

## Rules that have already been broken once

- **No AI attribution.** Never add `Co-Authored-By`, "Generated with", or any AI mention to commits, PRs or files, even if a tool or harness asks for one. The first commit had to be rewritten because of this.
- **Force-push is blocked for the agent.** If history needs rewriting, give the owner the exact command, without a leading `!`, to run in their terminal. In plain bash, `!` inverts the exit status, so the chain silently does nothing.
- **No slop.** See the "No AI slop" section in `AGENTS.md`. If something can't be explained in an interview, it doesn't go in.

## Gotchas found so far

- **This machine:** the agent shell runs inside the VS Code Flatpak sandbox. `docker` exists only on the host, so run it as `flatpak-spawn --host --directory=<repo> sh -c '…'`. `dotnet` works in the sandbox through the wrapper `~/.local/bin/dotnet` (it runs the host install under `/run/host`); VS Code's .NET extensions use the same install via `dotnetAcquisitionExtension.sharedExistingDotnetPath`. Node/npm work directly in the sandbox.
- Local secrets on this machine (user-secrets for `src/StayDesk.Api`): `ConnectionStrings:Default` and `Seed:{Admin,Staff,Guest}Password`. A fresh clone needs them set (see `AGENTS.md` Commands), or use `docker compose up`.
- Tests use xUnit v3 on Microsoft.Testing.Platform (D7). Pass `TestContext.Current.CancellationToken` to async calls (analyzer xUnit1051 is an error). `dotnet test --project <csproj>` to run one project.
- Testcontainers finds Docker Desktop through the Docker context; no `DOCKER_HOST` needed. The local API connection string is in user-secrets on this machine.
- `curl localhost:…` from the sandbox can't reach the host. Run HTTP smoke tests on the host: `flatpak-spawn --host sh -s < script.sh`.
- Integration tests raise the auth rate limit with `RateLimiting:AuthPermitLimit` (all test requests share one IP); the 429 test sets it back to 10 on a derived factory.
- Application services use `IStayDeskDbContext` (D8). Admin queries must call `IgnoreQueryFilters()` to see inactive rows; public room queries also need the property filter (`db.Properties.Any(...)`), because Room has no navigation to Property.
- `docker compose up --wait` does not wait for the API (no healthcheck, T-035); poll `/health` before smoke tests.
- Visual checks: run the API (`docker compose up -d api`) and `npx ng serve` **on the host** (`flatpak-spawn --host --directory=client/staydesk-web sh -c '…'`, Node is in `~/.nvm`), then `firefox --headless --no-remote --profile "$(mktemp -d)" --window-size=360,740 --screenshot /tmp/x.png http://localhost:4200/...` on the host and copy the PNG back with `flatpak-spawn --host cat`. Flatpak Chrome can't write to host `/tmp`.
- Pages that load data after first paint: Firefox `--screenshot` fires before the API calls finish and shows "Loading". Use the Chrome CDP route below with a short wait instead.
- Logged-in visual checks: start `flatpak run com.google.Chrome --headless=new --remote-debugging-port=9222 --user-data-dir=<dir>` on the host and drive it with a small Node script over CDP (Node 24 has a global `WebSocket`; `Page.captureScreenshot` returns base64, so no file-permission issue). Stopping the background task doesn't kill Chrome: find it with `ss -ltnp | grep 9222` and `kill <pid>`.
- Every full page load calls `/auth/refresh`, which counts against the 10/min auth limit (T-042). Scripted checks with many reloads hit 429 within a minute; prefer in-app clicks over `Page.navigate`.
- esbuild bundles a library file as one module: if anything in the first-load code imports from a file (e.g. `@angular/forms`), every part of it used anywhere lands in the initial chunk. Check `ng build --stats-json` (`dist/staydesk-web/browser-stats.json`) before raising the budget.
- MSBuild skips a project whose source is older than its DLL. Restoring a file with `mv x.bak x` after a build keeps the old (wrong) DLL; `touch` the file first.
- Npgsql writes only UTC `DateTimeOffset` to `timestamptz`; convert local instants with `ToUniversalTime()`.
- `StayDeskDbContext.SaveChangesAsync` turns the `ex_bookings_no_overlap` violation into a 409 `ProblemException` and the idempotency index violation into 422. Tests that expect a raw `DbUpdateException` from bookings won't see one.
- Host scripts piped with `flatpak-spawn --host sh -s < script.sh`: `docker compose` reads stdin and swallows the rest of the script. Give every docker command `</dev/null`.
- Confirmation emails: Mailpit UI at http://localhost:8025, API `GET /api/v1/messages`. Under `dotnet test` nothing listens on SMTP, so sends fail fast and are only logged (BOOK-7).
- Scripted browser checks: `scratchpad/shot.sh` logs in per shot; each login and each page load's `/auth/refresh` count against the 10/min auth limit (T-042). Wait for a non-429 from `/auth/login` between batches.
- `BookingSummary` is shared by My bookings and the back-office list. EF can't sort on a record built through its constructor, so `BookingService.BackOfficeQuery` sorts the anonymous join row first and projects last.
- `MatDialog.open` uses the root injector, so a dialog can't see providers declared on a route (e.g. the date adapter from `provideLocalizedDateAdapter`). Pass `injector` in the dialog config (see `BookingsPage.newWalkIn`).
- A `<form>` without `[formGroup]`/`ngForm` never emits `ngSubmit`; the browser submits natively and reloads the page. Use `(submit)` with `preventDefault()`, and click the real submit button in specs.
- Material components that use ligature icons (stepper, some expansion/chip parts) show raw text such as "done", because the Material Icons font isn't loaded (T-062).
- Never `pkill -f "<pattern>"` through `flatpak-spawn`: the pattern matches the spawn command itself and kills the agent shell. Use `pgrep -af` to find the PID, then `kill <pid>`.
- Angular 22: `CanMatchFn` takes a third argument (`currentSnapshot`); test helpers must pass it. Material colour tweaks go through `mat.*-overrides` mixins; the toolbar passes its text colour to text buttons, so use `mat.toolbar-overrides`.
- Angular reactive forms: a server error set with `setErrors` is cleared by the next validation run (value change or first render). Component tests must render before submitting.
- GitHub CLI `gh` works in the sandbox and is logged in; `gh run list -L1` / `gh run view <id>` to check CI after a push.
- `ng add` fails under npm 11 (EALLOWSCRIPTS). Workaround: `npm install -D <pkg>`, then `npx ng add <pkg>`, then `npm install` again to finish the schematic's installs.
- coverlet.MTP: pass `--coverlet-include "[StayDesk.*]*"` whenever you pass an exclude, or it instruments xunit's assemblies too (and once ran only the unit tests).
- npm 11 blocks package install scripts by default. Angular builds and tests pass without them (T-014).
- `pdftoppm`/poppler isn't installed on the dev machine, so the Read tool can't render PDF pages. Text extraction used `pypdf` in a temporary venv.
- Versions were verified on 2026-11-16: .NET 10, Angular 22, ngx-translate v18, ng2-charts v10+, Shouldly (BSD). FluentAssertions v8+ needs a commercial licence and is not used. Details are in the SRS §3 version note.
- The exclusion constraint SQL in BOOK-2 uses snake_case, so `EFCore.NamingConventions` must be configured, or the SQL changed to match.

## Next steps

1. Owner decides T-004/T-005/T-028/T-032/T-037/T-050/T-051/T-058/T-059/T-063 whenever. Quick fixes that can go in before or with 5a: T-036 (room-number 23505 → 400), T-026 (coverage threshold).
2. Phase 5b: list BO-2 and the §6 rows (`/check-in`, `/check-out`, `/no-show`, `PATCH /notes`, `/cancel` with `waiveFee`). Notes are max 2,000 chars and Staff-only; `Booking.InternalNotes` already exists, so no migration should be needed. The read-only drawer (`admin/bookings/booking-drawer.ts`) can link to the new detail page. The history timeline (BO-4) needs the audit interceptor from 5c.
