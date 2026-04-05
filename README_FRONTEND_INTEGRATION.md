# Backend → Frontend Integration Report: Filtros y Ordenamiento

> Generado el: 2026-04-05
> Proyecto: Project Ledger API
> Alcance: Todos los endpoints GET con filtros, paginación y ordenamiento

---

## Resumen Ejecutivo

Se auditaron todos los endpoints GET de la API con parámetros de query. Este reporte documenta con precisión qué filtros acepta cada endpoint, qué valores de `sortBy` están realmente implementados en la base de datos, y qué comportamientos son silenciosos (parámetros ignorados sin error). También incluye dos correcciones que se hicieron en esta sesión.

---

## Correcciones Aplicadas en Esta Sesión

| # | Problema | Fix |
|---|----------|-----|
| 1 | `sortBy=convertedAmount` en expenses caía silenciosamente al orden por fecha | Mapeado correctamente. Ahora funciona igual que en incomes |
| 2 | `isDescending=true/false` era ignorado — `IsDescending` no tenía setter en el DTO | Ahora acepta binding desde query string como booleano |

---

## Convenciones Globales de Paginación

Todos los endpoints paginados usan el mismo DTO base `PagedRequest`. Estos parámetros aplican en **todos** los endpoints que los acepten:

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | Página (base 1) |
| `pageSize` | `number` | `20` | Registros por página. Máximo `100` |
| `sortBy` | `string` | `null` | Campo de orden. Ver tabla de cada endpoint |
| `isDescending` | `boolean` | `true` | `true` = mayor a menor / más reciente primero |
| `sortDirection` | `"asc" \| "desc"` | `"desc"` | Alternativa a `isDescending`. Si se envían ambos, `isDescending` tiene precedencia |

> ⚠️ `sortBy` es **case-insensitive** en todos los endpoints (`convertedAmount`, `CONVERTEDAMOUNT`, `convertedamount` son equivalentes).

> ⚠️ Si se envía un valor de `sortBy` desconocido (ej: `sortBy=foo`), el backend aplica el **orden por defecto sin error ni advertencia**.

---

## Endpoints con Filtros — Referencia por Endpoint

---

### 1. `GET /api/projects/{projectId}/expenses`

**Rol mínimo:** Viewer. Para `includeDeleted=true` requiere Editor.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | `null` → fecha | Ver tabla abajo |
| `isDescending` | `boolean` | `true` | |
| `includeDeleted` | `boolean` | `false` | Incluir gastos eliminados. Requiere rol Editor |
| `isActive` | `boolean?` | `null` (todos) | `true` = activos, `false` = inactivos/recordatorios |
| `from` | `DateOnly` (YYYY-MM-DD) | `null` | Fecha de gasto desde (inclusive) |
| `to` | `DateOnly` (YYYY-MM-DD) | `null` | Fecha de gasto hasta (inclusive) |

> ⚠️ Si `from > to`, retorna **400 Bad Request**.

**Valores válidos de `sortBy`:**

| Valor | Ordena por | Notas |
|-------|-----------|-------|
| `date` | Fecha del gasto | |
| `expenseDate` | Fecha del gasto | Alias de `date` |
| `title` | Título A→Z / Z→A | |
| `convertedAmount` | Monto convertido (moneda del proyecto) | |
| `amount` | Monto convertido | Alias de `convertedAmount` |
| `originalAmount` | Monto en moneda original | |
| `createdAt` | Fecha de creación del registro | |
| *(omitido o desconocido)* | Fecha del gasto | Default |

**Ejemplos:**
```
GET /api/projects/{id}/expenses?page=1&pageSize=10&sortBy=convertedAmount&isDescending=true&isActive=true
GET /api/projects/{id}/expenses?from=2026-01-01&to=2026-03-31&sortBy=date&isDescending=false
GET /api/projects/{id}/expenses?isActive=false&page=1&pageSize=20
```

---

### 2. `GET /api/projects/{projectId}/incomes`

**Rol mínimo:** Viewer. Para `includeDeleted=true` requiere Editor.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | `null` → fecha | Ver tabla abajo |
| `isDescending` | `boolean` | `true` | |
| `includeDeleted` | `boolean` | `false` | Incluir ingresos eliminados. Requiere rol Editor |
| `isActive` | `boolean?` | `null` (todos) | `true` = activos, `false` = inactivos/recordatorios |
| `from` | `DateOnly` (YYYY-MM-DD) | `null` | Fecha de ingreso desde (inclusive) |
| `to` | `DateOnly` (YYYY-MM-DD) | `null` | Fecha de ingreso hasta (inclusive) |

> ⚠️ Si `from > to`, retorna **400 Bad Request**.

**Valores válidos de `sortBy`:**

| Valor | Ordena por |
|-------|-----------|
| `date` | Fecha del ingreso |
| `incomeDate` | Fecha del ingreso (alias) |
| `title` | Título |
| `convertedAmount` | Monto convertido |
| `amount` | Alias de `convertedAmount` |
| `originalAmount` | Monto en moneda original |
| `createdAt` | Fecha de creación del registro |
| *(omitido o desconocido)* | Fecha del ingreso (default) |

---

### 3. `GET /api/projects`

**Auth:** Usuario autenticado. Devuelve solo proyectos donde el usuario es owner o miembro.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | `null` → createdAt | Ver tabla abajo |
| `isDescending` | `boolean` | `true` | |

**Valores válidos de `sortBy`:**

| Valor | Ordena por |
|-------|-----------|
| `name` | Nombre del proyecto |
| `updatedAt` | Última modificación |
| `currencyCode` | Código de moneda |
| *(omitido o desconocido)* | Fecha de creación (default) |

---

### 4. `GET /api/projects/{projectId}/obligations`

**Rol mínimo:** Viewer.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | `null` → createdAt | Ver tabla abajo |
| `isDescending` | `boolean` | `true` | |

**Valores válidos de `sortBy`:**

| Valor | Ordena por |
|-------|-----------|
| `title` | Título |
| `amount` | Monto total de la obligación |
| `dueDate` | Fecha de vencimiento |
| *(omitido o desconocido)* | Fecha de creación (default) |

---

### 5. `GET /api/payment-methods/{id}/expenses`

**Auth:** Owner del payment method.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | `null` → fecha | Mismos valores que expenses del proyecto |
| `isDescending` | `boolean` | `true` | |
| `isActive` | `boolean?` | `null` (todos) | |
| `from` | `DateOnly` | `null` | |
| `to` | `DateOnly` | `null` | |
| `projectId` | `uuid?` | `null` | Filtrar por proyecto específico (cross-project) |

> ⚠️ Si `from > to`, retorna **400 Bad Request** (única validación de rango de fechas).

**Response incluye:** `totalActiveAmount` (suma de montos activos con los filtros aplicados).

**Valores de `sortBy`:** idénticos a expenses del proyecto (`date`, `title`, `convertedAmount`, `amount`, `originalAmount`, `createdAt`).

---

### 6. `GET /api/payment-methods/{id}/incomes`

**Auth:** Owner del payment method.

Mismos parámetros que el endpoint de expenses de payment method, incluyendo la validación de `from > to` → 400.

**Valores de `sortBy`:** idénticos a incomes del proyecto (`date`, `title`, `convertedAmount`, `amount`, `originalAmount`, `createdAt`).

**Response incluye:** `totalActiveAmount`.

---

### 7. `GET /api/partners`

**Auth:** Usuario autenticado. Solo devuelve los partners del usuario.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `search` | `string?` | `null` | Búsqueda por nombre (contains, case-insensitive) |
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | — | **IGNORADO** |
| `isDescending` | `boolean` | — | **IGNORADO** |

> ⚠️ `sortBy` e `isDescending` **no tienen efecto**. El resultado siempre viene ordenado por **nombre A→Z**.

---

### 8. `GET /api/partners/{id}/payment-methods`

**Auth:** Owner del partner.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | — | **IGNORADO** |
| `isDescending` | `boolean` | — | **IGNORADO** |

> ⚠️ Solo se usa `page`/`pageSize`. El orden de los resultados es indeterminado (orden de inserción en DB).

---

### 9. `GET /api/partners/{id}/projects`

**Auth:** Owner del partner.

Igual que payment-methods del partner: solo `page`/`pageSize` tienen efecto. `sortBy` e `isDescending` son **ignorados**.

---

### 10. `GET /api/projects/{projectId}/partner-settlements`

**Rol mínimo:** Viewer. Requiere que el proyecto tenga partners habilitados.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | — | **IGNORADO** |
| `isDescending` | `boolean` | — | **IGNORADO** |

> ⚠️ Los liquidaciones siempre vienen ordenadas por **fecha descendente**. El `sortBy` no tiene efecto.

---

### 11. `GET /api/projects/{projectId}/partners/{partnerId}/history`

**Rol mínimo:** Viewer. Requiere que el proyecto tenga partners habilitados.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | Paginación de las transacciones del historial |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | — | **IGNORADO** |
| `isDescending` | `boolean` | — | **IGNORADO** |

> ⚠️ `sortBy` no tiene efecto. Las transacciones vienen en orden fijo por fecha.

---

### 12. `GET /api/workspaces/{id}/projects`

**Auth:** Miembro del workspace.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | `null` → createdAt | Mismos valores que `GET /api/projects` |
| `isDescending` | `boolean` | `true` | |

**Valores de `sortBy`:** `name`, `updatedAt`, `currencyCode`. Default: `createdAt`.

---

### 13. `GET /api/audit-logs/me`

**Auth:** Usuario autenticado.

| Parámetro | Tipo | Descripción |
|-----------|------|-------------|
| `page` | `number` | |
| `pageSize` | `number` | |
| `sortBy` | `string` | **IGNORADO** |
| `isDescending` | `boolean` | **IGNORADO** |

> ⚠️ Siempre ordenado por **fecha descendente** (más reciente primero). El orden no es configurable.

---

### 14. `GET /api/audit-logs/entity/{entityName}/{entityId}`

Mismo comportamiento que `/audit-logs/me`. `sortBy` e `isDescending` **ignorados**.

---

### 15. `GET /api/dashboard/monthly-summary` · `monthly-daily-trend` · `monthly-top-categories` · `monthly-payment-methods`

**Auth:** Usuario autenticado. Sin rol de proyecto requerido.

| Parámetro | Tipo | Descripción |
|-----------|------|-------------|
| `month` | `string?` | Formato `YYYY-MM`. Si se omite, usa el mes actual. Retorna **400** si el formato es inválido |
| `projectId` | `uuid?` | **Requerido** (retorna 400 si falta). Sin efecto para usuarios admin |

> No hay paginación, sortBy ni filtros de fecha en estos endpoints.

---

### 16. `GET /api/projects/{projectId}/reports/summary`

**Rol mínimo:** Viewer.

| Parámetro | Tipo | Descripción |
|-----------|------|-------------|
| `from` | `DateOnly?` | Inicio del rango de fechas |
| `to` | `DateOnly?` | Fin del rango de fechas |

---

### 17. `GET /api/projects/{projectId}/reports/expenses` · `reports/incomes` · `reports/partner-balances`

**Rol mínimo:** Viewer. Exportación requiere plan con `CanExportData`. PDF requiere `CanUseAdvancedReports`.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `from` | `DateOnly?` | `null` | Inicio del rango |
| `to` | `DateOnly?` | `null` | Fin del rango |
| `format` | `string` | `"json"` | Formato de respuesta: `"json"`, `"excel"`, `"pdf"` |

> `reports/partner-balances` requiere además que el proyecto tenga partners habilitados (`PrjPartnersEnabled`).

---

### 18. `GET /api/workspaces/{workspaceId}/reports/summary`

**Auth:** Miembro del workspace. Requiere plan con `CanUseAdvancedReports`.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `from` | `DateOnly?` | `null` | Inicio del rango |
| `to` | `DateOnly?` | `null` | Fin del rango |
| `referenceCurrency` | `string?` | `null` | Código ISO de moneda de referencia para consolidar proyectos |
| `format` | `string` | `"json"` | `"json"`, `"excel"`, `"pdf"` |

---

### 19. `GET /api/partners/{id}/reports/general`

**Auth:** Owner del partner. Requiere plan con `CanUseAdvancedReports`.

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `from` | `DateOnly?` | `null` | Inicio del rango |
| `to` | `DateOnly?` | `null` | Fin del rango |
| `format` | `string` | `"json"` | `"json"`, `"excel"`, `"pdf"`. Excel requiere `CanExportData` |

---

### 20. `GET /api/admin/users` *(solo administradores)*

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `page` | `number` | `1` | |
| `pageSize` | `number` | `20` | |
| `sortBy` | `string` | `null` → createdAt | |
| `isDescending` | `boolean` | `true` | |
| `includeDeleted` | `boolean` | `false` | Incluye usuarios eliminados |

**Valores de `sortBy`:** `email`, `fullName`, `lastLogin`. Default: `createdAt`.

---

## Resumen de Comportamientos Silenciosos (Sin Error)

| Situación | Comportamiento |
|-----------|---------------|
| `sortBy` desconocido | Se aplica el orden por defecto del endpoint, sin error |
| `sortBy` en endpoints que lo ignoran | Se ignora silenciosamente |
| `from` o `to` solos (sin el otro) | Se aplica solo el límite provisto, sin error |
| `from > to` | Retorna **400 Bad Request** en: `/expenses`, `/incomes`, `/payment-methods/{id}/expenses`, `/payment-methods/{id}/incomes`. En el resto (reportes, obligaciones) no hay validación — devuelve lista vacía silenciosamente |
| `isActive` omitido | Devuelve tanto activos como inactivos |
| `includeDeleted` omitido | Solo devuelve no eliminados |

---

## Endpoints SIN Filtros Reseñables

Estos endpoints solo aceptan parámetros de ruta, no de query:

- `GET /api/projects/{projectId}/expenses/{expenseId}` — por ID
- `GET /api/projects/{projectId}/incomes/{incomeId}` — por ID
- `GET /api/projects/{projectId}/expenses/templates` — lista completa sin paginación
- `GET /api/projects/{projectId}/expenses/extract-from-image/quota` — cuota OCR
- `GET /api/projects/{projectId}/categories` — lista completa sin paginación
- `GET /api/projects/{projectId}/members` — lista completa sin paginación
- `GET /api/payment-methods/{id}/balance?projectId=...` — balance puntual

---

## Validación de Rango de Fechas

La validación `from > to` → **400 Bad Request** aplica en los siguientes endpoints:

| Endpoint | Valida `from > to` |
|----------|-------------------|
| `GET /api/projects/{id}/expenses` | ✅ Sí |
| `GET /api/projects/{id}/incomes` | ✅ Sí |
| `GET /api/payment-methods/{id}/expenses` | ✅ Sí |
| `GET /api/payment-methods/{id}/incomes` | ✅ Sí |
| `GET /api/projects/{id}/reports/summary` | ❌ No (retorna vacío) |
| `GET /api/projects/{id}/reports/expenses` | ❌ No (retorna vacío) |
| `GET /api/projects/{id}/reports/incomes` | ❌ No (retorna vacío) |
| `GET /api/projects/{id}/reports/partner-balances` | ❌ No (retorna vacío) |
| `GET /api/workspaces/{id}/reports/summary` | ❌ No (retorna vacío) |
| `GET /api/partners/{id}/reports/general` | ❌ No (retorna vacío) |