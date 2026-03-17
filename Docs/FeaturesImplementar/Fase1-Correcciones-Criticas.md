# Fase 1 — Correcciones Críticas

> Sin cambios de modelos. Deployable de inmediato.
> Riesgo: **Bajo** | Estimado: 1 semana

---

## Objetivo

Corregir dos bugs de cálculo que afectan la experiencia actual sin introducir cambios de schema.

---

## 1.1 Fix del Dashboard multi-proyecto

### Problema
El dashboard no filtra por proyecto, mezcla datos de todos los proyectos del usuario.

### Solución
`GET /dashboard?project_id=uuid` — toda agregación filtrada por ese proyecto.
Montos en `prj_currency_code` usando `inc_converted_amount` / `exp_converted_amount`.

### Archivos a modificar

| Capa | Archivo | Cambio |
|---|---|---|
| Controller | `Controllers/DashboardController.cs` | Agregar query param `project_id`, pasarlo al servicio |
| Service | `Services/DashboardService.cs` | Filtrar todas las queries por `project_id` |
| Repository | `Repositories/DashboardRepository.cs` | Agregar `WHERE exp_project_id = @projectId` / `inc_project_id` |
| DTO | `DTOs/DashboardDto.cs` | Agregar campo `currency_code` junto a cada cifra |

### Validaciones requeridas
- `project_id` debe ser un proyecto al que el usuario tiene acceso (`IProjectAccessService`).
- Si no se provee `project_id`, retornar `400 project_id_required`.

### Criterios de aceptación
- [ ] El dashboard solo muestra datos del proyecto seleccionado.
- [ ] El código de moneda aparece junto a cada cifra.
- [ ] Acceso a proyectos ajenos retorna `403`.

---

## 1.2 Fix del balance por método de pago

### Problema
El balance de una cuenta no se calcula correctamente por proyecto.

### Solución
Query correcta que une `incomes` y `expenses` por `payment_method_id` filtrando por proyecto:

```sql
SELECT
  pm.pmt_id,
  pm.pmt_name,
  pm.pmt_currency,
  COALESCE(SUM(i.inc_original_amount), 0) AS total_ingresos,
  COALESCE(SUM(e.exp_original_amount), 0) AS total_gastos,
  COALESCE(SUM(i.inc_original_amount), 0)
    - COALESCE(SUM(e.exp_original_amount), 0) AS balance
FROM payment_methods pm
LEFT JOIN incomes i
  ON i.inc_payment_method_id = pm.pmt_id
  AND i.inc_project_id = :project_id
  AND i.inc_is_deleted = false
LEFT JOIN expenses e
  ON e.exp_payment_method_id = pm.pmt_id
  AND e.exp_project_id = :project_id
  AND e.exp_is_deleted = false
WHERE pm.pmt_id IN (
  SELECT ppm_payment_method_id FROM project_payment_methods
  WHERE ppm_project_id = :project_id
)
GROUP BY pm.pmt_id, pm.pmt_name, pm.pmt_currency;
```

El balance resultante está en `pmt_currency` (moneda de la cuenta, no del proyecto).

### Nuevo endpoint

`GET /payment-methods/:id/balance?project_id=uuid`

```json
{
  "payment_method_id": "uuid",
  "payment_method_name": "Cuenta SINPE Harold",
  "currency": "CRC",
  "total_income": 500000.00,
  "total_expenses": 120000.00,
  "balance": 380000.00
}
```

### Archivos a modificar

| Capa | Archivo | Cambio |
|---|---|---|
| Controller | `Controllers/PaymentMethodController.cs` | Agregar endpoint `GET /:id/balance` |
| Service | `Services/PaymentMethodService.cs` | Método `GetBalanceAsync(pmtId, projectId, userId)` |
| Repository | `Repositories/PaymentMethodRepository.cs` | Query de balance filtrada por proyecto |
| DTO | `DTOs/PaymentMethodBalanceDto.cs` | Nuevo DTO de respuesta |

### Criterios de aceptación
- [ ] El balance refleja solo las transacciones del proyecto indicado.
- [ ] La moneda del balance es la de la cuenta (`pmt_currency`), no la del proyecto.
- [ ] Llamar con un `payment_method` que no pertenece al proyecto retorna `404`.
