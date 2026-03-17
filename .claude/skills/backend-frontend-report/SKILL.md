---
name: backend-frontend-report
description: >
  Genera un README de integración frontend al final de una conversación de desarrollo backend.
  Úsalo SIEMPRE que el usuario diga frases como "genera el reporte para el frontend", "crea el README del front",
  "documenta los cambios para el front", "haz el informe para el frontend", o cualquier variante.
  También úsalo si el usuario terminó cambios en el backend (endpoints, modelos, base de datos, servicios, lógica de negocio)
  y pide documentar qué necesita saber el frontend. No esperes que el usuario mencione explícitamente
  "skill" — si hay trabajo backend terminado y se pide algún tipo de reporte o documentación para frontend, usa esta skill.
---

# Skill: Backend → Frontend Integration Report

## Objetivo

Producir un **README técnico para desarrolladores frontend** que explique con precisión qué cambió en el backend, qué impacto tiene en la integración con la API, y qué acciones debe tomar el frontend.

Este documento se genera **al final de una tarea o conversación**, después de que el backend fue modificado.

---

## Proceso de Ejecución

### 1. Leer el contexto de la conversación

Antes de generar nada, analiza toda la conversación para extraer:

- ¿Qué endpoints fueron creados, modificados o eliminados?
- ¿Cambiaron modelos o esquemas de base de datos que afecten respuestas de la API?
- ¿Hay campos nuevos, renombrados o eliminados en los responses?
- ¿Cambiaron reglas de validación o autenticación?
- ¿Hay cambios de formato en requests o responses (paginación, tipos, estructura)?
- ¿Hay breaking changes que rompan integraciones existentes del frontend?

No inventes cambios. Si no hay información suficiente, indica qué falta con `[POR CONFIRMAR]`.

---

### 2. Clasificar el impacto

Clasifica cada cambio en una de estas categorías:

| Categoría | Descripción |
|-----------|-------------|
| 🔴 Breaking | El frontend dejará de funcionar sin este cambio |
| 🟡 Importante | Funcionalidad nueva que el frontend debe implementar |
| 🟢 Opcional | Campo nuevo o mejora que el frontend puede aprovechar |

---

### 3. Generar el README

Produce el documento siguiendo exactamente esta estructura:

```markdown
# Backend → Frontend Integration Report

> Generado el: [FECHA]
> Proyecto: [NOMBRE SI SE CONOCE]
> Autor backend: [SI SE MENCIONA]

---

## Resumen Ejecutivo

[2-4 líneas: qué cambió en el backend y cuál es el impacto general en el frontend]

---

## Cambios en el Backend

[Descripción clara de qué se modificó: endpoints, modelos, servicios, lógica de negocio, esquema de base de datos, etc.]

---

## Campos Eliminados

Campos que el backend ya **no devuelve**. El frontend debe dejar de consumirlos.

| Campo | Endpoint afectado | Motivo | Prioridad |
|-------|-------------------|--------|-----------|
| `field_name` | `GET /resource` | Deprecado en modelo | 🔴 Breaking |

---

## Campos Nuevos Disponibles

Nuevos campos en los responses que el frontend **puede o debe consumir**.

| Campo | Tipo | Endpoint | Descripción | Prioridad |
|-------|------|----------|-------------|-----------|
| `field_name` | `string` | `GET /resource` | Descripción del dato | 🟡 Importante |

---

## Cambios en la API

### Endpoints Nuevos

Para cada endpoint nuevo:

**`METHOD /ruta`**
- **Propósito:** [qué hace]
- **Request:**
```json
{ "ejemplo": "valor" }
```
- **Response:**
```json
{ "campo": "valor" }
```
- **Prioridad:** 🔴 / 🟡 / 🟢

---

### Endpoints Modificados

**`METHOD /ruta`**
- **Cambio:** [qué cambió y por qué]
- **Antes:**
```json
{ "campo_viejo": "..." }
```
- **Después:**
```json
{ "campo_nuevo": "..." }
```
- **Acción requerida del frontend:** [qué debe actualizar]
- **Prioridad:** 🔴 / 🟡 / 🟢

---

### Endpoints Deprecados

| Endpoint | Motivo | Reemplazo sugerido | Prioridad |
|----------|--------|--------------------|-----------|
| `GET /old-route` | Lógica eliminada del backend | `GET /new-route` | 🔴 Breaking |

---

## Cambios en Modelos

Si cambiaron modelos o esquema de base de datos que afectan los responses:

**Modelo: `NombreModelo`**

| Campo | Cambio | Tipo | Impacto en frontend |
|-------|--------|------|---------------------|
| `nuevo_campo` | Agregado | `string[]` | Disponible para mostrar en UI |
| `campo_viejo` | Eliminado | `number` | Dejar de leer de response |

---

## Notas de Implementación para Frontend

[Guía práctica: qué formularios actualizar, qué validaciones cambiaron, qué datos nuevos mostrar en UI, qué dejar de usar, etc.]

---

## Preguntas Abiertas

[Puntos que el frontend debe confirmar con el backend antes de implementar.]

- [ ] ¿El campo X es obligatorio en el request o es opcional?
- [ ] ¿El endpoint Y requiere permisos especiales?
```

---

## Reglas de Calidad

**Siempre:**
- Incluir ejemplos de payload reales (no genéricos como `"value": "string"`)
- Marcar claramente los breaking changes con 🔴
- Usar nombres de campo y rutas exactos si se mencionaron en la conversación
- Anotar `[POR CONFIRMAR]` donde haya ambigüedad

**Nunca:**
- Modificar código del backend
- Inventar endpoints o cambios que no emergen de la conversación
- Usar lenguaje vago como "probablemente" o "tal vez debería"
- Omitir breaking changes aunque parezcan menores

---

## Nombre del Archivo de Salida

Guardar como:

```
README_FRONTEND_INTEGRATION.md
```

Presentar al usuario con `present_files` al finalizar.