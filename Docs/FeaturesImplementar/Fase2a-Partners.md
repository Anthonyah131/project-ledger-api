# Fase 2a — Partners: Entidad, CRUD y Migración

> Prerrequisito: Fase 1 completada (recomendado, no bloqueante).
> Riesgo: **Medio** (migración de FK) | Estimado: 2 semanas

---

## Objetivo

Introducir `partners` como dueños de los métodos de pago, reemplazando `pmt_owner_user_id` por `pmt_owner_partner_id`. Incluye migración de datos existentes.

---

## Concepto

Un **partner** es un contacto financiero que el usuario crea y gestiona globalmente. Tiene nombre y datos opcionales. Puede tener uno o varios métodos de pago. No tiene vínculo al sistema de usuarios.

El usuario puede crear un partner que lo represente a sí mismo (con el nombre que prefiera), e incluir partners externos (socios, familiares, etc.).

---

## 2a.1 Nueva tabla: `partners`

### Script SQL (`Scripts/add_partners_table.sql`)

```sql
CREATE TABLE public.partners (
  ptr_id UUID NOT NULL DEFAULT gen_random_uuid(),
  ptr_owner_user_id UUID NOT NULL,
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

### Modificación en `payment_methods` (`Scripts/add_pmt_owner_partner_id.sql`)

```sql
ALTER TABLE public.payment_methods
  ADD COLUMN pmt_owner_partner_id UUID NULL
    REFERENCES public.partners(ptr_id);

CREATE INDEX idx_pmt_owner_partner_id
  ON public.payment_methods (pmt_owner_partner_id ASC)
  WHERE pmt_owner_partner_id IS NOT NULL;
```

> `pmt_owner_user_id` y `pmt_owner_partner_id` coexisten durante la transición.
> `pmt_owner_user_id` se elimina en Fase 2c una vez migrados todos los endpoints.

---

## 2a.2 Migración de datos existentes

### Script (`Scripts/migrate_partners_from_users.sql`)

```sql
-- PASO 1: Crear un partner por cada usuario existente
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
```

---

## 2a.3 Modelo y configuración EF Core

### Nuevo modelo: `Models/Partner.cs`

Campos: `PtrId`, `PtrOwnerUserId`, `PtrName`, `PtrEmail`, `PtrPhone`, `PtrNotes`, `PtrCreatedAt`, `PtrUpdatedAt`, `PtrIsDeleted`, `PtrDeletedAt`, `PtrDeletedByUserId`.

Navegación: `OwnerUser`, `DeletedByUser`, `PaymentMethods` (colección), `ProjectPartners` (colección — Fase 2c).

### Nueva configuración: `Configurations/PartnerConfiguration.cs`

Tabla `partners`, todas las columnas en snake_case con prefijo `ptr_`, FK a `users`.

### Modificación: `Models/PaymentMethod.cs`

Agregar: `PmtOwnerPartnerId` (nullable UUID) + navegación `OwnerPartner`.

### Modificación: `Configurations/PaymentMethodConfiguration.cs`

Mapear la nueva columna `pmt_owner_partner_id` y la FK a `partners`.

### Modificación: `Data/AppDbContext.cs`

Agregar `DbSet<Partner> Partners`.

---

## 2a.4 Endpoints CRUD de Partners

### `GET /partners`
Lista los partners del usuario autenticado (no eliminados). Soporta paginación y búsqueda por nombre.

### `GET /partners/:id`
Detalle de un partner con sus payment methods asociados.

### `POST /partners`
Crear partner. Body: `{ name, email?, phone?, notes? }`.

### `PATCH /partners/:id`
Actualizar campos opcionales. No permite cambiar `owner_user_id`.

### `DELETE /partners/:id`
Soft-delete. Validar que el partner no está asignado a proyectos activos antes de borrar.

### `GET /partners/:id/payment-methods`
Lista los métodos de pago del partner.

---

## 2a.5 Archivos a crear/modificar

| Capa | Archivo | Acción |
|---|---|---|
| Script SQL | `Scripts/add_partners_table.sql` | Crear |
| Script SQL | `Scripts/add_pmt_owner_partner_id.sql` | Crear |
| Script SQL | `Scripts/migrate_partners_from_users.sql` | Crear |
| Model | `Models/Partner.cs` | Crear |
| Config EF | `Configurations/PartnerConfiguration.cs` | Crear |
| Model | `Models/PaymentMethod.cs` | Modificar: agregar columna + nav |
| Config EF | `Configurations/PaymentMethodConfiguration.cs` | Modificar: nueva FK |
| DbContext | `Data/AppDbContext.cs` | Modificar: agregar DbSet |
| DTO | `DTOs/Partners/PartnerDto.cs` | Crear |
| DTO | `DTOs/Partners/CreatePartnerDto.cs` | Crear |
| DTO | `DTOs/Partners/UpdatePartnerDto.cs` | Crear |
| Repository | `Repositories/PartnerRepository.cs` | Crear |
| Service | `Services/PartnerService.cs` | Crear |
| Controller | `Controllers/PartnerController.cs` | Crear |
| Extensions | `Extensions/ServiceCollectionExtensions.cs` | Registrar repositorio y servicio |

---

## 2a.6 Reglas de negocio

- Un usuario solo puede ver/editar sus propios partners (`ptr_owner_user_id = currentUserId`).
- Un partner no puede eliminarse si está asignado a proyectos activos (verificar `project_partners` en Fase 2c; por ahora verificar si tiene payment methods en proyectos activos).
- Nombre obligatorio, máx. 255 caracteres.
- Email y teléfono son opcionales y de solo referencia (no se usan para autenticación).

---

## Criterios de aceptación

- [ ] CRUD completo de partners funcionando.
- [ ] Script de migración ejecutado: cada usuario tiene su partner y sus payment methods tienen `pmt_owner_partner_id` asignado.
- [ ] `GET /partners/:id/payment-methods` devuelve las cuentas del partner.
- [ ] Los endpoints de payment methods existentes siguen funcionando (columna vieja intacta).
- [ ] Un usuario no puede ver los partners de otro usuario.
