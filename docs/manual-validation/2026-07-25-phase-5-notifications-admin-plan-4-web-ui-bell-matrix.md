# Manual Validation: Web UI Bell + Notification Panel

**Plan:** `docs/plans/2026-07-25-phase-5-notifications-admin-plan-4-web-ui-bell.md`
**Branch:** `task/web-ui-bell`
**Parent:** `feat/phase-5-notifications-admin`

## Result

**Partial pass, 2026-07-25.** Happy path 1-6 and 8 verified manually (testuser1, notifications seeded directly via SQL — no trigger wiring exists yet, that's Task 7). Found + fixed along the way:
- Web container was serving a stale prebuilt image (`docker-compose.yml` `web`/`server` services `COPY . .` + build at image time, no volume mount — code changes need `docker compose up -d --build <service>`, not just a page refresh). Bell was invisible for this reason, not a code bug.
- Notification panel header + "Mark all read" now `sticky top-0` so they don't scroll away with a long list (UX fix requested during this pass, applied to `NotificationPanel.vue`).

Both fixes are in commit `2a67829` (PR #55, squash-merged) along with the base Task 4 implementation.

**Not yet exercised** — happy path 7 (empty state), edge cases 1/3/4/5/6/7, all regressions. Run these before considering Task 4 fully signed off; nothing found so far suggests they're at risk, they just haven't been clicked through yet.

## Setup

- [x] Server running on `localhost:5000` (Docker Compose up)
- [x] Web UI dev server running on `localhost:3000` (Docker `web` service — required a rebuild, see Result above)
- [x] Logged in as a non-admin user with at least one project
- [x] At least one notification exists for this user (seeded via direct SQL insert — no API/trigger path exists yet)

## Happy Path

1. **See bell icon in navbar header** — [x]
   - Log in → navigate to any page
   - → Bell icon (`i-lucide-bell`) visible in top-right navbar, before color mode button

2. **Unread badge shows correct count** — [x]
   - With unread notifications in DB
   - → Red badge on bell shows number (truncated to `99+` if >99)

3. **Open notification panel** — [x]
   - Click bell icon
   - → UPopover opens below bell with "Notifications" header
   - → "Mark all read" button visible in header
   - → List of notifications displayed, most recent first

4. **Notification displays correctly** — [x]
   - Each notification row shows: title (bold), body (gray, truncated if long), relative timestamp ("2m ago", "1h ago", "3d ago")
   - → Unread ones have blue left dot + blue background tint
   - → Read ones lack the dot and tint

5. **Mark single notification as read** — [x]
   - Click an unread notification with `actionUrl`
   - → `POST /api/Notifications/{id}/read` called
   - → Notification visually becomes read (dot + tint removed)
   - → Unread count decreases by 1
   - → Page navigates to `actionUrl`

6. **Mark all as read** — [x]
   - Click "Mark all read" button
   - → `POST /api/Notifications/read-all` called
   - → All notifications become visually read
   - → Unread badge on bell disappears (shows 0 / no badge)

7. **Empty state** — [ ]
   - With zero notifications (or all read)
   - Open panel
   - → Shows "No notifications yet" centered in gray text

8. **Bell count loads on page mount** — [x]
   - Hard refresh page with unread notifications
   - → `GET /api/Notifications/unread-count` fires on mount
   - → Badge shows correct count immediately

## Edge Cases

1. **99+ overflow** — [ ]
   - Create 100+ unread notifications for user
   - → Badge shows "99+" not raw number

2. **Click notification without actionUrl** — [ ]
   - Click notification where `actionUrl` is null
   - → Notification marked as read
   - → No navigation occurs
   - → Panel stays open

3. **Panel close on outside click** — [ ]
   - Panel open → click outside popover
   - → Panel closes
   - → Bell count persists (not reset)

4. **Concurrent unread decrement** — [ ]
   - Mark a notification as read that was already read
   - → No error. Count unchanged.

5. **Not authenticated** — [ ]
   - Log out (or access page without auth)
   - → Bell icon not rendered in header

6. **API failure (unread count)** — [ ]
   - Stop server or break network
   - → Bell renders with no badge (0 count assumed)

7. **API failure (notifications list)** — [ ]
   - Open panel while server down
   - → Empty panel shown (notifications list stays empty, no error toast)

## Regressions

1. **Color mode button still works** — [ ]
   - Click color mode button in header
   - → Theme toggles as before (light/dark)

2. **Logout button still works** — [ ]
   - Click Logout
   - → Redirects to login page, session cleared

3. **Header layout unchanged** — [ ]
   - Check header on project board page
   - → Title, bell, color mode, logout all visible in correct order
   - → No layout shift or overlap

4. **Session expiry modal still renders** — [ ]
   - Let session expire
   - → SessionExpiryModal appears as before

## Cleanup

- [ ] No extra notifications left in DB (cleanup if test data was seeded) — **pending**: seeded rows for `testuser1` (`cc71c3af-3b20-4715-ac61-d536b063d841`) still in `notifications` table, run `DELETE FROM notifications WHERE "UserId"='cc71c3af-3b20-4715-ac61-d536b063d841';` before archiving this matrix.
