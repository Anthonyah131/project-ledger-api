# Fase 3a — Splits Core (expense_splits e income_splits)

> Prerrequisito: Fase 2c completada.
> Riesgo: **Bajo** | Estimado: 1 semana

---

## Objetivo

Crear las tablas de splits para gastos e ingresos, e implementar el auto-split en todos los movimientos existentes y nuevos. Los splits son 100% al partner dueño de la cuenta por defecto, sin visibilidad en UI todavía.

---

## 3a.1 Nuevas tablas de splits

### Script SQL (`Scripts/add_splits_tables.sql`)

```sql
CREATE TABLE public.expense_splits (
  exs_id UUID NOT NULL DEFAULT gen_random_uuid(),
  exs_expense_id UUID NOT NULL,
  exs_partner_id UUID NOT NULL,
  exs_split_type VARCHAR(10) NOT NULL,
  exs_split_value DECIMAL(14,4) NOT NULL,
  exs_resolved_amount DECIMAL(14,2) NOT NULL,
  exs_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  exs_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  CONSTRAINT expense_splits_pkey PRIMARY KEY (exs_id ASC),
  CONSTRAINT exs_expense_fkey FOREIGN KEY (exs_expense_id)
    REFERENCES public.expenses(exp_id) ON DELETE CASCADE,
  CONSTRAINT exs_partner_fkey FOREIGN KEY (exs_partner_id)
    REFERENCES public.partners(ptr_id),
  UNIQUE INDEX uq_exs_expense_partner (exs_expense_id ASC, exs_partner_id ASC),
  INDEX idx_exs_expense_id (exs_expense_id ASC),
  INDEX idx_exs_partner_id (exs_partner_id ASC),
  CONSTRAINT exs_split_type_check CHECK (exs_split_type IN ('percentage':::STRING, 'fixed':::STRING)),
  CONSTRAINT exs_split_value_positive CHECK (exs_split_value > 0:::DECIMAL),
  CONSTRAINT exs_resolved_amount_positive CHECK (exs_resolved_amount > 0:::DECIMAL)
);
COMMENT ON TABLE public.expense_splits IS
  'División del costo de un gasto entre partners del proyecto.';

CREATE TABLE public.income_splits (
  ins_id UUID NOT NULL DEFAULT gen_random_uuid(),
  ins_income_id UUID NOT NULL,
  ins_partner_id UUID NOT NULL,
  ins_split_type VARCHAR(10) NOT NULL,
  ins_split_value DECIMAL(14,4) NOT NULL,
  ins_resolved_amount DECIMAL(14,2) NOT NULL,
  ins_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  ins_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  CONSTRAINT income_splits_pkey PRIMARY KEY (ins_id ASC),
  CONSTRAINT ins_income_fkey FOREIGN KEY (ins_income_id)
    REFERENCES public.incomes(inc_id) ON DELETE CASCADE,
  CONSTRAINT ins_partner_fkey FOREIGN KEY (ins_partner_id)
    REFERENCES public.partners(ptr_id),
  UNIQUE INDEX uq_ins_income_partner (ins_income_id ASC, ins_partner_id ASC),
  INDEX idx_ins_income_id (ins_income_id ASC),
  INDEX idx_ins_partner_id (ins_partner_id ASC),
  CONSTRAINT ins_split_type_check CHECK (ins_split_type IN ('percentage':::STRING, 'fixed':::STRING)),
  CONSTRAINT ins_split_value_positive CHECK (ins_split_value > 0:::DECIMAL),
  CONSTRAINT ins_resolved_amount_positive CHECK (ins_resolved_amount > 0:::DECIMAL)
);
COMMENT ON TABLE public.income_splits IS
  'División de un ingreso entre partners del proyecto.';
```

---

## 3a.2 Migración: auto-split 100% para movimientos existentes

### Script (`Scripts/migrate_auto_splits.sql`)

```sql
-- Splits de gastos existentes: 100% al partner dueño de la cuenta
INSERT INTO expense_splits
  (exs_expense_id, exs_partner_id, exs_split_type, exs_split_value, exs_resolved_amount)
SELECT
  e.exp_id,
  pm.pmt_owner_partner_id,
  'percentage',
  100,
  e.exp_original_amount
FROM expenses e
JOIN payment_methods pm ON pm.pmt_id = e.exp_payment_method_id
WHERE e.exp_is_deleted = false
  AND pm.pmt_owner_partner_id IS NOT NULL
  AND e.exp_id NOT IN (SELECT DISTINCT exs_expense_id FROM expense_splits);

-- Splits de ingresos existentes: 100% al partner dueño de la cuenta
INSERT INTO income_splits
  (ins_income_id, ins_partner_id, ins_split_type, ins_split_value, ins_resolved_amount)
SELECT
  i.inc_id,
  pm.pmt_owner_partner_id,
  'percentage',
  100,
  i.inc_original_amount
FROM incomes i
JOIN payment_methods pm ON pm.pmt_id = i.inc_payment_method_id
WHERE i.inc_is_deleted = false
  AND pm.pmt_owner_partner_id IS NOT NULL
  AND i.inc_id NOT IN (SELECT DISTINCT ins_income_id FROM income_splits);
```

---

## 3a.3 Comportamiento en nuevos movimientos (lógica de servicio)

### Regla: Auto-split por defecto

Cuando se crea un gasto o ingreso **sin** pasar `splits` en el body:

1. Obtener el partner dueño de la cuenta seleccionada (`payment_method.pmt_owner_partner_id`).
2. Crear un solo split: `split_type = 'percentage'`, `split_value = 100`, `resolved_amount = original_amount`.

### Validaciones para splits enviados explícitamente (preparación para Fase 3b)

1. **Suma de montos:** `SUM(exs_resolved_amount) = exp_original_amount`. Error → `400 splits_dont_sum_to_total`.
2. **Suma de porcentajes:** Si `split_type = 'percentage'`, suma = 100 exacto. Error → `400 splits_percentage_not_100`.
3. **Partners válidos:** Todos los `partner_id` deben ser partners activos del proyecto. Error → `400 invalid_split_partner`.
4. **Sin duplicados:** Un partner no puede aparecer dos veces en el mismo array de splits.

---

## 3a.4 Modelos y configuración EF Core

### Nuevos modelos

**`Models/ExpenseSplit.cs`**: `ExsId`, `ExsExpenseId`, `ExsPartnerId`, `ExsSplitType`, `ExsSplitValue`, `ExsResolvedAmount`, `ExsCreatedAt`, `ExsUpdatedAt`. Navegación: `Expense`, `Partner`.

**`Models/IncomeSplit.cs`**: Misma estructura con prefijo `ins_`. Navegación: `Income`, `Partner`.

### Nuevas configuraciones

`Configurations/ExpenseSplitConfiguration.cs` y `Configurations/IncomeSplitConfiguration.cs`.

### Modificaciones en modelos existentes

**`Models/Expense.cs`**: Agregar colección `Splits` (tipo `ICollection<ExpenseSplit>`).

**`Models/Income.cs`**: Agregar colección `Splits` (tipo `ICollection<IncomeSplit>`).

### `Data/AppDbContext.cs`

Agregar `DbSet<ExpenseSplit>` y `DbSet<IncomeSplit>`.

---

## 3a.5 Archivos a crear/modificar

| Capa | Archivo | Acción |
|---|---|---|
| Script SQL | `Scripts/add_splits_tables.sql` | Crear |
| Script SQL | `Scripts/migrate_auto_splits.sql` | Crear |
| Model | `Models/ExpenseSplit.cs` | Crear |
| Model | `Models/IncomeSplit.cs` | Crear |
| Config EF | `Configurations/ExpenseSplitConfiguration.cs` | Crear |
| Config EF | `Configurations/IncomeSplitConfiguration.cs` | Crear |
| Model | `Models/Expense.cs` | Modificar: agregar colección `Splits` |
| Model | `Models/Income.cs` | Modificar: agregar colección `Splits` |
| DbContext | `Data/AppDbContext.cs` | Agregar DbSets |
| Service | `Services/ExpenseService.cs` | Modificar: auto-split en create/update |
| Service | `Services/IncomeService.cs` | Modificar: auto-split en create/update |
| Repository | `Repositories/ExpenseRepository.cs` | Modificar: incluir splits en queries con detail |
| Repository | `Repositories/IncomeRepository.cs` | Modificar: incluir splits en queries con detail |

---

## 3a.6 Nota sobre `exs_resolved_amount`

Siempre en moneda original del gasto (`exp_original_currency`). Para mostrar en moneda del proyecto, multiplicar por `exp_exchange_rate`. **No** duplicar el monto convertido en la tabla de splits — se calcula al vuelo.

---

## Criterios de aceptación

- [ ] Tablas `expense_splits` e `income_splits` creadas.
- [ ] Todos los movimientos existentes tienen un split 100% al partner de la cuenta.
- [ ] Crear nuevo gasto sin `splits` genera el auto-split automáticamente.
- [ ] Las validaciones de suma de splits retornan los errores correctos.
- [ ] Borrar un gasto hace CASCADE en sus splits.
