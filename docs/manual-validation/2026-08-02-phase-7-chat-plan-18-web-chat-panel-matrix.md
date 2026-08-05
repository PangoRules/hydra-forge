# Phase 7 Chat — Plan 18 Web Chat Panel: Manual Validation

**Date:** 2026-08-04
**Feature:** F6 implicit close, ChatPanel component on board

## Environment

- API server running with `ASPNETCORE_ENVIRONMENT=Development`
- Web UI dev server (`pnpm dev`) running
- Login as any project member

## Setup

1. Create or open a project with at least one card.
2. Navigate to the board view.

---

## Test Cases

### TC-1: Auto-prompt pre-fill on card open

| Step | Action | Expected |
|------|--------|----------|
| 1 | Click a card to open CardModal | CardModal opens |
| 2 | Click the chat icon in the header toolbar | ChatPanel slides in from right |
| 3 | Observe the inline textarea at bottom of panel | Textarea is pre-filled with: `Card #N [title] opened — what are we doing?` |

**Pass/Fail:** _____

---

### TC-2: New chat button — no blocking on prior session

| Step | Action | Expected |
|------|--------|----------|
| 1 | With ChatPanel open, click **New chat** (+) button | Button disables (isCreating=true) |
| 2 | Without waiting, immediately click **New chat** again | Second click is ignored (button still disabled) |
| 3 | Wait for POST to return | New session created; old session replaced seamlessly |
| 4 | No flash of empty state between sessions | Old session stays until new session ID assigned |

**Pass/Fail:** _____

---

### TC-3: Panel collapse — session stays Active, F6 does NOT fire

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open ChatPanel for a card | Session created and active |
| 2 | Click **X** to collapse panel | Panel hides; session stays Active (no F6 implicit-close fires) |
| 3 | Click chat icon in toolbar | Panel reopens; same session shown with all messages (resumable) |

**Pass/Fail:** _____

---

### TC-4: Navigate away and back — session still Active

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open ChatPanel for a card | Session created |
| 2 | Navigate to **Projects** list (back arrow or nav) | ChatPanel unmounts |
| 3 | Navigate back into the same project board | Board loads; ChatPanel not auto-opened |
| 4 | Click chat icon | Panel opens; session for that card is still Active |

**Pass/Fail:** _____

---

### TC-5: Inline textarea submit creates new session

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open ChatPanel | Session active |
| 2 | Type in inline textarea: `What is the priority here?` | Text appears in textarea |
| 3 | Press **Ctrl+Enter** or click send button | New session created with typed content as title |
| 4 | Textarea cleared; new session view loads | Old session gone; new session with typed message |

**Pass/Fail:** _____

---

### TC-6: F6 — session implicit close on navigate-away

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open ChatPanel for a card | Session created |
| 2 | Press **F6** to navigate to Projects list | Board unmounts; session implicitly closed on server |
| 3 | In a separate tab, GET `/api/chat/sessions/{id}` | Session state is Closed (or AiSummary populated if narrative gen ran) |

**Pass/Fail:** _____

---

## Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Tester | | | |
