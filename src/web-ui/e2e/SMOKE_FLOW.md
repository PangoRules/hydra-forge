# E2E smoke flow — "Website Revamp"

This describes, in plain language, the single flow encoded in
`project-lifecycle.spec.ts`. Read this first before touching that file —
it explains *why* each step exists, so a future edit doesn't quietly drop
coverage.

## Why one flow, not one test per feature

Each Playwright spec that seeds its own project mints real rows in the
dev/CI database (`workers: 1`, no DB reset between tests — see CLAUDE.md).
Five specs used to mean five throwaway projects per CI run. This flow
does the same work — create a project, drive every card feature, clean up
by archiving — in one linear pass, one project, one test function. It
folds in what `card-archive-restore.spec.ts` and `card-description-save.spec.ts`
used to check separately (see "What got folded in" below).

## The fictional project

**"Website Revamp"** — a small, believable project a real team would run
through HydraForge. Board uses the **General** column template
(`Backlog → In Progress → Review → Done`, see
`ColumnTemplate.cs`).

Cards:

| Card | Type | Notes |
|---|---|---|
| Launch redesigned marketing site | Goal | Has a Spec (Specification) + Plans |
| Add dark mode toggle | Idea | Has a Spec (Concept) only — gets a real spec written, then flipped to Task to exercise the "will hide Docs tab" confirm (D-44) |
| Checkout page throws 500 on Safari | Issue | Has a Spec (Report) + Plans — used for the description-save check |
| Update hero image assets | Task | Created as a **child** of the Goal card |
| Write launch announcement email | Task | Due date set at creation; gets a checklist, an attachment, and a "Blocked by → Update hero image assets" dependency |

## Step-by-step

1. **Create the project** via the "New Project" button — name, description,
   leave the template on its default (**General**). Git fields are left
   untouched; they're not part of what this smoke test is checking.
2. Click the new project's row to land on its board.
3. **Create all five cards** above via "Add Card", picking Type/Column
   (and Parent, for the hero-image Task) in the create form.
4. Open the **Idea** card → Docs tab → write a one-line Concept spec →
   Create. This gives it a real Spec row (a card with no Spec never
   triggers the type-change warning — the confirm dialog only fires if a
   Spec/Plan actually exists, per `CardMetadata.vue`'s `actuallyHasSpec`
   check).
5. Change the Idea card's **Type → Task** via the sidebar select. Confirm
   the "Change card type?" warning appears (it would lose its Spec) and
   accept it. Verify the Docs tab now shows Plan-only content, not the
   Spec form.
6. Open **"Write launch announcement email"**:
   - **Checklist**: add three items, check one, reorder one, delete one.
   - **Attachments**: upload a small file, confirm it lists, delete it.
   - **Dependencies**: link card → search for "Update hero image assets" →
     relationship type "Blocked by" → Link.
   - **Column**: move it Backlog → In Progress via the sidebar Column
     select (not drag-and-drop — Playwright can't reliably simulate the
     native HTML5 DnD `BoardCard.vue` uses; the Column select hits the
     identical `Cards.move` endpoint).
7. Open **"Update hero image assets"** and confirm its Parent pill shows
   the Goal card. Open the **Goal card** and confirm its Children panel
   lists the Task — proves the parent link set at creation time is live
   in both directions.
8. **Archive with dependents**: archive "Write launch announcement email".
   Because it's the *source* card of the "Blocked by" relationship
   (`CardModal.vue`'s dependent-check keys off `sourceCardId`, not
   semantic direction), archiving it must show `ArchiveCardWarning` (not
   the plain confirm) listing "Update hero image assets". Confirm through
   the warning. Toggle the board's "Archived" + per-column "Archived
   only" filters, reopen the card, restore it.
9. **Description save**: open the Issue card, type in its description
   editor, confirm the Save button enables, save, confirm it disables
   again. (The old suite also asserted the 2-second autosave debounce
   fires without a click — dropped here as redundant: it's the same
   `save()` call, just timer-triggered instead of click-triggered, and
   asserting it costs a multi-second real wait. If the debounce timer
   itself ever regresses independently, it's cheap to re-add as its own
   small spec.)
10. **Cleanup — archive the whole project** from the project list. Confirm
    the dialog, confirm the project drops out of the default (non-archived)
    list. End state: nothing left active.

## What got folded in

- `card-archive-restore.spec.ts` → step 8 (archive/restore), now via the
  warning path instead of the plain-confirm path, since the plain path is
  strictly simpler and this way both get covered.
- `card-description-save.spec.ts` → step 9, manual-save assertion only
  (see the note above on why autosave-debounce was dropped).

## What stayed separate

- `smoke.spec.ts` — logged-out sanity check, creates no data, cheap.
- `card-concurrency.spec.ts` — optimistic-locking race condition between
  two browser contexts on one card. Orthogonal to this flow (it's testing
  a conflict scenario, not a feature), and folding it in would mean this
  spec needs two contexts for one step in the middle of an otherwise
  single-context flow.

## Bugs this flow caught before it ever went green

Writing this flow — the first time "archive with dependents" and any real
typing into a card's rich-text editor got exercised end-to-end — surfaced
two real, pre-existing app bugs (both fixed alongside this file, not just
routed around):

1. **`CardModal.vue`'s Archive shortcut ate the letter "a"**. It was
   registered with `allowWhileEditing: true`, which is supposed to let the
   `a` shortcut fire "even when the Tiptap editor is focused" — but the
   underlying `useKeyboard.ts` guard can't distinguish "user pressed the
   bare shortcut" from "user typed the letter a as text." Every `a`
   character typed into a card's description (or Spec/Plan editor) popped
   the archive-confirm dialog and the keystroke never reached the editor.
   This also broke the pre-existing `card-description-save.spec.ts` (both
   its tests failed on this, independent of anything in this file). Fixed
   by dropping `allowWhileEditing` so the normal input/textarea/
   contentEditable exemption applies again.
2. **`ArchiveCardWarning.vue` rendered broken when opened from inside an
   already-open card modal** — i.e. every real invocation, since it only
   ever opens from `CardModal.vue`'s archive button. It wrapped its
   content in `<UCard>` placed in `UModal`'s *default* slot, but per this
   project's own Nuxt UI v4 convention (see CLAUDE.md), the default slot
   is treated as a `DialogTrigger`, not modal content — `#header`/`#body`/
   `#footer` are required. Content rendered inline and unstyled at the
   bottom of the page instead of as a centered dialog. Its own unit test
   (`ArchiveCardWarning.test.ts`) never caught this because it mounts the
   component standalone, never nested inside a parent modal, and the old
   (broken) default-slot rendering happened to still produce visible text
   for `wrapper.text()` to find — the test was inadvertently validating
   the bug. Fixed by moving content into the named slots (matching
   `ConfirmDialog.vue`'s pattern) and updating the unit test to stub
   `UModal` so it doesn't depend on `<Teleport>` resolving in a
   component-only test.

A third issue was found but *not* fixed here: a hard `page.goto`/
`page.reload()` late in a long authenticated session redirects to
`/login` (a stray 401 clears the auth cookie somewhere — see `useApi.ts`'s
401 handler). It reproduces on the old `card-description-save.spec.ts`
autosave test too, so it predates this file. Step 10 works around it by
navigating back to `/projects` via the in-app "HydraForge" link instead of
a hard reload — which is also just how a real user would do it. Worth its
own investigation later.

## Known scope cuts (ask before restoring)

- Assignees — not part of the original ask, skipped.
- Git remote / provider fields on project creation — same.
- Spec/Plan content on the Goal and Issue cards — the Idea→Spec path is
  exercised once (step 4); duplicating it on Goal/Issue would just repeat
  the same `CardSpec.vue` code path for no new coverage.
- Column/board CRUD (add/rename/delete a column) — not part of the
  original ask; this flow only uses the columns the General template
  creates.
