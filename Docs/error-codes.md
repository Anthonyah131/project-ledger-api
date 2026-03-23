# Error Codes Reference

All API error responses follow this JSON shape:

```json
{
  "code": "NOT_FOUND",
  "message": "The requested resource was not found."
}
```

The `code` field is stable and machine-readable. The `message` field is human-readable and localized based on the `Accept-Language` header (`en` or `es`). Frontend logic should branch on `code`, never on `message`.

---

## Standard Codes

These codes cover the vast majority of responses.

| Code | HTTP Status | When it appears |
|------|------------|-----------------|
| `NOT_FOUND` | 404 | Requested resource does not exist or was deleted |
| `CONFLICT` | 409 | Duplicate record or uniqueness constraint violation |
| `VALIDATION_ERROR` | 400 | Invalid or missing input in the request body/query |
| `UNAUTHORIZED` | 401 | Missing, expired, or invalid JWT token |
| `FORBIDDEN` | 403 | Authenticated but lacking permission on this resource |
| `SUCCESS` | 200 / 201 | Explicit success with an accompanying message (e.g. logout, password reset) |
| `BAD_REQUEST` | 400 | Invalid operation state caught server-side (not a client input issue) |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

---

## Specific Codes

These codes require the frontend to branch UI behavior (e.g. show a specific error screen, trigger an upsell modal, or redirect).

### Authentication

| Code | HTTP Status | When it appears |
|------|------------|-----------------|
| `INVALID_CREDENTIALS` | 401 | Email/password login failed |
| `USER_ALREADY_EXISTS` | 409 | Registration attempted with an email already in use |
| `INVALID_OTP` | 400 | OTP code is wrong or expired |

### Billing / Plan limits

| Code | HTTP Status | When it appears | Extra fields |
|------|------------|-----------------|--------------|
| `PLAN_DENIED` | 403 | Feature is not available on the user's current plan | `feature` — the permission name that was denied |
| `PLAN_LIMIT_EXCEEDED` | 403 | User has reached the maximum count allowed by their plan (e.g. max projects) | `feature` — the limit name that was exceeded |

`PLAN_DENIED` and `PLAN_LIMIT_EXCEEDED` responses include an extra `feature` field:

```json
{
  "code": "PLAN_DENIED",
  "message": "Your current plan does not include this feature.",
  "feature": "CanShareProjects"
}
```

```json
{
  "code": "PLAN_LIMIT_EXCEEDED",
  "message": "You have reached the maximum number of projects allowed by your plan.",
  "feature": "MaxProjects"
}
```

### Billing availability

| Code | HTTP Status | When it appears |
|------|------------|-----------------|
| `STRIPE_DISABLED` | 503 | Stripe billing is disabled in this deployment; hide all billing UI |

---

## Rate Limiting

| Code | HTTP Status | When it appears |
|------|------------|-----------------|
| `TOO_MANY_REQUESTS` | 429 | Request rate limit exceeded |

---

## Where Each Code is Generated

| Source | Codes emitted |
|--------|--------------|
| Controllers (explicit returns) | `NOT_FOUND`, `CONFLICT`, `VALIDATION_ERROR`, `UNAUTHORIZED`, `SUCCESS`, `INVALID_CREDENTIALS`, `USER_ALREADY_EXISTS`, `INVALID_OTP`, `STRIPE_DISABLED` |
| `GlobalExceptionHandlerMiddleware` | `NOT_FOUND`, `FORBIDDEN`, `UNAUTHORIZED`, `PLAN_DENIED`, `PLAN_LIMIT_EXCEEDED`, `BAD_REQUEST`, `INTERNAL_ERROR` |
| JWT middleware (`SecurityExtensions`) | `UNAUTHORIZED` (missing/invalid token), `FORBIDDEN` (valid token, wrong role) |
| Rate limiter (`SecurityExtensions`) | `TOO_MANY_REQUESTS` |

---

## Localization

Responses are translated automatically based on the `Accept-Language` request header.

- `Accept-Language: en` → English messages (default)
- `Accept-Language: es` → Spanish messages

The `code` field is **never** translated. Only `message` changes.
