# Hotel Booking Platform — Backend

A REST backend for an online hotel booking platform. Guests search hotels, filter by dates and
price, fill a cart, check out, and get a confirmation email. Admins manage cities, hotels, rooms
and deals.

The goal was never "the endpoints return 200". It was one sentence:

> **A room-night is sold at most once, under concurrency.**

Everything below follows from that.

---

## Table of Contents

- [Technology Stack](#technology-stack)
- [Key Features](#key-features)
- [System Architecture](#system-architecture)
- [The Critical Path: Checkout](#the-critical-path-checkout)
- [Database Design](#database-design)
- [Indexes](#indexes)
- [Redis](#redis)
- [Design Decisions & Tradeoffs](#design-decisions--tradeoffs)
- [Getting Started](#getting-started)
- [API Reference](#api-reference)
- [Security](#security)
- [Reliability & Fault Tolerance](#reliability--fault-tolerance)
- [Observability](#observability)
- [Testing](#testing)
- [CI/CD](#cicd)
- [Measured Numbers](#measured-numbers)
- [Project Structure](#project-structure)
- [What I Didn't Build](#what-i-didnt-build)

---

## Technology Stack

| Category | Technology |
| --- | --- |
| **Language** | C# 14 |
| **Runtime** | .NET 10 (LTS), nullable enabled, warnings-as-errors |
| **Web Framework** | ASP.NET Core Minimal APIs |
| **Database** | SQL Server 2025 |
| **ORM** | EF Core 10 |
| **Cache** | Redis 8 (StackExchange.Redis) |
| **Messaging** | Transactional Outbox in SQL Server — no broker |
| **Authentication** | JWT + rotating refresh tokens + Redis session denylist |
| **Password Hashing** | ASP.NET Core Identity (PBKDF2) |
| **Validation** | FluentValidation |
| **Email** | MailKit over SMTP |
| **Logs / Traces / Metrics** | Serilog + OpenTelemetry → Collector → Seq / Jaeger / Prometheus |
| **Docs** | OpenAPI + Scalar |
| **Testing** | xunit.v3, Shouldly, NSubstitute, Testcontainers, NetArchTest |
| **Containerization** | Docker + Docker Compose |
| **CI/CD** | GitHub Actions |

---

## Key Features

| Feature | Description |
| --- | --- |
| **No double booking** | A room-night is a primary key. The database refuses the second buyer |
| **Idempotent checkout** | `Idempotency-Key` header; a replay returns the original booking, not a second one |
| **Search & filters** | City, dates, guests, price range, stars, room type, sorted and paged |
| **Cart** | Redis-backed, 24h TTL, survives any instance dying |
| **Deals** | Night-by-night discounts, repriced server-side at checkout |
| **Home page discovery** | Featured deals, recently viewed hotels, trending cities |
| **Confirmation email** | Written in the checkout transaction, delivered by the outbox |
| **Cancellation** | Guest-initiated inside a 48h window; releases the nights immediately |
| **Rotating refresh tokens** | Hashed, with reuse detection that revokes the whole family |
| **RFC 9457 errors** | Every non-2xx is a problem document carrying a `traceId` and an `errorCode` |
| **Rate limiting** | Fixed window, by user when signed in and by IP otherwise |
| **Full telemetry** | One trace id joins the HTTP request, the SQL, the cache and the email |

---

## System Architecture

Four projects. Dependencies point inward, and nothing points back out.

![Dependency direction between the four projects](diagrams/readme-01-layers.svg)

<sub>Source: [`readme-01-layers.excalidraw`](diagrams/readme-01-layers.excalidraw) — open it at excalidraw.com to edit.</sub>

The Domain depends on nothing — not even EF Core. It doesn't know a database exists. That rule is
enforced by NetArchTest assertions that fail the build, not by me remembering it.

Inside Domain and Application, folders are **aggregates** (`Bookings/`, `Hotels/`, `Deals/`), never
technical types. `Services/` + `Dtos/` + `Validators/` as top-level folders splits one feature
across three places, so every change touches all three. I've done that before. It's miserable.

### Runtime shape

![Runtime shape: API, SQL Server, Redis, outbox dispatcher and telemetry](diagrams/readme-02-runtime.svg)

<sub>Source: [`readme-02-runtime.excalidraw`](diagrams/readme-02-runtime.excalidraw) — open it at excalidraw.com to edit.</sub>

### Two read paths, on purpose

| Path | Used by | Returns | Why |
| --- | --- | --- | --- |
| **Repository** | writes | domain objects | Rules need real entities to enforce invariants |
| **Queries** (`IHotelQueries`) | list/search reads | DTOs, `AsNoTracking()` | Loading 20 hotels as tracked entities to render cards is waste |

Repositories never expose `IQueryable`. The moment they do, query logic leaks into services and the
repository can no longer be optimised or tested. Repositories also never call `SaveChanges` — that
belongs to `IUnitOfWork`, so **one use case is one transaction**.

### Stateless by design

| State | Where it lives | Consequence |
| --- | --- | --- |
| Identity | JWT, verified per request | No server-side session |
| Logout | Redis, keyed by session, self-expiring | Revocation is global and instant |
| Refresh tokens | SQL, hashed, rotating | Reuse detection survives a restart |
| Cart | Redis hash, 24h TTL | No sticky sessions needed |
| Recently viewed / trending | Redis sorted sets | Hot counters stay off the write path |
| Cached reads | Redis, with a DB fallback | Any instance benefits from any instance's fill |

No in-memory cache affects correctness and no local disk is on the request path. Kill any instance
mid-traffic and you lose only its in-flight requests.

---

## The Critical Path: Checkout

This is the part worth reading carefully.

![Checkout sequence: idempotency, payment and the single transaction](diagrams/readme-03-checkout.svg)

<sub>Source: [`readme-03-checkout.excalidraw`](diagrams/readme-03-checkout.excalidraw) — open it at excalidraw.com to edit.</sub>

Three things make this safe:

**1. Availability is not a boolean column.** It's derived from `RoomNightInventory`, primary key
`(RoomId, StayDate)` — one row per sold night. The primary key *is* the double-booking guard. I
don't check-then-act; I just insert, and the database refuses the loser. Correctness doesn't depend
on my code getting an ordering right.

**2. Inventory rows go in sorted by `(RoomId, StayDate)`.** Two overlapping multi-room checkouts
take their row locks in the same order, so one blocks instead of both deadlocking.

**3. Nothing slow runs inside the transaction.** Payment authorizes before and captures after,
because a competing checkout for the same night blocks for exactly as long as that transaction
stays open.

Unique violations are translated **by constraint name**:

| Constraint | Error | HTTP |
| --- | --- | --- |
| `PK_RoomNightInventory` | `Booking.RoomUnavailable` | 409 |
| `PK_IdempotencyRecords` | `Idempotency.RequestInProgress` | 409 |

Both are 409, never a 500, and **never retried**. A sold room stays sold no matter how many times
you ask.

---

## Database Design

![Database tables and their relationships](diagrams/readme-04-database.svg)

<sub>Source: [`readme-04-database.excalidraw`](diagrams/readme-04-database.excalidraw) — open it at excalidraw.com to edit.</sub>

Six composite primary keys do real work here, and none of them are accidental:

| Key | What it prevents |
| --- | --- |
| `PK_RoomNightInventory (RoomId, StayDate)` | Two guests buying the same night |
| `PK_DealNights (RoomId, StayDate)` | Two deals discounting the same night |
| `PK_IdempotencyRecords (UserId, Key)` | A double-submitted checkout becoming two bookings |
| `PK_BookingLines (BookingId, LineNumber)` | A line existing without its booking |
| `PK_HotelImages (HotelId, Position)` | Two images claiming the same gallery slot |
| `PK_HotelAmenities (HotelId, AmenityId)` | A hotel listing the same amenity twice |

**Soft delete** is `IsDeleted` + a global query filter + **filtered indexes**. The filter means I
can't forget it; the filtered index means the index only carries rows anyone can actually see.

**Optimistic concurrency** on Cities, Hotels, Rooms, Deals and RefreshTokens is `rowversion`,
mapped as an EF **shadow property** — the domain entity never grows a `byte[]` field to satisfy the
database. A stale admin edit gets a 409 explaining what happened.

---

## Indexes

Every query added in this project shipped with the index that serves it. 22 indexes, 12 of them
filtered.

| Index | Columns | Filter | Serves |
| --- | --- | --- | --- |
| `IX_Rooms_HotelId_Capacity` | HotelId, AdultCapacity, ChildrenCapacity | not deleted | Search: rooms that fit the party *(includes BasePrice, Type)* |
| `IX_Rooms_HotelId_BasePrice` | HotelId, BasePrice | not deleted | "From" price per hotel card *(covering)* |
| `IX_Rooms_Type_BasePrice` | Type, BasePrice | not deleted | Room-type + price-range filters |
| `IX_Rooms_HotelId_Number` | HotelId, Number — **unique** | not deleted | No two rooms share a number in a hotel |
| `IX_Hotels_CityId_StarRating` | CityId, StarRating | not deleted | Search by city + stars *(includes Name, Thumbnail)* |
| `IX_Hotels_CityId_Name` | CityId, Name — **unique** | not deleted | No duplicate hotel name in a city |
| `IX_Hotels_Name` | Name | not deleted | Name sort and lookup |
| `IX_Cities_Country_Name` | Country, Name — **unique** | not deleted | No duplicate city per country |
| `IX_Cities_Name` | Name | not deleted | Admin city grid |
| `IX_Deals_Live` | StartsOn, EndsOn | featured + not deleted | Home page featured deals *(includes RoomId, HotelId)* |
| `IX_Deals_RoomId_StartsOn_EndsOn` | RoomId, StartsOn, EndsOn | not deleted | Overlap check when a deal is written |
| `IX_Bookings_ConfirmationNumber` | ConfirmationNumber — **unique** | — | Confirmation lookup |
| `IX_Users_Email` | Email — **unique** | not deleted | Login, and one account per address |
| `IX_RefreshTokens_TokenHash` | TokenHash — **unique** | — | Refresh lookup by hash |
| `IX_RefreshTokens_FamilyId` | FamilyId | not revoked | Revoking a family on reuse |
| `IX_OutboxMessages_Unprocessed` | OccurredOnUtc | unprocessed only | The dispatcher's claim *(covering)* |

The rest are foreign-key indexes EF creates for the joins.

The outbox index is my favourite one. It's filtered on `ProcessedOnUtc IS NULL` and includes every
column the claim reads, so the dispatcher's query touches only the handful of rows still waiting —
even once the table holds a million processed messages.

### Availability, as a query

Search excludes rooms already sold for the requested nights by checking the same table that
guards checkout (`HotelQueries.MatchingRooms`, `RoomQueries.Matching`):

```csharp
rooms.Where(room => !context.RoomNightInventory.Any(night =>
    night.RoomId == room.Id && night.StayDate >= checkIn && night.StayDate < checkOut));
```

EF translates this to a `NOT EXISTS` anti-join, so each room costs one seek on
`PK_RoomNightInventory (RoomId, StayDate)`.

One source of truth for availability, used by both the read path and the write path. They can't
disagree.

### Pagination

Offset paging: `page` starts at 1, `pageSize` is capped at **50**. Every sort breaks ties on the
id — without that, rows drift between pages and a guest sees the same hotel twice.

---

## Redis

Redis is a **cache, not a source of truth**. Every cached read has a database fallback, and the app
keeps serving if Redis is down. The cart is the one exception, and it's a deliberate one.

| Key | Type | TTL | Purpose | If Redis dies |
| --- | --- | --- | --- | --- |
| `hotel:{id}:details` | string | 5 min | Hotel detail page | Falls back to SQL |
| `hotels:search:{sha256}` | string | 90 s | Search results, keyed by a hash of every filter | Falls back to SQL |
| `deals:featured:{limit}` | string | 10 min | Home page deals | Falls back to SQL |
| `cart:{userId}` | Redis hash — a field → value map, one entry per cart item | 24 h | The guest's cart | **Cart endpoints return 503 `Cart.Unavailable`** until Redis is back |
| `auth:revoked-session:{id}` | string | token's remaining life | Logout denylist | Logged-out tokens work until they expire |
| `user:{id}:recent` | sorted set | 30 d | Last 5 hotels viewed | `GET /viewed-hotels` returns an empty list |
| `trending:cities:{date}` | sorted set | 8 d | One bucket per day | `GET /cities?sort=trending` returns 503 `City.TrendingUnavailable`; sorting by name still works |
| `trending:window:{date}` | sorted set | 60 s | 7 days unioned into a ranking | Same as above |

**Why the search key is a hash.** A search has twelve inputs — city, dates, guests, price bounds,
stars, room type, sort, page, page size. They are first written as one canonical string, in a fixed
order with the stars sorted, so the same search from two guests always produces the same string
and hits the same entry. That string is then SHA-256'd so every key is a fixed 64 characters,
however many filters are set, instead of a long key made from whatever the guest typed.

**Trending is a rolling window, not a counter.** A single `trending:cities` counter would be a
hall of fame — whatever was popular in March stays on top in December. Instead, each day gets its
own sorted set, and the ranking is the union of the last 7. Yesterday's spike ages out on its own,
with no cleanup job.

**The cart's honest cost.** It lives only in Redis, so a Redis flush empties every cart. I took
that trade because a cart is a convenience, not a commitment — nothing is paid for and nothing is
held. A SQL cart would mean a write on every "add to cart" and a cleanup job for carts nobody ever
checks out.

Every Redis failure is caught, counted on a metric, and degraded past. A cache that can take the
site down with it is worse than no cache.

---

## Design Decisions & Tradeoffs

### 1. There Is No Message Broker

**The challenge:** Checkout has to send a confirmation email. The email must not be lost if the
process dies, and must not be sent if the booking rolls back.

**The solution:** The outbox row is written **inside the checkout transaction**. A background
dispatcher claims rows with one atomic statement and hands them to a handler.

```sql
UPDATE TOP (@batchSize) OutboxMessages WITH (READPAST, UPDLOCK, ROWLOCK)
SET ProcessingAtUtc = SYSUTCDATETIME(), Attempts = Attempts + 1
OUTPUT inserted.Id, inserted.Type, inserted.Content, inserted.TraceParent
WHERE ProcessedOnUtc IS NULL AND Attempts < @maxAttempts
  AND (ProcessingAtUtc IS NULL OR ProcessingAtUtc < DATEADD(second, -@lease, SYSUTCDATETIME()))
```

RabbitMQ was in the plan. It was **built** — topology, publisher confirms, manual ack, a dedupe
table, a dead-letter queue — and then removed, because I couldn't answer one question: what does it
do that this table doesn't already do?

`ProcessingAtUtc` is the lease. `Attempts` against `MaxAttempts` is the dead-letter state.
`READPAST` is what makes N instances safe. The broker would have re-implemented all three in a
second system that can also be down.

**What I gained:**
- One delivery mechanism, one failure mode, one thing to operate
- The email can't be sent for a booking that rolled back — same transaction
- N API instances drain the queue safely with no coordination

**What I traded:**
- **Email volume and request volume scale together.** They're the same process
- Polling latency: up to 5 seconds before a message is picked up
- No fan-out to other systems without writing that myself

The isolation a broker sells is kept in the code anyway: a handler implements
`IOutboxMessageHandler` and never learns how its message arrived. Moving one to its own process
changes `Program.cs`, not the handler.

---

### 2. Emails Are At-Least-Once, Not Exactly-Once

**The challenge:** SMTP isn't part of the database transaction. "Sent" and "recorded as sent" can't
be made atomic.

**The solution:** Accept it. If the process dies after SMTP accepted the message but before the row
was marked processed, the lease expires, the row is claimed again, and the guest gets a second copy.

**What I gained:**
- No lost confirmations, ever — the failure mode is always "one too many", never "none"
- No distributed-transaction machinery, no two-phase commit, no provider-side protocol

**What I traded:**
- A rare duplicate email

That's the right way round. **A duplicate confirmation is mildly annoying; a missing one is a
support ticket and a guest at a front desk with no reservation.**

---

### 3. Expected Failures Are Values, Not Exceptions

**The challenge:** "Room unavailable" isn't a bug. Neither is "that city doesn't exist". Throwing
for outcomes the domain predicts means unwinding the stack for normal business flow and hoping
nothing catches it as a 500.

**The solution:** Every fallible operation returns `Result` / `Result<T>` with a typed `Error`
(`NotFound`, `Conflict`, `Validation`, `Forbidden`, `Failure`). Exceptions are for bugs and
infrastructure faults only. One mapping function turns an `Error` into a problem document.

**What I gained:**
- The signature tells you it can fail; you can't forget to handle it
- Every error reaches the client with the right status and a stable `errorCode`
- No `try/catch` as control flow

**What I traded:**
- More ceremony than `throw` — results have to be checked and propagated by hand

---

### 4. Refresh-Token Reuse Revokes the Whole Family

**The challenge:** A rotating refresh token that gets stolen is indistinguishable from a legitimate
one — until the real user tries to use the token the thief already spent.

**The solution:** Tokens are hashed at rest, rotate on every use, and carry a family id. Presenting
an already-revoked token means the token leaked, so the **entire family** is revoked.

**What I gained:**
- A stolen token buys the attacker one refresh before the family dies
- Detection is strict: **no grace window**, because a grace window is exactly the gap an attacker
  races into
- The raw token is never stored, so a database dump doesn't hand over sessions

**What I traded:**
- **The client must refresh single-flight.** Two parallel refreshes look exactly like a replay, and
  the second one revokes the family. That's a real constraint pushed onto callers

---

### 5. Cancellation Deletes Inventory, Immediately

**The challenge:** When a guest cancels, the nights have to go back on sale. If that happens
asynchronously, the room sits unsellable for as long as the queue is behind.

**The solution:** The cancellation transaction deletes the `RoomNightInventory` rows itself.

**What I gained:**
- The nights are purchasable again the instant the response returns
- One transaction: the booking is cancelled and the rooms are released, or neither happened

**What I traded:**
- Cancellation costs a few more row deletes inside the transaction
- Only bookings inside the **48-hour** window can be cancelled; past that it's a business decision,
  not a technical one

---

## Getting Started

### Prerequisites

Docker and Docker Compose. Nothing else — no SQL Server, Redis, SMTP or .NET SDK on the host.

### Quick Start

```bash
# 1. Clone
git clone <repository-url>
cd Travel-and-Accommodation-Booking-Platform

# 2. Set the two secrets
cp .env.example .env
echo "JWT_SIGNING_KEY=$(openssl rand -base64 48)" >> .env

# 3. Start everything
docker compose up -d --build
```

That's it. The API applies its own migrations at startup, seeds a demo catalogue (6 cities, 20
hotels, 80 rooms, 5 deals), and reports healthy.

```bash
docker compose ps                                        # wait for hotelbooking-api to be healthy
curl -s http://localhost:8080/health/ready               # Healthy
```

### Try It

```bash
curl -X POST http://localhost:8080/api/users \
  -H 'Content-Type: application/json' \
  -d '{"email":"you@example.com","password":"Str0ng!Passw0rd","firstName":"Abdalkreem","lastName":"Bzoor"}'

TOKEN=$(curl -s -X POST http://localhost:8080/api/sessions \
  -H 'Content-Type: application/json' \
  -d '{"email":"you@example.com","password":"Str0ng!Passw0rd"}' | jq -r .accessToken)

curl -s "http://localhost:8080/api/hotels?page=1&pageSize=5" | jq
curl -s http://localhost:8080/api/cart -H "Authorization: Bearer $TOKEN" | jq
```

### Where Everything Is

| Service | URL | Notes |
| --- | --- | --- |
| **API** | http://localhost:8080/api | All routes are under `/api` |
| **Scalar API reference** | http://localhost:8080/scalar | Development only |
| **OpenAPI document** | http://localhost:8080/openapi/v1.json | Development only |
| **Mailpit** | http://localhost:8025 | Confirmation emails land here |
| **Jaeger** | http://localhost:16686 | Traces |
| **Seq** | http://localhost:5341 | Logs |
| **Prometheus** | http://localhost:9090 | Metrics |
| SQL Server | `localhost,1433` | user `sa` |
| Redis | `localhost:6379` | |

### Environment Configuration

| Variable | Purpose | Example |
| --- | --- | --- |
| `MSSQL_SA_PASSWORD` | SQL Server SA password | A strong password |
| `JWT_SIGNING_KEY` | HS256 signing key, **32+ bytes** | `openssl rand -base64 48` |
| `ConnectionStrings__Database` | SQL Server connection | Set by Compose |
| `ConnectionStrings__Redis` | Redis connection | `redis:6379` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Where telemetry goes | `http://otel-collector:4317` |
| `Email__Smtp__Host` | SMTP host | `mailpit` |

Secrets are never in `appsettings.json` — the committed file has empty placeholders so a missing
secret **fails loudly at startup** instead of quietly falling back to something insecure.

### Running Without Docker

```bash
docker compose up -d sqlserver redis mailpit     # dependencies only

cd src/HotelBooking.Api
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;Database=HotelBooking;User Id=sa;Password=<yours>;TrustServerCertificate=True"
dotnet user-secrets set "Authentication:Jwt:SigningKey" "$(openssl rand -base64 48)"

dotnet run                                        # migrates and seeds on start
```

---

## API Reference

**33 routes**, one endpoint class per use case, all under `/api`.

Authorization is **deny-by-default**: a fallback policy requires an authenticated user, and each
public endpoint opts out explicitly. 12 routes are anonymous, 13 are admin-only, 8 need a signed-in
guest.

### Users & Sessions

| Method | Route | Auth | Purpose |
| --- | --- | --- | --- |
| `POST` | `/users` | anonymous | Register |
| `POST` | `/sessions` | anonymous | Log in — returns access + refresh token |
| `PUT` | `/sessions/current` | anonymous | Rotate the refresh token |
| `DELETE` | `/sessions/current` | guest | Log out — denylists the session |

### Discovery

| Method | Route | Auth | Purpose |
| --- | --- | --- | --- |
| `GET` | `/hotels` | anonymous | **Search** — city, dates, guests, price, stars, type, sort, page |
| `GET` | `/hotels/{id}` | anonymous | Hotel details (counts towards trending) |
| `GET` | `/hotels/{id}/rooms` | anonymous | Rooms, with availability if a stay is given |
| `GET` | `/hotels/{id}/images` | anonymous | Gallery |
| `GET` | `/rooms/{id}` | anonymous | Room details |
| `GET` | `/cities` / `/cities/{id}` | anonymous | Cities |
| `GET` | `/deals` | anonymous | Featured deals |
| `GET` | `/amenities` | anonymous | Amenity catalogue |
| `GET` | `/viewed-hotels` | guest | Last 5 hotels this guest viewed |

### Cart & Booking

| Method | Route | Auth | Purpose |
| --- | --- | --- | --- |
| `GET` | `/cart` | guest | Read the cart |
| `POST` | `/cart/items` | guest | Add a stay |
| `DELETE` | `/cart/items/{itemId}` | guest | Remove one item |
| `DELETE` | `/cart` | guest | Empty the cart |
| `POST` | `/bookings` | guest | **Checkout** — requires `Idempotency-Key` |
| `POST` | `/bookings/{id}/cancellation` | guest | Cancel, releasing the nights |

### Admin

`POST` / `PUT` / `DELETE` on `/cities`, `/hotels`, `/rooms`, `/deals`, plus
`POST /hotels/{id}/rooms` and `GET /deals/{id}`. All admin-only, all `rowversion`-guarded.

### Errors

Every non-2xx is an RFC 9457 problem document, produced by exactly one mapping function:

```json
{
  "title": "Room unavailable",
  "status": 409,
  "detail": "One or more of the nights requested has already been booked.",
  "instance": "/api/bookings",
  "errorCode": "Booking.RoomUnavailable",
  "traceId": "00-6b85bea42af8c1c3919d28491e56cca7-f4e680198ecdb845-01"
}
```

| `errorCode` prefix | Meaning | Status |
| --- | --- | --- |
| `*.NotFound` | No such record | 404 |
| `Booking.RoomUnavailable` | Those nights are sold | 409 |
| `Idempotency.RequestInProgress` | Same key already in flight | 409 |
| `Persistence.ConcurrencyConflict` | `rowversion` mismatch — someone else edited it | 409 |
| `Booking.CancellationWindowClosed` | Past the 48h window | 409 |
| `Concurrency.VersionRequired` | Admin edit sent without `If-Match` | 428 |
| `Auth.*` | Credentials, tokens, sessions | 401 |
| `Request.TooManyRequests` | Rate limited (`Retry-After` set) | 429 |

---

## Security

| Concern | How |
| --- | --- |
| **Passwords** | ASP.NET Core Identity hasher. Never logged |
| **Access token** | 15 minutes, carries `sub`, `jti`, `role`, `email`, `sid` |
| **Refresh token** | 7 days, rotating, SHA-256 stored, family reuse detection |
| **Logout** | Session id on a Redis denylist, TTL = the token's remaining life |
| **RBAC** | Policies (`AdminOnly`, `AuthenticatedUser`) |
| **Ownership** | "A guest reads only their own bookings" is checked in the service, not the endpoint |
| **Deny by default** | Fallback policy requires auth; public routes opt out one by one |
| **Rate limits** | Auth: 10 / 15 min. Global: 300 / min. By user when signed in, by IP otherwise |
| **Request timeout** | 30 seconds, everywhere |

**The denylist keys the session, not the token.** Denylisting each `jti` means a logout only kills
the token you happened to be holding. Keying the session kills every token minted from that login
in one write.

Rate limits are counted **per instance**.

---

## Reliability & Fault Tolerance

| Scenario | What happens |
| --- | --- |
| **Two guests buy the same night** | The `(RoomId, StayDate)` PK refuses one. 409, never a 500 |
| **Guest double-clicks checkout** | Idempotency PK catches it. Replay returns the original booking |
| **Process dies mid-checkout** | Transaction rolls back. No booking, no inventory, no email |
| **Process dies after commit** | Outbox row survives. The dispatcher sends the email on restart |
| **Handler throws** | Lease expires, row is re-claimed, retried up to 5 times |
| **Handler fails 5 times** | Abandoned and counted on `outbox.abandoned` |
| **Payment capture fails** | The booking is voided and its nights released; the hold is given back |
| **Redis is down** | Cached reads fall back to SQL. Carts are unavailable. Site stays up |
| **SMTP is down** | Emails retry on the lease. The booking is unaffected |
| **Two admins edit one hotel** | `rowversion` mismatch → 409 explaining the stale write |

### Outbox Settings

| Setting | Value | Meaning |
| --- | --- | --- |
| Polling interval | 5 s | How often the dispatcher looks |
| Batch size | 20 | Rows claimed per cycle |
| Claim lease | 5 min | How long a claim holds before another instance may retry it |
| Max attempts | 5 | Then abandoned |

Handlers are **at-least-once** and signal failure by throwing. Background services live in
Infrastructure and are registered by an explicit opt-in call (`AddOutboxDispatcher`) — that call is
the seam. A handler that outgrows this host moves to its own host without changing a line of the
handler.

---

## Observability

The app emits **OTLP and nothing else** — no Prometheus client library, no Jaeger SDK, no Seq sink.
Which backends sit behind the collector is a Compose concern, not a code one.

![OTLP from the API and dispatcher through the collector to Jaeger, Seq and Prometheus](diagrams/readme-05-observability.svg)

<sub>Source: [`readme-05-observability.excalidraw`](diagrams/readme-05-observability.excalidraw) — open it at excalidraw.com to edit.</sub>

### One Trace, Three Tools

Make a request, take the `@tr` field from the log line, and the same id finds it everywhere:

- **Jaeger** → `http://localhost:16686/trace/<traceId>`
- **Seq** → filter `@TraceId = '<traceId>'`

Traces cover ASP.NET Core, HttpClient, **SqlClient** and **Redis**, so one trace shows the HTTP
request, every query it ran and every cache call it made.

**The email joins the trace too.** The outbox row stores the `traceparent` of the checkout that
wrote it. When the dispatcher picks it up seconds later, the email span attaches to the original
checkout — one trace from "guest pressed book" to "mail server accepted".

### Metrics

Business metrics, not just RED:

| Metric | Why it matters |
| --- | --- |
| `bookings.created` / `bookings.duration` | The money path |
| `bookings.conflicts` | A rising conflict rate is a **business** signal — inventory is too thin |
| `idempotency.replays` / `idempotency.in_progress` | Clients double-submitting |
| `bookings.void_failed` | **Any value above zero is rooms held for a guest with no booking** |
| `payments.holds.not_released` | **Any value above zero is a guest's money held wrongly** |
| `outbox.pending` / `outbox.abandoned` | Queue depth, and promises not kept |
| `cache.hits` / `cache.misses` / `cache.unavailable` | Tagged by key prefix |
| `auth.refresh.reused` | Token replay detected |
| `deals.unpriceable` | Bad catalogue data reaching the home page |
| `http.ratelimit.rejected` | Who's being throttled, by endpoint |

The two in bold are the ones I'd page on. They mean a guest has been wronged in a way they can
see and I can't.

### Logs & Health

Structured JSON via Serilog, correlation id on every line, with `ClientIp` pushed onto the context.
Tokens, passwords and full email addresses are never logged.

| Probe | Checks |
| --- | --- |
| `/health/live` | The process is up |
| `/health/ready` | SQL Server and Redis are reachable |

Health endpoints are excluded from tracing — a probe every 10 seconds would drown the real traffic.

---

## Testing

105 tests across four layers. Each layer proves something the others can't.

| Suite | Tests | Proves |
| --- | --- | --- |
| **Domain unit** | 44 | Invariants and value objects. No mocks — pure functions in, `Result` out |
| **Application unit** | 16 | Orchestration: success, not-found, forbidden, conflict, validation |
| **Integration** | 33 | Real SQL Server + Redis via Testcontainers, over the real HTTP route |
| **Architecture** | 12 | The dependency rule, enforced by NetArchTest at build time |

The test I care about most fires **50 concurrent checkouts for the same room on the same nights**
and asserts exactly one 201, forty-nine 409s, no 5xx, that every loser got `Booking.RoomUnavailable`
specifically, and that the ledger ended up with exactly one row per night and one booking. It runs
**five times**, because a race that passes once has proved nothing. It's the only test that can
actually falsify the central claim of this project.

Others in that set: the reverse-order multi-room checkout (deadlock ordering), concurrent
idempotency keys, refresh-token rotation under a race, and a checkout traced end-to-end from the
HTTP request to the email it sends.

---

## CI/CD

### CI — every push to `main`/`develop`, every PR to `main`

| Step | What it does |
| --- | --- |
| **Restore** | With a NuGet cache keyed on `Directory.Packages.props` |
| **Format** | `dotnet format --verify-no-changes` |
| **Build** | Release, `-warnaserror` — zero warnings or it fails |
| **Unit tests** | Domain, Application, Architecture |
| **Integration tests** | Testcontainers against the runner's Docker daemon — real SQL Server and Redis |
| **Image** | Builds the API image to prove the Dockerfile still works |

### CD — after CI goes green on `main`

Builds the image and pushes it to **GitHub Container Registry**, tagged `latest` and the commit SHA:

```
ghcr.io/<owner>/hotelbooking-api:latest
ghcr.io/<owner>/hotelbooking-api:<sha>
```

---

## Project Structure

```
src/
├── HotelBooking.Domain/           # depends on nothing
│   ├── Bookings/                  # Booking, BookingLine, RoomNight, pricing, IBookingRepository
│   ├── Hotels/  Rooms/  Cities/  Deals/  Carts/  Users/  RefreshTokens/
│   ├── Common/                    # Money, DateRange, Email, StarRating, Occupancy, GeoLocation
│   └── Results/                   # Result, Error, ErrorType
│
├── HotelBooking.Application/      # depends on Domain
│   ├── Bookings/                  # BookingService, BookingCancellationService, Dtos/, Validators/
│   ├── Hotels/  Cities/  Deals/  Carts/  Rooms/  Visits/  Authentication/
│   ├── Abstractions/              # ICacheService, IEmailSender, IDateTimeProvider, ...
│   └── Telemetry.cs               # every metric, in one place
│
├── HotelBooking.Infrastructure/   # depends on Application + Domain
│   ├── Persistence/               # DbContext, Configurations/, Repositories/, Queries/, Migrations/
│   ├── Caching/                   # RedisCacheService, RedisCartRepository, RedisVisitStore
│   ├── Outbox/                    # dispatcher, store, handlers, the claim SQL
│   ├── Authentication/            # JWT, refresh tokens, denylist, password hashing
│   └── Notifications/             # SMTP sender, email templates
│
└── HotelBooking.Api/              # depends on all, to compose only
    ├── Endpoints/                 # one class per use case, scanned and mapped
    ├── Errors/                    # the single Error → ProblemDetails mapping
    ├── Authentication/  Authorization/  RateLimiting/  Observability/
    ├── Dockerfile
    └── Program.cs

tests/
├── HotelBooking.Domain.UnitTests/
├── HotelBooking.Application.UnitTests/
├── HotelBooking.Api.IntegrationTests/     # Testcontainers: SQL Server + Redis
└── HotelBooking.Architecture.Tests/       # NetArchTest

.github/workflows/    ci.yml, cd.yml
observability/        collector and Prometheus config
docker-compose.yml    API + SQL Server + Redis + Mailpit + the telemetry pipeline
```

---

## What I Didn't Build

- **A real payment gateway.**
- **Grafana dashboards.**
- **Flushing trending counters to SQL.**
