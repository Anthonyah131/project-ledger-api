# Project Ledger SaaS — Roadmap de Mejoras v7.0

> **Filosofía central:** El sistema se adapta a flujos reales de trabajo, no al revés.  
> La complejidad vive en el backend; el usuario ve simplicidad.

---

## Índice

1. [Diagnóstico del estado actual](#1-diagnóstico-del-estado-actual)
2. [Entidad nueva: Partners](#2-entidad-nueva-partners)
3. [Nueva arquitectura: Espacios de trabajo → Proyectos](#3-nueva-arquitectura-espacios-de-trabajo--proyectos)
4. [Módulo de socios](#4-módulo-de-socios)
5. [Fase 1 — Correcciones críticas (sin cambios de modelo)](#5-fase-1--correcciones-críticas)
6. [Fase 2 — Partners + Espacios de trabajo](#6-fase-2--partners--espacios-de-trabajo)
7. [Fase 3 — Módulo de socios activo](#7-fase-3--módulo-de-socios-activo)
8. [Fase 4 — Dashboard, balances y reportes](#8-fase-4--dashboard-balances-y-reportes)
9. [Cambios de base de datos completos (SQL)](#9-cambios-de-base-de-datos-completos-sql)
10. [Resumen de impacto por capa](#10-resumen-de-impacto-por-capa)

---

## 1. Diagnóstico del estado actual

### Fortalezas del schema existente que se conservan

- `project_members` — roles `owner / editor / viewer`, acceso al proyecto por usuario del sistema. No cambia.
- `transaction_currency_exchanges` — soporte multi-moneda por transacción.
- `categories` — dinámicas por proyecto.
- `obligations`, `project_budgets` — sin cambios.
- Soft-delete consistente en todas las entidades.

### Problema de raíz identificado

El schema actual asigna `payment_methods` directamente a `users` del sistema (`pmt_owner_user_id`). Esto impide asignar una cuenta a un socio que no tiene cuenta en la plataforma, y no permite saber qué cuentas pertenecen a qué persona en un proyecto compartido.

La solución es introducir `partners` como dueños de los métodos de pago: entidades simples de nombre que el usuario crea y gestiona libremente, igual que ya hace con sus cuentas.

---

## 2. Entidad nueva: Partners

### Qué es un partner

Un **partner** es un contacto financiero que el usuario crea y gestiona globalmente. Tiene nombre y datos opcionales de referencia. Puede tener uno o varios métodos de pago asociados.

Es exactamente el mismo flujo que los métodos de pago actuales:
- El usuario va a su panel de partners.
- Crea "Argelida Restrepo".
- Le asigna su cuenta SINPE.
- Listo.

No hay vinculación a cuentas del sistema. No hay concepto de "partner propio". El usuario crea los partners que necesita — incluido uno que lo represente a él mismo si quiere ("Nondier", "Yo", como prefiera nombrarlo) — y les asigna las cuentas correspondientes.

### Cómo reemplaza `pmt_owner_user_id`

`payment_methods.pmt_owner_user_id` se reemplaza por `pmt_owner_partner_id`. Los métodos de pago dejan de pertenecer a un usuario del sistema y pasan a pertenecer a un partner.

**Migración:** Para cada usuario existente se crea automáticamente un partner con su nombre completo, y todos sus métodos de pago se reasignan a ese partner.

### Flujo global de partners

El usuario gestiona sus partners desde el sidebar, igual que los métodos de pago:

```
Mis Partners
├── Nondier Amariles    → Cuenta SINPE Harold, Tarjeta Visa
├── Argelida Restrepo   → Cuenta SINPE personal
└── [+ Agregar partner]
```

Al crear un proyecto, elige qué partners participan. Los métodos de pago disponibles en ese proyecto son los que pertenecen a los partners asignados.

---

## 3. Nueva arquitectura: Espacios de trabajo → Proyectos

### Por qué se necesitan los espacios de trabajo

Un usuario maneja proyectos para contextos distintos: casa, negocio, sociedad A, sociedad B. Sin agrupación, todo queda mezclado y no hay forma de ver reportes por contexto.

### Cómo funciona

Un **espacio de trabajo** agrupa proyectos relacionados. El usuario lo nombra libremente ("Miravalles", "Empresa ABC", "Casa").

**Reglas:**
- Todo proyecto pertenece a exactamente un espacio de trabajo.
- Un usuario puede tener múltiples espacios de trabajo.
- Un espacio de trabajo puede tener múltiples miembros del sistema (para acceso compartido al espacio).
- Los miembros del workspace no heredan acceso automático a cada proyecto dentro de él.
- Se pueden generar reportes consolidados de todos los proyectos de un workspace en una moneda de referencia.

### Jerarquía resultante

```
Usuario
│
├── Partners (global)
│     ├── Nondier Amariles   → Cuenta SINPE Harold, Tarjeta Visa
│     └── Argelida Restrepo  → Cuenta SINPE personal
│
└── Espacios de trabajo
      └── "Miravalles"
            ├── Proyecto "Compra del lote"     → partners: [Nondier]
            └── Proyecto "Gastos compartidos"  → partners: [Nondier, Argelida]
```

---

## 4. Módulo de socios

### Cómo se activa

- **1 partner asignado al proyecto** → módulo de socios desactivado. Comportamiento idéntico al actual.
- **2 o más partners asignados** → módulo disponible. El owner lo activa con un toggle en la configuración del proyecto.

Una vez activo: aparece el tab **"Socios"** y los formularios de gastos/ingresos muestran la sección de splits.

### Lógica del balance — base en splits

El balance se construye desde los splits: qué porción de cada movimiento se asignó a cada partner. El monto total pagado no entra en la fórmula — solo importan los splits cruzados.

**Componente 1 — Gastos:**

```
Para cada gasto pagado con cuenta del partner A:
  → Splits asignados a OTROS = lo que esos otros le deben a A

Para cada gasto pagado con cuenta de OTROS:
  → Split asignado a A = lo que A les debe

Saldo gastos A =
  SUM(splits de otros en gastos que pagó A)
  - SUM(split de A en gastos que pagaron otros)
```

**Ejemplo:**
```
Gasto $300 — paga A (50% A / 50% B) → B le debe $150 a A
Gasto $200 — paga B (50% A / 50% B) → A le debe $100 a B
Saldo A = $150 − $100 = +$50
```

Si A asume el 100% de un gasto, ese gasto no genera deuda con nadie.

**Componente 2 — Ingresos:**

```
Saldo ingresos A =
  SUM(split de A en ingresos recibidos por otros)
  - SUM(splits de otros en ingresos recibidos por A)
```

**Componente 3 — Liquidaciones directas:**

```
Saldo liquidaciones A = recibidas − pagadas
```

**Balance neto:**

```
Balance A = Saldo gastos + Saldo ingresos + Saldo liquidaciones
Positivo → otros le deben a A.
Negativo → A debe a otros.
```

### División equitativa por defecto

Al activar splits en un formulario, el sistema pre-llena 100/N% entre los partners del proyecto. El usuario ajusta si el caso lo requiere.

- 2 partners → 50% / 50%
- 3 partners → 33.33% / 33.33% / 33.34%
- N partners → 100/N% para cada uno

### Nivelación del balance

- **Liquidación directa:** un partner le paga al otro fuera del sistema y se registra como `partner_settlement`.
- **Compensación orgánica:** el que debe más paga más gastos futuros con splits ajustados; el balance se nivela solo.

---

## 5. Fase 1 — Correcciones críticas

> **Sin cambios de modelos. Deployable de inmediato.**

### 5.1 Fix del Dashboard multi-proyecto

**Solución:** `GET /dashboard?project_id=uuid` — toda agregación filtrada por ese proyecto. Montos en `prj_currency_code` usando `inc_converted_amount` / `exp_converted_amount`.

**Frontend:** selector de proyecto en el header, persistido en `localStorage`, código de moneda visible junto a cada cifra.

**Impacto en schema:** Ninguno.

### 5.2 Fix del balance por método de pago

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

Balance en `pmt_currency` (moneda de la cuenta, no del proyecto).

---

## 6. Fase 2 — Partners + Espacios de trabajo

### 6.1 Nueva tabla: `partners`

Simple y directa. Sin vínculos al sistema de usuarios, sin flags especiales.

```sql
CREATE TABLE public.partners (
  ptr_id UUID NOT NULL DEFAULT gen_random_uuid(),
  ptr_owner_user_id UUID NOT NULL,   -- usuario del sistema que creó este partner
  ptr_name VARCHAR(255) NOT NULL,
  ptr_email VARCHAR(255) NULL,
  ptr_phone VARCHAR(50) NULL,
  ptr_notes STRING NULL,
  ptr_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  ptr_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  ptr_is_deleted BOOL NOT NULL DEFAULT false,
  ptr_deleted_at TIMESTAMPTZ NULL,
  ptr_deleted_by_user_id UUID NULL,
  CONSTRAINT partners_pkey PRIMARY KEY (ptr_id ASC),
  CONSTRAINT ptr_owner_fkey FOREIGN KEY (ptr_owner_user_id)
    REFERENCES public.users(usr_id),
  CONSTRAINT ptr_deleted_by_fkey FOREIGN KEY (ptr_deleted_by_user_id)
    REFERENCES public.users(usr_id),
  INDEX idx_ptr_owner_user_id (ptr_owner_user_id ASC),
  INDEX idx_ptr_is_deleted (ptr_is_deleted ASC)
);
COMMENT ON TABLE public.partners IS
  'Contactos financieros del usuario. Dueños de métodos de pago y asignados a proyectos.';
```

### 6.2 Modificación en `payment_methods`

Se agrega `pmt_owner_partner_id` que reemplaza progresivamente a `pmt_owner_user_id`:

```sql
ALTER TABLE public.payment_methods
  ADD COLUMN pmt_owner_partner_id UUID NULL
    REFERENCES public.partners(ptr_id);

CREATE INDEX idx_pmt_owner_partner_id
  ON public.payment_methods (pmt_owner_partner_id ASC)
  WHERE pmt_owner_partner_id IS NOT NULL;
```

Durante la transición ambas columnas coexisten. Una vez completada la migración y actualizados los endpoints, `pmt_owner_user_id` se depreca con `ALTER TABLE DROP COLUMN`.

### 6.3 Nueva tabla: `project_partners`

Reemplaza a `project_payment_methods` como la forma de controlar qué entidades financieras participan en un proyecto. Los métodos de pago disponibles se derivan de los partners asignados.

```sql
CREATE TABLE public.project_partners (
  ptp_id UUID NOT NULL DEFAULT gen_random_uuid(),
  ptp_project_id UUID NOT NULL,
  ptp_partner_id UUID NOT NULL,
  ptp_added_by_user_id UUID NOT NULL,
  ptp_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  ptp_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  ptp_is_deleted BOOL NOT NULL DEFAULT false,
  ptp_deleted_at TIMESTAMPTZ NULL,
  ptp_deleted_by_user_id UUID NULL,
  CONSTRAINT project_partners_pkey PRIMARY KEY (ptp_id ASC),
  CONSTRAINT ptp_project_fkey FOREIGN KEY (ptp_project_id)
    REFERENCES public.projects(prj_id),
  CONSTRAINT ptp_partner_fkey FOREIGN KEY (ptp_partner_id)
    REFERENCES public.partners(ptr_id),
  CONSTRAINT ptp_added_by_fkey FOREIGN KEY (ptp_added_by_user_id)
    REFERENCES public.users(usr_id),
  CONSTRAINT ptp_deleted_by_fkey FOREIGN KEY (ptp_deleted_by_user_id)
    REFERENCES public.users(usr_id),
  UNIQUE INDEX uq_ptp_project_partner_active (ptp_project_id ASC, ptp_partner_id ASC)
    WHERE ptp_is_deleted = false,
  INDEX idx_ptp_project_id (ptp_project_id ASC),
  INDEX idx_ptp_partner_id (ptp_partner_id ASC),
  INDEX idx_ptp_is_deleted (ptp_is_deleted ASC)
);
COMMENT ON TABLE public.project_partners IS
  'Partners asignados a un proyecto. Los métodos de pago disponibles se derivan de estos partners.';
```

### 6.4 Cómo se determinan los métodos de pago disponibles en un proyecto

```sql
SELECT pm.*
FROM payment_methods pm
JOIN partners ptr ON ptr.ptr_id = pm.pmt_owner_partner_id
JOIN project_partners ptp ON ptp.ptp_partner_id = ptr.ptr_id
WHERE ptp.ptp_project_id = :project_id
  AND ptp.ptp_is_deleted = false
  AND pm.pmt_is_deleted = false;
```

Si se agrega un nuevo método de pago a un partner que ya está en el proyecto, queda disponible de inmediato sin pasos adicionales.

### 6.5 Nuevas tablas: `workspaces` y `workspace_members`

```sql
CREATE TABLE public.workspaces (
  wks_id UUID NOT NULL DEFAULT gen_random_uuid(),
  wks_name VARCHAR(255) NOT NULL,
  wks_owner_user_id UUID NOT NULL,
  wks_description STRING NULL,
  wks_color VARCHAR(7) NULL,    -- color hex (#3B82F6), para identificación visual
  wks_icon VARCHAR(50) NULL,
  wks_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wks_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wks_is_deleted BOOL NOT NULL DEFAULT false,
  wks_deleted_at TIMESTAMPTZ NULL,
  wks_deleted_by_user_id UUID NULL,
  CONSTRAINT workspaces_pkey PRIMARY KEY (wks_id ASC),
  CONSTRAINT wks_owner_fkey FOREIGN KEY (wks_owner_user_id)
    REFERENCES public.users(usr_id),
  CONSTRAINT wks_deleted_by_fkey FOREIGN KEY (wks_deleted_by_user_id)
    REFERENCES public.users(usr_id),
  INDEX idx_wks_owner_user_id (wks_owner_user_id ASC),
  INDEX idx_wks_is_deleted (wks_is_deleted ASC)
);
COMMENT ON TABLE public.workspaces IS
  'Espacios de trabajo que agrupan proyectos relacionados.';

CREATE TABLE public.workspace_members (
  wkm_id UUID NOT NULL DEFAULT gen_random_uuid(),
  wkm_workspace_id UUID NOT NULL,
  wkm_user_id UUID NOT NULL,
  wkm_role VARCHAR(20) NOT NULL DEFAULT 'member', -- 'owner' | 'member'
  wkm_joined_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wkm_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wkm_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wkm_is_deleted BOOL NOT NULL DEFAULT false,
  wkm_deleted_at TIMESTAMPTZ NULL,
  wkm_deleted_by_user_id UUID NULL,
  CONSTRAINT workspace_members_pkey PRIMARY KEY (wkm_id ASC),
  CONSTRAINT wkm_workspace_fkey FOREIGN KEY (wkm_workspace_id)
    REFERENCES public.workspaces(wks_id),
  CONSTRAINT wkm_user_fkey FOREIGN KEY (wkm_user_id)
    REFERENCES public.users(usr_id),
  CONSTRAINT wkm_deleted_by_fkey FOREIGN KEY (wkm_deleted_by_user_id)
    REFERENCES public.users(usr_id),
  UNIQUE INDEX uq_wkm_workspace_user_active (wkm_workspace_id ASC, wkm_user_id ASC)
    WHERE wkm_is_deleted = false,
  INDEX idx_wkm_workspace_id (wkm_workspace_id ASC),
  INDEX idx_wkm_user_id (wkm_user_id ASC),
  INDEX idx_wkm_is_deleted (wkm_is_deleted ASC),
  CONSTRAINT wkm_role_check CHECK (wkm_role IN ('owner':::STRING, 'member':::STRING))
);
```

### 6.6 Modificación en `projects`

```sql
ALTER TABLE public.projects
  ADD COLUMN prj_workspace_id UUID NOT NULL
    REFERENCES public.workspaces(wks_id),
  ADD COLUMN prj_partners_enabled BOOL NOT NULL DEFAULT false;

CREATE INDEX idx_prj_workspace_id ON public.projects (prj_workspace_id ASC);
```

### 6.7 Migración completa de datos existentes

```sql
-- PASO 1: Crear un partner por cada usuario existente (con su nombre)
INSERT INTO partners (ptr_owner_user_id, ptr_name)
SELECT usr_id, usr_full_name
FROM users
WHERE usr_is_deleted = false;

-- PASO 2: Asignar ese partner a todos sus métodos de pago
UPDATE payment_methods pm
SET pmt_owner_partner_id = ptr.ptr_id
FROM partners ptr
WHERE ptr.ptr_owner_user_id = pm.pmt_owner_user_id
  AND pm.pmt_is_deleted = false;

-- PASO 3: Crear workspace "General" para cada usuario con proyectos
INSERT INTO workspaces (wks_name, wks_owner_user_id)
SELECT 'General', prj_owner_user_id
FROM projects
WHERE prj_is_deleted = false
GROUP BY prj_owner_user_id;

-- PASO 4: Owner como miembro del workspace
INSERT INTO workspace_members (wkm_workspace_id, wkm_user_id, wkm_role)
SELECT wks_id, wks_owner_user_id, 'owner'
FROM workspaces;

-- PASO 5: Asignar proyectos a su workspace
UPDATE projects p
SET prj_workspace_id = w.wks_id
FROM workspaces w
WHERE w.wks_owner_user_id = p.prj_owner_user_id
  AND p.prj_is_deleted = false;

-- PASO 6: Poblar project_partners desde project_payment_methods existentes
INSERT INTO project_partners (ptp_project_id, ptp_partner_id, ptp_added_by_user_id)
SELECT DISTINCT
  ppm.ppm_project_id,
  pm.pmt_owner_partner_id,
  ppm.ppm_added_by_user_id
FROM project_payment_methods ppm
JOIN payment_methods pm ON pm.pmt_id = ppm.ppm_payment_method_id
WHERE pm.pmt_owner_partner_id IS NOT NULL
  AND pm.pmt_is_deleted = false;

-- PASO 7: Splits del 100% para movimientos existentes
-- (auto-split al partner dueño de la cuenta usada en cada movimiento)
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

## 7. Fase 3 — Módulo de socios activo

### 7.1 Splits — referencian `partners`

Los splits apuntan a partners, no a usuarios del sistema. El balance pertenece a la entidad financiera.

```sql
CREATE TABLE public.expense_splits (
  exs_id UUID NOT NULL DEFAULT gen_random_uuid(),
  exs_expense_id UUID NOT NULL,
  exs_partner_id UUID NOT NULL,           -- partner que asume esta porción
  exs_split_type VARCHAR(10) NOT NULL,    -- 'percentage' | 'fixed'
  exs_split_value DECIMAL(14,4) NOT NULL, -- % o monto fijo en moneda del gasto
  exs_resolved_amount DECIMAL(14,2) NOT NULL, -- siempre en moneda original del gasto
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

### 7.2 Liquidaciones directas entre partners

```sql
CREATE TABLE public.partner_settlements (
  pst_id UUID NOT NULL DEFAULT gen_random_uuid(),
  pst_project_id UUID NOT NULL,
  pst_from_partner_id UUID NOT NULL,  -- partner que paga
  pst_to_partner_id UUID NOT NULL,    -- partner que recibe
  pst_amount DECIMAL(14,2) NOT NULL,
  pst_currency VARCHAR(3) NOT NULL,
  pst_exchange_rate DECIMAL(18,6) NOT NULL DEFAULT 1.000000:::DECIMAL,
  pst_converted_amount DECIMAL(14,2) NOT NULL, -- en moneda base del proyecto
  pst_settlement_date DATE NOT NULL,
  pst_description STRING NULL,
  pst_notes STRING NULL,
  pst_created_by_user_id UUID NOT NULL,
  pst_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  pst_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  pst_is_deleted BOOL NOT NULL DEFAULT false,
  pst_deleted_at TIMESTAMPTZ NULL,
  pst_deleted_by_user_id UUID NULL,
  CONSTRAINT partner_settlements_pkey PRIMARY KEY (pst_id ASC),
  CONSTRAINT pst_project_fkey FOREIGN KEY (pst_project_id)
    REFERENCES public.projects(prj_id),
  CONSTRAINT pst_from_partner_fkey FOREIGN KEY (pst_from_partner_id)
    REFERENCES public.partners(ptr_id),
  CONSTRAINT pst_to_partner_fkey FOREIGN KEY (pst_to_partner_id)
    REFERENCES public.partners(ptr_id),
  CONSTRAINT pst_currency_fkey FOREIGN KEY (pst_currency)
    REFERENCES public.currencies(cur_code),
  CONSTRAINT pst_created_by_fkey FOREIGN KEY (pst_created_by_user_id)
    REFERENCES public.users(usr_id),
  CONSTRAINT pst_deleted_by_fkey FOREIGN KEY (pst_deleted_by_user_id)
    REFERENCES public.users(usr_id),
  CONSTRAINT pst_different_partners CHECK (pst_from_partner_id != pst_to_partner_id),
  INDEX idx_pst_project_id (pst_project_id ASC),
  INDEX idx_pst_from_partner_id (pst_from_partner_id ASC),
  INDEX idx_pst_to_partner_id (pst_to_partner_id ASC),
  INDEX idx_pst_date (pst_settlement_date ASC),
  INDEX idx_pst_is_deleted (pst_is_deleted ASC),
  CONSTRAINT pst_amount_positive CHECK (pst_amount > 0:::DECIMAL)
);
COMMENT ON TABLE public.partner_settlements IS
  'Pagos directos entre partners para saldar deudas. No afectan métodos de pago del proyecto.';
```

### 7.3 Reglas de negocio para splits

1. **Auto-split por defecto:** Sin splits enviados → split 100% al partner dueño del método de pago del movimiento.
2. **Pre-llenado equitativo:** Al activar la sección de splits, 100/N% entre los partners del proyecto.
3. **Validación de suma:** `SUM(exs_resolved_amount) = exp_original_amount`. Error → `400 splits_dont_sum_to_total`.
4. **Validación de porcentajes:** Si `split_type = 'percentage'`, suma = 100 exactamente.
5. **Validación de partners:** Todos los `exs_partner_id` deben ser partners activos del proyecto en `project_partners`.
6. **Edición:** Splits `percentage` → recalcular `resolved_amount` automático. Splits `fixed` → exigir corrección manual.
7. **Cada uno paga por separado:** Se registran dos gastos distintos, cada uno al 100% del que pagó. No es un split.

### 7.4 Flujo de UI al registrar un gasto

```
Título: Honorarios abogada
Monto:  $300.00 USD
Cuenta: SINPE Nondier  ← cuentas disponibles = las de los partners del proyecto

[ ] Dividir entre socios    ← toggle, por defecto cerrado
    ▼ (pre-llena equitativamente)

    Tipo: (•) Porcentaje  ( ) Monto fijo

    Nondier     [  50  ] %    = $150.00
    Argelida    [  50  ] %    = $150.00
                    Suma: 100% = $300.00  ✓

    [+ Agregar partner]
```

### 7.5 Tab "Socios" — Vista general

```
┌────────────────────────────────────────────────────────────────┐
│ SOCIOS DEL PROYECTO — Gastos compartidos Miravalles            │
├──────────────────┬──────────┬──────────┬─────────┬────────────┤
│ Partner          │ Ha pag.  │ Le deben │ Debe    │ Balance    │
├──────────────────┼──────────┼──────────┼─────────┼────────────┤
│ Nondier          │ $931.78  │ $465.89  │ $0      │ +$465.89  │
│ Argelida         │ $0       │ $0       │ $465.89 │ −$465.89  │
└──────────────────┴──────────┴──────────┴─────────┴────────────┘

Liquidación sugerida:
  Argelida → Nondier   $465.89 USD   [Registrar pago]
```

---

## 8. Fase 4 — Dashboard, balances y reportes

### 8.1 Balance completo del proyecto

`GET /projects/:id/balance`

```json
{
  "project_id": "uuid",
  "workspace": "Miravalles",
  "currency": "USD",
  "total_income": 11470.00,
  "total_expenses": 931.78,
  "net_balance": 10538.22,
  "by_category": [
    { "category": "Aporte Nondier", "type": "income",  "total": 11470.00 },
    { "category": "Honorarios",     "type": "expense", "total": 394.67,
      "budget": 500.00, "budget_used_pct": 78.9 }
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
    },
    {
      "partner": "Argelida",
      "paid_physically": 0,
      "others_owe_him": 0,
      "he_owes_others": 465.89,
      "income_entitled": 0,
      "income_received": 0,
      "settlements_received": 0,
      "settlements_paid": 0,
      "net_balance": -465.89
    }
  ]
}
```

### 8.2 Query del balance por partner

```sql
WITH
gastos_otros_deben AS (
  -- Splits de OTROS en gastos que pagó este partner
  SELECT
    pm.pmt_owner_partner_id AS acreedor_id,
    SUM(exs.exs_resolved_amount) AS total
  FROM expense_splits exs
  JOIN expenses e ON e.exp_id = exs.exs_expense_id
  JOIN payment_methods pm ON pm.pmt_id = e.exp_payment_method_id
  WHERE e.exp_project_id = :project_id
    AND e.exp_is_deleted = false
    AND exs.exs_partner_id != pm.pmt_owner_partner_id
  GROUP BY pm.pmt_owner_partner_id
),
gastos_partner_debe AS (
  -- Splits de este partner en gastos que pagaron OTROS
  SELECT
    exs.exs_partner_id AS deudor_id,
    SUM(exs.exs_resolved_amount) AS total
  FROM expense_splits exs
  JOIN expenses e ON e.exp_id = exs.exs_expense_id
  JOIN payment_methods pm ON pm.pmt_id = e.exp_payment_method_id
  WHERE e.exp_project_id = :project_id
    AND e.exp_is_deleted = false
    AND exs.exs_partner_id != pm.pmt_owner_partner_id
  GROUP BY exs.exs_partner_id
),
ingresos_corresponden AS (
  SELECT ins.ins_partner_id AS partner_id,
         SUM(ins.ins_resolved_amount) AS total
  FROM income_splits ins
  JOIN incomes i ON i.inc_id = ins.ins_income_id
  WHERE i.inc_project_id = :project_id AND i.inc_is_deleted = false
  GROUP BY ins.ins_partner_id
),
ingresos_recibidos AS (
  SELECT pm.pmt_owner_partner_id AS partner_id,
         SUM(i.inc_converted_amount) AS total
  FROM incomes i
  JOIN payment_methods pm ON pm.pmt_id = i.inc_payment_method_id
  WHERE i.inc_project_id = :project_id AND i.inc_is_deleted = false
  GROUP BY pm.pmt_owner_partner_id
),
liquidaciones AS (
  SELECT pst_to_partner_id AS partner_id,
         SUM(pst_converted_amount) AS recibidas,
         0 AS pagadas
  FROM partner_settlements
  WHERE pst_project_id = :project_id AND pst_is_deleted = false
  GROUP BY pst_to_partner_id
  UNION ALL
  SELECT pst_from_partner_id,
         0,
         SUM(pst_converted_amount)
  FROM partner_settlements
  WHERE pst_project_id = :project_id AND pst_is_deleted = false
  GROUP BY pst_from_partner_id
)
SELECT
  ptr.ptr_id,
  ptr.ptr_name,
  COALESCE(god.total, 0)          AS others_owe_him,
  COALESCE(gpd.total, 0)          AS he_owes_others,
  COALESCE(ic.total, 0)           AS income_entitled,
  COALESCE(ir.total, 0)           AS income_received,
  COALESCE(SUM(lq.recibidas), 0)  AS settlements_received,
  COALESCE(SUM(lq.pagadas), 0)    AS settlements_paid,
  (COALESCE(god.total, 0) - COALESCE(gpd.total, 0))
  + (COALESCE(ic.total, 0)  - COALESCE(ir.total, 0))
  + (COALESCE(SUM(lq.recibidas), 0) - COALESCE(SUM(lq.pagadas), 0))
    AS net_balance
FROM project_partners ptp
JOIN partners ptr ON ptr.ptr_id = ptp.ptp_partner_id
LEFT JOIN gastos_otros_deben  god ON god.acreedor_id = ptr.ptr_id
LEFT JOIN gastos_partner_debe gpd ON gpd.deudor_id   = ptr.ptr_id
LEFT JOIN ingresos_corresponden ic ON ic.partner_id  = ptr.ptr_id
LEFT JOIN ingresos_recibidos    ir ON ir.partner_id  = ptr.ptr_id
LEFT JOIN liquidaciones lq ON lq.partner_id = ptr.ptr_id
WHERE ptp.ptp_project_id = :project_id
  AND ptp.ptp_is_deleted = false
GROUP BY ptr.ptr_id, ptr.ptr_name,
         god.total, gpd.total, ic.total, ir.total;
```

### 8.3 Liquidaciones sugeridas

`GET /projects/:id/partners/settlement-suggestions`

Algoritmo: ordenar por `net_balance`, emparejar mayor acreedor con mayor deudor, cancelar hasta agotar uno, repetir.

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

### 8.4 Resumen de workspace

`GET /workspaces/:id/summary`

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

---

## 9. Cambios de base de datos completos (SQL)

### 9.1 Nuevas tablas

| Tabla | Propósito |
|---|---|
| `partners` | Contactos financieros globales del usuario |
| `project_partners` | Partners asignados a cada proyecto |
| `workspaces` | Espacios de trabajo que agrupan proyectos |
| `workspace_members` | Usuarios del sistema con acceso a un workspace |
| `expense_splits` | División de gastos entre partners |
| `income_splits` | División de ingresos entre partners |
| `partner_settlements` | Liquidaciones directas entre partners |

### 9.2 Tablas modificadas

| Tabla | Cambio |
|---|---|
| `payment_methods` | + `pmt_owner_partner_id UUID → partners(ptr_id)` |
| `projects` | + `prj_workspace_id UUID NOT NULL`, + `prj_partners_enabled BOOL DEFAULT false` |

### 9.3 Tablas deprecadas (gradualmente)

| Tabla | Reemplazada por | Cuándo eliminar |
|---|---|---|
| `project_payment_methods` | `project_partners` | Después de completar migración y actualizar todos los endpoints |

### 9.4 Tablas NO modificadas

`expenses`, `incomes`, `project_members`, `categories`, `obligations`,
`project_budgets`, `currencies`, `plans`, `users`, `project_alternative_currencies`.

---

## 10. Resumen de impacto por capa

### Plan de implementación por fases

| Fase | Entregables | Semanas | Riesgo |
|---|---|---|---|
| **Fase 1** | Fix dashboard + balance por método de pago | 1 | Bajo |
| **Fase 2a** | Tabla `partners` + CRUD global + migración `pmt_owner_partner_id` | 2 | Medio — migración FK |
| **Fase 2b** | `workspaces` + `workspace_members` + migración proyectos | 1 | Bajo |
| **Fase 2c** | `project_partners` + UI de asignación al crear/editar proyecto | 1-2 | Bajo |
| **Fase 3a** | `expense_splits` + `income_splits` + auto-split en movimientos | 1 | Bajo |
| **Fase 3b** | Toggle `partners_enabled` + UI de splits con pre-llenado equitativo | 1-2 | Medio |
| **Fase 3c** | Tab Socios + `partner_settlements` | 2 | Medio |
| **Fase 4** | Balance completo + liquidaciones sugeridas + resumen workspace | 2 | Bajo |

### Nuevos endpoints

| Método | Ruta | Fase | Descripción |
|---|---|---|---|
| `GET` | `/dashboard?project_id=` | 1 | Dashboard por proyecto |
| `GET` | `/payment-methods/:id/balance?project_id=` | 1 | Balance de cuenta |
| `GET/POST/PATCH/DELETE` | `/partners` | 2a | CRUD global de partners |
| `GET` | `/partners/:id/payment-methods` | 2a | Cuentas de un partner |
| `POST` | `/workspaces` | 2b | Crear workspace |
| `GET` | `/workspaces` | 2b | Listar workspaces del usuario |
| `GET` | `/workspaces/:id/summary` | 2b | Resumen consolidado |
| `GET/POST/DELETE` | `/projects/:id/partners` | 2c | Partners de un proyecto |
| `GET` | `/projects/:id/available-payment-methods` | 2c | Cuentas disponibles (derivadas de partners) |
| `PATCH` | `/projects/:id/settings` | 3a | Activar `partners_enabled` |
| `POST` | `/expenses` (modificado) | 3b | Acepta array `splits` opcional |
| `POST` | `/incomes` (modificado) | 3b | Acepta array `splits` opcional |
| `GET` | `/projects/:id/partners/balance` | 3c | Balances de socios |
| `GET` | `/projects/:id/partners/:pid/history` | 3c | Historial de un partner |
| `POST` | `/projects/:id/partner-settlements` | 3c | Registrar liquidación |
| `GET` | `/projects/:id/balance` | 4 | Balance completo |
| `GET` | `/projects/:id/partners/settlement-suggestions` | 4 | Liquidaciones sugeridas |

### Consideraciones de plan y permisos

- Partners, workspaces: disponibles para todos los planes.
- Módulo de socios: planes con `pln_can_share_projects = true`.
- Resumen consolidado workspace: `pln_can_use_advanced_reports = true`.
- Exportación: `pln_can_export_data = true`.

---

## Notas de implementación

**Partners como contactos puros:** No tienen vínculo al sistema de usuarios. El usuario los crea con un nombre y opcionalmente email o teléfono de referencia. Puede crear uno que lo represente a él mismo con cualquier nombre que prefiera. La migración crea automáticamente un partner por cada usuario existente usando su nombre completo.

**Derivar métodos de pago desde partners:** La query de cuentas disponibles para un proyecto no consulta `project_payment_methods`, sino `project_partners → partners → payment_methods`. Si el usuario agrega una nueva cuenta a un partner ya asignado al proyecto, queda disponible de inmediato.

**Migración de `pmt_owner_user_id`:** Ambas columnas coexisten durante la transición. Los nuevos endpoints usan `pmt_owner_partner_id`. Una vez migrados todos los endpoints y confirmado que no hay regresiones, se elimina `pmt_owner_user_id` con `ALTER TABLE DROP COLUMN`.

**Balance usa partner, no user:** El balance financiero pertenece a la entidad financiera. Un partner sin cuenta en el sistema puede tener saldo — el sistema lo registra igual que cualquier otro.

**`exs_resolved_amount` en moneda del gasto:** Se almacena en `exp_original_currency`. Para mostrar en moneda del proyecto se multiplica por `exp_exchange_rate`. No duplicar el monto convertido en los splits.

**Soft delete:** Todos los JOINs de cálculo filtran `is_deleted = false`. Los splits quedan intactos al borrar un movimiento pero se excluyen automáticamente.

**Workspace en la navegación:** Un solo workspace → se selecciona automáticamente. Varios workspaces → selector en el sidebar, persistido en sesión.