# panda-plate-watch

Pushes an [ntfy.sh](https://ntfy.sh) notification on the mornings when Panda Express
is running its discounted two-entree plate — i.e. the day after the Dodgers win a
home game.

It checks the previous day (Pacific time) against the public MLB Stats API, and
only fires when the game was **at Dodger Stadium**, **final**, and **a Dodgers
win**. Road wins, home losses, in-progress games, spring training and neutral-site
games are all ignored.

```
🐼 $7 Panda Plate today
Dodgers beat the San Francisco Giants 3-1 at home on Sun Sep 20.

Two-entree plate for $7 today (Mon Sep 21) with code DODGERSWIN —
online and app orders only, while the offer lasts.
```

A single .NET 10 console app with no NuGet dependencies — the MLB Stats API is open
and ntfy topics are just strings you pick.

## Quick start

1. Pick a topic name nobody will guess (`panda-plate-7f3a91` or similar) and
   subscribe to it in the ntfy [app](https://ntfy.sh/app) or on the web.
2. Build and run:

```bash
dotnet run --project src/PandaPlateWatch -- --dry-run
```

`--dry-run` prints what it would send and doesn't need a topic configured. Point
`--date` at a known Dodgers home win to see a real message:

```bash
dotnet run --project src/PandaPlateWatch -- --date 2026-09-20 --dry-run
```

For actual use, publish a self-contained binary:

```bash
dotnet publish src/PandaPlateWatch -c Release -o ./app
NTFY_TOPIC=panda-plate-7f3a91 ./app/panda-plate-watch
```

## Running it every day

### GitHub Actions (nothing to host)

Fork or push this repo, then add a repository secret **`NTFY_TOPIC`** under
Settings → Secrets and variables → Actions. The `check for the deal` workflow runs
daily at 15:00 UTC (8am PDT / 7am PST), after even the latest West Coast game has
gone final. Run it by hand from the Actions tab to test.

Optional repository *variables*: `NTFY_SERVER`, `NTFY_PRIORITY`, `NTFY_TAGS`,
`PANDA_DEAL_PRICE`, `PANDA_PROMO_CODE`. Optional secret: `NTFY_TOKEN`.

The workflow caches `.state.json` between runs, so a manual re-run on a day it has
already alerted stays quiet.

### cron on your own machine

```cron
0 8 * * * NTFY_TOPIC=panda-plate-7f3a91 /usr/local/bin/panda-plate-watch --state-file ~/.panda-plate-watch.json --quiet
```

## Configuration

| Variable | Default | Meaning |
| --- | --- | --- |
| `NTFY_TOPIC` | *(required)* | Topic to publish to |
| `NTFY_SERVER` | `https://ntfy.sh` | Self-hosted ntfy instance |
| `NTFY_TOKEN` | – | Bearer token, for protected topics |
| `NTFY_PRIORITY` | `4` | ntfy priority, 1–5 |
| `NTFY_TAGS` | `panda_face,baseball` | Comma-separated ntfy tags/emoji |
| `PANDA_DEAL_PRICE` | `$7` | Price shown in the message |
| `PANDA_PROMO_CODE` | `DODGERSWIN` | Promo code shown in the message |

A variable that is set but empty counts as unset, since that is how GitHub Actions
passes an undefined repository variable into a step.

Flags: `--date YYYY-MM-DD`, `--dry-run`, `--state-file PATH`, `--quiet`, `--help`.

Exit code is `0` whether or not there's a deal, and `1` only on a real failure
(unreachable API, bad config, bad arguments, ntfy rejected the publish), so a cron
wrapper can treat non-zero as something worth looking at.

## Caveats

Panda Express owns the promotion, not this repo. The price, the promo code, the
ordering channel and the whole offer can change or disappear without notice —
that's why the price and code are configurable rather than hard-coded. This tool
tells you the Dodgers won at home yesterday; it can't confirm the deal is live or
that you're in a participating location. Verify in the Panda Express app before
planning lunch around it.

The MLB Stats API is undocumented-but-public. Be polite: one request a day is
fine, hammering it is not.

## Development

```bash
dotnet test
```

73 tests, run on Linux, Windows and macOS in CI. They exercise recorded API
payloads in `tests/PandaPlateWatch.Tests/Fixtures/` — real responses for a home
win, a home loss, a road win and an off day — and a stub `HttpMessageHandler`, so
the suite never touches the network.

Layout:

| Path | What it does |
| --- | --- |
| `Mlb/Dodgers.cs` | the home-win rules (venue, game type, final, winner) |
| `Mlb/MlbScheduleClient.cs` | one-day schedule fetch |
| `Ntfy/NtfyPublisher.cs` | publishes to ntfy's JSON endpoint |
| `DealChecker.cs` | orchestration, message text, Pacific-day logic |
| `StateStore.cs` | duplicate suppression |
| `CommandLineOptions.cs` | argument parsing |

## License

MIT
