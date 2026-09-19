# TrafficLens — Database

Status: **Implemented (TL-009), schema v2 since TL-017.** Local SQLite database at
`%LOCALAPPDATA%\TrafficLens\data\trafficlens.db` (`AppPaths.DatabaseFile`; the
`data` directory is created on startup). WAL journal mode, `busy_timeout=5000`,
`Pooling=false` (single deterministic connection per repository instance so no
file locks leak across scopes).

## Requirements

- Store aggregated traffic samples, not every packet.
- Support Today / Yesterday / Last 7 Days / Last 30 Days / Lifetime views.
- Future retention policies (e.g., delete data older than N days).
- Local SQLite database, no external services.

## Schema (v2, `PRAGMA user_version = 2`)

Schema v1 (`PRAGMA user_version = 1`, TL-009) is migrated to v2 on startup by
TL-017. v2 only adds a Lifetime rollup table; every v1 table/column/semantic is
unchanged.

### traffic_samples
Per-UTC-minute aggregated SYSTEM/GLOBAL buckets (created from valid counter
deltas only; tunnels excluded by default per ADR-009/017).

| Column | Type | Notes |
|---|---|---|
| bucket_start_utc | INTEGER PK | Unix epoch seconds of the minute bucket start |
| bucket_duration_seconds | INTEGER | normally 60; < 60 only for a shutdown-flushed open minute (1..60) |
| download_bytes | INTEGER | system download bytes in bucket (>= 0) |
| upload_bytes | INTEGER | system upload bytes in bucket (>= 0) |

### daily_usage
Rollup of `traffic_samples` into local-date rows (bucketing via the service's
`TimeZoneInfo`; kept forever).

| Column | Type | Notes |
|---|---|---|
| local_date | TEXT PK | `yyyy-MM-dd` (local date shell) |
| download_bytes | INTEGER | daily system download bytes |
| upload_bytes | INTEGER | daily system upload bytes |

### lifetime_totals (added in schema v2, TL-017)
Single-row lifetime aggregate so the Lifetime / History-total view is an O(1)
read instead of an O(n) sum over `traffic_samples`.

| Column | Type | Notes |
|---|---|---|
| id | INTEGER PK | constant row id (e.g. 1) |
| download_bytes | INTEGER | lifetime system download bytes |
| upload_bytes | INTEGER | lifetime system upload bytes |

`lifetime_totals` is updated in the same transaction as the `traffic_samples`
append (kept consistent with the 30 s flush). The Lifetime query reads this row.

No separate indexes needed: INTEGER PK = rowid (table is its own index);
`local_date` PK covers the date-range lookups used by the views.
`traffic_samples` is append-only, one transaction per flush.

## Migration (v1 → v2)

On startup the repository checks `PRAGMA user_version`:
- `0` (fresh) → creates the full v2 schema (`user_version = 2`).
- `1` (v1) → creates `lifetime_totals`, back-fills it from the existing
  `daily_usage` sum (`INSERT OR IGNORE id=1` with the totals), sets
  `user_version = 2`. `daily_usage` is never pruned and is the exact sum of all
  persisted buckets, so the pre-upgrade Lifetime total is preserved.
- `2` → no-op.

## Idempotent appends (restart/crash safety)

A flush appends each new minute bucket with `INSERT OR IGNORE` and only roll the
matching `daily_usage` totals when `changes() == 1` (i.e., the bucket did not
already exist). Because minute buckets are keyed by their UTC start, an app
restart (or crash before a flush) can never double-count history: re-flushing
the same open minute is ignored, and graceful shutdown flushes the remaining
open minute before the repository is closed. Worst case on a hard kill: the
current unflushed minute is lost (nothing invented).

## Retention

- `daily_usage`: kept forever (a few KB/year).
- `traffic_samples`: pruned on startup to the most recent 90 days
  (`TrafficHistoryService.PruneRawSamplesBeforeAsync`); the 90-day horizon is a
  constant and future-settings-ready.
- A future hourly/current-day view only needs a new query against the retained
  raw minute samples.

## Growth estimates (order of magnitude)

Rows: `traffic_samples` ≈ 1440/day. Observed footprint after 20 MB of real
traffic across two sessions: **16 KB**.

| Horizon | traffic_samples rows | est. size | daily rows | est. size |
|---|---|---|---|---|
| 1 day | 1,440 | ≈ 0.1 MB | 1 | ≈ 50 B |
| 30 days | 43,200 | ≈ 2.6 MB | 30 | ≈ 1.5 KB |
| 90 days (steady state) | 129,600 | ≈ 7.8 MB | 90 | ≈ 5 KB |
| 1 year * | 525,600 | ≈ 32 MB | 365 | ≈ 20 KB |

\* 1-year figure assumes retention were extended to a year; with the current
90-day prune the DB stays near the 90-day row count.

## Design notes

- Pre-aggregation: raw ~1 s counter samples are never persisted — only 60 s
  system buckets; the UI reads cached aggregates via `HistorySnapshot`.
- Per-process / per-connection history is deliberately out of scope (TL-007 and
  TL-008 are live-only views).
- Batch transactional appends only (never per-sample writes).