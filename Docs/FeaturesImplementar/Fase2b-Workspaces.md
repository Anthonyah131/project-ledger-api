# Fase 2b — Workspaces

> Prerrequisito: Fase 2a completada.
> Riesgo: **Bajo** | Estimado: 1 semana

---

## Objetivo

Introducir `workspaces` como agrupador de proyectos relacionados. Cada proyecto pasa a pertenecer a exactamente un workspace. Incluye migración de todos los proyectos existentes a un workspace "General".

---

## Concepto

Un **workspace** (espacio de trabajo) agrupa proyectos con contexto común: "Casa", "Empresa ABC", "Sociedad Miravalles". El usuario puede tener múltiples workspaces y puede invitar a otros usuarios del sistema a uno (sin heredar acceso automático a los proyectos dentro).

---

## 2b.1 Nuevas tablas

### Script SQL (`Scripts/add_workspaces_tables.sql`)

```sql
CREATE TABLE public.workspaces (
  wks_id UUID NOT NULL DEFAULT gen_random_uuid(),
  wks_name VARCHAR(255) NOT NULL,
  wks_owner_user_id UUID NOT NULL,
  wks_description STRING NULL,
  wks_color VARCHAR(7) NULL,
  wks_icon VARCHAR(50) NULL,
  wks_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wks_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wks_is_deleted BOOL NOT NULL DEFAULT false,
  wks_deleted_at TIMESTAMPTZ NULL,
  wks_deleted_by_user_id UUID NULL,
  CONSTRAINT workspaces_pkey PRIMARY KEY (wks_id ASC),
  CONSTRAINT wks_owner_fkey FOREIGN KEY (wks_owner_user_id) REFERENCES public.users(usr_id),
  CONSTRAINT wks_deleted_by_fkey FOREIGN KEY (wks_deleted_by_user_id) REFERENCES public.users(usr_id),
  INDEX idx_wks_owner_user_id (wks_owner_user_id ASC),
  INDEX idx_wks_is_deleted (wks_is_deleted ASC)
);

CREATE TABLE public.workspace_members (
  wkm_id UUID NOT NULL DEFAULT gen_random_uuid(),
  wkm_workspace_id UUID NOT NULL,
  wkm_user_id UUID NOT NULL,
  wkm_role VARCHAR(20) NOT NULL DEFAULT 'member',
  wkm_joined_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wkm_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wkm_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
  wkm_is_deleted BOOL NOT NULL DEFAULT false,
  wkm_deleted_at TIMESTAMPTZ NULL,
  wkm_deleted_by_user_id UUID NULL,
  CONSTRAINT workspace_members_pkey PRIMARY KEY (wkm_id ASC),
  CONSTRAINT wkm_workspace_fkey FOREIGN KEY (wkm_workspace_id) REFERENCES public.workspaces(wks_id),
  CONSTRAINT wkm_user_fkey FOREIGN KEY (wkm_user_id) REFERENCES public.users(usr_id),
  CONSTRAINT wkm_deleted_by_fkey FOREIGN KEY (wkm_deleted_by_user_id) REFERENCES public.users(usr_id),
  UNIQUE INDEX uq_wkm_workspace_user_active (wkm_workspace_id ASC, wkm_user_id ASC)
    WHERE wkm_is_deleted = false,
  INDEX idx_wkm_workspace_id (wkm_workspace_id ASC),
  INDEX idx_wkm_user_id (wkm_user_id ASC),
  INDEX idx_wkm_is_deleted (wkm_is_deleted ASC),
  CONSTRAINT wkm_role_check CHECK (wkm_role IN ('owner':::STRING, 'member':::STRING))
);
```

### Modificación en `projects` (`Scripts/add_prj_workspace_id.sql`)

```sql
-- Primero se crean workspaces (migración), luego se activa NOT NULL
ALTER TABLE public.projects
  ADD COLUMN prj_workspace_id UUID NULL
    REFERENCES public.workspaces(wks_id),
  ADD COLUMN prj_partners_enabled BOOL NOT NULL DEFAULT false;

CREATE INDEX idx_prj_workspace_id ON public.projects (prj_workspace_id ASC);
```

---

## 2b.2 Migración de datos existentes

### Script (`Scripts/migrate_workspaces_from_projects.sql`)

```sql
-- PASO 1: Crear workspace "General" para cada usuario con proyectos
INSERT INTO workspaces (wks_name, wks_owner_user_id)
SELECT 'General', prj_owner_user_id
FROM projects
WHERE prj_is_deleted = false
GROUP BY prj_owner_user_id;

-- PASO 2: Owner como miembro del workspace
INSERT INTO workspace_members (wkm_workspace_id, wkm_user_id, wkm_role)
SELECT wks_id, wks_owner_user_id, 'owner'
FROM workspaces;

-- PASO 3: Asignar proyectos a su workspace
UPDATE projects p
SET prj_workspace_id = w.wks_id
FROM workspaces w
WHERE w.wks_owner_user_id = p.prj_owner_user_id
  AND p.prj_is_deleted = false;

-- PASO 4: Activar restricción NOT NULL (después de verificar que todos tienen workspace)
ALTER TABLE public.projects ALTER COLUMN prj_workspace_id SET NOT NULL;
```

---

## 2b.3 Modelos y configuración EF Core

### Nuevos modelos

**`Models/Workspace.cs`**: `WksId`, `WksName`, `WksOwnerUserId`, `WksDescription`, `WksColor`, `WksIcon`, campos de auditoría. Navegación: `OwnerUser`, `Members` (colección `WorkspaceMember`), `Projects` (colección).

**`Models/WorkspaceMember.cs`**: `WkmId`, `WkmWorkspaceId`, `WkmUserId`, `WkmRole`, `WkmJoinedAt`, campos de auditoría. Navegación: `Workspace`, `User`.

### Nuevas configuraciones

`Configurations/WorkspaceConfiguration.cs` — tabla `workspaces`, columnas con prefijo `wks_`.

`Configurations/WorkspaceMemberConfiguration.cs` — tabla `workspace_members`, columnas con prefijo `wkm_`.

### Modificación: `Models/Project.cs`

Agregar: `PrjWorkspaceId` (UUID), `PrjPartnersEnabled` (bool, default false). Navegación: `Workspace`.

### Modificación: `Configurations/ProjectConfiguration.cs`

Mapear `prj_workspace_id` y `prj_partners_enabled`.

### `Data/AppDbContext.cs`

Agregar `DbSet<Workspace>` y `DbSet<WorkspaceMember>`.

---

## 2b.4 Endpoints de Workspaces

### `POST /workspaces`
Crear workspace. Body: `{ name, description?, color?, icon? }`. El creador queda como `owner` en `workspace_members` automáticamente.

### `GET /workspaces`
Lista los workspaces donde el usuario es miembro (activos). Incluir conteo de proyectos.

### `GET /workspaces/:id`
Detalle del workspace con lista de proyectos y miembros.

### `PATCH /workspaces/:id`
Actualizar nombre, descripción, color, icono. Solo el owner puede modificarlo.

### `DELETE /workspaces/:id`
Soft-delete. Validar que no tenga proyectos activos antes de borrar. Solo el owner.

### `GET /workspaces/:id/summary`
Resumen consolidado de todos los proyectos del workspace en una moneda de referencia.

```json
{
  "workspace": "Miravalles",
  "reference_currency": "USD",
  "projects": [
    {
      "id": "uuid",
      "name": "Compra del lote",
      "currency": "CRC",
      "total_income_converted": 5200.00,
      "total_expenses_converted": 3800.00,
      "net_converted": 1400.00
    }
  ],
  "totals": {
    "total_income_converted": 6400.00,
    "total_expenses_converted": 4731.78,
    "net_converted": 1668.22,
    "note": "Conversiones aproximadas basadas en los tipos de cambio de cada transacción."
  }
}
```

> `GET /workspaces/:id/summary` requiere `pln_can_use_advanced_reports = true`.

---

## 2b.5 Archivos a crear/modificar

| Capa | Archivo | Acción |
|---|---|---|
| Script SQL | `Scripts/add_workspaces_tables.sql` | Crear |
| Script SQL | `Scripts/add_prj_workspace_id.sql` | Crear |
| Script SQL | `Scripts/migrate_workspaces_from_projects.sql` | Crear |
| Model | `Models/Workspace.cs` | Crear |
| Model | `Models/WorkspaceMember.cs` | Crear |
| Config EF | `Configurations/WorkspaceConfiguration.cs` | Crear |
| Config EF | `Configurations/WorkspaceMemberConfiguration.cs` | Crear |
| Model | `Models/Project.cs` | Modificar: `PrjWorkspaceId`, `PrjPartnersEnabled` |
| Config EF | `Configurations/ProjectConfiguration.cs` | Modificar: nuevas columnas |
| DbContext | `Data/AppDbContext.cs` | Modificar: agregar DbSets |
| DTO | `DTOs/Workspaces/WorkspaceDto.cs` | Crear |
| DTO | `DTOs/Workspaces/CreateWorkspaceDto.cs` | Crear |
| DTO | `DTOs/Workspaces/WorkspaceSummaryDto.cs` | Crear |
| Repository | `Repositories/WorkspaceRepository.cs` | Crear |
| Service | `Services/WorkspaceService.cs` | Crear |
| Controller | `Controllers/WorkspaceController.cs` | Crear |
| Extensions | `Extensions/ServiceCollectionExtensions.cs` | Registrar repo y service |

---

## 2b.6 Reglas de negocio

- Un usuario solo puede ver workspaces donde es miembro (`wkm_user_id = currentUserId`).
- Solo el owner puede modificar o eliminar un workspace.
- Un workspace no puede eliminarse si tiene proyectos activos.
- Si el usuario tiene un solo workspace, se selecciona automáticamente (sin selector en UI).
- Los miembros del workspace NO heredan acceso a los proyectos dentro de él — el acceso a proyectos se sigue controlando por `project_members`.
- Al crear un proyecto, se debe especificar `workspace_id` (obligatorio desde esta fase).

---

## Criterios de aceptación

- [ ] CRUD de workspaces funcionando.
- [ ] Todo proyecto existente tiene `prj_workspace_id` asignado tras la migración.
- [ ] Un nuevo proyecto requiere `workspace_id` en el body del `POST /projects`.
- [ ] `GET /workspaces/:id/summary` devuelve totales consolidados en moneda de referencia.
- [ ] Un usuario no puede ver workspaces ajenos.
