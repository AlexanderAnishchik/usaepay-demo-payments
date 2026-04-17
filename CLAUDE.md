# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

---

## Repository Overview

A PoC payment portal integrating the USAePay payment gateway:

- `backend/USAePayPortal/` — .NET 10 Web API
- `frontend/` — React 18 + Vite SPA
- `docker-compose.yml` — two-container Docker setup

---

## Commands

### Local dev

```bash
# Backend (http://localhost:5000)
cd backend/USAePayPortal && dotnet run

# Frontend (http://localhost:5173, proxies /api/* to :5000)
cd frontend && npm install && npm run dev
```

Scalar UI: `http://localhost:5000/scalar/v1` | OpenAPI: `http://localhost:5000/openapi/v1.json`

### Docker

```bash
docker-compose up --build
# frontend → http://localhost:3000
# backend  → http://localhost:5000
```

---

## Configuration

`backend/USAePayPortal/appsettings.json` (mirrored as env vars in `docker-compose.yml`):

```json
{
  "USAePay": {
    "ApiKey":       "server-side key",
    "Pin":          "PIN for HMAC signing",
    "PublicApiKey": "_P… key for pay.js",
    "BaseUrl":      "https://sandbox.usaepay.com/api/v2",
    "FrontendUrl":  "http://localhost:5173"
  }
}
```

In Docker, env vars use `__` separator: `USAePay__ApiKey`, etc. `FrontendUrl` is set to `http://localhost:3000` for CORS.

---

## Architecture

### Tokenisation (PCI boundary)

Card data never reaches the .NET backend:

1. `index.html` loads `pay.js` as a `<script>` tag → exposes `window.usaepay` globally
2. `PaymentForm.jsx` mounts a hosted card-entry iframe via `card.addHTML('payjs-card-container')`
3. On submit, `client.getPaymentKey(card)` sends card data directly to USAePay → returns a **single-use `payment_key`**
4. Frontend posts only the `payment_key` to the backend

React Strict Mode guard: `if (payClientRef.current) return` in `PaymentForm` prevents double-mounting the iframe.

### Save-card flow (`POST /api/payment/customers`)

Three sequential USAePay calls:

1. `POST /customers` → `custkey`
2. `POST /transactions` — `cc:sale` with `payment_key` + `custkey` → `transkey` + initial charge
3. `POST /customers/{custkey}/payment_methods` — `[{ "pay_type": "transaction", "transaction_key": transkey }]` → `methodkey`

Both `custkey` and `methodkey` are stored in `CustomerStoreSingleton` for recurring billing.

### Recurring billing

`RecurringPaymentService` is an `IHostedService` that fires every 10 minutes. Per cycle it resolves a new DI scope (because `IUSAePayService` is scoped) and calls `ChargeCustomerAsync` for every entry in `CustomerStoreSingleton`.

`ChargeCustomerAsync` uses `POST /customers/{custkey}/transactions` with `command: customer:sale`. No token fetch needed — USAePay uses the stored payment method automatically.

`CustomerStoreSingleton` is a singleton `ConcurrentDictionary` (in-memory, not persisted).

### API endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/payment/config` | Public API key for pay.js |
| `POST` | `/api/payment/charge` | One-off charge via `payment_key` |
| `POST` | `/api/payment/customers` | Save customer + card, run initial charge |
| `GET` | `/api/payment/recurring` | All customers + charge history |
| `POST` | `/api/payment/recurring/run` | Force an immediate billing cycle |
| `DELETE` | `/api/payment/customers/{key}` | Remove from local store (not from USAePay) |

### Backend service layer

`USAePayService` is the sole integration point. Registered as scoped, injected via `AddHttpClient<USAePayService>()`.

Auth: each outgoing call builds a fresh `Authorization: Basic` header — `s2/{seed}/{sha256(apiKey+seed+pin)}` — set on each `HttpRequestMessage` individually to avoid non-thread-safe `DefaultRequestHeaders` mutation.

All DI registrations live in `ServiceComponentsDI.AddApplicationServices()`.

### Frontend

`App.jsx` fetches `GET /api/payment/config` on mount, passes `publicApiKey` to `PaymentForm`.

`RecurringStatus` polls `GET /api/payment/recurring` every 30 s. All API calls go through `frontend/src/services/api.js`, which normalises errors and always throws on non-2xx.

### Docker

`backend/Dockerfile` — multi-stage: `dotnet/sdk:10.0` build → `dotnet/aspnet:10.0` runtime on port 8080.
`frontend/Dockerfile` — multi-stage: `node:22-alpine` build → `nginx:alpine` serving `dist/` with `nginx.conf` proxying `/api/*` to `http://backend:8080`.

---

## Coding Standards

### Naming

| Element | Convention |
|---|---|
| Namespace, Class, Method, Property, Constant, Public Field | `PascalCase` |
| Method arguments, Local variables | `camelCase` |
| Primary constructor args **not** assigned to a private field | `_camelCase` |
| Private field | `_camelCase` |

Additional rules:
- Interfaces start with `I` (e.g. `IUSAePayService`)
- Singleton classes registered in DI end with `Singleton` (e.g. `CustomerStoreSingleton`)
- Boolean identifiers use prefixes: `Is`, `Has`, `Any`, etc.
- Enums: singular name, no `Enum` suffix, values start from 0
- Classes: noun or noun phrase; methods: verb
- Namespaces match folder structure

### Class member order (StyleCop SA1201–SA1215)

1. Constant fields
2. Fields
3. Constructors
4. Finalizers
5. Delegates / Events / Enums
6. Properties
7. Indexers
8. Methods
9. Nested structs / classes

Within each group, order by access level (`public` → `internal` → `protected` → `private`), then `static` before non-static, then `readonly` before non-readonly.

### Comments

- Single-line `//` only; no `/* */` block comments.
- XML `///` docs on all public members, classes, and fields.
- Comments on their own line, start with uppercase, end with a period.
- One space between `//` and text.

### Types

- **No `var`** — always use explicit types. Use `object` for anonymous types.
- Use object-initialiser syntax: `ExampleClass instance = new();`
- Use predefined type aliases: `int`, `string`, `bool`, `double`, not `Int32`, `String`, etc.

### Clean code

- Use **primary constructors** instead of separate constructors where possible.
- No magic numbers or strings — move them to named constants.
- Use `string.Empty` instead of `""` for empty-string comparisons.
- Use `ArgumentNullException.ThrowIfNull(param)` to guard inputs.
- Always use `using` with disposable types.
- Declare variables as close as possible to their point of use.
- Delete dead code.
- Use positive conditions.
- Register dependencies in an extension method inside a static `ServiceComponentsDI` class:

```csharp
internal static class ServiceComponentsDI
{
    internal static IServiceCollection AddApplicationServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IMyService, MyService>();
        // ...
        return services;
    }
}
```

### LINQ

Prefer method syntax over query syntax:

```csharp
// Preferred
myList.Where(x => x.IsActive).Select(x => x.Name)

// Avoid
from x in myList where x.IsActive select x.Name
```

### Layout

- One statement per line; one declaration per line.
- Continuation lines indented four spaces.
- At least one blank line between method definitions; no more than one consecutive blank line.
- One empty line before `return`.
- One empty line after closing `}` blocks.
- Use parentheses to clarify compound expressions: `if ((a > b) && (c > d))`.

### Libraries

- **Serialisation**: `System.Text.Json` (not Newtonsoft.Json)
- **Logging**: `Microsoft.Extensions.Logging`
- **Mapping**: AutoMapper (use the typed overload `Map<TSource, TDest>(source)`)
- **Validation**: FluentValidation

### Security

- Apply the **minimum required access level** to every class and interface. Default to `internal`; use `public` only when the type appears in a public API surface.
- Do **not** store secrets, keys, or passwords in code files — use environment variables or a secrets store.
- Do not use deprecated or untrusted third-party packages.

### Engineering principles

**KISS** — prefer straightforward control flow; keep error handling explicit and local.

**YAGNI** — do not add abstractions, feature flags, or config keys without a concrete current use case.

**DRY** — extract reusable logic into a shared function; keep boundaries explicit.

**SOLID**
- SRP: one concern per module.
- OCP: open for extension, closed for modification.
- LSP: derived types substitutable for their base.
- ISP: small focused interfaces.
- DIP: depend on abstractions.

**Fail fast** — prefer explicit errors for unsupported states; never silently widen permissions.

### Anti-patterns (never do)

- Add heavy dependencies for minor convenience.
- Weaken security policy or access constraints.
- Add speculative config or feature flags.
- Mix formatting-only changes with functional changes in the same commit.
- Modify unrelated modules "while here".

---

## Testing

- Test method names start with `Should`.
- Unit tests: namespace `<Project>.<HighLevelClass>`, class name `<TestedClass>Tests`.
- Integration tests: namespace is the domain aggregate, class name is the action under test.
- Use `// Arrange / // Act / // Assert` comments for readability.
- Cover all branches per class in unit tests; one class per test file.
- Mock fields defined at class level when reused across tests:

```csharp
private readonly Mock<IMyService> _myServiceMock = new();
```
