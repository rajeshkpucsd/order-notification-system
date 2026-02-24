# Order & Notification Microservices (Event-Driven Architecture)

This project demonstrates an event-driven microservices system built using .NET, RabbitMQ, SQL Server and Docker.

Two independent services communicate asynchronously through a message broker.

---

## What the system does

1. A user creates an order using the Order Service.
2. Order Service saves the order in its database
3. It publishes an `Order-Created` event to RabbitMQ
4. Notification Service consumes the event
5. Notification Service stores a notification (simulating sending an email)

No direct HTTP communication exists between services.

---

## Architecture

Client → Order Service → RabbitMQ → Notification Service → Notification Database

This follows an event-driven microservices architecture.

Each service:

* has its own database
* can run independently
* communicates only through events

---

## Technologies Used

* .NET 8 Web API
* Entity Framework Core
* RabbitMQ (message broker)
* SQL Server (separate DB per service)
* Docker & Docker Compose
* BackgroundService worker

---

# How to Run the Project

## Prerequisites

* Install Docker Desktop
* Make sure Docker is running

---

## Run the system

From the project root directory (the folder containing `docker-compose.yml`) run:

```
docker-compose up --build
```

Docker will start:

* order-service
* notification-service
* rabbitmq
* sqlserver

Important:
On the first run, SQL Server initialization takes ~30–40 seconds.
Swagger pages may not open immediately — please wait before testing.

---

## Stop the system

```
docker-compose down
```

To also delete databases:

```
docker-compose down -v
```

---

# Accessing the Services

| Service                      | URL                           |
| ---------------------------- | ----------------------------- |
| Order Service Swagger        | http://localhost:5001/swagger |
| Notification Service Swagger | http://localhost:5002/swagger |
| RabbitMQ Dashboard           | http://localhost:15672        |

### RabbitMQ Credentials

Username: `guest`
Password: `guest`

You can open Queues → order-created to observe message processing.

---

## Why ports 5001 and 5002?

Inside Docker containers services run on ports 8080 and 8081.
Docker maps them to host machine ports:

| Service              | Container Port | Host Port |
| -------------------- | -------------- | --------- |
| Order Service        | 8080           | 5001      |
| Notification Service | 8081           | 5002      |

---

# Message Broker Choice & Configuration

RabbitMQ was chosen because:

* Lightweight
* Easy Docker setup
* Reliable message delivery
* Supports acknowledgments
* Widely used in enterprise systems

### Queue Configuration

Queue name:

```
order-created
```

Queue settings:

* Durable = true
* Exclusive = false
* AutoDelete = false

Message setting:

* Persistent messages enabled (DeliveryMode = 2)

This ensures messages survive broker restart.

---

# Event Format

When an order is created, the Order Service publishes the following event:

```json
{
  "eventId": "guid",
  "occurredAt": "2026-02-24T10:15:30Z",
  "orderId": "guid",
  "customerEmail": "customer@example.com",
  "productCode": "PRD-1001",
  "quantity": 2
}
```

## Event Fields

| Field         | Description                   |
| ------------- | ----------------------------- |
| eventId       | Unique identifier for event   |
| occurredAt    | Timestamp when event occurred |
| orderId       | Order identifier              |
| customerEmail | Customer email                |
| productCode   | Product identifier            |
| quantity      | Ordered quantity              |

---

# Error Handling Strategy

The system is designed to be resilient and reliable.

### 1. Startup Resilience

Notification service does not crash if RabbitMQ starts slowly.
It continuously retries connection until broker becomes available.

---

### 2. Manual Acknowledgement

Messages are acknowledged only after successful database save.

If processing fails:

* message is requeued
* it will be delivered again

---

### 3. Idempotent Consumer (Duplicate Protection)

The notification service checks:

* If the event was already processed
* Uses `eventId` to prevent duplicates

This avoids duplicate notifications.

---

### 4. At-Least-Once Delivery

Because of:

* durable queues
* persistent messages
* manual acknowledgement

The system guarantees:

An event will be delivered at least once.

Duplicate delivery is handled safely by idempotency.

---

### 5. Database Safety

Migrations are executed at startup but wrapped in safe handling so the service does not crash if the database already exists.

---

# Design Decisions

* Separate database per microservice
* Asynchronous communication
* Background worker consumer
* Singleton publisher connection
* Durable messaging
* Retry-based startup

---

# Possible Improvements

* Dead Letter Queue (DLQ)
* Outbox Pattern
* Email service integration
* Centralized logging
* Health checks
* Kubernetes deployment

---

# Conclusion

This project demonstrates:

* Event-driven microservices
* Reliable messaging using RabbitMQ
* Idempotent consumer handling
* Resilient startup behavior
* Dockerized distributed system
