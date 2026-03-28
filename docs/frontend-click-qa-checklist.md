# Frontend clickable coverage and QA checklist

Manual regression checklist for TubeArr UI. Routes match [`frontend/src/App/AppRoutes.tsx`](../frontend/src/App/AppRoutes.tsx); navigation matches [`frontend/src/Components/Page/Sidebar/PageSidebar.js`](../frontend/src/Components/Page/Sidebar/PageSidebar.js).

**How to use:** Work through layers in order. Check each box when behavior matches expectation. If the app uses a URL base (`window.TubeArr.urlBase`), repeat Layer 1 with that base prefixed.

**Scope:** Individual buttons and links are not listed exhaustively (hundreds of files). Layer 3 uses **pattern** checks per feature area.

---

## Disabled / unrouted code (skip unless you enable routes)

The following have UI code under `frontend/src/Settings` but are **commented out** in `AppRoutes.tsx` and `PageSidebar.js` (Quality, Custom Formats, Import Lists, Metadata). Do not expect these in production navigation until enabled.

---

## Layer 1 — Routes and redirects

### Core and channels

- [ ] `/` loads channel index (same as Channels)
- [ ] `/channels` loads channel index
- [ ] `/add/new` loads add-channel flow
- [ ] `/add/import` loads library import
- [ ] `/channels/:titleSlug` loads channel details (use a real slug)

### Activity

- [ ] `/activity` loads activity hub with links to sub-areas
- [ ] `/activity/download-queue` loads download queue
- [ ] `/activity/metadata-queue` loads metadata queue
- [ ] `/activity/history` loads history

### Legacy redirects

- [ ] `/queue` redirects to `/activity/download-queue`
- [ ] `/history` redirects to `/activity/history`

### Calendar

- [ ] `/calendar` loads calendar

### Settings (hub + each sub-page)

- [ ] `/settings` loads settings hub ([`Settings.js`](../frontend/src/Settings/Settings.js))
- [ ] `/settings/mediamanagement` loads media management
- [ ] `/settings/profiles` loads profiles
- [ ] `/settings/connect` loads notifications (Connect)
- [ ] `/settings/tags` loads tags
- [ ] `/settings/general` loads general settings
- [ ] `/settings/youtube` loads YouTube settings
- [ ] `/settings/tools` loads tools hub (Yt-Dlp + FFmpeg links)
- [ ] `/settings/tools/ytdlp` loads yt-dlp settings
- [ ] `/settings/tools/ffmpeg` loads FFmpeg settings
- [ ] `/settings/ui` loads UI settings

### System

- [ ] `/system/status` loads status
- [ ] `/system/tasks` loads tasks
- [ ] `/system/backup` loads backup
- [ ] `/system/updates` loads updates
- [ ] `/system/events` loads events log table

### Logs (nested under `Logs` component)

- [ ] `/system/logs/files` loads log files ([`Logs.js`](../frontend/src/System/Logs/Logs.js))
- [ ] `/system/logs/files/update` loads update log files view
- [ ] Tabs in [`LogsNavMenu.js`](../frontend/src/System/Logs/LogsNavMenu.js) switch between the two without errors

### URL base and fallbacks

- [ ] When `window.TubeArr.urlBase` is set, navigating to bare `/` (under host) redirects to the home route as in `AppRoutes` (Navigate to `p('/')`)
- [ ] An unknown path shows **Not Found** (`Components/NotFound`)

---

## Layer 2 — Global chrome

### Header ([`PageHeader.js`](../frontend/src/Components/Page/Header/PageHeader.js))

- [ ] Logo link navigates to `/`
- [ ] Sidebar toggle (`#sidebar-toggle-button`) opens/closes sidebar on narrow viewports
- [ ] Channel search: selecting a result navigates as designed ([`ChannelSearchInputConnector.js`](../frontend/src/Components/Page/Header/ChannelSearchInputConnector.js))

### Header menu ([`PageHeaderActionsMenu.tsx`](../frontend/src/Components/Page/Header/PageHeaderActionsMenu.tsx))

- [ ] **Keyboard shortcuts** opens the shortcuts modal
- [ ] **Restart** / **Shutdown** appear when not Docker; actions dispatch (confirm in environment where safe)
- [ ] **Logout** appears when authentication is `forms`; link goes to `${window.TubeArr.urlBase}/logout`

### Keyboard shortcuts modal ([`KeyboardShortcutsModal`](../frontend/src/Components/Page/Header/KeyboardShortcutsModal.js))

- [ ] Opens from menu and closes without breaking layout

### Sidebar ([`PageSidebar.js`](../frontend/src/Components/Page/Sidebar/PageSidebar.js))

- [ ] Every top-level item (Channels, Activity, Calendar, Settings, System) expands/navigates correctly
- [ ] **Channels** children: Add New → `/add/new`, Library Import → `/add/import`
- [ ] **Activity** children: Download queue, Metadata queue, History
- [ ] **Settings** children: Media Management, Profiles, Connect, Tools (single row to `/settings/tools`; Yt-Dlp/FFmpeg are linked from that hub, not separate sidebar rows), YouTube, Tags, General, UI — match [`PageSidebar.js` `links`](../frontend/src/Components/Page/Sidebar/PageSidebar.js) (lines 19–166)
- [ ] **System** children: Status, Tasks, Backup, Updates, Events, Log Files
- [ ] On small screens, tapping a link closes the sidebar (`onItemPress`)

### Sidebar messages ([`MessagesConnector`](../frontend/src/Components/Page/Sidebar/Messages/MessagesConnector.js))

- [ ] Any message dismiss or link behaves as expected

---

## Layer 3 — Per-area smoke (representative interactions)

Complete one pass per area; use real data where needed.

### Channel index ([`ChannelIndex.tsx`](../frontend/src/Channel/Index/ChannelIndex.tsx))

- [ ] View switcher (posters / table / overview per your menus)
- [ ] Refresh control
- [ ] Navigate into a channel (poster, table row, overview links)
- [ ] Selection mode: select multiple → at least one bulk action (e.g. edit, delete, organize, tags, monitoring) opens modal and completes or cancels safely
- [ ] Filters / sort / options modals open and apply or cancel

### Channel details ([`ChannelDetails.js`](../frontend/src/Channel/Details/ChannelDetails.js))

- [ ] Previous / next channel when available
- [ ] Tab or section navigation works
- [ ] Playlist / video actions (sample row)
- [ ] External YouTube link ([`ChannelDetailsLinks.tsx`](../frontend/src/Channel/Details/ChannelDetailsLinks.tsx)) opens expected URL

### Queues and history ([`QueuePage.js`](../frontend/src/Queue/QueuePage.js), [`HistoryPage.js`](../frontend/src/History/HistoryPage.js))

- [ ] Row action (e.g. remove, retry) on a sample item
- [ ] Pagination or “load more” if present
- [ ] Filter opens and applies

### Calendar ([`CalendarPage.tsx`](../frontend/src/Calendar/CalendarPage.tsx))

- [ ] Header navigation (prev/next, today, etc.)
- [ ] Options or iCal modal ([`CalendarLinkModalContent.tsx`](../frontend/src/Calendar/iCal/CalendarLinkModalContent.tsx)) opens and closes

### Settings save flow ([`SettingsToolbarConnector.js`](../frontend/src/Settings/SettingsToolbarConnector.js), [`PendingChangesModal.js`](../frontend/src/Settings/PendingChangesModal.js))

- [ ] On one settings page: change a field → **Save** persists (or **Discard** reverts)
- [ ] Pending-changes modal appears when navigating away with unsaved changes (if applicable)

### System samples

- [ ] **Backup** ([`BackupsConnector`](../frontend/src/System/Backup/BackupsConnector.js)): restore or download on a sample row (non-destructive test environment)
- [ ] **Tasks**: run or inspect a scheduled/queued task control
- [ ] **Updates**: check for updates control
- [ ] **Events**: open row details modal if available

### Activity hub ([`ActivityPage.tsx`](../frontend/src/Activity/ActivityPage.tsx))

- [ ] Links to download queue, metadata queue, and history navigate correctly

---

## Optional: inventory commands (diff-friendly audits)

Run from repository root. Use [ripgrep](https://github.com/BurntSushi/ripgrep) (`rg`) if installed; otherwise use your editor’s search.

**Interactive components (candidates):**

```bash
rg "<Button |IconButton|MenuItem|SpinnerButton|ClipboardButton" frontend/src
```

**Links:**

```bash
rg "from 'Components/Link/Link'|<Link " frontend/src
```

**Handlers:**

```bash
rg "onPress=|onClick=" frontend/src --glob "*.{js,jsx,ts,tsx}"
```

Treat output as a **hint list**, not complete coverage (some controls use custom elements or `role="button"` without these strings).

---

## Success criteria summary

- Layer 1: All routes, nested log routes, legacy redirects, Not Found, and URL-base behavior work as expected.
- Layer 2: Header, sidebar, search, and messages behave on desktop and a narrow viewport.
- Layer 3: Each major area has at least one full navigation plus one modal or table action path verified.
