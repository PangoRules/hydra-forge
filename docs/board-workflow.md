# HydraForge — Board Workflow for opencode-Driven Development

How the opencode multi-agent flow (`docs/specs/`, `docs/plans/`) maps onto HydraForge's own board — cards, columns, and the entities that already exist for exactly this (`Spec`, `Plan`, `PlanStatus` — see `docs/data-model.md` §2). Manual for now: you create and move the cards yourself. No agent calls the board API yet (see "Not automated yet" below).

---

## 1. The mapping

| opencode concept | Hydra card | Notes |
|---|---|---|
| Milestone / spec (`@brainstorm`'s output, `docs/specs/*.md`) | **Goal card**, `CardType.Goal` | Its `Spec` (`DocType.Specification`) holds the spec content. One Spec per Card. |
| Individual task plan (`@architect`'s output, `docs/plans/*.md`) | **Task card**, `CardType.Task`, `ParentCardId` → the Goal card | Its `Plan` (`SpecId` = the Goal's Spec) holds the plan content, grouped under the milestone. |
| tarnished's SCOPE CREEP items (currently `docs/backlog.md`) | **Idea card**, `CardType.Idea`, column `Backlog` | Optional — `backlog.md` still works fine; move to Idea cards when you want them board-visible instead of a flat file. |
| Carter's audit findings | **Issue card**, `CardType.Issue` (+ `DocType.Report` Spec) | One per finding, or one per Critical/High cluster. Optional, same as above. |
| Architect's "Step N depends on shared X from task M" plan notes | **CardRelationship** (`Precedes`/`BlockedBy`) between Task cards | Turns a prose note into an actual dependency edge, visible on the board. Optional — do this once the plan-dependency notes actually start mattering, not retroactively. |

**Only Goal and Task cards are load-bearing for the core loop.** Idea/Issue/CardRelationship are nice-to-haves — adopt them when the flat-file version starts to hurt, not as a prerequisite.

---

## 2. Column progression

Default columns: `Backlog → Spec-ing → Planned → In Dev → In Review → Done`.

**Goal card** (one per milestone):
| Opencode moment | Column |
|---|---|
| `@brainstorm` drafting, before GATE 1 | `Spec-ing` |
| GATE 1 approved, through GATE 2 (architect drafting plans) | `Spec-ing` still — plans aren't approved yet |
| GATE 2 approved (plans written, all Task cards created) | `Planned` |
| First Task card moves to `In Dev` | `In Dev` (the Goal just reflects "milestone is actively being worked," don't fuss over exact sync with whichever task is running) |
| All Task cards `Done`, milestone PR merged to `main` | `Done` |

**Task card** (one per plan/commander invocation), independent of its parent Goal's column:
| commander step | Column |
|---|---|
| Plan written, GATE 2 approved, not yet picked up | `Planned` (`Plan.Status = Pending`) |
| `@git` Task D sets up the branch, `@developer` starts | `In Dev` (`Plan.Status = Active`) |
| `@reviewer` loop running (first review call through LGTM) | `In Review` |
| Findings sent back to `@developer` mid-loop | back to `In Dev` — don't ping-pong on every cycle if that's noisy, `In Review` for the whole loop duration is a fine simplification |
| PR merged | `Done` (`Plan.Status = Done`, read-only until reactivated) |

If a task needs rework after shipping (matches git.md's "drop this branch" / reopen path): use HydraForge's `Reactivate` action on the Plan (Done → Active) and drag the card back to `In Dev`.

---

## 3. Starting from where you are now (Phase 7 — Chat)

Don't backfill Phases 1–6 — they're fully shipped and archived (`docs/archive/specs/`), a historical record with no ongoing board value. Start live tracking from Phase 7 forward:

1. Create one **Goal card**, "Phase 7: Chat", column `In Dev` (it's mid-flight — Tasks 1–18 shipped, 19–24 remaining per `AGENTS.md`). Paste `docs/specs/2026-08-02-phase-7-chat-design.md` into its Spec.
2. For the remaining tasks (19–24, plus whatever the current `task/web-chat-panel` work maps to), create **Task cards** as children of that Goal card, column matching wherever each one actually is right now (several are mid-`In Dev`/`In Review` today, not `Planned`).
3. Going forward, every new `/fire_keeper` milestone gets a Goal card at spec-write time; every `/commander` task gets a Task card at plan-write time. That's the whole habit — two card creations per milestone kickoff, one per task.

---

## 4. Not automated yet

No opencode agent calls HydraForge's API today — this is a deliberate, manual step for now (agents keep writing `docs/specs/`/`docs/plans/` exactly as before; you separately reflect status on the board). `.hydraforge/config.json` already has a live `serverUrl`+`jwtToken` sitting unused, so wiring an agent to create/move cards automatically is a small, well-scoped follow-up whenever it's worth it — not a schema gap, just unbuilt integration.

The longer-term direction: HydraForge is meant to grow its own harness — an in-app agent you chat with that talks to the HydraForge server directly and drives Spec/Plan/Card mutations itself, the way opencode's agents drive markdown files today. When that lands, `docs/specs/`+`docs/plans/` stop being the source of truth and the `Spec`/`Plan` DB entities (already built, mostly unused for this purpose) take over — versioned, linked to cards, no archive-shuffling needed since `SpecVersion`/`PlanVersion` already do that natively. This doc describes the bridge state, not the end state.
