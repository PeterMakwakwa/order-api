# API reference

All examples below are real responses from a local run. The interactive version of this page, generated from the code, is at http://localhost:5000/scalar/v1 when the API is running (the OpenAPI document is at `/openapi/v1.json`).

![The place-order endpoint in the Scalar API reference](images/api-reference-scalar.png)

**On this page:** [Headers](#headers) · [Place an order](#place-an-order) · [Get an order](#get-an-order) · [Order history](#order-history) · [Business rules](#business-rules) · [Validation](#validation) · [Status codes](#status-codes) · [Errors](#error-format)

## Headers

| Header | Required on | Value |
|---|---|---|
| `Authorization` | every `/api` call | `Bearer <token>`. Locally, create one with `./build/local/New-LocalToken.ps1` |
| `CorrelationId` | every `/api` call | Any unique id (a GUID is fine). It is added to every log line and echoed on the response, so one request can be traced end to end |
| `Idempotency-Key` | `POST /api/v1/order` | A new value per order (a GUID is fine), 1 to 100 characters. **Reuse the same value when retrying that order** |

The token identifies the caller:

| Claim | Meaning |
|---|---|
| `customer_id` | The customer the caller is. Required to place orders |
| `role` = `Support` | Support staff: can read any customer's orders, cannot place orders |

## Place an order

`POST /api/v1/order`

```http
POST /api/v1/order
Authorization: Bearer <token for customer 42>
CorrelationId: 3f2b8c1e-7d4a-4c9e-9a51-2b6d0e8f4a17
Idempotency-Key: 6c0a5a3e-1f0b-4c55-8d0e-9a7f2b1c4d33
Content-Type: application/json

{
  "customerEmail": "jane@example.com",
  "items": [
    { "productId": 1, "quantity": 1 },
    { "productId": 2, "quantity": 2 }
  ],
  "paymentMethod": "CreditCard",
  "promotionCode": "WELCOME10"
}
```

| Field | Notes |
|---|---|
| `customerEmail` | Where the confirmation is sent |
| `items` | 1 to 100 lines. The same product may appear more than once; the lines are merged |
| `paymentMethod` | `CreditCard`, `DebitCard`, `PayPal` or `BankTransfer` |
| `promotionCode` | Optional, case-insensitive. The discount comes from the server |

There is no customer id or price in the body: the customer comes from the token and prices from the database.

**Response: `201 Created`**

```http
HTTP/1.1 201 Created
Location: http://localhost:5000/api/v1/Order/5517a699-a85b-48cf-b821-8aca044e7e82
CorrelationId: 3f2b8c1e-7d4a-4c9e-9a51-2b6d0e8f4a17

{
  "id": "3f2b8c1e-7d4a-4c9e-9a51-2b6d0e8f4a17",
  "orderId": "5517a699-a85b-48cf-b821-8aca044e7e82",
  "promotionCode": "WELCOME10",
  "subtotal": 89.97,
  "total": 80.97,
  "status": "Completed",
  "dateCreated": "2026-10-15T22:02:01.6865045Z"
}
```

`id` is the request's correlation id; `orderId` identifies the order. `status` is `Completed` for card and PayPal payments, and `AwaitingPayment` for bank transfers.

### Retrying safely

If a request times out or the connection drops, **send it again with the same `Idempotency-Key`**:

| The earlier request with that key... | Response |
|---|---|
| succeeded or was declined | The **original response**, with header `Idempotent-Replayed: true`. Nothing is charged again |
| is still being processed | `409`: wait and retry with the same key |
| failed because the payment provider was unavailable (`503`) | The **same order is charged again** (safe: the provider sees the same order id) |
| had a *different* body | `422`: a key belongs to one order; use a new key for a new order |

A retry counts as "the same order" even if it lists the items in another order, splits a quantity over two lines, or writes the promotion code in another case.

## Get an order

`GET /api/v1/order/{orderId}`

```json
{
  "orderId": "5517a699-a85b-48cf-b821-8aca044e7e82",
  "customerId": 42,
  "status": "Completed",
  "paymentMethod": "CreditCard",
  "promotionCode": "WELCOME10",
  "discountRate": 0.1,
  "subtotal": 89.97,
  "total": 80.97,
  "dateCreated": "2026-10-15T22:02:01.6865045Z",
  "items": [
    { "productId": 1, "productName": "Keyboard", "unitPrice": 49.99, "quantity": 1 },
    { "productId": 2, "productName": "Mouse", "unitPrice": 19.99, "quantity": 2 }
  ]
}
```

`productName` and `unitPrice` are what they were when the order was placed, even if the product changes later. Another customer's order returns `404`, exactly like an order that does not exist, so order ids cannot be probed.

## Order history

`GET /api/v1/order/by/customer/{customerId}?pageSize=20&cursor=...`

Returns the customer's orders, **newest first**, one page at a time.

| Query parameter | Default | Notes |
|---|---|---|
| `pageSize` | 20 | 1 to 100 |
| `cursor` | none (first page) | The `nextCursor` from the previous page |

```json
{
  "customerId": 42,
  "pageSize": 2,
  "totalCount": 4,
  "orders": [
    { "id": "5517a699-a85b-48cf-b821-8aca044e7e82", "total": 80.97, "status": "Completed", "paymentMethod": "CreditCard", "dateCreated": "2026-10-15T22:02:01.6865045Z" },
    { "id": "39d7667f-95fb-40e9-b99a-eab268e6ec09", "total": 89.97, "status": "AwaitingPayment", "paymentMethod": "BankTransfer", "dateCreated": "2026-10-15T21:49:59.602355Z" }
  ],
  "nextCursor": "NjM5Mjc2OTc3OTk2MDIzNTUwOjI"
}
```

To get the next page, call again with `&cursor=NjM5Mjc2OTc3OTk2MDIzNTUwOjI`. When `nextCursor` is `null` you have reached the last page. Treat the cursor as an opaque string. Customers can only read their own history (`403` otherwise); support staff can read anyone's.

## Business rules

1. Customers place orders only for themselves: the customer id comes from the token, never the request.
2. An order has a valid email address, 1 to 100 line items and a supported payment method.
3. Every product must exist. Repeated products are merged into one line, with at most 1,000 of each product.
4. Line items keep the product's name and price at the moment of ordering.
5. A discount only comes from a promotion code that exists, is active and is within its validity period. The rate is stored on the promotion (between 0 and 1). Totals are calculated in `decimal` and rounded to 2 decimal places.
6. The order is saved as `PendingPayment` before it is charged, and the payment outcome is always recorded, even if the client disconnects.
7. Outcomes: card and PayPal → `Completed`; bank transfer → `AwaitingPayment`; declined, duplicate or provider failure → `PaymentFailed`. Only a provider failure can be retried; every other outcome is final.
8. Orders stuck in `PendingPayment` (the server died mid-payment) are settled automatically within about 6 minutes.
9. A confirmation email is sent for every `Completed` or `AwaitingPayment` order, listing what was ordered, the subtotal, any discount and the total. Email problems never affect the order.

Local seed data: products `1` Keyboard (49.99), `2` Mouse (19.99), `3` Monitor (199.00); promotion codes `WELCOME10` (10% off), `EXPIRED20` (expired) and `INACTIVE30` (inactive).

## Validation

Checked before anything else; every invalid field is reported in one response (`400`).

| Field | Rule |
|---|---|
| `Authorization` | A valid bearer token; `customer_id` claim required to place an order |
| `CorrelationId` | Present on every `/api` call |
| `Idempotency-Key` | Present on `POST`, 1 to 100 characters |
| `customerEmail` | A valid email address |
| `paymentMethod` | One of the supported methods |
| `promotionCode` | If present: not empty, at most 50 characters |
| `items` | 1 to 100; each with a product id > 0 and a quantity > 0; at most 1,000 of each product in total |
| `pageSize`, `cursor` | `pageSize` 1 to 100; `cursor` must be a `nextCursor` from this API |

Whether the products exist and the promotion code is valid is checked by the handler, after the [retry check](#retrying-safely), so a retry always gets its original answer. Those errors are also `400`.

## Status codes

| Code | Meaning | What the client should do |
|---|---|---|
| 200 | OK (reads) | – |
| 201 | Order placed, or a retry was replayed (`Idempotent-Replayed: true`) | – |
| 400 | Invalid request, unknown product, invalid promotion code or missing header | Fix the request |
| 401 | Missing, invalid or expired token | Log in again |
| 402 | The payment was declined | Use another payment method (with a new `Idempotency-Key`) |
| 403 | Not allowed (support staff placing an order, or another customer's history) | – |
| 404 | Order not found, or it belongs to another customer | – |
| 409 | The provider reported a duplicate, or a request with this key is still in progress | Wait, then retry with the same key |
| 422 | The `Idempotency-Key` was already used for a different order | Use a new key |
| 429 | Rate limit reached (60 requests per minute per customer by default) | Retry after the `Retry-After` seconds |
| 500 | Unexpected server error (no details are returned) | Retry later; report it with the `CorrelationId` |
| 503 | The payment provider is unavailable | Retry with the **same** `Idempotency-Key` |

## Error format

Errors use [RFC 7807 problem details](https://www.rfc-editor.org/rfc/rfc7807). Validation errors list every invalid field under `errors`:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/v1/order",
  "errors": {
    "CustomerEmail": ["The request payload has failed validation. A valid customer email address must be provided."],
    "Items": ["The request payload has failed validation. At least one item must be provided."]
  }
}
```

```json
{
  "title": "The order refers to products or a promotion that are not valid.",
  "status": 400,
  "instance": "/api/v1/order",
  "errors": {
    "Items": ["The request payload has failed validation. The item's product Id 99 does not exist."],
    "PromotionCode": ["The request payload has failed validation. The promotion code EXPIRED20 is not valid."]
  }
}
```

Other errors have a `title` and `status` only. A `500` never includes exception details.
