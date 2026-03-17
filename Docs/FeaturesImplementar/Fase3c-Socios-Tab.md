# Fase 3c — Tab Socios y Liquidaciones

> Prerrequisito: Fase 3b completada.
> Riesgo: **Medio** | Estimado: 2 semanas

---

## Objetivo

Implementar el tab "Socios" con balances por partner y registrar liquidaciones directas entre partners (`partner_settlements`).

---

## 3c.1 Nueva tabla: `partner_settlements`

### Script SQL (`Scripts/add_partner_settlements_table.sql`)

```sql
CREATE TABLE public.partner_settlements (
  pst_id UUID NOT NULL DEFAULT gen_random_uuid(),
  pst_project_id UUID NOT NULL,
  pst_from_partner_id UUID NOT NULL,
  pst_to_partner_id UUID NOT NULL,
  pst_amount DECIMAL(14,2) NOT NULL,
  pst_currency VARCHAR(3) NOT NULL,
  pst_exchange_rate DECIMAL(18,6) NOT NULL DEFAULT 1.000000:::DECIMAL,
  pst_converted_amount DECIMAL(14,2) NOT NULL,
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
  CONSTRAINT pst_project_fkey FOREIGN KEY (pst_project_id) REFERENCES public.projects(prj_id),
  CONSTRAINT pst_from_partner_fkey FOREIGN KEY (pst_from_partner_id) REFERENCES public.partners(ptr_id),
  CONSTRAINT pst_to_partner_fkey FOREIGN KEY (pst_to_partner_id) REFERENCES public.partners(ptr_id),
  CONSTRAINT pst_currency_fkey FOREIGN KEY (pst_currency) REFERENCES public.currencies(cur_code),
  CONSTRAINT pst_created_by_fkey FOREIGN KEY (pst_created_by_user_id) REFERENCES public.users(usr_id),
  CONSTRAINT pst_deleted_by_fkey FOREIGN KEY (pst_deleted_by_user_id) REFERENCES public.users(usr_id),
  CONSTRAINT pst_different_partners CHECK (pst_from_partner_id != pst_to_partner_id),
  CONSTRAINT pst_amount_positive CHECK (pst_amount > 0:::DECIMAL),
  INDEX idx_pst_project_id (pst_project_id ASC),
  INDEX idx_pst_from_partner_id (pst_from_partner_id ASC),
  INDEX idx_pst_to_partner_id (pst_to_partner_id ASC),
  INDEX idx_pst_date (pst_settlement_date ASC),
  INDEX idx_pst_is_deleted (pst_is_deleted ASC)
);
COMMENT ON TABLE public.partner_settlements IS
  'Pagos directos entre partners para saldar deudas. No afectan métodos de pago del proyecto.';
```

---

## 3c.2 Endpoint de balances por partner

`GET /projects/:id/partners/balance`

### Lógica del balance (tres componentes)

**Componente 1 — Gastos:**

```
Para cada gasto pagado con cuenta del partner A:
  → Splits a OTROS = lo que esos otros le deben a A

Para cada gasto pagado con cuenta de OTROS:
  → Split de A = lo que A les debe

Saldo gastos A =
  SUM(splits de otros en gastos que pagó A)
  - SUM(split de A en gastos que pagaron otros)
```

**Componente 2 — Ingresos:**

```
Saldo ingresos A =
  SUM(split de A en ingresos recibidos por OTROS)
  - SUM(splits de OTROS en ingresos recibidos por A)
```

**Componente 3 — Liquidaciones:**

```
Saldo liquidaciones A = recibidas - pagadas
```

**Balance neto:**

```
Balance A = Saldo gastos + Saldo ingresos + Saldo liquidaciones
Positivo → otros le deben a A
Negativo → A debe a otros
```

### Response

```json
{
  "project_id": "uuid",
  "currency": "USD",
  "partners": [
    {
      "partner_id": "uuid",
      "partner_name": "Nondier",
      "paid_physically": 931.78,
      "others_owe_him": 465.89,
      "he_owes_others": 0,
      "settlements_received": 0,
      "settlements_paid": 0,
      "net_balance": 465.89
    },
    {
      "partner_id": "uuid",
      "partner_name": "Argelida",
      "paid_physically": 0,
      "others_owe_him": 0,
      "he_owes_others": 465.89,
      "settlements_received": 0,
      "settlements_paid": 0,
      "net_balance": -465.89
    }
  ]
}
```

---

## 3c.3 Historial de un partner

`GET /projects/:id/partners/:partnerId/history`

Lista todas las transacciones donde el partner tiene splits, más sus liquidaciones.

```json
{
  "partner_id": "uuid",
  "partner_name": "Argelida",
  "transactions": [
    {
      "type": "expense",
      "transaction_id": "uuid",
      "title": "Honorarios abogada",
      "date": "2026-03-10",
      "split_amount": 150.00,
      "split_type": "percentage",
      "split_value": 50,
      "paying_partner": "Nondier"
    }
  ],
  "settlements": [
    {
      "type": "settlement_paid",
      "id": "uuid",
      "date": "2026-03-15",
      "amount": 150.00,
      "currency": "USD",
      "to_partner": "Nondier"
    }
  ]
}
```

---

## 3c.4 Liquidaciones directas

### `POST /projects/:id/partner-settlements`

```json
{
  "from_partner_id": "uuid",
  "to_partner_id": "uuid",
  "amount": 465.89,
  "currency": "USD",
  "exchange_rate": 1.0,
  "settlement_date": "2026-03-15",
  "description": "Saldo cuenta lote"
}
```

`pst_converted_amount` = `amount * exchange_rate`, calculado en el servidor.

### `GET /projects/:id/partner-settlements`

Lista las liquidaciones del proyecto (activas).

### `DELETE /projects/:id/partner-settlements/:id`

Soft-delete de una liquidación.

---

## 3c.5 Archivos a crear/modificar

| Capa | Archivo | Acción |
|---|---|---|
| Script SQL | `Scripts/add_partner_settlements_table.sql` | Crear |
| Model | `Models/PartnerSettlement.cs` | Crear |
| Config EF | `Configurations/PartnerSettlementConfiguration.cs` | Crear |
| DbContext | `Data/AppDbContext.cs` | Agregar `DbSet<PartnerSettlement>` |
| DTO | `DTOs/Partners/PartnerBalanceDto.cs` | Crear |
| DTO | `DTOs/Partners/PartnerHistoryDto.cs` | Crear |
| DTO | `DTOs/Partners/CreateSettlementDto.cs` | Crear |
| DTO | `DTOs/Partners/SettlementDto.cs` | Crear |
| Repository | `Repositories/PartnerSettlementRepository.cs` | Crear |
| Repository | `Repositories/PartnerBalanceRepository.cs` | Crear (lógica de la query compleja) |
| Service | `Services/PartnerBalanceService.cs` | Crear |
| Service | `Services/PartnerSettlementService.cs` | Crear |
| Controller | `Controllers/ProjectPartnersController.cs` | Crear (balance, history, settlements) |
| Extensions | `Extensions/ServiceCollectionExtensions.cs` | Registrar repos y services |

---

## 3c.6 Reglas de negocio

- Las liquidaciones son entre partners del proyecto; validar que ambos sean partners activos del proyecto.
- `from_partner_id != to_partner_id` (constraint en DB).
- Las liquidaciones no afectan `payment_methods` del proyecto — son registros directos.
- El balance muestra el neto: si Argelida le pagó $100 a Nondier y le debía $465.89, el saldo pasa a $365.89.
- El módulo de socios solo aplica si `prj_partners_enabled = true`.
- Acceso: solo miembros del proyecto con rol `owner` o `editor`.

---

## Criterios de aceptación

- [ ] `GET /projects/:id/partners/balance` devuelve balances correctos.
- [ ] Balance cambia correctamente al registrar una liquidación.
- [ ] Historial de partner lista sus transacciones con splits y sus liquidaciones.
- [ ] `POST /projects/:id/partner-settlements` registra la liquidación.
- [ ] Soft-delete de liquidación revierte su efecto en el balance.
- [ ] El balance solo es accesible cuando `prj_partners_enabled = true`.
