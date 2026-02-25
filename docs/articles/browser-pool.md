# Chromium Browser Pool

## Why pool Chromium instances?

Launching a Chromium process takes ~400ms and ~150MB of RAM. For high-volume badge printing (e.g. 200 attendees checking in simultaneously) spinning up a new browser per request would be both slow and memory-hungry.

The pool (`ChromiumBrowserPool`) keeps a set of warm browser instances and hands them out as leases — similar to a database connection pool.

## How it works

```
AcquireAsync()
    │
    ├─ SemaphoreSlim (cap at MaxSize)
    │
    ├─ ConcurrentQueue<PooledBrowser> (idle browsers)
    │     └─ Dequeue a warm browser if available
    │
    └─ No idle browser → LaunchAsync() a new Chromium
          └─ Track in ConcurrentDictionary<IBrowser, PooledBrowser>

    Returns: IBrowserLease<IBrowser>

On Dispose(lease):
    ├─ lease.Invalidate() was called (crash/error)
    │     └─ Close + discard browser
    └─ Normal return
          ├─ RenderCount >= MaxRendersPerInstance → recycle
          └─ Return to idle queue
```

## Configuration (`appsettings.json`)

```json
"BrowserPool": {
  "MinSize": 1,
  "MaxSize": 4,
  "AcquireTimeout": "00:00:30",
  "IdleTimeout": "00:05:00",
  "MaxRendersPerInstance": 100
}
```

| Setting | Default | Description |
|---|---|---|
| `MinSize` | 1 | Browsers kept warm at startup |
| `MaxSize` | 4 | Maximum concurrent Chromium processes |
| `AcquireTimeout` | 30s | How long to wait for a free browser before throwing |
| `IdleTimeout` | 5 min | Idle browsers are reaped after this duration |
| `MaxRendersPerInstance` | 100 | Browser is recycled after N renders to prevent memory leaks |

## Sizing guidance

Each Chromium instance uses approximately 100–200 MB of RAM. Set `MaxSize` based on available memory:

| RAM available | Recommended MaxSize |
|---|---|
| 1 GB | 2 |
| 2 GB | 4 (default) |
| 4 GB | 8–12 |

`MaxSize` should also be kept at or below `Kafka:MaxConcurrentRenders` to avoid pool starvation — there is no point having more concurrent Kafka render tasks than available browsers.

## Page vs browser reuse

Each render opens a new **Page** (browser tab) within a leased browser, then closes it immediately after. This is much cheaper than launching a new browser per render while still providing isolation between documents.
