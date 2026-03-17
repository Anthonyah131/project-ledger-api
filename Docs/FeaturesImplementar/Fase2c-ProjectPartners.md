# Fase 2c — Project Partners y Métodos de Pago derivados

> Prerrequisito: Fase 2a completada.
> Riesgo: **Bajo** | Estimado: 1-2 semanas

---

## Objetivo

Reemplazar `project_payment_methods` por `project_partners` como mecanismo de control de qué entidades financieras participan en un proyecto. Los métodos de pago disponibles se derivan automáticamente de los partners asignados.

También es el momento de deprecar y eliminar `pmt_owner_user_id` de `payment_methods`.

---

## 2c.1 Nueva tabla: `project_partners`

### Script SQL (`Scripts/add_project_partners_table.sql`)

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
  CONSTRAINT ptp_project_fkey FOREIGN KEY (ptp_project_id) REFERENCES public.projects(prj_id),
  CONSTRAINT ptp_partner_fkey FOREIGN KEY (ptp_partner_id) REFERENCES public.partners(ptr_id),
  CONSTRAINT ptp_added_by_fkey FOREIGN KEY (ptp_added_by_user_id) REFERENCES public.users(usr_id),
  CONSTRAINT ptp_deleted_by_fkey FOREIGN KEY (ptp_deleted_by_user_id) REFERENCES public.users(usr_id),
  UNIQUE INDEX uq_ptp_project_partner_active (ptp_project_id ASC, ptp_partner_id ASC)
    WHERE ptp_is_deleted = false,
  INDEX idx_ptp_project_id (ptp_project_id ASC),
  INDEX idx_ptp_partner_id (ptp_partner_id ASC),
  INDEX idx_ptp_is_deleted (ptp_is_deleted ASC)
);
COMMENT ON TABLE public.project_partners IS
  'Partners asignados a un proyecto. Los métodos de pago disponibles se derivan de estos partners.';
```

---

## 2c.2 Migración de datos existentes

### Script (`Scripts/migrate_project_partners_from_payment_methods.sql`)

```sql
-- Poblar project_partners desde project_payment_methods existentes
INSERT INTO project_partners (ptp_project_id, ptp_partner_id, ptp_added_by_user_id)
SELECT DISTINCT
  ppm.ppm_project_id,
  pm.pmt_owner_partner_id,
  ppm.ppm_added_by_user_id
FROM project_payment_methods ppm
JOIN payment_methods pm ON pm.pmt_id = ppm.ppm_payment_method_id
WHERE pm.pmt_owner_partner_id IS NOT NULL
  AND pm.pmt_is_deleted = false;
```

Después de confirmar que la migración es correcta y que todos los endpoints usan el nuevo esquema:

```sql
-- Deprecar pmt_owner_user_id (ejecutar solo tras confirmar migración completa)
ALTER TABLE public.payment_methods DROP COLUMN pmt_owner_user_id;
```

---

## 2c.3 Query de métodos de pago disponibles en un proyecto

```sql
SELECT pm.*
FROM payment_methods pm
JOIN partners ptr ON ptr.ptr_id = pm.pmt_owner_partner_id
JOIN project_partners ptp ON ptp.ptp_partner_id = ptr.ptr_id
WHERE ptp.ptp_project_id = :project_id
  AND ptp.ptp_is_deleted = false
  AND pm.pmt_is_deleted = false;
```

> Si se agrega un nuevo método de pago a un partner ya asignado al proyecto, queda disponible de inmediato sin pasos adicionales.

---

## 2c.4 Nuevos endpoints

### `GET /projects/:id/partners`
Lista los partners asignados al proyecto.

### `POST /projects/:id/partners`
Asignar un partner al proyecto. Body: `{ partner_id }`.
- Validar que el partner pertenece al usuario autenticado.
- Validar que no esté ya asignado.

### `DELETE /projects/:id/partners/:partnerId`
Soft-delete del partner del proyecto.
- Validar que no tenga splits activos en el proyecto (si Fase 3 ya está implementada).

### `GET /projects/:id/available-payment-methods`
Reemplaza `GET /projects/:id/payment-methods`.
Devuelve las cuentas derivadas de los partners asignados, agrupadas por partner.

```json
{
  "project_id": "uuid",
  "partners": [
    {
      "partner_id": "uuid",
      "partner_name": "Nondier Amariles",
      "payment_methods": [
        { "id": "uuid", "name": "Cuenta SINPE Harold", "currency": "CRC" }
      ]
    }
  ]
}
```

---

## 2c.5 Archivos a crear/modificar

| Capa | Archivo | Acción |
|---|---|---|
| Script SQL | `Scripts/add_project_partners_table.sql` | Crear |
| Script SQL | `Scripts/migrate_project_partners_from_payment_methods.sql` | Crear |
| Model | `Models/ProjectPartner.cs` | Crear |
| Config EF | `Configurations/ProjectPartnerConfiguration.cs` | Crear |
| DbContext | `Data/AppDbContext.cs` | Agregar `DbSet<ProjectPartner>` |
| DTO | `DTOs/Projects/ProjectPartnerDto.cs` | Crear |
| DTO | `DTOs/Projects/AvailablePaymentMethodsDto.cs` | Crear |
| Repository | `Repositories/ProjectPartnerRepository.cs` | Crear |
| Service | `Services/ProjectPartnerService.cs` | Crear |
| Controller | `Controllers/ProjectController.cs` | Modificar: nuevos endpoints de partners |
| Service | `Services/ExpenseService.cs` | Modificar: usar nueva query de métodos disponibles |
| Service | `Services/IncomeService.cs` | Modificar: usar nueva query de métodos disponibles |
| Extensions | `Extensions/ServiceCollectionExtensions.cs` | Registrar repo y service |

---

## 2c.6 Deprecación de `project_payment_methods`

Una vez que:
1. Todos los endpoints usan `project_partners` para determinar los métodos disponibles.
2. La migración está confirmada y todos los proyectos tienen sus partners.
3. No hay referencias a `project_payment_methods` en el código.

Ejecutar:
```sql
-- Opcional: mantener como read-only para historial, o eliminar directamente
DROP TABLE public.project_payment_methods;
```

---

## 2c.7 Reglas de negocio

- Solo el owner/editor del proyecto puede agregar o quitar partners.
- Solo se pueden asignar partners que pertenecen al usuario autenticado (dueño del proyecto).
- Al quitar un partner del proyecto, sus métodos de pago dejan de estar disponibles para nuevas transacciones. Las transacciones históricas no se modifican.
- Un partner puede estar en múltiples proyectos simultáneamente.

---

## Criterios de aceptación

- [ ] `GET /projects/:id/available-payment-methods` devuelve cuentas derivadas de partners.
- [ ] Agregar partner al proyecto hace disponibles sus cuentas de inmediato.
- [ ] La migración pobló `project_partners` correctamente desde `project_payment_methods`.
- [ ] `pmt_owner_user_id` eliminado tras confirmar migración.
- [ ] Crear gastos/ingresos sigue funcionando, pero ahora usando el nuevo query de cuentas disponibles.
