# Order API

A .NET 10 Web API that **places customer orders, charges them through a payment provider and emails an order confirmation**. Customers can then read an order and page through their order history.

It replaces a legacy `OrderProcessor` "god class" (one class that did validation, pricing, payment and email in one go) with a layered design that makes the important guarantees explicit and tested:

- **A customer is never charged twice**, even when a client retries, two requests race, or the server crashes mid-payment.
- **A slow or failing email server never fails an order.** Confirmations are sent in the background and retried.
- **Prices and discounts come from the server only.** The client cannot send its own discount.
- **Every outcome is visible** in logs, metrics, traces and health checks.

> New here? Read this page, then [background](docs/background.md) for *why* it is built this way and [architecture](docs/architecture.md) for *how*.

---

## Quick start (about 5 minutes)

You need the **.NET 10 SDK**, **Docker** (Docker Desktop or Rancher Desktop) and **PowerShell**. Run these from the repository root:

```powershell
# 1. Create your local secrets file and set two passwords in it (see the comments inside).
Copy-Item src/.env.example src/.env
notepad src/.env

# 2. Issue a test login token for customer 42, and share its signing key with the API container.
./build/local/New-LocalToken.ps1 -UpdateEnvFile

# 3. Start everything: SQL Server, the database deployment, a fake mail server and the API.
cd src
docker compose --profile api up -d --build
```

Then open:

| What | Where |
|---|---|
| Interactive API docs (try requests here) | http://localhost:5000/scalar/v1 |
| Confirmation emails (Mailpit inbox) | http://localhost:8025 |
| Health | http://localhost:5000/healthz/ready |

In the API docs, paste the token from step 2 under **Authentication → Bearer** and send a `POST /api/v1/order`. Payments are mocked locally, so every card payment succeeds.

### What you will see

| The running containers | The API reference | The confirmation email |
|---|---|---|
| ![Containers](docs/images/docker-compose-containers.png) | ![API reference](docs/images/api-reference-scalar.png) | ![Confirmation email](docs/images/mailpit-confirmation-email.png) |

Step-by-step instructions, running from Visual Studio / Rider, sample requests and troubleshooting are in **[docs/local-development.md](docs/local-development.md)**.

---

## What the API does

| Endpoint | Purpose |
|---|---|
| `POST /api/v1/order` | Place an order for the logged-in customer and charge it |
| `GET /api/v1/order/{orderId}` | Read one order with its line items |
| `GET /api/v1/order/by/customer/{customerId}` | Read a customer's orders, newest first, one page at a time |

Every call needs a bearer token and a `CorrelationId` header. Placing an order also needs an `Idempotency-Key` header: one value per order, reused when retrying that order. Request and response examples, business rules and status codes are in **[docs/api.md](docs/api.md)**.

## How it is built, in one picture

![Solution architecture](docs/diagrams/flow/solution-architecture.png)

Four projects, one per layer of an **onion (clean) architecture**. Dependencies only point inwards, towards the business rules:

| Project | Contains | References |
|---|---|---|
| `Order.Presentation.Api` | HTTP: controllers, middleware, authentication, wiring | Application, Infrastructure |
| `Order.Infrastructure` | Talks to the outside world: SQL Server, payment providers, SMTP, metrics | Application, Domain |
| `Order.Application` | The use cases: place order, read orders, background jobs, validation | Domain |
| `Order.Domain` | The business rules: the `Order` aggregate and the interfaces the other layers implement | nothing |

The full tour (folders, request flows, background jobs, database, design patterns) is in **[docs/architecture.md](docs/architecture.md)**.

> **Why one service and not microservices?** Ideally, orders, payments and emails would each be a separate microservice, connected by **Kafka** or **Azure Service Bus**. For the scope of this assignment, and to avoid over-engineering, they are kept in one solution. It is structured so the split is straightforward later: payments and email sit behind interfaces, and the outbox already stores the `OrderPlaced` event a broker would carry. See [the target design and reasoning](docs/background.md#one-service-for-now-not-microservices).

## Documentation

| Document | Read it to learn |
|---|---|
| [docs/background.md](docs/background.md) | What problems this solution solves, the decisions made and why, and the known trade-offs |
| [docs/architecture.md](docs/architecture.md) | The layers and folders, what happens on each request, the background jobs and the database |
| [docs/api.md](docs/api.md) | Endpoints with examples, business rules, validation and every status code |
| [docs/local-development.md](docs/local-development.md) | How to run, debug and test it on your machine, and fix common problems |
| [docs/operations.md](docs/operations.md) | Configuration, security and secrets, resilience, logs/metrics/traces, health checks and deploying the database |
| [docs/ai-usage.md](docs/ai-usage.md) | Which parts were written by hand and where AI tools were used |

## Repository layout

```
.github/workflows/ci.yml     CI: build, format check, all tests (incl. SQL Server), vulnerability scan, Docker image
build/
  docker/Dockerfile          the API image
  local/                     helper scripts: New-LocalToken.ps1 (login tokens), Set-LocalSecrets.ps1 (IDE secrets)
  mermaid/                   renders docs/diagrams/*.mermaid to PNG
docs/                        the documentation above, and its diagrams
src/
  Order.Domain/              business rules (core)
  Order.Application/         use cases
  Order.Infrastructure/      SQL Server, payments, email, observability
  Order.Presentation.Api/    the Web API (startup project)
  dbOrder/                   SQL scripts that create and migrate the database (deployed with grate)
  docker-compose.yml         the local stack
  .env.example               template for your local secrets (copy to .env, never commit .env)
test/Order.Tests/            unit, API and SQL Server integration tests
Order.slnx                   the solution
```

## Tests

```powershell
dotnet test
```

About 190 tests: domain rules, validators, handlers, the HTTP API end to end (in memory), and integration tests against a real SQL Server started in Docker. Without Docker the SQL Server tests are skipped, not failed. See [local-development.md](docs/local-development.md#running-the-tests).

## Technology

.NET 10 · ASP.NET Core · Entity Framework Core · SQL Server · [grate](https://erikbra.github.io/grate/) (database migrations) · FluentValidation · Polly (resilience) · MailKit · Serilog · prometheus-net · OpenTelemetry · Scalar (API docs) · NUnit · NSubstitute · Testcontainers
