# Order + Notification System

I built this as a small event-driven demo with two .NET services:

- `OrderService`: creates orders and publishes an event to RabbitMQ
- `NotificationService`: consumes that event and stores a notification

They communicate asynchronously through RabbitMQ.

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


## How to run (Docker)

- Get clone https://github.com/rajeshkpucsd/order-notification-system
- cd into the repo directory

From repo:

```bash
docker-compose up --build
```
Stop:

```bash
docker-compose down
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
  "email": "customer@test.com",
  "productCode": "P1",
  "quantity": 2,
  "createdAt": "2026-02-25T10:15:30Z"
}
```

## Notes / limitations

- I have not added Outbox pattern yet.
- So if order is saved but event publish fails, delivery is not guaranteed.
- For production, I would add outbox + background publisher and add more test cases.
