# MCP API - Guía de Endpoints para Tools

Este documento describe los endpoints disponibles en el módulo MCP de Project Ledger, cómo consumirlos y qué esperar en las respuestas.

El enfoque principal es su consumo desde un servidor MCP, donde un agente de IA en un chatbot invoca tools que internamente llaman estos endpoints.

## 1. Propósito

La API MCP expone consultas optimizadas para asistentes y tools analíticas sobre:

- Contexto de usuario y permisos.
- Portafolio y salud de proyectos.
- Pagos, ingresos, egresos y obligaciones.
- Resúmenes ejecutivos y alertas financieras.

## 1.1 Contexto de uso: agente IA + chatbot

Patrón esperado:

1. El usuario hace una pregunta en lenguaje natural al chatbot.
2. El agente de IA decide qué tool MCP invocar.
3. La tool MCP llama al endpoint HTTP correspondiente en `/api/mcp`.
4. El agente transforma la respuesta en lenguaje natural, manteniendo trazabilidad de datos.

Implicaciones de diseño:

- Las respuestas deben ser determinísticas y fáciles de resumir por un LLM.
- El agente debe preferir consultas acotadas por `projectId`, fechas y paginación.
- Los campos `status`, `code`, `priority` y métricas numéricas deben tratarse como fuente de verdad.

## 2. Base URL y Autenticación

- Base path: `/api/mcp`
- Todos los endpoints son `GET`.
- Requieren service token Bearer válido del servidor MCP.
- Requieren policy `Plan:CanUseApi`.

Headers:

- `Authorization` (required): `Bearer <mcp_service_token>`
- `X-User-Id` (required): `<userId>`
- `Accept` (optional): `application/json`

Notas importantes:

- El service token se valida contra la variable de entorno `MCP_SERVICE_TOKEN`.
- `X-User-Id` debe contener el `userId` real del usuario en la base de datos.
- La API construye internamente un principal autenticado equivalente al JWT para reutilizar filters, policies y acceso multi-tenant sin bifurcar controladores/servicios.
- Si se envía `projectId`, la API valida acceso del usuario al proyecto.

## 3. Convenciones Comunes

## 3.1 Paginación (endpoints paginados)

Cuando un endpoint hereda `PagedRequest`, acepta:

- `page` (optional, default `1`, mínimo `1`)
- `pageSize` (optional, default `20`, rango `1..100`)
- `sortBy` (optional, depende del endpoint)
- `sortDirection` (optional, `asc` o `desc`, default `desc`)

Formato de respuesta paginada:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

## 3.2 Fechas y rangos

- `DateOnly` usa formato `YYYY-MM-DD`.
- Campos `month` usan `YYYY-MM`.
- Si `from > to` (o equivalentes como `dueAfter > dueBefore`), retorna `400`.

Defaults de rango (si no envías fechas en tendencias):

- Granularidad `day`: últimos 30 días.
- Granularidad `week`: últimas 12 semanas aprox.
- Granularidad `month`: últimos 12 meses.

## 3.3 Moneda y montos

- Los totales agregados usan montos convertidos (`ConvertedAmount`) cuando aplica.
- Se incluyen también monedas originales en endpoints de detalle (ej. ingresos recibidos).

## 3.4 Estados relevantes

Proyectos (`status`):

- `active`
- `completed`
- `at_risk`
- `inactive`

Obligaciones (`status`):

- `open`
- `partially_paid`
- `overdue`
- `paid`

## 3.5 Errores

Respuestas de error estándar:

```json
{
  "status": 400,
  "message": "Invalid date range: 'from' cannot be greater than 'to'.",
  "detail": null
}
```

Errores por plan/límites pueden retornar forma especializada:

```json
{
  "statusCode": 403,
  "message": "Plan limit exceeded.",
  "errorCode": "PLAN_LIMIT_EXCEEDED",
  "feature": "CanUseApi"
}
```

## 4. Catálogo de Endpoints

| Endpoint | Descripción | Tipo de respuesta |
|---|---|---|
| `GET /api/mcp/context` | Contexto de usuario, permisos y proyectos visibles | `McpContextResponse` |
| `GET /api/mcp/projects/portfolio` | Vista de portafolio por proyecto con estado y métricas | `PagedResponse<McpProjectPortfolioItemResponse>` |
| `GET /api/mcp/projects/deadlines` | Deadlines de obligaciones por proyecto | `PagedResponse<McpProjectDeadlineItemResponse>` |
| `GET /api/mcp/projects/active-vs-completed` | Split de proyectos por estado | `McpProjectActivitySplitResponse` |
| `GET /api/mcp/payments/pending` | Obligaciones con saldo pendiente | `PagedResponse<McpPaymentObligationItemResponse>` |
| `GET /api/mcp/payments/received` | Ingresos recibidos | `PagedResponse<McpReceivedPaymentItemResponse>` |
| `GET /api/mcp/payments/overdue` | Obligaciones vencidas con deuda | `PagedResponse<McpPaymentObligationItemResponse>` |
| `GET /api/mcp/payments/by-method` | Uso de métodos de pago (in/out/net) | `McpPaymentMethodUsageResponse` |
| `GET /api/mcp/expenses/totals` | Totales de gasto y comparación opcional | `McpExpenseTotalsResponse` |
| `GET /api/mcp/expenses/by-category` | Distribución de gasto por categoría | `McpExpenseByCategoryResponse` |
| `GET /api/mcp/expenses/by-project` | Distribución de gasto por proyecto | `McpExpenseByProjectResponse` |
| `GET /api/mcp/expenses/trends` | Tendencia temporal de egresos | `McpExpenseTrendsResponse` |
| `GET /api/mcp/income/by-period` | Tendencia temporal de ingresos | `McpIncomeByPeriodResponse` |
| `GET /api/mcp/income/by-project` | Distribución de ingresos por proyecto | `McpIncomeByProjectResponse` |
| `GET /api/mcp/obligations/upcoming` | Obligaciones próximas a vencer | `PagedResponse<McpObligationItemResponse>` |
| `GET /api/mcp/obligations/unpaid` | Obligaciones impagas/abiertas | `PagedResponse<McpObligationItemResponse>` |
| `GET /api/mcp/summary/financial-health` | Score y señales de salud financiera | `McpFinancialHealthResponse` |
| `GET /api/mcp/summary/monthly-overview` | Resumen mensual consolidado | `McpMonthlyOverviewResponse` |
| `GET /api/mcp/summary/alerts` | Alertas financieras con prioridad | `McpAlertsResponse` |

## 5. Detalle de Endpoints

## 5.1 Contexto

### GET /api/mcp/context

Sin query params.

Entrega:

- `userId`, `generatedAtUtc`
- `defaultCurrencyCode`
- `permissions` y `limits` (capabilities del plan)
- `visibleProjects[]` con `projectId`, `projectName`, `currencyCode`, `userRole`

Uso típico: inicializar contexto de tools antes de consultas analíticas.

## 5.2 Proyectos

### GET /api/mcp/projects/portfolio

Query principal:

- `projectId` (optional)
- `status` (optional): `active|completed|at_risk|inactive`
- `activityDays` (optional, default `30`)
- `dueInDays` (optional, default `30`, actualmente no impacta el cálculo del servicio)
- Paginación estándar

`sortBy` soportado:

- `name`, `status`, `totalSpent`, `totalIncome`, `netBalance`, `progress`
- fallback: `lastActivityAtUtc`

Cada item incluye:

- Identidad de proyecto y rol
- Actividad reciente, próxima fecha límite
- Estado calculado, progreso
- Totales (`totalSpent`, `totalIncome`, `netBalance`)
- Contexto de presupuesto y obligaciones

### GET /api/mcp/projects/deadlines

Query principal:

- `projectId` (optional)
- `dueFrom` (optional)
- `dueTo` (optional)
- `includeOverdue` (optional, default `true`)
- Paginación estándar

Notas:

- Solo devuelve obligaciones con saldo restante > 0.
- Orden por `dueDate` (`sortDirection`), luego nombre de proyecto.

### GET /api/mcp/projects/active-vs-completed

Query:

- `projectId` (optional)
- `activityDays` (optional, default `30`)

Respuesta:

- Conteos: `activeCount`, `completedCount`, `atRiskCount`, `inactiveCount`
- `items[]` con `projectId`, `projectName`, `status`

## 5.3 Payments

### GET /api/mcp/payments/pending

Query:

- `projectId` (optional)
- `dueBefore` (optional)
- `dueAfter` (optional)
- `minRemainingAmount` (optional)
- Paginación estándar

Devuelve obligaciones con saldo pendiente (`remainingAmount > 0`).

### GET /api/mcp/payments/received

Query:

- `projectId` (optional)
- `from` (optional)
- `to` (optional)
- `paymentMethodId` (optional)
- `categoryId` (optional)
- `minAmount` (optional)
- Paginación estándar

`sortBy` soportado:

- `title`, `amount`, `project`
- fallback: `incomeDate`

Cada item contiene metadatos de ingreso y montos original/convertido.

### GET /api/mcp/payments/overdue

Query:

- `projectId` (optional)
- `overdueDaysMin` (optional, default `0`)
- `minRemainingAmount` (optional)
- Paginación estándar

Entrega solo obligaciones vencidas con saldo pendiente.

### GET /api/mcp/payments/by-method

Query:

- `projectId` (optional)
- `from` (optional)
- `to` (optional)
- `direction` (optional): `expense|income|both` (default `both`)
- `top` (optional): `1..100` (default `10`)

Respuesta:

- `from`, `to`, `direction`
- `items[]` con `totalOutgoing`, `totalIncoming`, `netFlow`, conteos y `usagePercentage`

## 5.4 Expenses

### GET /api/mcp/expenses/totals

Query:

- `projectId` (optional)
- `from` (optional)
- `to` (optional)
- `comparePreviousPeriod` (optional, `true/false`)

Respuesta:

- `totalSpent`, `transactionCount`, `averageExpense`
- Si comparas periodo previo: `previousPeriodTotal`, `deltaAmount`, `deltaPercentage`

### GET /api/mcp/expenses/by-category

Query:

- `projectId` (optional)
- `from` (optional)
- `to` (optional)
- `top` (optional, `1..100`, default `10`)
- `includeOthers` (optional, agrega bucket `Others`)
- `includeTrend` (optional, calcula `trendDelta` vs periodo previo)

Respuesta:

- `totalSpent`
- `items[]`: categoría, total, count, porcentaje, tendencia opcional

### GET /api/mcp/expenses/by-project

Query:

- `from` (optional)
- `to` (optional)
- `top` (optional, `1..100`, default `10`)
- `includeBudgetContext` (optional, default `true`)

Respuesta por proyecto:

- `totalSpent`, `expenseCount`
- `budget` y `budgetUsedPercentage` si hay presupuesto activo

### GET /api/mcp/expenses/trends

Query:

- `projectId` (optional)
- `from` (optional)
- `to` (optional)
- `granularity` (optional): `day|week|month` (default `month`)
- `categoryId` (optional)

Respuesta:

- Rango efectivo (`from`, `to`)
- `points[]` con `periodStart`, `periodLabel`, `totalSpent`, `expenseCount`

## 5.5 Income

### GET /api/mcp/income/by-period

Query:

- `projectId` (optional)
- `from` (optional)
- `to` (optional)
- `granularity` (optional): `day|week|month` (default `month`)
- `comparePreviousPeriod` (optional)

Respuesta:

- Totales (`totalIncome`, `incomeCount`)
- Delta opcional contra periodo previo
- `points[]` con serie temporal

### GET /api/mcp/income/by-project

Query:

- `from` (optional)
- `to` (optional)
- `top` (optional, `1..100`, default `10`)

Respuesta:

- `totalIncome`
- `items[]` con proyecto, moneda y conteo de ingresos

## 5.6 Obligations

### GET /api/mcp/obligations/upcoming

Query:

- `projectId` (optional)
- `dueWithinDays` (optional, `1..3650`, default `30`)
- `minRemainingAmount` (optional)
- Paginación estándar

Incluye obligaciones con due date entre hoy y el límite configurado.

### GET /api/mcp/obligations/unpaid

Query:

- `projectId` (optional)
- `status` (optional): `open|partially_paid|overdue`
- Paginación estándar

Incluye obligaciones con saldo restante positivo.

## 5.7 Summary

### GET /api/mcp/summary/financial-health

Query:

- `projectId` (optional)
- `from` (optional)
- `to` (optional)

Respuesta:

- `score` (`0..100`)
- `totalIncome`, `totalSpent`, `netBalance`, `burnRatePerDay`
- `budgetRiskProjects`, `overdueObligationsCount`
- `keySignals[]`

El score combina balance neto, mora, presión de presupuesto e ingresos vs gastos.

### GET /api/mcp/summary/monthly-overview

Query:

- `month` (optional, formato `YYYY-MM`, default mes actual)
- `projectId` (optional)

Respuesta:

- KPIs del mes (`totalSpent`, `totalIncome`, `netBalance`, counts)
- `topCategories[]`
- `paymentMethodSplit[]`
- `projectHealth[]`
- `alerts[]`

### GET /api/mcp/summary/alerts

Query:

- `month` (optional, formato `YYYY-MM`)
- `projectId` (optional)
- `minPriority` (optional, `0..100`, default `0`)

Respuesta:

- `items[]` ordenado por prioridad desc
- Códigos típicos: `BUDGET_OVER_80`, `OVERDUE_OBLIGATIONS`, `NEGATIVE_NET`

## 6. Ejemplos de Consumo

## 6.1 Obtener contexto MCP

```bash
curl -X GET "https://<host>/api/mcp/context" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>" \
  -H "Accept: application/json"
```

## 6.2 Consultar portafolio paginado

```bash
curl -G "https://<host>/api/mcp/projects/portfolio" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>" \
  --data-urlencode "page=1" \
  --data-urlencode "pageSize=20" \
  --data-urlencode "status=active" \
  --data-urlencode "sortBy=netBalance" \
  --data-urlencode "sortDirection=desc"
```

## 6.3 Tendencia de egresos mensual

```bash
curl -G "https://<host>/api/mcp/expenses/trends" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>" \
  --data-urlencode "granularity=month" \
  --data-urlencode "from=2025-01-01" \
  --data-urlencode "to=2025-12-31"
```

## 6.4 Secuencia recomendada para tool-calling en chatbot

Flujo mínimo recomendado para evitar respuestas ambiguas del agente:

1. Invocar `GET /api/mcp/context` al inicio de sesión o cuando cambie el contexto.
2. Identificar `projectId` objetivo desde `visibleProjects`.
3. Ejecutar consulta agregada principal (por ejemplo `summary/monthly-overview` o `expenses/totals`).
4. Si el usuario pide detalle, invocar endpoint drill-down (`expenses/by-category`, `payments/received`, etc.).
5. Si hay muchos resultados, paginar y resumir por bloques en vez de cargar todo en una sola respuesta del LLM.

## 6.5 Minimal call por endpoint (cero parámetros opcionales)

### GET /api/mcp/context

```bash
curl -X GET "https://<host>/api/mcp/context" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/projects/portfolio

```bash
curl -G "https://<host>/api/mcp/projects/portfolio" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/projects/deadlines

```bash
curl -G "https://<host>/api/mcp/projects/deadlines" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/projects/active-vs-completed

```bash
curl -G "https://<host>/api/mcp/projects/active-vs-completed" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/payments/pending

```bash
curl -G "https://<host>/api/mcp/payments/pending" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/payments/received

```bash
curl -G "https://<host>/api/mcp/payments/received" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/payments/overdue

```bash
curl -G "https://<host>/api/mcp/payments/overdue" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/payments/by-method

```bash
curl -G "https://<host>/api/mcp/payments/by-method" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/expenses/totals

```bash
curl -G "https://<host>/api/mcp/expenses/totals" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/expenses/by-category

```bash
curl -G "https://<host>/api/mcp/expenses/by-category" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/expenses/by-project

```bash
curl -G "https://<host>/api/mcp/expenses/by-project" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/expenses/trends

```bash
curl -G "https://<host>/api/mcp/expenses/trends" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/income/by-period

```bash
curl -G "https://<host>/api/mcp/income/by-period" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/income/by-project

```bash
curl -G "https://<host>/api/mcp/income/by-project" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/obligations/upcoming

```bash
curl -G "https://<host>/api/mcp/obligations/upcoming" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/obligations/unpaid

```bash
curl -G "https://<host>/api/mcp/obligations/unpaid" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/summary/financial-health

```bash
curl -G "https://<host>/api/mcp/summary/financial-health" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/summary/monthly-overview

```bash
curl -G "https://<host>/api/mcp/summary/monthly-overview" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

### GET /api/mcp/summary/alerts

```bash
curl -G "https://<host>/api/mcp/summary/alerts" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

## 7. Recomendaciones para Integración de Tools (Agente IA)

- Llamar primero `GET /api/mcp/context` para conocer permisos y scope.
- Reusar `projectId` en consultas posteriores para reducir ruido.
- Aplicar paginación siempre en listados para evitar respuestas muy grandes.
- Tratar `status` y `code` como catálogos controlados (no hardcodear solo en UI, también en lógica de tool).
- Si recibes `403`, revisar permisos de plan/capabilities antes de reintentar.

## 7.1 Buenas prácticas específicas para un agente en chatbot

- No adivinar: si falta `projectId` y hay múltiples proyectos, pedir aclaración al usuario o usar un default explícito.
- Reducir tokens: preferir endpoints de resumen antes de endpoints de detalle masivo.
- Mantener contexto temporal explícito: si el usuario no indica fechas, informar el rango default aplicado.
- En respuestas narradas, siempre incluir moneda y periodo para evitar ambiguedad.
- Si `items` llega vacío, responder como resultado válido sin tratarlo como error.
- Con errores `400`, corregir parámetros (rango de fechas, enums, formatos) antes de reintentar.

## 7.2 Convención sugerida de tools MCP

Para mejorar precisión del agente, definir tools con nombre y propósito explícito. Ejemplos:

- `mcp_get_context` -> `GET /api/mcp/context`
- `mcp_get_project_portfolio` -> `GET /api/mcp/projects/portfolio`
- `mcp_get_monthly_overview` -> `GET /api/mcp/summary/monthly-overview`
- `mcp_get_alerts` -> `GET /api/mcp/summary/alerts`

Sugerencia de contrato por tool:

- `description`: cuándo usarla (intención del usuario).
- `inputSchema`: solo parámetros permitidos por el endpoint.
- `outputMapping`: campos clave que el agente debe priorizar al redactar la respuesta.
- `errorHandling`: reglas de retry/no-retry según código HTTP.

## 7.3 Política de retry recomendada para tools

- `400`: no reintentar sin corregir parámetros.
- `401/403`: no reintentar automáticamente; escalar a renovación de sesión/permisos.
- `404`: tratar como recurso no encontrado en el scope del usuario.
- `500`: reintento con backoff limitado (por ejemplo 1-2 intentos).

## 8. Parameter Optionality Rules

- Todos los filtros/query params son `(optional)` salvo que explícitamente se marque `(required)`.
- Un filtro omitido se ignora; no se fuerza un valor restrictivo por defecto para filtrar resultados.
- En paginación, si `page`, `pageSize` o `sortDirection` se omiten, se aplican sus defaults.
- Los únicos headers requeridos para MCP son `Authorization` y `X-User-Id`.

## 9. Fuente de Verdad Técnica

Contratos y lógica de negocio están definidos en:

- `Controllers/McpController.cs`
- `DTOs/Mcp/McpDTOs.cs`
- `Services/Interfaces/IMcpService.cs`
- `Services/McpService.cs`
- `DTOs/Common/PaginationDTOs.cs`
- `Middleware/GlobalExceptionHandlerMiddleware.cs`
