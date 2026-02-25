# Order + Notification System

This is a small event-driven demo with two .NET services:

- `OrderService`: creates orders and publishes an event to RabbitMQ
- `NotificationService`: consumes that event and stores a notification

No direct HTTP call happens between services. Communication is async through RabbitMQ.

## Stack

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core + SQL Server
- RabbitMQ
- Docker Compose

## Services and ports

When running with Docker Compose:

- Order Swagger: `http://localhost:5001/swagger`
- Notification Swagger: `http://localhost:5002/swagger`
- RabbitMQ UI: `http://localhost:15672` (`guest` / `guest`)

Port mapping from `docker-compose.yml`:

- Order: container `8080` -> host `5001`
- Notification: container `8081` -> host `5002`

When running locally (non-Docker), launch profiles are:

- Order: `http://localhost:5001`
- Notification: `http://localhost:5002`

## How to run (Docker)

From repo root:

```bash
docker-compose up --build
```

First startup can take a little longer because SQL Server needs time to initialize.

Stop:

```bash
docker-compose down
```

Delete volumes too:

```bash
docker-compose down -v
```

## How to run locally

Prereqs:

- .NET 8 SDK
- SQL Server LocalDB (or any reachable SQL Server)
- RabbitMQ running locally on `5672`

Run in two terminals:

```bash
dotnet run --project OrderService --launch-profile http
```

```bash
dotnet run --project NotificationService --launch-profile NotificationService
```

## Current runtime behavior

### Order creation API behavior

`POST /api/orders`

- If order save + publish succeed: returns `200 OK`
- If order save succeeds but publish fails: returns `202 Accepted`
  - Message: `Order saved, but event publish failed. Notification may be delayed.`

### RabbitMQ startup retry

Both services retry RabbitMQ connection at startup using config:

- `RabbitMq:StartupRetryCount` (default in repo: `5`)
- `RabbitMq:StartupRetryDelaySeconds` (default in repo: `5`)

If retries are exhausted, startup fails.

### DB migration behavior

Both services run EF migrations on startup.

If migration fails, startup logs the error and throws (fail-fast). Service will not continue in a broken DB state.

### Idempotency in NotificationService

`NotificationService` stores notifications with a unique index on `EventId`.

- It checks if event already exists before insert.
- It also handles duplicate-key DB exceptions and `Ack`s those messages (instead of requeue), to avoid duplicate-message retry loops.

## Event shape

`OrderService` publishes `OrderCreatedEvent`:

```json
{
  "eventId": "guid",
  "orderId": "guid",
  "email": "customer@example.com",
  "productCode": "PRD-1001",
  "quantity": 2,
  "createdAt": "2026-02-25T10:15:30Z"
}
```

## Notes / limitations

- This demo does not implement Outbox pattern yet.
- So if order is saved but event publish fails, event delivery is not guaranteed.
- For production, add outbox + background publisher.
