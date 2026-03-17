# Fase 3b — Módulo de Socios: Toggle y UI de Splits

> Prerrequisito: Fase 3a completada.
> Riesgo: **Medio** | Estimado: 1-2 semanas

---

## Objetivo

Activar el módulo de socios por proyecto (toggle `prj_partners_enabled`) y habilitar que el frontend envíe splits al crear gastos e ingresos. Con 2+ partners asignados al proyecto, el usuario puede dividir cada movimiento equitativamente o con porcentajes/montos personalizados.

---

## 3b.1 Activación del módulo de socios

### Reglas de activación

- **1 partner asignado al proyecto** → módulo desactivado; `prj_partners_enabled` ignorado.
- **2+ partners asignados** → el owner puede activarlo con `PATCH /projects/:id/settings`.
- Una vez activo: el endpoint de creación de gastos/ingresos acepta el array `splits`.

### Endpoint

`PATCH /projects/:id/settings`

```json
{
  "partners_enabled": true
}
```

Validación: `partners_enabled = true` solo se acepta si el proyecto tiene 2+ partners activos.

---

## 3b.2 Modificación de endpoints de gastos e ingresos

### `POST /expenses` — body extendido

```json
{
  "title": "Honorarios abogada",
  "amount": 300.00,
  "currency": "USD",
  "payment_method_id": "uuid",
  "category_id": "uuid",
  "date": "2026-03-10",
  "splits": [
    { "partner_id": "uuid-nondier", "split_type": "percentage", "split_value": 50 },
    { "partner_id": "uuid-argelida", "split_type": "percentage", "split_value": 50 }
  ]
}
```

- `splits` es **opcional**. Si no se envía o el módulo está desactivado → auto-split 100%.
- Si se envía, aplican todas las validaciones de Fase 3a.
- `resolved_amount` lo calcula el servidor: `split_value / 100 * original_amount` para porcentaje, o el valor  directo para fixed.

### `POST /incomes` — idéntico pero con `ins_` splits.

### `PATCH /expenses/:id` y `PATCH /incomes/:id`

- Si se pasa `splits` en el body → reemplazar los splits existentes (delete + insert en transacción).
- Si no se pasa `splits` → no modificar los splits existentes.

---

## 3b.3 Pre-llenado equitativo (lógica backend)

El endpoint `GET /projects/:id/partners/split-defaults` devuelve la distribución equitativa para ese proyecto:

```json
{
  "partners": [
    { "partner_id": "uuid", "name": "Nondier", "default_percentage": 50.00 },
    { "partner_id": "uuid", "name": "Argelida", "default_percentage": 50.00 }
  ]
}
```

- 2 partners → 50% / 50%
- 3 partners → 33.33% / 33.33% / 33.34% (el último absorbe el redondeo)
- N partners → 100/N% para cada uno, el último ajusta el decimal

---

## 3b.4 Retorno de splits en responses de detalle

`GET /expenses/:id` y `GET /incomes/:id` deben incluir los splits:

```json
{
  "id": "uuid",
  "title": "Honorarios abogada",
  "amount": 300.00,
  "splits": [
    { "partner_id": "uuid", "partner_name": "Nondier",   "split_type": "percentage", "split_value": 50, "resolved_amount": 150.00 },
    { "partner_id": "uuid", "partner_name": "Argelida",  "split_type": "percentage", "split_value": 50, "resolved_amount": 150.00 }
  ]
}
```

Los splits solo se incluyen cuando `prj_partners_enabled = true` en el proyecto.

---

## 3b.5 Archivos a modificar

| Capa | Archivo | Cambio |
|---|---|---|
| Controller | `Controllers/ProjectController.cs` | Agregar `PATCH /:id/settings` |
| Controller | `Controllers/ExpenseController.cs` | Agregar `splits` a body y response |
| Controller | `Controllers/IncomeController.cs` | Agregar `splits` a body y response |
| DTO | `DTOs/Expenses/CreateExpenseDto.cs` | Agregar propiedad `Splits` opcional |
| DTO | `DTOs/Expenses/ExpenseDetailDto.cs` | Agregar propiedad `Splits` |
| DTO | `DTOs/Incomes/CreateIncomeDto.cs` | Agregar propiedad `Splits` opcional |
| DTO | `DTOs/Incomes/IncomeDetailDto.cs` | Agregar propiedad `Splits` |
| DTO | `DTOs/Splits/SplitInputDto.cs` | Crear: `partner_id`, `split_type`, `split_value` |
| DTO | `DTOs/Splits/SplitResponseDto.cs` | Crear: incluye `partner_name`, `resolved_amount` |
| Service | `Services/ExpenseService.cs` | Lógica de splits en create/update |
| Service | `Services/IncomeService.cs` | Lógica de splits en create/update |
| Service | `Services/ProjectService.cs` | Agregar `UpdateSettingsAsync` |
| Controller | `Controllers/ProjectController.cs` | Agregar endpoint `split-defaults` |

---

## 3b.6 Reglas de negocio

- `splits` se ignora silenciosamente si `prj_partners_enabled = false`.
- Splits de tipo `fixed` deben sumar exactamente `original_amount`.
- Splits de tipo `percentage` deben sumar exactamente 100.
- No se pueden mezclar `percentage` y `fixed` en el mismo movimiento.
- `resolved_amount` siempre se calcula en el servidor; el cliente no lo envía.
- Editar un movimiento con `split_type = 'percentage'` → `resolved_amount` se recalcula automáticamente al cambiar el monto.
- Editar un movimiento con `split_type = 'fixed'` → si cambia el monto, exigir que el usuario corrija los splits manualmente (`400 fixed_splits_require_update`).

---

## Criterios de aceptación

- [ ] `PATCH /projects/:id/settings` activa `prj_partners_enabled` correctamente.
- [ ] `POST /expenses` con `splits` crea los splits correctamente.
- [ ] `POST /expenses` sin `splits` sigue generando auto-split 100%.
- [ ] `GET /expenses/:id` incluye splits cuando el módulo está activo.
- [ ] `GET /projects/:id/partners/split-defaults` retorna distribución equitativa.
- [ ] Validaciones de suma retornan los errores correctos.
- [ ] Editar monto de un gasto con splits `percentage` recalcula `resolved_amount`.
