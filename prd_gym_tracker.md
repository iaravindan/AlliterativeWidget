# PRD — Gym Heatmap & Target Tracking (Geofency → Windows Desktop Widget)

## 1) Overview
Add a **Gym tracking module** to the existing Windows desktop widget:
- Ingest gym enter/exit events from **Geofency** (iOS) via webhook (Path A).
- Derive daily attendance for **Mon–Fri** and render:
  1) **Progress bar**: visits vs target (period configurable).
  2) **Heatmap**: calendar-mapped grid (Mon–Fri only), **green** = visited, **red** = missed, neutral for future.


The feature must be **fully automatic** after initial setup, **offline-friendly** on the desktop side (reads from a small API), and **configurable via JSON**.

## 2) Goals
- Fully automated gym attendance tracking with minimal user effort.
- Clear, compact visual of consistency (progress + heatmap) aligned to real calendar dates.
- Configurable view horizon (e.g., last 12 weeks by default; optionally up to a full year).
- Simple integration into the current desktop widget 

## 3) Non-Goals (MVP)
- No manual editing of visits inside the widget.
- No workout detail capture (exercises/sets), no health sensor integrations.
- No weekend display (Sat/Sun) in heatmap.
- No social sharing, leaderboards, or gamification beyond streak/target metrics.

## 4) Users & Use Cases
### Primary user
- A single user on iOS who uses Geofency for location tracking and a Windows desktop widget for daily dashboards.

### Core use cases
1. Track gym attendance automatically when entering/leaving the gym.
2. View weekly/monthly progress toward a configurable target.
3. See a calendar-accurate heatmap of misses vs visits across weeks/months.
4. Review a short log of the latest gym sessions.

## 5) Assumptions
- User enables Geofency tracking for a single gym location (additional locations may be added later).
- Geofency sends webhooks for enter/exit events to a public HTTPS endpoint.
- Desktop widget is a standalone Windows app with JSON configuration.
- Local timezone for calendar mapping is user-configurable (default: system timezone).

## 6) Functional Requirements

### 6.1 Webhook ingestion (backend)
**FR-1** The system must expose a public HTTPS endpoint to receive Geofency events.
- Endpoint: `POST /ingest/geofency`
- Auth: token in query string or header (e.g., `X-Auth-Token`).

**FR-2** The ingestion must store raw events idempotently.
- If Geofency provides an event id, use it as an idempotency key.
- If not, derive an idempotency key: `hash(location_id + timestamp_utc + entry_flag)`.

**FR-3** The ingestion must support at minimum:
- event timestamp (UTC)
- enter/exit flag
- location identifier and name
- optional metadata if present (wifi/motion/device)

### 6.2 Visit sessionization (backend)
Convert raw enter/exit events into visit sessions.

**FR-4** Pair `enter` → `exit` events per location into a **Visit**.

**FR-5** Apply quality rules (configurable):
- **Minimum duration** (default: 20 minutes) to count as a visit.
- **Dedup window** (default: 3 hours) to prevent double counting when multiple transitions occur.
- Missing exit handling:
  - Auto-close an open visit after `max_visit_minutes` (default: 240) and mark it `estimated=true`.

**FR-6** A day is marked **Visited** if at least one qualified visit exists on that local date (Mon–Fri).

### 6.3 Daily rollup (backend)
Generate rollups for a date range and cache them.

**FR-7** Rollups must be computed on:
- new event ingestion (incremental update), and/or
- scheduled job (daily at 00:05 local timezone).

**FR-8** Rollup output per date includes:
- `date_local` (YYYY-MM-DD)
- `is_workday` (Mon–Fri)
- `status`: `visit | miss | future | excluded`
- `minutes_total` (sum of qualified visit minutes; optional)
- `visit_count`

**FR-9** Miss logic:
- For past workdays in the range with no qualified visit, mark **miss**.
- For future workdays, mark **future**.

### 6.4 Summary API (backend)
Provide a stable contract for the widget.

**FR-10** Expose `GET /gym/summary` returning:
- progress metrics for the chosen period (week or month)
- heatmap grid data for requested view (rolling weeks or calendar year)
- recent visits list

Query parameters:
- `mode=rolling_weeks|calendar_year`
- `weeks=N` (for rolling_weeks; default 12; allow up to 52)
- `year=YYYY` (for calendar_year)

Auth:
- `READ_TOKEN` required.

### 6.5 Desktop widget UI (Windows)
Add a second tile under the existing content.

**FR-11** Gym tile layout:
1. Title line: `Gym: {actual}/{target} ({period_label})`
2. Thin progress bar
3. Heatmap grid (Mon–Fri rows, weeks columns, small market for the short form a month (e.g., 'Feb')


**FR-12** Heatmap rules:
- Rows represent weekdays in order: Mon, Tue, Wed, Thu, Fri.
- Columns represent weeks; each cell maps to an exact calendar date.
- Colors:
  - **Visited**: green
  - **Missed**: red
  - **Future**: neutral/low opacity
  - **Excluded** (optional): neutral with marker

**FR-13** Month labels:
- Show short month markers (e.g., `Feb`, `Mar`) above the first column where that month begins within the visible range.
- Month labeling must map to calendar reality (not “every 4 weeks”).

**FR-14** No interaction:
- Click does nothing.
- No menus in MVP.

**FR-15** Update cadence:
- Widget refreshes from API at app start and then once per day shortly after midnight local time.
- Optional (config): refresh every X hours.

### 6.6 Configuration (JSON-only)
**FR-16** Extend widget config with a `gym` block.

Required fields (MVP):
- `enabled` (bool)
- `api_base_url`
- `read_token`
- `timezone` (IANA string or `system`)
- `target_period` (`weekly` or `monthly`)
- `target_visits` (int)
- `qualification_min_minutes` (int)
- `heatmap_mode` (`rolling_weeks` or `calendar_year`)
- `heatmap_weeks` (int; max 52)
- `show_weekends` (bool; default false)

Optional fields:
- `excluded_dates` (array of YYYY-MM-DD)
- `refresh_interval_minutes`
- `recent_visits_count`

## 7) Data Contracts (proposed)

### 7.1 Ingest payload (Geofency → backend)
Store all fields as received; required normalized fields:
- `timestamp_utc`
- `entry` (true for enter, false for exit)
- `location_id`
- `location_name`

### 7.2 Summary response (backend → widget)
Return JSON shaped for direct rendering:
- `meta`: timezone, generated_at
- `target`: period, workdays_only, target_visits
- `progress`: label, actual, target, pct, streaks (optional)
- `heatmap`: mode, weeks[], month_labels[]
- `recent_visits`: list of {date_local, start_local, minutes, estimated}

## 8) Algorithms & Rules

### 8.1 Sessionization (enter/exit pairing)
- Maintain an “open visit” per location.
- On `enter`:
  - if no open visit: create open visit with start timestamp.
  - if open visit exists: ignore (or close previous as estimated and start new) based on dedupe rules.
- On `exit`:
  - if open visit exists: close and compute duration.
  - if no open visit: ignore or create an estimated visit (config).

### 8.2 Calendar mapping
- Convert timestamps to `date_local` using configured timezone.
- Heatmap week columns start on **Monday**.
- Each cell date = `week_start_monday + row_index` (0..4).

### 8.3 Status classification per date
- If date in `excluded_dates`: `excluded`
- Else if date > today: `future`
- Else if qualified visit exists: `visit`
- Else if Mon–Fri: `miss`

## 9) Security & Privacy
- Use separate `WRITE_TOKEN` (ingest) and `READ_TOKEN` (summary).
- Store only what is necessary:
  - timestamps, location id/name, derived duration
  - avoid storing continuous location traces
- Encrypt secrets at rest where supported; never embed tokens in public repos.

## 10) Non-Functional Requirements
- **Reliability:** ingestion endpoint available 99%+ (free tier best-effort acceptable).
- **Latency:** summary endpoint p95 < 500ms on typical free serverless.
- **Cost:** should fit within a free tier for typical personal use.
- **Portability:** backend should be deployable to common free hosts (e.g., Cloudflare/Vercel/Netlify/Supabase).

## 11) Acceptance Criteria

### Backend
- Webhook events are accepted and stored; duplicates do not create double counts.
- Visits are correctly derived with minimum-duration and dedupe rules.
- Rollups correctly mark Mon–Fri past dates without visits as **miss**.
- `GET /gym/summary` returns correct progress metrics and heatmap dates.

### Widget
- Gym tile renders correctly under the current content.
- Heatmap cells map to the correct calendar dates and weekdays.
- Month labels appear as `Jan/Feb/Mar...` at correct week columns.
- Heatmap view horizon is configurable up to a full year.
- Weekend dates are not shown in the heatmap.

