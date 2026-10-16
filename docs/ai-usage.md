# AI usage

This page explains where AI tools were and were not used to build this solution, so the work can be assessed fairly.

## The original solution (author's account)

- **The solution was scaffolded by hand, without AI**: the solution and project structure, the onion layering and the folder layout.
- **The models project** (`Order.Application.Models`, now the `Models` folder of `Order.Application`) **was created by hand, without AI.**
- **AI was used most for unit tests and for the infrastructure layer.**
- **By the author's estimate, about 65% of the code in that original version was written by hand.**

## AI-assisted review and changes afterwards

After the original version was complete, an AI coding assistant (Claude Code) was used to review the solution and then make the changes the author chose to accept. Those changes are larger than the original split above suggests, and they touch the Domain and Application layers as well as tests and infrastructure:

| Area | AI-assisted changes |
|---|---|
| Payments | The charge is no longer cancelled when the client disconnects; payments that failed with a provider error can be retried; the reconciliation job for stalled payments (`StalledPaymentReconciliationHandler`, `OrderPaymentProcessor`, `PaymentStartedDate`) |
| Domain | `Order.RetryPayment`, the order concurrency token, item and quantity limits |
| Application | Validation reports every error; keyset paging for the order history (`OrderHistoryCursor`); request-hash normalisation for retries; ownership check in the get-order query; the `rejected` metric; domain rule errors mapped to 500 |
| Infrastructure | Product cache removed (current prices on every order); stable `Message-ID` on confirmation emails; ordered items listed in the confirmation email (`OrderPlaced` event version 2); the SQL scripts `0008` and `0009` |
| Structure | The original projects merged into one project per layer (namespaces unchanged) |
| Tests | About 60 new tests, including SQL Server integration tests; the fix that made the SQL Server integration tests pass on a fresh database |

The full before-and-after list is in [background.md](background.md#improvements-made-after-code-review).

## Documentation

The README, the pages in `docs/` and the diagrams were written with AI assistance, based on the code and on the author's decisions. The screenshots in `docs/images` were taken by the author. The decision to keep orders, payments and email in one solution rather than separate microservices ([background.md](background.md#one-service-for-now-not-microservices)) is the author's.

## How AI-generated work was checked

- The author decided which review findings to act on, and reviewed the results.
- Every change had to pass the same checks: a Release build with warnings treated as errors, `dotnet format --verify-no-changes`, the full test suite (about 190 tests, including SQL Server integration tests in Docker) and a vulnerable-package scan.
- The running stack (`docker compose`) was smoke-tested end to end: placing, retrying and reading orders, validation, authorization, confirmation emails, metrics and payment reconciliation.
