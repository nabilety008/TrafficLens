# TrafficLens — Database

Status: **Design phase** (schema lands in TL-009).

## Requirements

- Store aggregated traffic samples, not every packet.
- Support Today / Yesterday / Last 7 Days / Last 30 Days / Lifetime views.
- Future retention policies (e.g., delete data older than N days).
- Local SQLite database, no external services.

## Planned location

`%LOCALAPPDATA%\TrafficLens\trafficlens.db` (`AppPaths.DatabaseFile`).

## Planned schema (draft)

### traffic_samples
Aggregated per-interface samples.

| Column | Type | Notes |
|---|---|---|
| id | INTEGER PK | |
| timestamp | TEXT (ISO) | sample bucket time |
| bucket | TEXT | e.g. `minute`, `hour`, `day` |
| adapter_id | TEXT | interface identifier |
| download_bytes | INTEGER | bytes in bucket |
| upload_bytes | INTEGER | bytes in bucket |

### process_samples
Aggregated per-process samples.

| Column | Type | Notes |
|---|---|---|
| id | INTEGER PK | |
| timestamp | TEXT (ISO) | |
| bucket | TEXT | |
| process_id | INTEGER | owning PID |
| process_name | TEXT | display/executable name |
| executable_path | TEXT | nullable |
| download_bytes | INTEGER | |
| upload_bytes | INTEGER | |

Indexes: `(bucket, timestamp)`, `(adapter_id)`, `(process_id)`.

## Design notes

- Pre-aggregate to reduce write volume; sample cadence per settings (default ~1s in memory,
  persisted aggregates coarser).
- Retention policy is a later feature; schema must not preclude deleting old buckets.
- Write-once/append-only simplifies "no packet-level storage" guarantee.
- Batch inserts in transactions; never insert per sample (avoid SQLite write amplification).

Detailed schema will be finalized in TL-009 and documented against actual code.