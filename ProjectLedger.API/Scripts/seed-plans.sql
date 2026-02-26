-- ============================================================
-- SEED: Planes del sistema (Free, Basic, Premium)
-- Compatible con PostgreSQL 16+ / CockroachDB
-- ============================================================
-- Ejecutar DESPUÉS de haber creado la tabla `plans`.
-- Usa INSERT ... ON CONFLICT DO NOTHING para ser idempotente.
-- ============================================================

INSERT INTO public.plans (
    pln_id,
    pln_name,
    pln_slug,
    pln_description,
    pln_is_active,
    pln_display_order,

    -- Permisos
    pln_can_create_projects,
    pln_can_edit_projects,
    pln_can_delete_projects,
    pln_can_share_projects,
    pln_can_export_data,
    pln_can_use_advanced_reports,
    pln_can_use_ocr,
    pln_can_use_api,
    pln_can_use_multi_currency,
    pln_can_set_budgets,

    -- Límites
    pln_limits,

    pln_created_at,
    pln_updated_at
)
VALUES

-- ============================================================
-- PLAN 1: FREE
-- ============================================================
-- Ideal para usuarios individuales que quieren probar el sistema.
-- Proyectos y gastos limitados. Sin exportación ni sharing.
-- ============================================================
(
    gen_random_uuid(),
    'Free',
    'free',
    'Plan gratuito para uso personal básico. Perfecto para empezar a organizar tus finanzas.',
    true,
    1,

    -- Permisos
    true,    -- can_create_projects
    true,    -- can_edit_projects
    true,    -- can_delete_projects
    false,   -- can_share_projects       ← no puede compartir proyectos
    false,   -- can_export_data          ← sin exportación
    false,   -- can_use_advanced_reports ← sin reportes avanzados
    false,   -- can_use_ocr              ← sin OCR
    false,   -- can_use_api              ← sin acceso a API
    true,    -- can_use_multi_currency
    true,    -- can_set_budgets

    -- Límites
    '{
        "max_projects": 2,
        "max_expenses": 30,
        "max_categories_per_project": 5,
        "max_payment_methods": 2,
        "max_team_members_per_project": 0
    }'::jsonb,

    now(),
    now()
),

-- ============================================================
-- PLAN 2: BASIC
-- ============================================================
-- Para freelancers y pequeños equipos.
-- Más proyectos, gastos ilimitados, exportación y colaboración.
-- ============================================================
(
    gen_random_uuid(),
    'Basic',
    'basic',
    'Plan básico para freelancers y equipos pequeños. Incluye exportación y colaboración.',
    true,
    2,

    -- Permisos
    true,    -- can_create_projects
    true,    -- can_edit_projects
    true,    -- can_delete_projects
    true,    -- can_share_projects       ← puede compartir proyectos
    true,    -- can_export_data          ← exportación habilitada (CSV/Excel)
    false,   -- can_use_advanced_reports ← sin reportes avanzados
    false,   -- can_use_ocr              ← sin OCR
    false,   -- can_use_api              ← sin acceso a API
    true,    -- can_use_multi_currency
    true,    -- can_set_budgets

    -- Límites
    '{
        "max_projects": 10,
        "max_expenses": 200,
        "max_categories_per_project": 20,
        "max_payment_methods": 10,
        "max_team_members_per_project": 5
    }'::jsonb,

    now(),
    now()
),

-- ============================================================
-- PLAN 3: PREMIUM
-- ============================================================
-- Sin límites. Todas las funcionalidades habilitadas.
-- Para empresas y equipos que necesitan el sistema completo.
-- ============================================================
(
    gen_random_uuid(),
    'Premium',
    'premium',
    'Plan premium sin límites. Todas las funcionalidades: OCR, API, reportes avanzados y colaboración ilimitada.',
    true,
    3,

    -- Permisos
    true,    -- can_create_projects
    true,    -- can_edit_projects
    true,    -- can_delete_projects
    true,    -- can_share_projects       ← colaboración
    true,    -- can_export_data          ← exportación
    true,    -- can_use_advanced_reports ← reportes avanzados
    true,    -- can_use_ocr              ← OCR de recibos
    true,    -- can_use_api              ← acceso a API pública
    true,    -- can_use_multi_currency
    true,    -- can_set_budgets

    -- Sin límites (null = ilimitado)
    '{
        "max_projects": null,
        "max_expenses": null,
        "max_categories_per_project": null,
        "max_payment_methods": null,
        "max_team_members_per_project": null
    }'::jsonb,

    now(),
    now()
)

ON CONFLICT (pln_slug) DO NOTHING;

-- ============================================================
-- SEED: Usuarios de prueba
-- ============================================================
-- Contraseña de ambos usuarios: 123  (BCrypt work factor 12)
-- ============================================================

INSERT INTO public.users (
    usr_id,
    usr_email,
    usr_password_hash,
    usr_full_name,
    usr_plan_id,
    usr_is_active,
    usr_is_admin,
    usr_created_at,
    usr_updated_at
)
VALUES
(
    gen_random_uuid(),
    'anthonyah131@gmail.com',
    '$2a$12$gO9ZBRRBE3be9HNHq1Kfz.xFUkRQYVv0hU971I5zW7GXmSv2msloO',  -- 123
    'Anthony (Admin)',
    (SELECT pln_id FROM public.plans WHERE pln_slug = 'premium'),
    true,   -- activo
    true,   -- es admin
    now(),
    now()
),
(
    gen_random_uuid(),
    'anthonyah1312@gmail.com',
    '$2a$12$RpgoT4yZ/ApAQ6sROAF7WOy.WVei9eUsJQXTUz561jxws5K54KHxu',  -- 123
    'Anthony (Usuario)',
    (SELECT pln_id FROM public.plans WHERE pln_slug = 'free'),
    true,   -- activo
    false,  -- usuario normal
    now(),
    now()
)
ON CONFLICT (usr_email) DO NOTHING;

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
-- SELECT pln_name, pln_slug, pln_display_order,
--        pln_can_share_projects      AS "sharing",
--        pln_can_export_data         AS "export",
--        pln_can_use_advanced_reports AS "reports",
--        pln_can_use_ocr             AS "ocr",
--        pln_can_use_api             AS "api",
--        pln_limits
-- FROM public.plans
-- ORDER BY pln_display_order;

-- SELECT usr_email, usr_full_name, usr_is_admin, usr_is_active, p.pln_slug
-- FROM public.users u
-- JOIN public.plans p ON p.pln_id = u.usr_plan_id
-- WHERE usr_email IN ('anthonyah131@gmail.com', 'anthonyah1312@gmail.com');
