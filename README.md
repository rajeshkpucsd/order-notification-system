# Order + Notification System

I built this as a small event-driven demo with two .NET services:

- `OrderService`: creates orders and publishes an event to RabbitMQ, then listens for email-sent updates
- `NotificationService`: consumes order-created events, stores a notification, and publishes email-sent

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
- Make sure Docker is running on your machine

### for migration (if needed):
- Add-Migration InitialCreate
- Update-Database

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

### Order status update on email

`OrderService` updates `Order.Status` to `EMAIL_SENT` when it receives the email-sent event.

### RabbitMQ startup retry

Both services retry RabbitMQ connection at startup using config:

- `RabbitMq:StartupRetryCount` (default in repo: `5`)
- `RabbitMq:StartupRetryDelaySeconds` (default in repo: `5`)

If retries are exhausted, startup fails.

### DB migration behavior

Both services run EF migrations on startup.

If migration fails, startup logs the error and throws. Service will not continue in a broken DB state.

### Idempotency in NotificationService

`NotificationService` stores notifications with a unique index on `EventId`.

- It checks if event already exists before insert.
- It also handles duplicate-key DB exceptions and `Ack`s those messages (instead of requeue), to avoid duplicate-message retry loops.


## Messaging topology

- Exchange: `orders.exchange` (topic)
- Routing keys: `created`, `email-sent`
- Queues:
  - `notification-service` (bound to `created`)
  - `order-service` (bound to `email-sent`)

## Notes / limitations

- I have not added Outbox pattern yet.
- So if order is saved but event publish fails, delivery is not guaranteed.
- Could have added more test coverage, especially for failure scenarios such as network being down. Request parameters validations etc.
- For now the status of orders are updated through Rabbit MQ but I feel we could have done better by webhooks. 
