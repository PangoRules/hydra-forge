# Manual Validation: Web UI Bell + Notification Panel

**Plan:** `docs/plans/2026-07-25-phase-5-notifications-admin-plan-4-web-ui-bell.md`
**Branch:** `task/web-ui-bell`
**Parent:** `feat/phase-5-notifications-admin`

## Setup

- [ ] Server running on `localhost:5000` (Docker Compose up)
- [ ] Web UI dev server running on `localhost:3000` (`pnpm dev`)
- [ ] Logged in as a non-admin user with at least one project
- [ ] At least one notification exists for this user (seed via API or DB)

## Happy Path

1. **See bell icon in navbar header**
   - Log in → navigate to any page
   - → Bell icon (`i-lucide-bell`) visible in top-right navbar, before color mode button

2. **Unread badge shows correct count**
   - With unread notifications in DB
   - → Red badge on bell shows number (truncated to `99+` if >99)

3. **Open notification panel**
   - Click bell icon
   - → UPopover opens below bell with "Notifications" header
   - → "Mark all read" button visible in header
   - → List of notifications displayed, most recent first

4. **Notification displays correctly**
   - Each notification row shows: title (bold), body (gray, truncated if long), relative timestamp ("2m ago", "1h ago", "3d ago")
   - → Unread ones have blue left dot + blue background tint
   - → Read ones lack the dot and tint

5. **Mark single notification as read**
   - Click an unread notification with `actionUrl`
   - → `POST /api/Notifications/{id}/read` called
   - → Notification visually becomes read (dot + tint removed)
   - → Unread count decreases by 1
   - → Page navigates to `actionUrl`

6. **Mark all as read**
   - Click "Mark all read" button
   - → `POST /api/Notifications/read-all` called
   - → All notifications become visually read
   - → Unread badge on bell disappears (shows 0 / no badge)

7. **Empty state**
   - With zero notifications (or all read)
   - Open panel
   - → Shows "No notifications yet" centered in gray text

8. **Bell count loads on page mount**
   - Hard refresh page with unread notifications
   - → `GET /api/Notifications/unread-count` fires on mount
   - → Badge shows correct count immediately

## Edge Cases

1. **99+ overflow**
   - Create 100+ unread notifications for user
   - → Badge shows "99+" not raw number

2. **Click notification without actionUrl**
   - Click notification where `actionUrl` is null
   - → Notification marked as read
   - → No navigation occurs
   - → Panel stays open

3. **Panel close on outside click**
   - Panel open → click outside popover
   - → Panel closes
   - → Bell count persists (not reset)

4. **Concurrent unread decrement**
   - Mark a notification as read that was already read
   - → No error. Count unchanged.

5. **Not authenticated**
   - Log out (or access page without auth)
   - → Bell icon not rendered in header

6. **API failure (unread count)**
   - Stop server or break network
   - → Bell renders with no badge (0 count assumed)

7. **API failure (notifications list)**
   - Open panel while server down
   - → Empty panel shown (notifications list stays empty, no error toast)

## Regressions

1. **Color mode button still works**
   - Click color mode button in header
   - → Theme toggles as before (light/dark)

2. **Logout button still works**
   - Click Logout
   - → Redirects to login page, session cleared

3. **Header layout unchanged**
   - Check header on project board page
   - → Title, bell, color mode, logout all visible in correct order
   - → No layout shift or overlap

4. **Session expiry modal still renders**
   - Let session expire
   - → SessionExpiryModal appears as before

## Cleanup

- [ ] No extra notifications left in DB (cleanup if test data was seeded)
