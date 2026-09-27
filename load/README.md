# Load tests

Four [k6](https://k6.io) scenarios that run against the full Docker stack, through the gateway.
Each one **passes or fails**: k6 exits non-zero when a threshold is crossed.

| Scenario | What it does | Passes when |
| --- | --- | --- |
| `contention.js` | 50 guests book **the same room, same nights**, at the same instant | exactly one `201`, 49 × `409 Booking.RoomUnavailable`, no `5xx` |
| `browse.js` | 50 users browse cities → search → hotel → rooms for two minutes | no failed requests, p95 under each route's threshold |
| `checkout.js` | 10 checkouts per second for two minutes, no two touching the same nights | every checkout succeeds; even a `409` fails it |
| `flood.js` | 100,000 room-availability queries: 1,000 a second for 100 seconds | no failed requests, p99 under 200 ms, k6 kept up the full rate |

Nothing to install: k6 runs as a container on the compose network.

## Run

From the repository root:

```bash
docker compose up -d --build                                   # 1. start the stack

export COMPOSE_FILE=docker-compose.yml:load/docker-compose.load.yml
docker compose up -d gateway                                   # 2. raise the gateway's rate limits

docker compose run --rm k6 run scenarios/contention.js         # 3. run a scenario
docker compose run --rm k6 run scenarios/browse.js
docker compose run --rm k6 run scenarios/checkout.js
docker compose run --rm k6 run scenarios/flood.js
docker compose run --rm k6 run -e RATE=2000 -e DURATION=30s scenarios/flood.js   # other rate/length

unset COMPOSE_FILE
docker compose up -d gateway                                   # 4. put the shipped limits back
```

Step 2 matters: the gateway allows 300 requests a minute per IP, and k6 is one IP, so without it
the run measures the rate limiter instead of the API. The override raises the limits; it does not
turn the limiter off.

## Results on a laptop

| Endpoint | p50 | p95 | p99 |
| --- | --- | --- | --- |
| `GET /hotels` (search, 50 users) | 2.2 ms | 3.6 ms | 4.9 ms |
| `GET /hotels/{id}/rooms` (50 users) | 8.7 ms | 13.9 ms | 17.2 ms |
| `POST /bookings`, uncontended | 17.9 ms | 31.8 ms | 36.3 ms |
| `POST /bookings`, 50 callers on one room | 298 ms | 439 ms | 515 ms |

The last two rows are the same endpoint. The gap is fifty callers waiting on the same room-nights,
which is the double-booking guard doing its job: the losers are told `409` in half a second
instead of being sold a room that is already taken.

**Flood:** 100,000 availability queries at 1,000 a second finished with 0 errors, p50 5.4 ms and
p99 19 ms. Availability isn't cached, so every one of them reached SQL Server. The laptop's ceiling
is between 2,000 and 3,000 a second. At 3,000, k6 couldn't send every request on time (2,168 were
skipped) and p99 jumped to 2.5 s. At that point the whole machine is saturated (k6 included), so
treat it as a limit of this laptop, not of the API.

## Files

```
lib/api.js                shared helpers: register, login, find rooms, book
scenarios/*.js            the four scenarios
docker-compose.load.yml   the k6 container + the raised gateway limits
```

Each run registers its own guest and books nights far in the future, so runs never collide and
nothing needs cleaning up.
