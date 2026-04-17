# USAePay Payment Portal

A proof-of-concept payment portal built on the [USAePay](https://usaepay.com) gateway.  
Card data is tokenised client-side via **pay.js** — raw card numbers never touch the server.

```
backend/    .NET 10 Web API   – USAePay REST integration, recurring billing
frontend/   React 18 + Vite   – pay.js iframe, payment form, billing dashboard
```

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0+ |
| Node.js | 18+ |
| Docker + Compose | any recent |
| USAePay sandbox account | [sandbox.usaepay.com](https://sandbox.usaepay.com) |

---

## Getting API Keys

1. Register dev account  at [developer.usaepay.com](https://developer.usaepay.com/).
2. Request test Sandox Merchant account. [help.usaepay.info](https://help.usaepay.info/developer/reference/sandbox/).
3. Receive source key by email.
3. Now you can sign up at [sandbox.usaepay.com](https://sandbox.usaepay.com).

---

## Setup

### Local dev

1. Copy the settings template and fill in your keys:

```bash
cp backend/USAePayPortal/appsettings.example.json backend/USAePayPortal/appsettings.json
# Edit appsettings.json — set ApiKey, Pin, PublicApiKey
```

2. Run backend and frontend in separate terminals:

```bash
# Terminal 1 — backend  (http://localhost:5000)
cd backend/USAePayPortal && dotnet run

# Terminal 2 — frontend  (http://localhost:5173)
cd frontend && npm install && npm run dev
```

The Vite dev server proxies `/api/*` to `:5000`, so no extra CORS setup is needed.

### Docker

1. Copy the Docker settings template and fill in your keys:

```bash
cp docker.settings.example.env docker.settings.env
# Edit docker.settings.env — set USAePay__ApiKey, USAePay__Pin, USAePay__PublicApiKey
```

2. Build and start:

```bash
docker-compose up --build
```

| URL | |
|-----|-|
| `http://localhost:3000` | Frontend |
| `http://localhost:5000` | Backend API |
| `http://localhost:5000/scalar/v1` | Interactive API docs (Scalar UI) |
| `http://localhost:5000/openapi/v1.json` | OpenAPI spec |

---

## Configuration reference

### `appsettings.json` (local dev)

| Key | Description |
|-----|-------------|
| `USAePay:ApiKey` | Server-side API key — used for HMAC-signed backend calls |
| `USAePay:Pin` | PIN for `s2/{seed}/{sha256(apiKey+seed+pin)}` auth header |
| `USAePay:PublicApiKey` | Client-side public key passed to pay.js in the browser |
| `USAePay:BaseUrl` | USAePay v2 API base URL (sandbox or production) |
| `USAePay:FrontendUrl` | Frontend origin for CORS (default `http://localhost:5173`) |

### `docker.settings.env` (Docker)

Same keys in `UPPER__CASE` format (`USAePay__ApiKey`, `USAePay__Pin`, …).  
`FrontendUrl` defaults to `http://localhost:3000` in Docker mode.

---

## Features

### One-off payment

1. Enter card details in the pay.js hosted iframe.
2. Set an amount and click **Pay**.
3. `client.getPaymentKey()` tokenises the card directly with USAePay and returns a single-use `payment_key` — **raw card data never reaches the backend**.
4. The `payment_key` is posted to `POST /api/payment/charge`.

### Save card + recurring billing

1. Toggle **Save card for recurring billing**, fill in customer details, click **Save Card & Pay**.
2. The backend runs three sequential USAePay calls:
   - `POST /customers` — create a customer record
   - `POST /transactions` — `cc:sale` with the single-use `payment_key` (initial charge)
   - `POST /customers/{custkey}/payment_methods` — attach the card via the transaction reference
3. `RecurringPaymentService` charges every saved customer **$1.00 every 10 minutes** using the Customer Transaction API.

### Recurring status dashboard

- Lists all saved customers with their Customer Key and Payment Method Key.
- **Force Billing** — trigger an immediate charge cycle for all customers.
- **Delete** — remove a customer from local billing (a confirmation note warns the USAePay vault record is unaffected).
- Auto-refreshes every 30 seconds.

---

## API Reference

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/payment/config` | Public API key for pay.js initialisation |
| `POST` | `/api/payment/charge` | One-off charge via `payment_key` |
| `POST` | `/api/payment/customers` | Save customer + card, run initial charge |
| `GET` | `/api/payment/recurring` | List customers and charge history |
| `POST` | `/api/payment/recurring/run` | Force an immediate billing cycle |
| `DELETE` | `/api/payment/customers/{key}` | Remove customer from local store only |

Full interactive docs are available at `/scalar/v1` when the server is running.

---

## Architecture

### PCI tokenisation boundary

```
Browser                          USAePay
  │                                 │
  │── pay.js iframe (card entry) ──▶│
  │◀─────────── payment_key ────────│  (single-use token)
  │
  │── POST /api/payment/charge ──▶ Backend
        { payment_key, amount }       │
                                      │── POST /transactions ──▶ USAePay
```

The backend only ever sees the opaque `payment_key` — not card numbers, CVVs, or expiry dates.

### Save-card flow

```
POST /api/payment/customers
  1. POST /customers              → custkey
  2. POST /transactions (cc:sale) → transkey  (token consumed, card charged)
  3. POST /customers/{custkey}/payment_methods ← transkey → methodkey
```

Both `custkey` and `methodkey` are kept in an in-memory `ConcurrentDictionary` for recurring use.  
**Replace with a persistent store before production use.**

---

## Test Cards (USAePay Sandbox)

| Card Number | Result |
|-------------|--------|
| `4111 1111 1111 1111` | Approved |
| `4111 1111 1111 1129` | Declined |

Use any future expiry and any 3-digit CVV. Full list at [USAePay test cards](https://help.usaepay.info/developer/test-cards/).
