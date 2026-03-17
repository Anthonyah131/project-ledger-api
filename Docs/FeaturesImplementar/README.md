# Roadmap de Implementación — Project Ledger SaaS

> Documento maestro de planificación. Para el detalle de cada fase ver el archivo correspondiente.

---

## Resumen de fases

| Fase | Archivo | Descripción | Riesgo | Prerrequisito |
|---|---|---|---|---|
| **1** | [Fase1-Correcciones-Criticas.md](./Fase1-Correcciones-Criticas.md) | Fix dashboard multi-proyecto + balance por método de pago | Bajo | — |
| **2a** | [Fase2a-Partners.md](./Fase2a-Partners.md) | Entidad `partners` + CRUD global + migración `pmt_owner_partner_id` | Medio | Fase 1 (recomendado) |
| **2b** | [Fase2b-Workspaces.md](./Fase2b-Workspaces.md) | `workspaces` + `workspace_members` + migración de proyectos | Bajo | Fase 2a |
| **2c** | [Fase2c-ProjectPartners.md](./Fase2c-ProjectPartners.md) | `project_partners` + métodos de pago derivados + deprecar `project_payment_methods` | Bajo | Fase 2a |
| **3a** | [Fase3a-Splits-Core.md](./Fase3a-Splits-Core.md) | `expense_splits` + `income_splits` + auto-split en todos los movimientos | Bajo | Fase 2c |
| **3b** | [Fase3b-Splits-UI.md](./Fase3b-Splits-UI.md) | Toggle `partners_enabled` + endpoints aceptan array `splits` + pre-llenado equitativo | Medio | Fase 3a |
| **3c** | [Fase3c-Socios-Tab.md](./Fase3c-Socios-Tab.md) | Balance por partner + `partner_settlements` + historial | Medio | Fase 3b |
| **4** | [Fase4-Dashboard-Balance-Reportes.md](./Fase4-Dashboard-Balance-Reportes.md) | Balance completo + liquidaciones sugeridas + resumen workspace | Bajo | Fase 3c |

---

## Nuevas tablas por fase

| Tabla | Fase | Propósito |
|---|---|---|
| `partners` | 2a | Contactos financieros globales del usuario |
| `workspaces` | 2b | Espacios de trabajo que agrupan proyectos |
| `workspace_members` | 2b | Usuarios con acceso a un workspace |
| `project_partners` | 2c | Partners asignados a un proyecto |
| `expense_splits` | 3a | División de gastos entre partners |
| `income_splits` | 3a | División de ingresos entre partners |
| `partner_settlements` | 3c | Liquidaciones directas entre partners |

---

## Columnas nuevas en tablas existentes

| Tabla | Columna | Fase |
|---|---|---|
| `payment_methods` | `pmt_owner_partner_id` | 2a |
| `projects` | `prj_workspace_id` | 2b |
| `projects` | `prj_partners_enabled` | 2b |

---

## Tablas deprecadas

| Tabla | Reemplazada por | Fase de eliminación |
|---|---|---|
| `project_payment_methods` | `project_partners` | 2c |

---

## Nuevos endpoints por fase

| Fase | Método | Ruta | Descripción |
|---|---|---|---|
| 1 | `GET` | `/dashboard?project_id=` | Dashboard filtrado por proyecto |
| 1 | `GET` | `/payment-methods/:id/balance?project_id=` | Balance de cuenta en un proyecto |
| 2a | `GET/POST/PATCH/DELETE` | `/partners` | CRUD global de partners |
| 2a | `GET` | `/partners/:id/payment-methods` | Cuentas de un partner |
| 2b | `POST/GET/PATCH/DELETE` | `/workspaces` | CRUD de workspaces |
| 2b | `GET` | `/workspaces/:id/summary` | Resumen consolidado (plan avanzado) |
| 2c | `GET/POST/DELETE` | `/projects/:id/partners` | Partners de un proyecto |
| 2c | `GET` | `/projects/:id/available-payment-methods` | Cuentas disponibles (derivadas de partners) |
| 3b | `PATCH` | `/projects/:id/settings` | Activar `partners_enabled` |
| 3b | `GET` | `/projects/:id/partners/split-defaults` | Pre-llenado equitativo |
| 3c | `GET` | `/projects/:id/partners/balance` | Balances de socios |
| 3c | `GET` | `/projects/:id/partners/:pid/history` | Historial de un partner |
| 3c | `POST/GET/DELETE` | `/projects/:id/partner-settlements` | Liquidaciones directas |
| 4 | `GET` | `/projects/:id/balance` | Balance completo del proyecto |
| 4 | `GET` | `/projects/:id/partners/settlement-suggestions` | Liquidaciones sugeridas |

---

## Orden de trabajo sugerido

```
Fase 1  →  Fase 2a  →  Fase 2b
                    ↘
                     Fase 2c  →  Fase 3a  →  Fase 3b  →  Fase 3c  →  Fase 4
```

Fase 2b y 2c pueden ejecutarse en paralelo una vez completada la 2a.
