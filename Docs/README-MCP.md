# MCP API - Guía de Endpoints para Tools
# MCP API — Guía de Endpoints para Tools

Referencia técnica del módulo MCP de Project Ledger: cómo consumir los endpoints, qué parámetros aceptan y qué esperar en las respuestas.

Diseñado para consumo desde un servidor MCP donde un agente de IA invoca tools que llaman estos endpoints en `/api/mcp`.

---

## Changelog de mejoras (v2)

Las siguientes mejoras fueron aplicadas para hacer los endpoints más flexibles y tolerantes al consumo desde agentes de IA:

| Área | Mejora |
|---|---|
| Validadores enum | Eliminados `[RegularExpression]` estrictos en `granularity`, `status`, `direction`. Ahora la API normaliza el valor recibido en lugar de rechazarlo con 400. |
| `granularity` | Acepta `day/daily`, `week/weekly`, `month/monthly`. Cualquier otro valor se trata como `month`. |
| `direction` | Acepta `expense/expenses/out/outgoing`, `income/incomes/in/incoming`, y cualquier otro valor como `both`. |
| `status` (portfolio, obligations) | El matching es case-insensitive; `Active`, `ACTIVE` y `active` son equivalentes. |
| `expenses/totals` | Nuevo: `categoryId` y `categoryName` para filtrar totales por categoría específica. |
| `expenses/by-project` | Nuevo: `projectId` y `projectName` para filtrar la vista comparativa a proyectos específicos. |
| `income/by-project` | Nuevo: `projectId` y `projectName` para filtrar la vista comparativa a proyectos específicos. |
| `projects/deadlines` | Nuevo: `search` para filtrar deadlines por título o descripción de obligación. |
| `projects/portfolio` | Eliminado: parámetro `dueInDays` (era dead code sin efecto sobre el cálculo). |
| `payments/pending` | Nuevo campo en respuesta: `daysUntilDue` (entero positivo, null si ya vencida). |

---

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

- Las respuestas son determinísticas y fáciles de resumir por un LLM.
- El agente puede enviar valores de enum en cualquier capitalización o variante común (ver sección 3.7).
- Los campos `status`, `code`, `priority` y métricas numéricas deben tratarse como fuente de verdad.
- Todos los endpoints pueden llamarse sin parámetros opcionales y retornan datos útiles.

---

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

---

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
  "hasNextPage": false,
  "searchNote": "No projects matched projectName 'proyecto x'. Returned empty results."
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

Proyectos (`status`) — case-insensitive:

- `active`
- `completed`
- `at_risk`
- `inactive`

Obligaciones (`status`) — case-insensitive:

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

Errores por plan/límites:

```json
{
  "statusCode": 403,
  "message": "Plan limit exceeded.",
  "errorCode": "PLAN_LIMIT_EXCEEDED",
  "feature": "CanUseApi"
}
```

## 3.6 Natural Language Search

Para soportar prompts en lenguaje natural del agente:

- Todos los endpoints que aceptan `projectId` también aceptan `projectName` como alternativa fuzzy.
- El matching por nombre es case-insensitive y priorizado: `equals` -> `startsWith` -> `contains`.
- Si hay múltiples matches en el nivel de prioridad seleccionado, se devuelven todos.
- Si no hay match por nombre, el endpoint devuelve resultados vacíos (nunca error) y adjunta `searchNote`.
- La misma prioridad de matching (`equals` -> `startsWith` -> `contains`) aplica en filtros por `categoryName` y `paymentMethodName`.
- Para listados (`payments/received`, `payments/pending`, `payments/overdue`, `obligations/upcoming`, `obligations/unpaid`, `projects/deadlines`) existe `search` para buscar por título y descripción.

## 3.7 Normalización de valores enum

La API normaliza automáticamente los valores de `granularity` y `direction` antes de procesarlos. El agente puede enviar variantes coloquiales sin recibir un error 400:

**`granularity`**:

| Valor enviado | Interpretado como |
|---|---|
| `day`, `daily` | `day` |
| `week`, `weekly` | `week` |
| `month`, `monthly`, null, vacío, cualquier otro | `month` |

**`direction`** (solo en `payments/by-method`):

| Valor enviado | Interpretado como |
|---|---|
| `expense`, `expenses`, `out`, `outgoing` | `expense` |
| `income`, `incomes`, `in`, `incoming` | `income` |
| `both`, null, vacío, cualquier otro | `both` |

**`status`** (en `projects/portfolio`, `obligations/unpaid`): el filtro es case-insensitive, por lo que `Active`, `ACTIVE` y `active` son equivalentes.

---

## 4. Catálogo de Endpoints

| Endpoint | Descripción | Tipo de respuesta |
|---|---|---|
| `GET /api/mcp/context` | Contexto de usuario, permisos y proyectos visibles | `McpContextResponse` |
| `GET /api/mcp/projects/portfolio` | Vista de portafolio por proyecto con estado y métricas | `McpPagedResponse<McpProjectPortfolioItemResponse>` |
| `GET /api/mcp/projects/deadlines` | Deadlines de obligaciones por proyecto | `McpPagedResponse<McpProjectDeadlineItemResponse>` |
| `GET /api/mcp/projects/active-vs-completed` | Split de proyectos por estado | `McpProjectActivitySplitResponse` |
| `GET /api/mcp/payments/pending` | Obligaciones con saldo pendiente | `McpPagedResponse<McpPaymentObligationItemResponse>` |
| `GET /api/mcp/payments/received` | Ingresos recibidos | `McpPagedResponse<McpReceivedPaymentItemResponse>` |
| `GET /api/mcp/payments/overdue` | Obligaciones vencidas con deuda | `McpPagedResponse<McpPaymentObligationItemResponse>` |
| `GET /api/mcp/payments/by-method` | Uso de métodos de pago (in/out/net) | `McpPaymentMethodUsageResponse` |
| `GET /api/mcp/expenses/totals` | Totales de gasto con filtro de categoría opcional | `McpExpenseTotalsResponse` |
| `GET /api/mcp/expenses/by-category` | Distribución de gasto por categoría | `McpExpenseByCategoryResponse` |
| `GET /api/mcp/expenses/by-project` | Distribución de gasto por proyecto (filtrables) | `McpExpenseByProjectResponse` |
| `GET /api/mcp/expenses/trends` | Tendencia temporal de egresos | `McpExpenseTrendsResponse` |
| `GET /api/mcp/income/by-period` | Tendencia temporal de ingresos | `McpIncomeByPeriodResponse` |
| `GET /api/mcp/income/by-project` | Distribución de ingresos por proyecto (filtrables) | `McpIncomeByProjectResponse` |
| `GET /api/mcp/obligations/upcoming` | Obligaciones próximas a vencer | `McpPagedResponse<McpObligationItemResponse>` |
| `GET /api/mcp/obligations/unpaid` | Obligaciones impagas/abiertas | `McpPagedResponse<McpObligationItemResponse>` |
| `GET /api/mcp/summary/financial-health` | Score y señales de salud financiera | `McpFinancialHealthResponse` |
| `GET /api/mcp/summary/monthly-overview` | Resumen mensual consolidado | `McpMonthlyOverviewResponse` |
| `GET /api/mcp/summary/alerts` | Alertas financieras con prioridad | `McpAlertsResponse` |

---

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

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `status` (optional, case-insensitive): `active|completed|at_risk|inactive`
- `activityDays` (optional, default `30`)
- Paginación estándar

`sortBy` soportado: `name`, `status`, `totalSpent`, `totalIncome`, `netBalance`, `progress`  
Fallback: `lastActivityAtUtc`

Cada item incluye:

- Identidad de proyecto y rol
- `lastActivityAtUtc`, `nextDeadline`
- `status` calculado, `progressPercent`
- `totalSpent`, `totalIncome`, `netBalance`
- `budgetUsedPercentage`, `openObligations`, `overdueObligations`

### GET /api/mcp/projects/deadlines

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `dueFrom` (optional)
- `dueTo` (optional)
- `includeOverdue` (optional, default `true`)
- `search` (optional) — **nuevo**: filtra por título o descripción de la obligación
- Paginación estándar

Notas:

- Solo devuelve obligaciones con saldo restante > 0.
- Orden por `dueDate` (`sortDirection`), luego nombre de proyecto.

Cada item incluye: `projectName`, `obligationId`, `title`, `dueDate`, `daysUntilDue`, `remainingAmount`, `currency`, `status`.

### GET /api/mcp/projects/active-vs-completed

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `activityDays` (optional, default `30`)

Respuesta:

- Conteos: `activeCount`, `completedCount`, `atRiskCount`, `inactiveCount`
- `items[]` con `projectId`, `projectName`, `status`

## 5.3 Payments

### GET /api/mcp/payments/pending

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `dueBefore` (optional)
- `dueAfter` (optional)
- `minRemainingAmount` (optional)
- `search` (optional, búsqueda parcial por título y descripción)
- Paginación estándar

Devuelve obligaciones con saldo pendiente (`remainingAmount > 0`).

Respuesta por item (`McpPaymentObligationItemResponse`):

```json
{
  "obligationId": "...",
  "projectId": "...",
  "projectName": "string",
  "title": "string",
  "dueDate": "YYYY-MM-DD",
  "daysUntilDue": 12,
  "daysOverdue": null,
  "totalAmount": 1000.00,
  "paidAmount": 0.00,
  "remainingAmount": 1000.00,
  "currency": "USD",
  "status": "open"
}
```

> `daysUntilDue`: entero positivo si la fecha de vencimiento es futura; `null` si ya venció.  
> `daysOverdue`: entero positivo si está vencida; `null` si aún no vence.

### GET /api/mcp/payments/received

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `from` (optional)
- `to` (optional)
- `paymentMethodId` (optional)
- `paymentMethodName` (optional, alternativa fuzzy)
- `categoryId` (optional)
- `categoryName` (optional, alternativa fuzzy)
- `minAmount` (optional)
- `search` (optional, búsqueda parcial por título y descripción)
- Paginación estándar

`sortBy` soportado: `title`, `amount`, `project` — fallback: `incomeDate`

Cada item contiene: `incomeDate`, `title`, `originalAmount`, `originalCurrency`, `convertedAmount`, `categoryName`, `paymentMethodName`.

### GET /api/mcp/payments/overdue

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `overdueDaysMin` (optional, default `0`)
- `minRemainingAmount` (optional)
- `search` (optional, búsqueda parcial por título y descripción)
- Paginación estándar

Entrega solo obligaciones vencidas con saldo pendiente. Cada item incluye `daysOverdue`.

### GET /api/mcp/payments/by-method

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `from` (optional)
- `to` (optional)
- `direction` (optional, default `both`) — acepta variantes: ver sección 3.7
- `top` (optional, `1..100`, default `10`)

Respuesta:

- `from`, `to`, `direction` (valor normalizado)
- `items[]` con `totalOutgoing`, `totalIncoming`, `netFlow`, conteos y `usagePercentage`

## 5.4 Expenses

### GET /api/mcp/expenses/totals

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `from` (optional)
- `to` (optional)
- `comparePreviousPeriod` (optional, `true/false`)
- `categoryId` (optional) — **nuevo**: filtra totales a una categoría específica por ID
- `categoryName` (optional) — **nuevo**: filtra totales a una categoría específica por nombre (fuzzy)

Respuesta:

- `totalSpent`, `transactionCount`, `averageExpense`
- `searchNote` (incluye nota si `categoryName` no encontró coincidencias)
- Si `comparePreviousPeriod=true`: `previousPeriodTotal`, `deltaAmount`, `deltaPercentage`
  - El filtro de categoría se aplica también al período previo para comparación coherente.

### GET /api/mcp/expenses/by-category

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `from` (optional)
- `to` (optional)
- `top` (optional, `1..100`, default `10`)
- `includeOthers` (optional, agrega bucket `Others` para categorías fuera del top)
- `includeTrend` (optional, calcula `trendDelta` vs periodo previo equivalente)

Respuesta:

- `totalSpent`
- `items[]`: `categoryId`, `categoryName`, `totalAmount`, `expenseCount`, `percentage`, `trendDelta?`

### GET /api/mcp/expenses/by-project

Muestra cuánto gastó cada proyecto en el rango. Ahora acepta filtro de proyecto.

Query:

- `projectId` (optional) — **nuevo**: acota el breakdown a proyectos específicos
- `projectName` (optional) — **nuevo**: alternativa fuzzy a `projectId`
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
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `from` (optional)
- `to` (optional)
- `granularity` (optional, default `month`) — acepta variantes: ver sección 3.7
- `categoryId` (optional)
- `categoryName` (optional, alternativa fuzzy)

Respuesta:

- `from`, `to` (rango efectivo), `granularity` (valor normalizado)
- `points[]` con `periodStart`, `periodLabel`, `totalSpent`, `expenseCount`

## 5.5 Income

### GET /api/mcp/income/by-period

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `from` (optional)
- `to` (optional)
- `granularity` (optional, default `month`) — acepta variantes: ver sección 3.7
- `comparePreviousPeriod` (optional)

Respuesta:

- `totalIncome`, `incomeCount`, `granularity` (normalizado)
- Delta opcional contra periodo previo
- `points[]` con `periodStart`, `periodLabel`, `totalIncome`, `incomeCount`

### GET /api/mcp/income/by-project

Muestra cuánto ingresó cada proyecto. Ahora acepta filtro de proyecto.

Query:

- `projectId` (optional) — **nuevo**: acota el breakdown a proyectos específicos
- `projectName` (optional) — **nuevo**: alternativa fuzzy a `projectId`
- `from` (optional)
- `to` (optional)
- `top` (optional, `1..100`, default `10`)

Respuesta:

- `totalIncome`
- `items[]` con `projectId`, `projectName`, `currencyCode`, `totalIncome`, `incomeCount`

## 5.6 Obligations

### GET /api/mcp/obligations/upcoming

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `dueWithinDays` (optional, `1..3650`, default `30`)
- `minRemainingAmount` (optional)
- `search` (optional, búsqueda parcial por título y descripción)
- Paginación estándar

Incluye obligaciones con due date entre hoy y `hoy + dueWithinDays`. Solo las con saldo positivo.

Cada item incluye: `daysUntilDue`, `daysOverdue`, `paidAmount`, `remainingAmount`, `status`.

### GET /api/mcp/obligations/unpaid

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `status` (optional, case-insensitive): `open|partially_paid|overdue`
- `search` (optional, búsqueda parcial por título y descripción)
- Paginación estándar

Incluye obligaciones con saldo restante positivo.

## 5.7 Summary

### GET /api/mcp/summary/financial-health

Query:

- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `from` (optional)
- `to` (optional)

Respuesta:

- `score` (`0..100`) — combinación de balance neto, mora, presión de presupuesto e ingresos vs gastos
- `totalIncome`, `totalSpent`, `netBalance`, `burnRatePerDay`
- `budgetRiskProjects`, `overdueObligationsCount`
- `keySignals[]` — frases legibles por humano/LLM con las señales más relevantes

### GET /api/mcp/summary/monthly-overview

Query:

- `month` (optional, formato `YYYY-MM`, default mes actual)
- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)

Respuesta:

- KPIs del mes: `totalSpent`, `totalIncome`, `netBalance`, `expenseCount`, `incomeCount`
- `topCategories[]` (top 5 por gasto)
- `paymentMethodSplit[]`
- `projectHealth[]` con `spent`, `income`, `net`, `budgetUsedPercentage` por proyecto
- `alerts[]` generadas automáticamente

### GET /api/mcp/summary/alerts

Query:

- `month` (optional, formato `YYYY-MM`)
- `projectId` (optional)
- `projectName` (optional, alternativa fuzzy a `projectId`)
- `minPriority` (optional, `0..100`, default `0`)

Respuesta:

- `items[]` ordenados por prioridad desc
- Cada item: `code`, `type`, `message`, `priority`, `projectId?`, `obligationId?`
- Códigos típicos: `BUDGET_OVER_80`, `OVERDUE_OBLIGATIONS`, `NEGATIVE_NET`

---

## 6. Ejemplos de Consumo

## 6.1 Obtener contexto MCP

```bash
curl -X GET "https://<host>/api/mcp/context" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>"
```

## 6.2 Portafolio paginado — solo proyectos activos

```bash
curl -G "https://<host>/api/mcp/projects/portfolio" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>" \
  --data-urlencode "status=active" \
  --data-urlencode "sortBy=netBalance" \
  --data-urlencode "sortDirection=desc"
```

## 6.3 Tendencia mensual de egresos (granularity en variante coloquial)

```bash
curl -G "https://<host>/api/mcp/expenses/trends" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>" \
  --data-urlencode "granularity=monthly" \
  --data-urlencode "from=2025-01-01" \
  --data-urlencode "to=2025-12-31"
```

> `monthly` es normalizado automáticamente a `month`.

## 6.4 Totales de gasto filtrados por categoría

```bash
curl -G "https://<host>/api/mcp/expenses/totals" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>" \
  --data-urlencode "categoryName=viajes" \
  --data-urlencode "from=2025-01-01" \
  --data-urlencode "to=2025-06-30" \
  --data-urlencode "comparePreviousPeriod=true"
```

## 6.5 Pagos pendientes con días hasta vencimiento

```bash
curl -G "https://<host>/api/mcp/payments/pending" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>" \
  --data-urlencode "projectName=mi proyecto" \
  --data-urlencode "sortDirection=asc"
```

La respuesta incluye `daysUntilDue` en cada item para que el agente pueda priorizar sin calcular fechas.

## 6.6 Deadlines buscando por texto

```bash
curl -G "https://<host>/api/mcp/projects/deadlines" \
  -H "Authorization: Bearer <token>" \
  -H "X-User-Id: <userId>" \
  --data-urlencode "search=proveedor" \
  --data-urlencode "includeOverdue=true"
```

## 6.7 Secuencia recomendada para tool-calling en chatbot

Flujo mínimo recomendado:

1. Invocar `GET /api/mcp/context` al inicio de sesión para obtener `visibleProjects` y `permissions`.
2. Identificar `projectId` objetivo desde `visibleProjects` (o dejar que el endpoint haga el fuzzy match con `projectName`).
3. Ejecutar la consulta agregada principal (`summary/monthly-overview`, `expenses/totals`, `summary/financial-health`).
4. Si el usuario pide detalle, invocar endpoints drill-down (`expenses/by-category`, `payments/received`, `projects/deadlines`).
5. Si hay muchos resultados, paginar y resumir por bloques en lugar de cargar todo en una sola respuesta del LLM.

## 6.8 Minimal call por endpoint (ningún parámetro requerido)

Todos los endpoints pueden llamarse sin parámetros opcionales:

```bash
# Reemplazar <endpoint> por cualquiera de los listados en el catálogo
curl -G "https://<host>/api/mcp/<endpoint>" \
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

## 9. Estructura del Módulo MCP

Archivos principales del módulo MCP luego del refactor:

- `Controllers/McpController.cs`
- `Services/Interfaces/IMcpService.cs`
- `Services/Mcp/McpContextService.cs`
- `Services/Mcp/McpProjectService.cs`
- `Services/Mcp/McpPaymentService.cs`
- `Services/Mcp/McpExpenseService.cs`
- `Services/Mcp/McpIncomeService.cs`
- `Services/Mcp/McpObligationService.cs`
- `Services/Mcp/McpSummaryService.cs`
- `DTOs/Mcp/McpContextDTOs.cs`
- `DTOs/Mcp/McpProjectDTOs.cs`
- `DTOs/Mcp/McpPaymentDTOs.cs`
- `DTOs/Mcp/McpExpenseDTOs.cs`
- `DTOs/Mcp/McpIncomeDTOs.cs`
- `DTOs/Mcp/McpObligationDTOs.cs`
- `DTOs/Mcp/McpSummaryDTOs.cs`
- `DTOs/Mcp/McpRequestDTOs.cs`

## 10. Fuente de Verdad Técnica

Contratos y lógica de negocio están definidos en:

- `Controllers/McpController.cs`
- `DTOs/Mcp/`
- `Services/Interfaces/IMcpService.cs`
- `Services/Mcp/`
- `DTOs/Common/PaginationDTOs.cs`
- `Middleware/GlobalExceptionHandlerMiddleware.cs`
