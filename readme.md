1. **PRD (Product Requirements Document)**
2. **System Design**
3. **Project Structure + Implementation Plan**

---

# 1. Product Requirements Document (PRD)

## Product Name

**Distributed Payment Processing System**

## Overview

A backend service that processes financial transactions reliably using **idempotent APIs, asynchronous processing, and event-driven architecture**.

The system prevents duplicate charges, guarantees transaction integrity, and ensures resilience through retries, message queues, and dead-letter queues.

The platform simulates how **Stripe / Adyen style payment pipelines** work internally.

---

## Problem

Payment systems must guarantee:

* **No duplicate charges**
* **Reliable processing under failure**
* **Auditability**
* **High throughput**
* **Event driven integrations**

Traditional synchronous APIs fail under network retries or crashes.

---

## Goals

Primary goals:

1. Prevent duplicate transactions
2. Process payments asynchronously
3. Guarantee eventual consistency
4. Provide full audit logs
5. Handle failures safely with retries

---

## Non-Goals

Out of scope for v1:

* Real banking integrations
* PCI compliance
* Card tokenization
* Fraud detection

---

## Key Features

### 1️⃣ Idempotent Payment API

Clients submit payments with an **Idempotency-Key** header.

If the same request is retried:

```
POST /payments
Idempotency-Key: abc123
```

The system returns the **original response instead of creating a new charge.**

---

### 2️⃣ Event Driven Payment Processing

The API does **not process payments synchronously**.

Instead it publishes an event:

```
PaymentRequested
```

to a message queue.

Workers process it asynchronously.

---

### 3️⃣ Retry Logic + Backoff

Workers retry failed transactions using **exponential backoff**:

```
1s
2s
4s
8s
16s
```

---

### 4️⃣ Dead Letter Queue (DLQ)

If a payment fails too many times:

```
retry_count > 5
```

it is moved to a **Dead Letter Queue** for investigation.

---

### 5️⃣ Audit Logging

Every system action is recorded:

```
PaymentCreated
PaymentProcessingStarted
PaymentSucceeded
PaymentFailed
RetryAttempt
MovedToDLQ
```

---

### 6️⃣ Transactional Integrity

Database transactions ensure:

```
payment_record
event_publish
idempotency_record
```

either all succeed or all fail.

---

## User Flow

Client → Submit Payment

```
POST /payments
```

↓

API validates request

↓

Idempotency key checked

↓

Payment record created

↓

Event published to queue

↓

Worker processes payment

↓

Result stored in DB

↓

Event emitted

```
PaymentCompleted
```

---

## Success Metrics

* Zero duplicate transactions
* < 200ms API response time
* 99.9% successful processing
* Retry success rate

---

# 2. System Design

## High Level Architecture

```
                +------------------+
                |    API Gateway   |
                +------------------+
                         |
                         |
                +------------------+
                |   Payment API    |
                | (.NET / FastAPI) |
                +------------------+
                         |
              ---------------------
              |                   |
       +-------------+     +-------------+
       | PostgreSQL  |     | Message     |
       |             |     | Broker      |
       | Payments DB |     | RabbitMQ    |
       +-------------+     +-------------+
                                  |
                                  |
                         +------------------+
                         | Worker Services  |
                         | Payment Engine   |
                         +------------------+
                                  |
                         +------------------+
                         | Dead Letter Queue|
                         +------------------+
```

---

## Core Components

### API Gateway

Responsibilities:

* Authentication
* Rate limiting
* Routing

Tech options:

* NGINX
* Kong
* AWS API Gateway

---

### Payment API

Responsibilities:

* Validate request
* Handle idempotency
* Create payment record
* Publish message to queue

Example endpoint:

```
POST /payments
GET /payments/{id}
```

---

### PostgreSQL

Stores:

```
payments
idempotency_keys
audit_logs
```

---

### Message Broker

Options:

* RabbitMQ
* Redis Streams
* Kafka

Used for:

```
payment_requested
payment_completed
payment_failed
```

---

### Worker Services

Workers consume queue messages:

```
PaymentRequested
```

and process the payment.

Responsibilities:

* Payment processing
* Retry logic
* Publishing events

---

### Dead Letter Queue

Messages that exceed retry limit are moved to:

```
payment_dlq
```

---

# 3. Database Design

## Payments Table

```
payments
-------
id (UUID)
amount
currency
status
created_at
updated_at
```

Statuses:

```
pending
processing
completed
failed
```

---

## Idempotency Table

```
idempotency_keys
----------------
key
request_hash
response_body
created_at
```

---

## Audit Logs

```
audit_logs
-----------
id
payment_id
event_type
payload
created_at
```

---

# 4. API Design

## Create Payment

```
POST /payments
```

Body:

```json
{
  "amount": 1000,
  "currency": "USD",
  "user_id": "123"
}
```

Headers:

```
Idempotency-Key: abc123
```

Response:

```json
{
  "payment_id": "uuid",
  "status": "pending"
}
```

---

## Get Payment

```
GET /payments/{id}
```

Response:

```json
{
  "payment_id": "uuid",
  "status": "completed"
}
```

---

# 5. Retry Strategy

Pseudo logic:

```
retry_count = 0

while retry_count < MAX_RETRY:

    try:
        process_payment()

    except Error:

        retry_count += 1

        sleep(2^retry_count)

if retry_count >= MAX_RETRY:
    move_to_dead_letter_queue()
```

---

# 6. Project Structure (What Your Repo Should Look Like)

```
payment-processing-system
│
├── api
│   ├── controllers
│   ├── services
│   ├── middleware
│   └── routes
│
├── workers
│   ├── payment_worker
│   ├── retry_handler
│
├── infrastructure
│   ├── database
│   ├── message_broker
│   └── migrations
│
├── shared
│   ├── events
│   ├── models
│   └── utils
│
├── docker
│   ├── docker-compose.yml
│
├── docs
│   ├── PRD.md
│   ├── SYSTEM_DESIGN.md
│
└── README.md
```

---

# 7. Tech Stack (Recruiter Friendly)

Example stack:

**Backend**

* .NET Web API **or** FastAPI

**Database**

* PostgreSQL

**Queue**

* RabbitMQ

**Infra**

* Docker
* Docker Compose

**Observability**

* Prometheus
* Grafana
* Structured logging

---

# 8. Resume Bullet Points (Strong Ones)

Put something like this:

**Distributed Payment Processing System**

• Designed an event-driven payment processing architecture using RabbitMQ and worker services
• Implemented idempotent APIs preventing duplicate financial transactions
• Built retry mechanisms with exponential backoff and dead-letter queues for failure recovery
• Ensured transactional integrity using PostgreSQL constraints and database transactions
• Implemented audit logging for complete payment lifecycle tracking

---

# 9. BONUS (Makes the Project 10x Stronger)

Add these and recruiters will lose their minds:

### 1️⃣ Distributed Locks

Prevent workers from processing same payment twice.

Use:

```
Redis locks
```

---

### 2️⃣ Outbox Pattern

Guarantee message delivery.

Used by **Stripe / Shopify / Uber**

---

### 3️⃣ Observability

Add:

```
OpenTelemetry
Prometheus
Grafana
```

---

# 10. The Secret Sauce

What turns this from **student project → senior backend project**

Add:

* **Load testing with k6**
* **Chaos testing (kill workers)**
* **Horizontal scaling workers**
* **Webhook system**