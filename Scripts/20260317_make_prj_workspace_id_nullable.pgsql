-- Migración: hacer prj_workspace_id nullable en la tabla projects
-- Fecha: 2026-03-17
-- Motivo: El modelo C# tiene PrjWorkspaceId como Guid? (nullable). El constraint NOT NULL
--         causaba un error 500 al crear proyectos cuando el usuario no tenía un workspace
--         "General" todavía. Los requerimientos dicen que workspace_id puede ser null
--         si el proyecto no pertenece a ningún workspace.

ALTER TABLE public.projects ALTER COLUMN prj_workspace_id DROP NOT NULL;
