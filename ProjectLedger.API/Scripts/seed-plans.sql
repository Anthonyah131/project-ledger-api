-- ============================================================
-- SEED: Datos de prueba completos
-- Compatible con PostgreSQL 16+ / CockroachDB
-- ============================================================
-- Ejecutar DESPUÉS de haber creado todas las tablas.
-- Orden: plans → users → payment_methods → projects →
--        categories → project_members → project_payment_methods
--        → obligations → expenses
-- ============================================================

-- ============================================================
-- 1. PLANES
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
(
    'd154e21d-4ca9-4b39-82ec-9ea055e80a2d',
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

    '2026-02-26 12:29:59.650-06',
    '2026-02-26 12:29:59.650-06'
),

-- ============================================================
-- PLAN 2: BASIC
-- ============================================================
(
    '65f485a6-0d18-4348-8f8c-0127a7c8eaa3',
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

    '2026-02-26 12:29:59.650-06',
    '2026-02-26 12:29:59.650-06'
),

-- ============================================================
-- PLAN 3: PREMIUM
-- ============================================================
(
    'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac',
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

    '2026-02-26 12:29:59.650-06',
    '2026-02-26 12:29:59.650-06'
)

ON CONFLICT (pln_slug) DO NOTHING;

-- ============================================================
-- 2. USUARIOS
-- ============================================================
-- Contraseña de todos los usuarios: 123  (BCrypt work factor 12)
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
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    'anthonyah131@gmail.com',
    '$2a$12$gO9ZBRRBE3be9HNHq1Kfz.xFUkRQYVv0hU971I5zW7GXmSv2msloO',  -- 123
    'Anthony (Admin)',
    'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac',
    true, true,
    '2026-03-01 14:14:24.325-06', '2026-03-01 14:14:24.325-06'
),
(
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'anthonyah1312@gmail.com',
    '$2a$12$RpgoT4yZ/ApAQ6sROAF7WOy.WVei9eUsJQXTUz561jxws5K54KHxu',  -- 123
    'Anthony (Usuario)',
    'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac',
    true, false,
    '2026-03-01 14:14:33.531-06', '2026-03-01 14:24:09.999-06'
),
(
    '6540f863-6a3c-4b02-813f-f1aeceefef1d',
    'anthonyah1313@gmail.com',
    '$2a$12$q.MQT.9UvLmm0RrsjlocoeRoXplk7zV4caOfYwAOnbZPXRMT10nrO',  -- 123
    'Anthony Ávila',
    '65f485a6-0d18-4348-8f8c-0127a7c8eaa3',
    true, false,
    '2026-03-01 14:14:41.847-06', '2026-03-01 14:23:52.197-06'
)
ON CONFLICT (usr_email) DO NOTHING;

-- ============================================================
-- 3. MÉTODOS DE PAGO
-- ============================================================

INSERT INTO public.payment_methods (
    pmt_id,
    pmt_owner_user_id,
    pmt_name,
    pmt_type,
    pmt_currency,
    pmt_bank_name,
    pmt_account_number,
    pmt_description,
    pmt_created_at,
    pmt_updated_at,
    pmt_is_deleted
)
VALUES
(
    '849f3091-e3a3-48c6-8afc-24289b7575d8',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'Ahorro a la vista BN', 'bank', 'USD',
    'Banco Nacional', null, null,
    '2026-02-27 15:37:21.710-06', '2026-02-27 15:37:21.710-06', false
),
(
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'Ahorro a la vista', 'bank', 'CRC',
    'Coopealianza', null, 'Cuenta de ahorro a la vista Colones',
    '2026-02-27 12:24:10.758-06', '2026-02-27 12:24:10.758-06', false
)
ON CONFLICT (pmt_id) DO NOTHING;

-- ============================================================
-- 4. PROYECTOS
-- ============================================================

INSERT INTO public.projects (
    prj_id,
    prj_name,
    prj_owner_user_id,
    prj_currency_code,
    prj_description,
    prj_created_at,
    prj_updated_at,
    prj_is_deleted
)
VALUES
(
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    'Carro Nissan',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'CRC',
    'Carro de la casa',
    '2026-02-26 14:51:04.733-06', '2026-02-27 11:26:12.649-06', false
),
(
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    'Casa Nueva',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'USD',
    'Gastos de la casa nueva en San gerado de Asis',
    '2026-02-27 11:55:36.586-06', '2026-02-27 11:55:36.586-06', false
)
ON CONFLICT (prj_id) DO NOTHING;

-- ============================================================
-- 5. CATEGORÍAS
-- ============================================================

INSERT INTO public.categories (
    cat_id,
    cat_project_id,
    cat_name,
    cat_description,
    cat_is_default,
    cat_budget_amount,
    cat_created_at,
    cat_updated_at,
    cat_is_deleted
)
VALUES
-- Proyecto: Carro Nissan
(
    '0840a426-d08a-4ee2-82b5-f21fe7b8585c',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    'General', 'Categoría por defecto del proyecto.',
    true, null,
    '2026-02-26 14:51:04.790-06', '2026-02-26 14:51:04.790-06', false
),
(
    '569ca339-9e46-40fc-a790-2104a7891279',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    'Compra del carro', null,
    false, null,
    '2026-02-28 12:49:32.049-06', '2026-02-28 12:49:32.049-06', false
),
(
    'e182a8a1-aac3-47bb-a187-06d5f4ace4e1',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    'Pintura carro', null,
    false, null,
    '2026-02-28 13:02:11.442-06', '2026-02-28 13:02:11.442-06', false
),
-- Proyecto: Casa Nueva
(
    'a6e0cda1-594e-40fa-8b44-d4260d14933f',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    'General', 'Categoría por defecto del proyecto.',
    true, null,
    '2026-02-27 11:55:36.635-06', '2026-02-27 11:55:36.635-06', false
),
(
    '606d37ab-b388-447d-a096-b0a58f273aa7',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    'Alimentacion', null,
    false, null,
    '2026-02-27 15:16:43.867-06', '2026-02-27 15:16:43.867-06', false
),
(
    '7ac87664-a9ab-40dd-9373-2ccb103cb5fe',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    'Contructores', 'Gastos relacionados a empleados de construccion',
    false, 5000.00,
    '2026-02-27 16:47:03.509-06', '2026-02-27 16:47:14.591-06', false
)
ON CONFLICT (cat_id) DO NOTHING;

-- ============================================================
-- 6. MIEMBROS DE PROYECTO
-- ============================================================

INSERT INTO public.project_members (
    prm_id,
    prm_project_id,
    prm_user_id,
    prm_role,
    prm_joined_at,
    prm_created_at,
    prm_updated_at,
    prm_is_deleted
)
VALUES
(
    'd5f46680-e460-4254-a138-5ee141db6d1d',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'owner',
    '2026-02-26 14:51:04.770-06', '2026-02-26 14:51:04.770-06', '2026-02-26 14:51:04.770-06', false
),
(
    '645f7574-20d4-4bc2-851f-3c6d82ed9e5b',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    '6540f863-6a3c-4b02-813f-f1aeceefef1d',
    'viewer',
    '2026-03-01 14:26:47.658-06', '2026-03-01 14:26:47.658-06', '2026-03-01 14:26:47.658-06', false
),
(
    'c292c20d-9cd4-4475-ad5d-e8cbd373dcce',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'owner',
    '2026-02-27 11:55:36.592-06', '2026-02-27 11:55:36.593-06', '2026-02-27 11:55:36.593-06', false
)
ON CONFLICT (prm_id) DO NOTHING;

-- ============================================================
-- 7. MÉTODOS DE PAGO VINCULADOS A PROYECTOS
-- ============================================================
-- Carro Nissan ← Ahorro a la vista (CRC)
-- Casa Nueva   ← Ahorro a la vista BN (USD)
-- Casa Nueva   ← Ahorro a la vista (CRC)
-- ============================================================

INSERT INTO public.project_payment_methods (
    ppm_id,
    ppm_project_id,
    ppm_payment_method_id,
    ppm_added_by_user_id,
    ppm_created_at
)
VALUES
(
    gen_random_uuid(),
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '2026-02-26 14:51:04.770-06'
),
(
    gen_random_uuid(),
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    '849f3091-e3a3-48c6-8afc-24289b7575d8',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '2026-02-27 11:55:36.592-06'
),
(
    gen_random_uuid(),
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '2026-02-27 11:55:36.592-06'
)
ON CONFLICT (ppm_project_id, ppm_payment_method_id) DO NOTHING;

-- ============================================================
-- 8. OBLIGACIONES
-- ============================================================

INSERT INTO public.obligations (
    obl_id,
    obl_project_id,
    obl_created_by_user_id,
    obl_title,
    obl_description,
    obl_total_amount,
    obl_currency,
    obl_due_date,
    obl_created_at,
    obl_updated_at,
    obl_is_deleted
)
VALUES
(
    '92fa7061-3601-4a6e-b95a-67b94703fbd8',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'Prestamos BAC',
    'Prestamo para comprar carro',
    10000000.00, 'CRC', '2026-03-08',
    '2026-02-28 12:39:39.346-06', '2026-02-28 12:39:39.346-06', false
)
ON CONFLICT (obl_id) DO NOTHING;

-- ============================================================
-- 9. GASTOS
-- ============================================================

INSERT INTO public.expenses (
    exp_id,
    exp_project_id,
    exp_category_id,
    exp_payment_method_id,
    exp_created_by_user_id,
    exp_obligation_id,
    exp_original_amount,
    exp_original_currency,
    exp_exchange_rate,
    exp_converted_amount,
    exp_title,
    exp_description,
    exp_expense_date,
    exp_receipt_number,
    exp_notes,
    exp_is_template,
    exp_alt_currency,
    exp_alt_exchange_rate,
    exp_alt_amount,
    exp_created_at,
    exp_updated_at,
    exp_is_deleted,
    exp_deleted_at,
    exp_deleted_by_user_id
)
VALUES
-- Abono prestamos (eliminado)
(
    '102e136a-b550-4017-a56d-dda67bdcd5b1',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    '0840a426-d08a-4ee2-82b5-f21fe7b8585c',
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '92fa7061-3601-4a6e-b95a-67b94703fbd8',
    200000.00, 'CRC', 1.000000, 200000.00,
    'Abono prestamos', null, '2026-02-25',
    null, null, false, null, null, null,
    '2026-02-28 12:48:20.920-06', '2026-02-28 12:48:42.706-06',
    true, '2026-02-28 12:48:42.706-06', '5603f479-46fa-496a-8ed1-b64725bb7581'
),
-- Pago salarios (activo, Casa Nueva, USD)
(
    '40ecff52-21ee-44c1-9f11-4303d00143fc',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    '7ac87664-a9ab-40dd-9373-2ccb103cb5fe',
    '849f3091-e3a3-48c6-8afc-24289b7575d8',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    1000.00, 'USD', 1.000000, 1000.00,
    'Pago salarios', 'Salario mensual de empleados', '2026-02-27',
    null, null, false, 'CRC', 471.630000, 471626.60,
    '2026-02-27 16:53:52.199-06', '2026-02-27 16:53:52.199-06',
    false, null, null
),
-- Cancelacion de prestamo (eliminado)
(
    '6a48af56-02bc-4679-a598-800987faa470',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    '569ca339-9e46-40fc-a790-2104a7891279',
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '92fa7061-3601-4a6e-b95a-67b94703fbd8',
    9000000.00, 'CRC', 1.000000, 9000000.00,
    'Cancelacion de prestamo', null, '2026-03-01',
    null, null, false, null, null, null,
    '2026-02-28 12:51:04.745-06', '2026-02-28 13:01:47.838-06',
    true, '2026-02-28 13:01:47.838-06', '5603f479-46fa-496a-8ed1-b64725bb7581'
),
-- Salario Fermin (activo, Casa Nueva, CRC)
(
    '7c2e307c-5a18-4d9f-8c30-7dce3b0289a9',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    'a6e0cda1-594e-40fa-8b44-d4260d14933f',
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    30000.00, 'CRC', 0.002100, 63.00,
    'Salario Fermin', 'Salario mensual de Fermin', '2026-02-01',
    null, null, false, 'CRC', 471.630000, 30000.00,
    '2026-02-27 15:42:04.486-06', '2026-02-27 16:51:36.165-06',
    false, null, null
),
-- Abono a prestamo (activo, Carro Nissan, CRC con alt USD)
(
    'e72cc085-1eb2-481f-bfa3-28a6e6854f2e',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    'e182a8a1-aac3-47bb-a187-06d5f4ace4e1',
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '92fa7061-3601-4a6e-b95a-67b94703fbd8',
    1000000.00, 'CRC', 1.000000, 1000000.00,
    'Abono a prestamo', 'Compra del carro', '2026-02-28',
    null, 'Para comprar el carro', false, 'USD', 0.002100, 2124.10,
    '2026-02-28 12:45:53.618-06', '2026-02-28 13:02:27.981-06',
    false, null, null
)
ON CONFLICT (exp_id) DO NOTHING;

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
-- SELECT pln_name, pln_slug FROM public.plans ORDER BY pln_display_order;

-- SELECT usr_email, usr_full_name, usr_is_admin, p.pln_slug
-- FROM public.users u JOIN public.plans p ON p.pln_id = u.usr_plan_id
-- ORDER BY u.usr_created_at;

-- SELECT prj_name, prj_currency_code FROM public.projects;

-- SELECT cat_name, p.prj_name
-- FROM public.categories c JOIN public.projects p ON p.prj_id = c.cat_project_id
-- ORDER BY p.prj_name, c.cat_name;

-- SELECT p.prj_name, pm.pmt_name, pm.pmt_currency
-- FROM public.project_payment_methods ppm
-- JOIN public.projects p ON p.prj_id = ppm.ppm_project_id
-- JOIN public.payment_methods pm ON pm.pmt_id = ppm.ppm_payment_method_id;

-- SELECT exp_title, exp_converted_amount, exp_is_deleted
-- FROM public.expenses ORDER BY exp_created_at;
