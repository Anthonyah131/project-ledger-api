# Backend → Frontend Integration Report

> Generado el: 2026-03-26
> Proyecto: Project Ledger API
> Feature: Importación Rápida de Movimientos (Bulk Quick Import)

---

## Resumen Ejecutivo

Se implementaron dos nuevos endpoints de importación masiva: uno para gastos y otro para ingresos. Permiten crear hasta 100 movimientos en una sola llamada. No hay breaking changes — todos los endpoints existentes siguen funcionando sin modificaciones. El frontend debe implementar la nueva vista de "Importación Rápida" y llamar a estos endpoints.

---

## Descripción del Flujo

El caso de uso esperado es:

1. El usuario abre la vista de **Importación Rápida** (gastos o ingresos).
2. Pega filas copiadas de una hoja de Excel. El frontend detecta y mapea las columnas que coincidan (título, monto, fecha, descripción).
3. El usuario llena manualmente las columnas que no venían del Excel: **categoría, método de pago, moneda, tipo de cambio, monto convertido**, y opcionalmente currency exchanges y splits por item.
4. El frontend valida el lote localmente antes de enviar.
5. Se llama al endpoint bulk. Si todos los items son válidos, se crean todos. Si alguno falla, no se crea ninguno (**all-or-nothing**).

---

## Cambios en el Backend

### Nuevos archivos creados

| Archivo | Descripción |
|---------|-------------|
| `DTOs/Expense/BulkExpenseDTOs.cs` | Request y response para importación de gastos |
| `DTOs/Income/BulkIncomeDTOs.cs` | Request y response para importación de ingresos |

### Métodos de servicio agregados

- `IExpenseService.BulkCreateAsync` — inserta todo el lote en una sola transacción
- `IIncomeService.BulkCreateAsync` — inserta todo el lote en una sola transacción

### Lógica interna relevante para el frontend

- El backend **no calcula ni deriva ningún campo**. Todo lo que no se envíe queda en `null`.
- `convertedAmount` lo calcula el frontend (`originalAmount × exchangeRate`), igual que en el formulario individual.
- `accountAmount` es opcional. Si se omite, el backend lo puede resolver internamente desde el método de pago — **pero se recomienda enviarlo** si el proyecto tiene múltiples monedas para evitar errores de ambigüedad.
- Si el método de pago tiene un partner dueño y no se envían splits, el backend genera un auto-split 100% automáticamente (mismo comportamiento que el formulario individual).
- La validación del límite de plan se evalúa para **todo el lote de una vez** antes de escribir nada.

---

## Campos Eliminados

Ninguno. No hay breaking changes.

---

## Campos Nuevos Disponibles

No hay campos nuevos en los responses existentes. Los endpoints nuevos tienen sus propios responses detallados abajo.

---

## Cambios en la API

### Endpoints Nuevos

---

#### `POST /api/projects/{projectId}/expenses/bulk`

- **Propósito:** Crea hasta 100 gastos en un solo request. Operación all-or-nothing.
- **Auth:** JWT Bearer + rol `editor` o superior en el proyecto.
- **Request:**

```json
{
  "items": [
    {
      "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "paymentMethodId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "originalAmount": 150.00,
      "originalCurrency": "USD",
      "exchangeRate": 530.000000,
      "convertedAmount": 79500.00,
      "accountAmount": 150.00,
      "title": "Factura proveedor A",
      "description": "Compra de suministros de oficina",
      "date": "2025-03-15",
      "notes": null,
      "currencyExchanges": [
        {
          "currencyCode": "EUR",
          "exchangeRate": 0.92,
          "convertedAmount": 138.00
        }
      ],
      "splits": [
        {
          "partnerId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "splitType": "percentage",
          "splitValue": 60.0,
          "resolvedAmount": 90.00,
          "currencyExchanges": null
        },
        {
          "partnerId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
          "splitType": "percentage",
          "splitValue": 40.0,
          "resolvedAmount": 60.00,
          "currencyExchanges": null
        }
      ]
    },
    {
      "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "paymentMethodId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "originalAmount": 89.50,
      "originalCurrency": "USD",
      "exchangeRate": 530.000000,
      "convertedAmount": 47435.00,
      "accountAmount": null,
      "title": "Compra papelería",
      "description": null,
      "date": "2025-03-16",
      "notes": null,
      "currencyExchanges": null,
      "splits": null
    }
  ]
}
```

- **Response `201 Created`:**

```json
{
  "created": 2,
  "items": [
    {
      "id": "e1d2c3b4-a5f6-7890-1234-567890abcdef",
      "title": "Factura proveedor A",
      "originalAmount": 150.00,
      "convertedAmount": 79500.00,
      "date": "2025-03-15"
    },
    {
      "id": "f2e3d4c5-b6a7-8901-2345-678901bcdef0",
      "title": "Compra papelería",
      "originalAmount": 89.50,
      "convertedAmount": 47435.00,
      "date": "2025-03-16"
    }
  ]
}
```

- **Prioridad:** 🟡 Importante

---

#### `POST /api/projects/{projectId}/incomes/bulk`

- **Propósito:** Crea hasta 100 ingresos en un solo request. Operación all-or-nothing.
- **Auth:** JWT Bearer + rol `editor` o superior en el proyecto.
- **Request:** Estructura idéntica al de gastos. Los nombres de campo son los mismos.

```json
{
  "items": [
    {
      "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "paymentMethodId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "originalAmount": 5000.00,
      "originalCurrency": "USD",
      "exchangeRate": 530.000000,
      "convertedAmount": 2650000.00,
      "accountAmount": 5000.00,
      "title": "Pago cliente Empresa XYZ",
      "description": "Factura #1042",
      "date": "2025-03-10",
      "notes": null,
      "currencyExchanges": null,
      "splits": null
    }
  ]
}
```

- **Response `201 Created`:**

```json
{
  "created": 1,
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "title": "Pago cliente Empresa XYZ",
      "originalAmount": 5000.00,
      "convertedAmount": 2650000.00,
      "date": "2025-03-10"
    }
  ]
}
```

- **Prioridad:** 🟡 Importante

---

## Esquema Completo del Item

Ambos endpoints (gastos e ingresos) usan la misma estructura por item:

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| `categoryId` | `uuid` | ✅ | ID de la categoría seleccionada por el usuario |
| `paymentMethodId` | `uuid` | ✅ | ID del método de pago seleccionado por el usuario |
| `originalAmount` | `decimal` | ✅ | Monto en la moneda original. Rango: `0.01 – 999,999,999,999.99` |
| `originalCurrency` | `string(3)` | ✅ | Código ISO 4217 en mayúsculas. Ej: `"USD"`, `"CRC"`, `"EUR"` |
| `exchangeRate` | `decimal` | No (default `1.0`) | Tipo de cambio a la moneda del proyecto |
| `convertedAmount` | `decimal` | ✅ | `originalAmount × exchangeRate`, calculado por el frontend |
| `accountAmount` | `decimal?` | No | Monto en la moneda del método de pago. Requerido si moneda del PM ≠ moneda original Y ≠ moneda del proyecto |
| `title` | `string` | ✅ | Máximo 255 caracteres |
| `description` | `string?` | No | Sin límite definido |
| `date` | `DateOnly` | ✅ | Formato: `"YYYY-MM-DD"` |
| `notes` | `string?` | No | Sin límite definido |
| `currencyExchanges` | `array?` | No | Ver estructura abajo |
| `splits` | `array?` | No | Ver estructura abajo. Solo relevante si el proyecto tiene `partnersEnabled = true` |

### Estructura de `currencyExchanges` (por item)

```json
"currencyExchanges": [
  {
    "currencyCode": "EUR",
    "exchangeRate": 0.920000,
    "convertedAmount": 138.00
  }
]
```

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| `currencyCode` | `string(3)` | ✅ | Código ISO 4217 en mayúsculas |
| `exchangeRate` | `decimal` | ✅ | Tipo de cambio hacia esta moneda alternativa |
| `convertedAmount` | `decimal` | ✅ | `originalAmount × exchangeRate`, calculado por el frontend |

### Estructura de `splits` (por item)

```json
"splits": [
  {
    "partnerId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "splitType": "percentage",
    "splitValue": 60.0,
    "resolvedAmount": 90.00,
    "currencyExchanges": null
  }
]
```

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| `partnerId` | `uuid` | ✅ | Partner asignado al proyecto |
| `splitType` | `"percentage" \| "fixed"` | ✅ | Tipo de split |
| `splitValue` | `decimal` | ✅ | Porcentaje (0–100) o monto fijo |
| `resolvedAmount` | `decimal` | ✅ | Monto resuelto en moneda original, calculado por el frontend |
| `currencyExchanges` | `array?` | No | Equivalencias del split en monedas alternativas |

**Reglas de validación para splits:**
- No se pueden mezclar tipos: todos deben ser `percentage` o todos `fixed`.
- Si son `percentage`: la suma debe ser exactamente `100` (tolerancia `±0.01`).
- Si son `fixed`: la suma debe igualar `originalAmount` del item (tolerancia `±0.01`).
- No se pueden repetir partners dentro del mismo item.
- Todos los partners deben estar asignados al proyecto.

---

## Errores Posibles

| Código HTTP | Causa | Acción sugerida en UI |
|-------------|-------|-----------------------|
| `400` | Algún campo obligatorio falta o tiene formato inválido. El body incluye detalles por campo con su índice (ej: `Items[2].OriginalCurrency`) | Mostrar errores inline por fila en la tabla de importación |
| `403` | Sin acceso al proyecto, rol insuficiente, o límite de plan excedido para el lote completo | Mostrar mensaje con razón del plan si está disponible |
| `404` / `500` | Categoría, método de pago o partner no existen o no están vinculados al proyecto | Mostrar error genérico y sugerir verificar la configuración |

---

## Notas de Implementación para Frontend

### Vista de Importación Rápida

1. **Tabla editable** — cada fila es un movimiento. Las columnas que pueden venir de Excel (título, monto, fecha, descripción) se auto-populan al pegar. El resto se llena manualmente por el usuario en celdas editables de la tabla.

2. **Campos por item que el usuario llena manualmente:**
   - Categoría (selector)
   - Método de pago (selector)
   - Moneda original (selector ISO 4217)
   - Tipo de cambio (input numérico)
   - Monto convertido (calculado automáticamente por el frontend como `monto × tipo de cambio`, pero editable)
   - Account amount (solo si aplica diferencia de monedas)
   - Currency exchanges (si el proyecto tiene monedas alternativas)
   - Splits (si `project.partnersEnabled = true`)

3. **Validación local antes de enviar** — el frontend debe validar todo el lote antes de llamar al endpoint para dar feedback inmediato. El backend también valida, pero el error del backend describe la fila con su índice (`Items[n].Campo`).

4. **All-or-nothing** — si el backend retorna `400` o `403`, ningún item fue creado. La UI debe mostrar los errores y permitir al usuario corregir sin perder lo que llenó.

5. **Límite de 100 items** — mostrar contador en la UI y deshabilitar "agregar fila" al llegar a 100.

6. **Respuesta del backend** — el response `201` incluye los IDs creados. Se recomienda invalidar el cache de la lista de movimientos y redirigir o mostrar un resumen de lo importado.

### Cálculo de `convertedAmount`

```
convertedAmount = Math.round(originalAmount * exchangeRate, 2)
```

Si `originalCurrency` es igual a la moneda del proyecto, el frontend debe precargar `exchangeRate = 1` y `convertedAmount = originalAmount`.

### Cálculo de `accountAmount`

- Si `paymentMethod.currency === originalCurrency` → `accountAmount = originalAmount`
- Si `paymentMethod.currency === projectCurrency` → `accountAmount = convertedAmount`
- Si ambas son distintas → el usuario debe ingresar el monto manualmente

Referencia: mismo comportamiento que el formulario individual de creación de gasto/ingreso.

---

## Preguntas Abiertas

- [ ] ¿La vista de importación rápida es una ruta nueva o un modal sobre la lista existente?
El front va a abrir abrir un modal donde va a poder pegar los datos copiados de Excel. Los datos se cargan visualmente en la tabla y el usuario llena los datos obligatorios faltantes (categoría, método de pago, moneda, tipo de cambio, monto convertido).
- [ ] ¿Debe el frontend pre-seleccionar categoría/método de pago por defecto para nuevas filas (ej: el último usado)?No
- [ ] ¿El proyecto siempre expone `partnersEnabled` en el response de detalle de proyecto? Confirmar que el frontend ya tiene este dato disponible para mostrar/ocultar la columna de splits.El frontend ya sabe o debe saber si el proyecto tiene habilitados los partners.
- [ ] ¿Se requiere soporte para importar desde archivo `.xlsx` directamente, o solo pegado desde portapapeles?
De momento solo pegado desde portapapeles.
- [ ] ¿El response del bulk (`id`, `title`, `amount`, `date`) es suficiente para mostrar el resumen post-importación, o se necesita re-fetchear los registros completos?No es necesario un response, mejor se refresca la lista completa después de la importación para mostrar los nuevos items con toda su info.
