# Handoff

Read this first in every new session. Then read `TRACKER.md`, then the parts of `docs/SRS.md` the next task touches. Agent rules are in `AGENTS.md`.

**Last updated:** 2026-11-16 21:25 +06

## Where things stand

- **Phase:** specification done; no code yet. The next build step is **Phase 1a** (solution and Angular skeleton, SRS §10), once the owner says go.
- **Repo:** `main` on GitHub is the only branch. Latest commit: `docs: add SRS rev 2, design decisions and agent rules`.
- **Not committed on purpose:** the original SRS PDF and `SRS-revision-plan.md` (decision T-003).

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

- `pdftoppm`/poppler isn't installed on the dev machine, so the Read tool can't render PDF pages. Text extraction used `pypdf` in a temporary venv.
- Versions were verified on 2026-11-16: .NET 10, Angular 22, ngx-translate v18, ng2-charts v10+, Shouldly (BSD). FluentAssertions v8+ needs a commercial licence and is not used. Details are in the SRS §3 version note.
- The exclusion constraint SQL in BOOK-2 uses snake_case, so `EFCore.NamingConventions` must be configured, or the SQL changed to match.

## Next steps

1. Owner decides T-003 (PDF and plan), T-004 (licence) and T-005 (sign-off on D1–D5).
2. Phase 1a: create the .NET solution with the four projects and two test projects, plus the Angular 22 app in `client/staydesk-web`. Make sure both build with zero warnings, then replace the placeholder commands in `AGENTS.md` (T-012) and confirm Angular Material 22 compatibility (T-009).
