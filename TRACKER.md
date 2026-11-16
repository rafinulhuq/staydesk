# StayDesk Tracker

Every open issue, todo, unverified item and pending decision, in one file. Update it in the same step as the finding, not later. Never write secret values here.

**Last updated:** 2026-11-16 21:20 +06

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
| T-007 | 2026-11-16 | P1 | OPEN | backend | Not verified: how EF Core surfaces PostgreSQL `23P01` (expected: `DbUpdateException` wrapping `PostgresException`). | Prove it in the BOOK-2 integration test (Phase 4b) |
| T-008 | 2026-11-16 | P2 | OPEN | backend | Not verified: whether mapping a `uint` version property to `xmin` creates a migration. Sources disagree. | Check the generated migration in Phase 1b |
| T-009 | 2026-11-16 | P2 | OPEN | frontend | No official Angular Material 22 compatibility table was found. | Confirm when scaffolding (Phase 1a) |
| T-010 | 2026-11-16 | P2 | OPEN | ops | The free hosting choice for the API isn't made. Render was checked; Fly.io and Azure were not. | Decide before Phase 7d |
| T-011 | 2026-11-16 | P1 | OPEN | build | Phase 1a (solution and Angular skeleton) hasn't started. | Start when the owner says go |
| T-012 | 2026-11-16 | P3 | OPEN | docs | The commands in `AGENTS.md` are placeholders that haven't been run. | Replace with real commands after Phase 1a |

## Done log

| ID | Closed | Item | Commit |
|----|--------|------|--------|
| T-001 | 2026-11-16 | Removed the AI co-author trailer from the first commit (amend + force-push by owner) | f9870a2 |
| T-002 | 2026-11-16 | Committed SRS rev 2, decisions, agent rules, tracker, README, `.gitignore` | `docs: add SRS rev 2, design decisions and agent rules` |
