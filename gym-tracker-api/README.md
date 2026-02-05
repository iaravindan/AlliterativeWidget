# Gym Tracker API

Cloudflare Workers API for tracking gym visits via Geofency webhooks.

## Setup

### 1. Install Dependencies

```bash
npm install
```

### 2. Create D1 Database

```bash
npm run db:create
```

Copy the database ID from the output and update `wrangler.toml`:
```toml
database_id = "YOUR_DATABASE_ID_HERE"
```

### 3. Run Migrations

Local development:
```bash
npm run db:migrate
```

Production:
```bash
npm run db:migrate:prod
```

### 4. Configure Secrets

Set the auth tokens as secrets:
```bash
wrangler secret put WRITE_TOKEN
wrangler secret put READ_TOKEN
wrangler secret put TIMEZONE  # Optional, e.g., "America/New_York"
```

### 5. Deploy

```bash
npm run deploy
```

## API Endpoints

### POST /ingest/geofency

Receives Geofency webhooks. Requires `X-Auth-Token: WRITE_TOKEN`.

**Request (JSON):**
```json
{
  "name": "Gym Name",
  "entry": "1",
  "date": "2024-01-15T08:30:00Z"
}
```

**Request (Form Data):**
- `name`: Location name
- `entry`: "1" for enter, "0" for exit
- `date`: ISO 8601 timestamp

**Response:**
```json
{
  "status": "created",
  "event_id": 1,
  "event_hash": "abc123...",
  "action": "enter",
  "timestamp": "2024-01-15T08:30:00.000Z",
  "location": "Gym Name",
  "session": {
    "action": "visit_started",
    "visitId": 1
  }
}
```

### GET /gym/summary

Returns gym progress and heatmap data. Requires `X-Auth-Token: READ_TOKEN`.

**Query Parameters:**
- `mode`: "weekly" or "monthly" (default: "weekly")
- `weeks`: 12-52 (default: 12)
- `target`: Target visits per period (default: 4)

**Response:**
```json
{
  "currentPeriod": {
    "label": "This Week",
    "visits": 3,
    "target": 4,
    "progressPercent": 75
  },
  "heatmap": {
    "weeks": 12,
    "grid": [...],
    "monthLabels": [...]
  },
  "stats": {
    "totalVisits": 45,
    "totalMinutes": 2700,
    "currentStreak": 5,
    "longestStreak": 12
  },
  "generatedAt": "2024-01-15T12:00:00.000Z"
}
```

### GET /health

Health check endpoint (no auth required).

## Geofency Configuration

1. In Geofency, create a location for your gym
2. Add a webhook:
   - URL: `https://your-worker.workers.dev/ingest/geofency`
   - Method: POST
   - Format: Form-encoded or JSON
   - Headers: `X-Auth-Token: YOUR_WRITE_TOKEN`

## Scheduled Jobs

The worker runs a daily cron job at 5 AM UTC to:
- Auto-close visits open for more than 240 minutes
- Recompute rollups for the past 7 days

## Development

```bash
npm run dev
```

Test with curl:
```bash
# Enter event
curl -X POST http://localhost:8787/ingest/geofency \
  -H "Content-Type: application/json" \
  -H "X-Auth-Token: your-write-token" \
  -d '{"name": "My Gym", "entry": "1", "date": "2024-01-15T08:30:00Z"}'

# Exit event
curl -X POST http://localhost:8787/ingest/geofency \
  -H "Content-Type: application/json" \
  -H "X-Auth-Token: your-write-token" \
  -d '{"name": "My Gym", "entry": "0", "date": "2024-01-15T09:30:00Z"}'

# Get summary
curl http://localhost:8787/gym/summary?weeks=12 \
  -H "X-Auth-Token: your-read-token"
```
