# BasketMicroservice

## Why this exists

The repo already has a generic messaging library (`Messaging/`) used by
`CatalogMicroservice` and wired into several places in the monolith
(`OrderService`, `PublicApi` endpoints, feature toggles, etc.). It's flexible,
but there's a lot of indirection to explain in an exam setting: DI extension
methods, `IMessageSender`/`IMessageRequester` abstractions, dead-letter
exchanges, generic handler registration, etc.

This project is a **second, deliberately simpler microservice**, built to
demonstrate the same core idea (a service that talks over RabbitMQ instead of
being called in-process) in a way that's easier to read top-to-bottom and
easier to explain out loud. It is not meant to replace the generic messaging
library — it's a simpler reference example to show alongside it.

The style is based on earlier attempts at a simpler Catalog
messaging setup, using the AMQP 1.0
`RabbitMQ.AMQP.Client` package directly, with no shared abstraction layer.
Two messaging patterns are used, picked by what each operation actually
needs — not for variety's sake:

- **Fire-and-forget commands** (`DeleteBasket`, `TransferBasket`): nobody
  waits on these (checked against the actual `Web` call sites — their return
  values are never used). Each command type gets its own queue
  (`basket-delete-queue`, `basket-transfer-queue`), routed there by a direct
  exchange (`basket-commands-exchange`) with a binding key matching the
  command name. `BasketQueue` publishes with that routing key baked into the
  message's address (`IMessage.ToAddress().Exchange(...).Key(...)`); the
  broker — not app code — decides which queue the message lands in.
- **Request/reply "queries"** (`GetBasket`, `AddItem`, `SetQuantities`): one
  shared queue (`basket-queries`), using the client library's built-in
  requester/responder so the sender gets a reply back. `AddItem` and
  `SetQuantities` live here even though they write to the basket, because
  the `Web` pages that call them (`Index.cshtml.cs`, `Checkout.cshtml.cs`)
  need the updated basket back in the same call to render the page — a true
  fire-and-forget command can't give them that. Dispatch between the three
  is a plain `switch (request.Subject())` in `BasketMessageWorker`, because
  splitting a request/reply queue by exchange/routing key wouldn't add
  anything: the requester/responder pairing already routes each reply back
  to the one caller waiting on it.

**Note on the exchange:** the delete/transfer split into separate queues only
pays for itself once something other than `BasketMessageWorker` needs to
consume `DeleteBasket` or `TransferBasket` independently (e.g. a second
service reacting to basket deletions). Right now there's exactly one
consumer for both queues, so a single shared queue with a `switch` on
`Subject` would behave identically. It's implemented this way anyway
specifically to demonstrate exchanges/bindings/routing keys for the exam —
worth remembering later that this is currently over-engineered relative to
actual need, in case anyone asks "why two queues for one consumer?".

## Why Basket, not Order

Both were considered. `Basket` is a self-contained aggregate (`Basket`/
`BasketItem`) with no foreign reads into other aggregates — `BasketService`
only touches `IRepository<Basket>`. `Order`, by contrast, already reads from
`Basket` *and* `CatalogItem` just to construct itself, and is already wired
into the generic messaging library's `IMessageRequester` for catalog lookups.
Extracting Order cleanly would require either chaining RPC calls out to two
other services just to create one entity, or redesigning checkout to pass a
pre-built item snapshot — a real design change, not a simple extraction.
Basket has none of that baggage, so it's the cleaner demo.

## What's deliberately fixed vs. the reference branch

- Queue/exchange names and container id are defined once
  (`BasketMessagingConstants`), not hardcoded separately in every file.
- No dead code (no unused scaffolding classes left over from an abandoned
  attempt at using the generic library).
- The `Subject == "error"` / publish-outcome handling bug from the reference
  branch's `CatalogQueue` (wrong variable name in the rejected-outcome
  branch) is not present here.

## Broker host configuration (do not hardcode this again)

The first Docker run of this feature failed everywhere (`PublicApi`, `Web`,
and `BasketMicroservice` itself) with
`SocketException: Cannot assign requested address`. Root cause: the broker
address was hardcoded as `amqp://guest:guest@localhost:5672/%2f` in all
three classes (`BasketQueue`, `BasketRpcClient`, `BasketMessageWorker`).
Centralizing it into one constant (the previous bullet point) was not
enough — `localhost` inside a Docker container means *that container*, not
the `rabbitmq` container, so the connection was always doomed regardless of
how many places repeated the string.

Fix: the broker host/port/user/password now come from configuration instead
of a constant, reusing the **same `"Messaging"` config section** the
generic `Messaging` library already uses (which Docker already points at
`rabbitmq` — see `src/Web/appsettings.Docker.json` /
`src/PublicApi/appsettings.Docker.json`):

- `BasketQueue`/`BasketRpcClient` (in `src/ApplicationCore/BasketMessaging/`,
  used by `Web`/`PublicApi`) inject `IOptions<Messaging.MessagingOptions>` -
  the exact same options type/config section the generic library binds, so
  there's only one `"Messaging"` section to keep correct, not two.
- `BasketMessageWorker` (in this project, which deliberately doesn't
  reference `Messaging`/`ApplicationCore`) has its own equivalent
  `BasketBrokerOptions`, bound from this project's own `"Messaging"` section
  in `appsettings.Docker.json` (added here, pointing at `rabbitmq`). Same
  shape, same broker, just not the same compiled type, because of the
  standalone-project decision above.

Takeaway: when adding a new sender/receiver to this demo, never hardcode a
broker URI/host again, even behind a shared constant - bind it from config
so it can differ between local dev (`localhost`) and Docker (`rabbitmq`).

## Second Docker issue: startup race against RabbitMQ (fixed in `docker-compose.yml`)

After fixing the host above, the second `--build` run still failed - this
time `SocketException: Connection refused` instead of "cannot assign
requested address", meaning DNS resolved `rabbitmq` fine but nothing was
listening on port 5672 yet. Cause: `eshopwebmvc`/`eshoppublicapi` didn't
even list `rabbitmq` under `depends_on` (only `sqlserver`), and `depends_on`
only waits for a container to *start*, not for the service inside it to be
ready to accept connections - RabbitMQ takes a few seconds after its
container starts before it's actually accepting AMQP connections. Every
service here connects to RabbitMQ eagerly at startup (`BasketQueue`/
`BasketRpcClient.InitializeAsync()` right after `builder.Build()`, and
`BasketMessageWorker.ExecuteAsync()` immediately), so there was no
retry/backoff to ride out that window.

Fix, in `docker-compose.yml`: added a `healthcheck` to the `rabbitmq`
service (`rabbitmq-diagnostics -q ping`), and changed every service that
talks to RabbitMQ (`eshopwebmvc`, `eshoppublicapi`, `catalogmicroservice`,
`basketmicroservice`) to `depends_on: rabbitmq: condition: service_healthy`
instead of the plain list form. Compose now blocks starting those
containers until RabbitMQ's healthcheck passes, instead of racing it.

That alone still wasn't reliable in practice (all three connection
failures came back within ~4 seconds of each other on a retest, too fast
for the healthcheck to plausibly have been the gate - and `condition`-based
`depends_on` support varies by Compose tool/version). So the connection
itself is now also retried with backoff (`BasketBrokerRetry.ConnectAsync`,
duplicated once per project same as everything else here): up to 10
attempts, 2s apart, logging each failure instead of crashing the whole
process. This is the same reason the generic `Messaging` library's
`RabbitMqConnection` never hit this bug in the first place - it connects
*lazily* on first send instead of eagerly at startup, so by the time
anything actually tries to talk to RabbitMQ, it's long since finished
booting. `BasketQueue`/`BasketRpcClient`/`BasketMessageWorker` all connect
eagerly (simpler to read, worse for this specific race), so they needed
their own retry instead. Belt-and-suspenders: keep the compose healthcheck
(fixes the common case fast) **and** the retry (fixes it regardless).

## Third issue: toggled-on basket looked empty after Add to Basket

With the toggle on and the broker reachable, `AddItemToBasket` correctly
sent `AddItem` to `BasketMicroservice` and got a `BasketDto` back - but the
Basket page still showed "basket is empty" straight after. Cause: there
are **two separate basket-read paths** in `Web`, and only one of them was
ever routed through `IBasketService`:

- `IBasketService`'s own write methods (fixed earlier) - fine.
- `BasketViewModelService.GetOrCreateBasketForUser` (what actually renders
  the Basket page on load/after redirect) read straight from
  `IRepository<Basket>` - the SQL database - never calling `BasketService`
  at all. Same for the header basket-count badge
  (`IBasketQueryService.CountTotalBasketItems`, called via
  `BasketViewModelService.CountTotalBasketItems`). Toggling on
  `UseBasketMicroservice` made writes go to `BasketMicroservice`'s
  in-memory store while these two reads kept looking at the (now
  untouched) SQL database, so the page always saw an empty basket.

This was a gap in the original toggle wiring, not a separate feature to
scope-check - "all basket calls in the Web app" includes reading the
basket to render it. Fix:

- `IBasketService` gained `GetOrCreateBasketIfHasNoneForUser(buyerId)` and
  `CountTotalBasketItems(buyerId)`, each with the same on/off branch as the
  other four methods. The microservice query queue gained a matching
  `GetOrCreateBasket` case (reusing the existing `GetBasketQuery` message -
  same shape as the read-only `GetBasket`, just creates-if-missing to
  match what the SQL path already did via `BasketViewModelService`).
- `BasketViewModelService` no longer touches `IRepository<Basket>` or
  `IBasketQueryService` directly - it calls `IBasketService` for both, so
  the toggle actually covers every basket read in `Web`, not just writes.

## Fourth issue: checkout threw `ArgumentNullException: basket`

Same bug class again, in a third place: `OrderService.CreateOrderAsync`
also read the basket straight from `IRepository<Basket>` (keyed by
`basketId`, an `int`, not `buyerId`), completely bypassing `IBasketService`.
With the toggle on, the basket lives in `BasketMicroservice`'s in-memory
store, so this lookup always came back `null` and `Guard.Against.Null`
threw during checkout.

This is the same root cause as the "empty basket" issue above - just a
third, independent read path that was never routed through the toggle,
because checking "every basket call in the Web app" needed one more pass.
Fix: added a `GetBasketByIdQuery(int BasketId)` message (mirrored in both
projects, same convention as the rest) and a matching `GetBasketById` case
in `BasketMessageWorker`'s query switch. `OrderService` now branches on
`UseBasketMicroservice` and, when on, calls that via an injected
`BasketRpcClient`, then rehydrates a `Basket` entity with
`Basket.Rehydrate`/`BasketItem.Rehydrate` (same helpers `BasketService`
already uses) so the rest of `CreateOrderAsync` - guard clauses, item
mapping - didn't need to change at all.

**If something else still reads a basket straight from `IRepository<Basket>`
after this, same bug, same fix shape: find the read, add/reuse a query
message, branch on the toggle.**

## Fifth issue: eager connect crashed `FunctionalTests`/`PublicApiIntegrationTests`

Before trying to commit this, running `FunctionalTests` surfaced a real
regression: `BasketQueue`/`BasketRpcClient.InitializeAsync()` were called
eagerly in `Program.cs` right after `builder.Build()`. `WebApplicationFactory`
(what those test projects use to boot the app) runs that code for real, and
there's no RabbitMQ available in that environment - so the test host itself
crashed (not a test failure, the host wouldn't even start), taking down
every test in the project, not just basket-related ones.

This is exactly the bug the generic `Messaging` library's `RabbitMqConnection`
doesn't have, and the reason is the same thing already called out in
"Startup race against RabbitMQ" above: that class connects *lazily*, on
first actual send, instead of eagerly at startup - registering it in DI
never forces a connection. `BasketQueue`/`BasketRpcClient` now do the same
(lazy, double-checked-lock connect on first `SendAsync`/`RequestAsync`,
mirroring `RabbitMqConnection.GetConnectionAsync()`); the explicit
`InitializeAsync()` calls were removed from both `Program.cs` files. Since
`FunctionalTests`/`PublicApiIntegrationTests`/`IntegrationTests`/`UnitTests`
are protected (not to be edited for this feature), this had to be fixed in
the production code, not the tests - confirmed by rerunning all four
(`UnitTests` 44, `FunctionalTests` 12, `PublicApiIntegrationTests` 15,
`IntegrationTests` 3 - all passing, none of those files touched).

## What's intentionally left out (vs. `CatalogMicroservice`)

No Identity, no EF migrations (uses `UseInMemoryDatabase`), no Swagger, no
HTTP endpoints at all — this service only has two queues and a worker. It
now does have a `Dockerfile` and a `docker-compose` service entry, and it
is wired into `Web` and `PublicApi` via `BasketService` behind the
`FeatureToggles:UseBasketMicroservice` toggle (env var
`USE_BASKET_MICROSERVICE` in `.env`). `PublicApi` currently has no Basket
endpoints of its own, though, so the wiring there is inert until some
endpoint actually calls `IBasketService`.
