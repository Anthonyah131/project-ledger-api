# Fase 4 — Dashboard, Balance Completo y Reportes

> Prerrequisito: Fase 3c completada.
> Riesgo: **Bajo** | Estimado: 2 semanas

---

## Objetivo

Consolidar toda la información del proyecto en un endpoint de balance completo, implementar sugerencias automáticas de liquidación y el resumen consolidado del workspace.

---

## 4.1 Balance completo del proyecto

`GET /projects/:id/balance`

### Response

```json
{
  "project_id": "uuid",
  "workspace": "Miravalles",
  "currency": "USD",
  "total_income": 11470.00,
  "total_expenses": 931.78,
  "net_balance": 10538.22,
  "by_category": [
    {
      "category": "Aporte Nondier",
      "type": "income",
      "total": 11470.00
    },
    {
      "category": "Honorarios",
      "type": "expense",
      "total": 394.67,
      "budget": 500.00,
      "budget_used_pct": 78.9
    }
  ],
  "by_payment_method": [
    {
      "method": "Cuenta SINPE Harold",
      "owner_partner": "Nondier",
      "expenses": 931.78,
      "income": 11470.00,
      "net": 10538.22
    }
  ],
  "partners_enabled": true,
  "by_partner": [
    {
      "partner": "Nondier",
      "paid_physically": 931.78,
      "others_owe_him": 465.89,
      "he_owes_others": 0,
      "income_entitled": 11470.00,
      "income_received": 11470.00,
      "settlements_received": 0,
      "settlements_paid": 0,
      "net_balance": 465.89
    }
  ]
}
```

### Notas de implementación

- `by_partner` solo se incluye si `partners_enabled = true`.
- `by_category` usa `exp_converted_amount` / `inc_converted_amount` para uniformar moneda.
- `by_payment_method` reporta en `pmt_currency` de cada cuenta.
- `budget_used_pct` = `total / budget * 100`, solo si el proyecto tiene `project_budgets` para esa categoría.

### Archivos a modificar/crear

| Capa | Archivo | Acción |
|---|---|---|
| DTO | `DTOs/Projects/ProjectBalanceDto.cs` | Crear |
| Repository | `Repositories/ProjectBalanceRepository.cs` | Crear (queries agregadas) |
| Service | `Services/ProjectBalanceService.cs` | Crear |
| Controller | `Controllers/ProjectController.cs` | Agregar endpoint `GET /:id/balance` |

---

## 4.2 Liquidaciones sugeridas

`GET /projects/:id/partners/settlement-suggestions`

### Algoritmo

1. Obtener `net_balance` de cada partner (positivo = acreedor, negativo = deudor).
2. Ordenar acreedores de mayor a menor, deudores de mayor deuda a menor.
3. Emparejar el mayor acreedor con el mayor deudor.
4. El deudor paga al acreedor `min(balance_acreedor, deuda_deudor)`.
5. Reducir ambos saldos y repetir hasta que todos queden en 0.

### Response

```json
{
  "suggestions": [
    {
      "from": { "partner_id": "uuid", "name": "Argelida" },
      "to":   { "partner_id": "uuid", "name": "Nondier" },
      "amount": 465.89,
      "currency": "USD"
    }
  ],
  "note": "Con 1 transferencia todos los balances quedan en cero."
}
```

### Archivos a crear

| Capa | Archivo | Acción |
|---|---|---|
| DTO | `DTOs/Partners/SettlementSuggestionDto.cs` | Crear |
| Service | `Services/PartnerBalanceService.cs` | Agregar método `GetSettlementSuggestionsAsync` |
| Controller | `Controllers/ProjectPartnersController.cs` | Agregar endpoint |

---

## 4.3 Resumen consolidado del workspace

`GET /workspaces/:id/summary?reference_currency=USD`

> Requiere plan con `pln_can_use_advanced_reports = true`.

### Response

```json
{
  "workspace": "Miravalles",
  "reference_currency": "USD",
  "projects": [
    {
      "id": "uuid",
      "name": "Compra del lote",
      "currency": "CRC",
      "total_income_converted": 5200.00,
      "total_expenses_converted": 3800.00,
      "net_converted": 1400.00
    },
    {
      "id": "uuid",
      "name": "Gastos compartidos",
      "currency": "USD",
      "total_income_converted": 1200.00,
      "total_expenses_converted": 931.78,
      "net_converted": 268.22
    }
  ],
  "totals": {
    "total_income_converted": 6400.00,
    "total_expenses_converted": 4731.78,
    "net_converted": 1668.22,
    "note": "Conversiones aproximadas basadas en los tipos de cambio de cada transacción."
  }
}
```

### Notas de implementación

- La conversión usa el promedio ponderado de `exchange_rate` de las transacciones del proyecto hacia la `reference_currency`.
- Si no hay transacciones con tasa hacia `reference_currency`, marcar proyecto como `conversion_unavailable: true`.
- No almacenar la conversión; calcularse al vuelo.

### Archivos a modificar/crear

| Capa | Archivo | Acción |
|---|---|---|
| DTO | `DTOs/Workspaces/WorkspaceSummaryDto.cs` | Completar (ya creado en Fase 2b) |
| Repository | `Repositories/WorkspaceRepository.cs` | Agregar query de resumen consolidado |
| Service | `Services/WorkspaceService.cs` | Agregar `GetSummaryAsync` |
| Controller | `Controllers/WorkspaceController.cs` | Agregar endpoint `GET /:id/summary` |

---

## 4.4 Permisos y plan requeridos

| Feature | Plan requerido |
|---|---|
| Partners, workspaces básicos | Todos los planes |
| Módulo de socios (splits, balance) | `pln_can_share_projects = true` |
| Resumen consolidado workspace | `pln_can_use_advanced_reports = true` |
| Exportación de reportes | `pln_can_export_data = true` |

---

## Criterios de aceptación

- [ ] `GET /projects/:id/balance` devuelve todas las secciones correctamente.
- [ ] `by_partner` solo aparece cuando `partners_enabled = true`.
- [ ] Las sugerencias de liquidación minimizan el número de transferencias.
- [ ] `GET /workspaces/:id/summary` devuelve totales consolidados correctos.
- [ ] El resumen de workspace falla con `403` si el plan no lo permite.
- [ ] La conversión de moneda se calcula al vuelo y no se persiste.
