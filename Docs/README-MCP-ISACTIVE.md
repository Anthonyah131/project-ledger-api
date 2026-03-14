# MCP Guide: isActive en Gastos e Ingresos

## Objetivo

Documentar como se considera el campo isActive en consultas MCP relacionadas con gastos e ingresos, y como utilizarlo cuando sea necesario.

## Resumen de comportamiento

- isActive=false significa transaccion en modo recordatorio (existe, pero no contabiliza).
- En MCP, los calculos agregados de gastos/ingresos usan solo transacciones activas.
- Los pagos usados para estado de obligaciones tambien excluyen transacciones inactivas.

## Impacto funcional en MCP

### 1) Metricas y agregados

Las siguientes familias MCP trabajan con datos activos (contables):
- Expense totals/by category/by project/trends
- Income by period/by project
- Financial health
- Monthly overview
- Alerts
- Obligation-related summaries

Esto garantiza que las metricas de negocio no se contaminen con recordatorios no ejecutados.

### 2) Received payments (detalle de ingresos)

Se agrego soporte explicito de isActive en MCP:

- Query:
  - McpReceivedPaymentsQuery.IsActive (bool?)
- Response item:
  - McpReceivedPaymentItemResponse.IsActive (bool)

Uso recomendado:
- isActive=true: solo ingresos contables efectivos
- isActive=false: solo recordatorios de ingresos
- sin filtro: ambos (segun alcance y filtros adicionales)

## Ejemplo de uso conceptual

Request (query object):
```json
{
  "projectId": "79d6f0ad-95d2-4f1a-95d8-7c1f95f1b329",
  "from": "2026-03-01",
  "to": "2026-03-31",
  "isActive": true,
  "page": 1,
  "pageSize": 20
}
```

Response item:
```json
{
  "incomeId": "25595cf0-dfbb-422a-8ee4-80bb2d5de2a8",
  "projectId": "79d6f0ad-95d2-4f1a-95d8-7c1f95f1b329",
  "projectName": "Proyecto Alfa",
  "title": "Cobro factura 1021",
  "originalAmount": 120000.00,
  "originalCurrency": "CRC",
  "convertedAmount": 120000.00,
  "isActive": true
}
```

## Recomendaciones para agentes MCP

- Para analisis financiero real, usar siempre datos activos.
- Si el caso de uso requiere forecast o pipeline de cobros/pagos pendientes, consultar/filtrar isActive=false de forma explicita en endpoints de detalle que lo soporten.
- En reportes ejecutivos, aclarar si el conjunto usado fue activo-only o mixto.

## Consideraciones de obligaciones

En obligaciones, el monto pagado y el estado (open/partially_paid/paid/overdue) ignoran pagos inactivos.
Esto evita marcar como pagada una obligacion con transacciones de recordatorio.

## Referencias tecnicas

- Campo en dominio:
  - expenses.exp_is_active
  - incomes.inc_is_active
- Endpoint API para cambio rapido:
  - PATCH /api/projects/{projectId}/expenses/{expenseId}/active-state
  - PATCH /api/projects/{projectId}/incomes/{incomeId}/active-state
