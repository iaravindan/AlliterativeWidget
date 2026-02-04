Institutional investors - analysts, fund managers, head of equities,...

cost of growth - contract


1) Overview

A minimalist, standalone Windows desktop widget that appears as a small transparent glassy tile pinned to the top-right corner. On workdays only (Mon–Fri), it displays a single daily combination:

Headline (large): upbeat, exaggerated, alliterative prefix matching weekday initial (e.g., Fantastic Friday)

Punchline (small): dry corporate satire, slightly edgy but workplace-appropriate

It updates once per day at midnight local time. No display on Saturday/Sunday.

2) Goals

Provide a clean, elegant corner widget with one daily headline + punchline

Ensure alliteration constraint (prefix starts with same letter as day)

Avoid repetition via no-repeat windows

Be offline, fast, and unobtrusive

Configuration and content managed via a JSON file (no in-app editor)

3) Non-Goals (MVP)

No click actions (click does nothing)

No menu, tray icon, or interaction-driven features

No calendar/news/AI sentiment integrations

No weekend content (widget hidden on Sat/Sun)

4) Target User

Windows user who wants a tiny daily satirical “mood cue” without desktop clutter.

5) Core UX Requirements
Placement and Layout

Default position: Top-right corner

Snap-to-corner behavior: always snaps to top-right with padding

Default padding: 16 px from top and right edges (configurable in JSON)

Fixed position (no drag reposition in MVP)

Visual Style

Transparent glassy tile (Acrylic/Blur-behind effect)

Subtle rounded corners (e.g., 12–16 px)

Minimal shadow (soft, not heavy)

Typography:

Headline: large, bold (single line)

Punchline: smaller, regular (wrap up to 2 lines max; then ellipsis)

No icons, no extra labels, no date shown (unless you later choose to add it)

Interaction

Click does nothing

No hover effects required (optional subtle cursor change is fine)

6) Functional Requirements
Workday-only Display

On Saturday and Sunday, the widget must not show (either:

do not launch UI surface, or

launch and remain fully hidden).

On Mon–Fri, widget is visible with daily content.

Daily Refresh Rule (Midnight Local Time)

App computes a daily key using local date (YYYY-MM-DD).

At local midnight (or on first app start after midnight), if the daily key changed:

generate new headline + punchline (subject to anti-repeat rules)

persist selection in history

update UI

Content Generation Rules

Determine weekday (Mon–Fri).

Select prefix from weekday pool matching initial:

Monday: M_prefixes

Tuesday: T_prefixes_tue

Wednesday: W_prefixes

Thursday: T_prefixes_thu (separate pool to reduce repeats)

Friday: F_prefixes

Construct headline: "{Prefix} {Weekday}"

Select punchline from configured punchline pool (edgy-by-default per your direction).

Anti-Repeat Logic

Persisted locally. Defaults can be tuned via JSON.

Do not reuse the same headline for that weekday within last 8 occurrences

Do not reuse the same punchline within last 75 days

If pools are exhausted, relax constraints in a deterministic order:

allow prefix reuse beyond window

then allow punchline reuse beyond window
(Always log internally; no user-facing messaging)

Offline & Local Storage

No network calls

Store config + history locally:

Config: %AppData%/AlliterativeWidget/config.json

History: %AppData%/AlliterativeWidget/history.json

7) Configuration (JSON-Only)
Config JSON (proposed schema)
{
  "ui": {
    "corner": "top_right",
    "padding_px": { "top": 16, "right": 16 },
    "width_px": 300,
    "max_punchline_lines": 2,
    "opacity": 0.92
  },
  "schedule": {
    "refresh_time_local": "00:00",
    "show_weekends": false
  },
  "rules": {
    "headline_no_repeat_window": 8,
    "punchline_no_repeat_days": 75
  },
  "tone": {
    "punchline_pool": "edgy"
  },
  "content": {
    "M_prefixes": ["Marvellous", "Magnificent", "Miraculous"],
    "T_prefixes_tue": ["Transformational", "Triumphant", "Top-Tier"],
    "W_prefixes": ["World-Class", "Winning", "Well-Aligned"],
    "T_prefixes_thu": ["Thriving", "Tremendous", "Turbocharged"],
    "F_prefixes": ["Fantastic", "Flawless", "Phenomenal"],
    "punchlines": {
      "standard": ["Progress update: we are updating progress."],
      "edgy": ["Shipping confidence. Testing later."]
    }
  }
}

History JSON (proposed)

last_daily_key

last_rendered_text

rolling arrays:

headline_history: per weekday (queue of last N headlines)

punchline_history: queue with date stamps (for day-based expiry)

8) Technical Approach (Windows)

To reliably deliver a glassy tile:

Recommended: WinUI 3 (Windows App SDK) using Acrylic/Mica where appropriate.

Alternative: WPF with DWM blur-behind (works, but Acrylic fidelity can vary).

MVP architecture

SchedulerService: detects midnight rollover, triggers refresh

ContentEngine: selects prefix + punchline with constraints

Persistence: loads/saves config + history

WidgetWindow: borderless, always-on-top, top-right snapped

9) Edge Cases

If system time/date changes (manual adjustment, timezone change):

regenerate if daily key differs from stored

If config JSON is invalid:

fall back to bundled default config and continue

If content pools are too small:

gracefully relax anti-repeat rules as specified

10) Acceptance Criteria (MVP)

On Mon–Fri: shows one headline + punchline, pinned top-right with 16 px padding.

On Sat/Sun: widget does not show.

Alliteration: prefix initial matches weekday initial (M/T/W/T/F).

Updates once per day at local midnight (or first run after midnight).

No repeats per configured windows.

Click does nothing.

Entirely offline; configurable via JSON; persists history across restarts.

11) MVP Scope Summary

Included: Workday-only glass tile widget, midnight refresh, JSON config, no-repeat logic, local history
Excluded: UI settings, menus, click actions, tray icon, weekend modes, multi-monitor targeting (can be v1.1)