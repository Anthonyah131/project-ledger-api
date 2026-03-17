-- ============================================================
-- FULL SEED: initial and test data
-- Project Ledger API
-- ============================================================
-- Usage:
--   1) Run after schema/migrations are applied.
--   2) This script truncates data and refills all main tables.
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- 0) Clean current data
-- ------------------------------------------------------------
TRUNCATE TABLE
    public.audit_logs,
    public.transaction_currency_exchanges,
    public.incomes,
    public.expenses,
    public.obligations,
    public.project_alternative_currencies,
    public.project_budgets,
    public.project_payment_methods,
    public.project_partners,
    public.categories,
    public.project_members,
    public.payment_methods,
    public.projects,
    public.external_auth_providers,
    public.password_reset_tokens,
    public.refresh_tokens,
    public.stripe_webhook_events,
    public.user_subscriptions,
    public.workspace_members,
    public.workspaces,
    public.partners,
    public.users,
    public.plans,
    public.currencies
CASCADE;

-- ------------------------------------------------------------
-- 1) Currencies
-- ------------------------------------------------------------
INSERT INTO public.currencies (
    cur_code,
    cur_name,
    cur_symbol,
    cur_decimal_places,
    cur_is_active,
    cur_created_at
)
VALUES
    ('ARS', 'Argentine Peso', '$', 2, true, '2026-03-01 15:14:08.532-06'),
    ('AUD', 'Australian Dollar', '$', 2, true, '2026-03-01 15:14:08.532-06'),
    ('BRL', 'Brazilian Real', 'R$', 2, true, '2026-03-01 15:14:08.532-06'),
    ('CAD', 'Canadian Dollar', '$', 2, true, '2026-03-01 15:14:08.532-06'),
    ('CHF', 'Swiss Franc', 'Fr', 2, true, '2026-03-01 15:14:08.532-06'),
    ('CLP', 'Chilean Peso', '$', 0, true, '2026-03-01 15:14:08.532-06'),
    ('CNY', 'Chinese Yuan', 'Y', 2, true, '2026-03-01 15:14:08.532-06'),
    ('COP', 'Colombian Peso', '$', 0, true, '2026-03-01 15:14:08.532-06'),
    ('CRC', 'Costa Rican Colon', 'CRC', 0, true, '2026-03-01 15:14:08.532-06'),
    ('EUR', 'Euro', 'EUR', 2, true, '2026-03-01 15:14:08.532-06'),
    ('GBP', 'British Pound', 'GBP', 2, true, '2026-03-01 15:14:08.532-06'),
    ('JPY', 'Japanese Yen', 'JPY', 0, true, '2026-03-01 15:14:08.532-06'),
    ('MXN', 'Mexican Peso', '$', 2, true, '2026-03-01 15:14:08.532-06'),
    ('PEN', 'Peruvian Sol', 'PEN', 2, true, '2026-03-01 15:14:08.532-06'),
    ('USD', 'US Dollar', '$', 2, true, '2026-03-01 15:14:08.532-06');

-- ------------------------------------------------------------
-- 2) Plans
-- ------------------------------------------------------------
INSERT INTO public.plans (
    pln_id,
    pln_name,
    pln_slug,
    pln_description,
    pln_is_active,
    pln_display_order,
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
    pln_limits,
    pln_created_at,
    pln_updated_at,
    pln_monthly_price,
    pln_currency,
    pln_stripe_product_id,
    pln_stripe_price_id,
    pln_stripe_payment_link_id,
    pln_stripe_payment_link_url
)
VALUES
(
    '65f485a6-0d18-4348-8f8c-0127a7c8eaa3',
    'Basic',
    'basic',
    'Basic plan for freelancers and small teams.',
    true,
    2,
    true, true, true, true, true, false, true, false, true, true,
    '{"max_alternative_currencies_per_project": 10, "max_categories_per_project": 20, "max_document_reads_per_month": 10, "max_expenses": 200, "max_incomes_per_month": 100, "max_payment_methods": 10, "max_projects": 10, "max_team_members_per_project": 5}'::jsonb,
    '2026-02-26 12:29:59.650-06',
    '2026-03-08 15:09:15.970-06',
    9.99,
    'usd',
    'prod_U5cSqfOrkUyaBj',
    'price_1T7RDaB5vtTuVkxpcVMj5jLn',
    'plink_1T7SaVB5vtTuVkxpYkoMd0Ov',
    'https://buy.stripe.com/test_cNi8wOdrq2rpdstfI76kg0a'
),
(
    'd154e21d-4ca9-4b39-82ec-9ea055e80a2d',
    'Free',
    'free',
    'Free plan for basic personal usage.',
    true,
    1,
    true, true, true, false, false, false, false, false, true, true,
    '{"max_alternative_currencies_per_project": 3, "max_categories_per_project": 5, "max_document_reads_per_month": 0, "max_expenses": 30, "max_incomes_per_month": 10, "max_payment_methods": 2, "max_projects": 2, "max_team_members_per_project": 0}'::jsonb,
    '2026-02-26 12:29:59.650-06',
    '2026-03-08 15:09:15.970-06',
    0.00,
    'usd',
    'prod_U5cSW1BCSVWmCx',
    'price_1T7RDZB5vtTuVkxpU2FK94EY',
    'plink_1T7SaUB5vtTuVkxpdf0GkdHF',
    'https://buy.stripe.com/test_00w00idrqgifgEF8fF6kg09'
),
(
    'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac',
    'Premium',
    'premium',
    'Premium plan with advanced features and no practical limits.',
    true,
    3,
    true, true, true, true, true, true, true, true, true, true,
    '{"max_alternative_currencies_per_project": -1, "max_categories_per_project": null, "max_document_reads_per_month": null, "max_expenses": null, "max_incomes_per_month": -1, "max_payment_methods": null, "max_projects": null, "max_team_members_per_project": null}'::jsonb,
    '2026-02-26 12:29:59.650-06',
    '2026-03-08 15:09:15.970-06',
    19.99,
    'usd',
    'prod_U5cS7nxWY2mx1y',
    'price_1T7RDcB5vtTuVkxpdh8opuDA',
    'plink_1T7SaWB5vtTuVkxpoQKIrai2',
    'https://buy.stripe.com/test_aFadR8gDC9TRewxeE36kg0b'
)
ON CONFLICT (pln_slug) DO UPDATE
SET
    pln_name = EXCLUDED.pln_name,
    pln_description = EXCLUDED.pln_description,
    pln_is_active = EXCLUDED.pln_is_active,
    pln_display_order = EXCLUDED.pln_display_order,
    pln_can_create_projects = EXCLUDED.pln_can_create_projects,
    pln_can_edit_projects = EXCLUDED.pln_can_edit_projects,
    pln_can_delete_projects = EXCLUDED.pln_can_delete_projects,
    pln_can_share_projects = EXCLUDED.pln_can_share_projects,
    pln_can_export_data = EXCLUDED.pln_can_export_data,
    pln_can_use_advanced_reports = EXCLUDED.pln_can_use_advanced_reports,
    pln_can_use_ocr = EXCLUDED.pln_can_use_ocr,
    pln_can_use_api = EXCLUDED.pln_can_use_api,
    pln_can_use_multi_currency = EXCLUDED.pln_can_use_multi_currency,
    pln_can_set_budgets = EXCLUDED.pln_can_set_budgets,
    pln_limits = EXCLUDED.pln_limits,
    pln_updated_at = EXCLUDED.pln_updated_at,
    pln_monthly_price = EXCLUDED.pln_monthly_price,
    pln_currency = EXCLUDED.pln_currency,
    pln_stripe_product_id = EXCLUDED.pln_stripe_product_id,
    pln_stripe_price_id = EXCLUDED.pln_stripe_price_id,
    pln_stripe_payment_link_id = EXCLUDED.pln_stripe_payment_link_id,
    pln_stripe_payment_link_url = EXCLUDED.pln_stripe_payment_link_url;

-- ------------------------------------------------------------
-- 3) Users
-- ------------------------------------------------------------
INSERT INTO public.users (
    usr_id,
    usr_email,
    usr_password_hash,
    usr_full_name,
    usr_plan_id,
    usr_is_active,
    usr_is_admin,
    usr_avatar_url,
    usr_stripe_customer_id,
    usr_last_login_at,
    usr_created_at,
    usr_updated_at,
    usr_is_deleted,
    usr_deleted_at,
    usr_deleted_by_user_id
)
VALUES
(
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'anthonyah1312@gmail.com',
    '$2a$12$tynZW2MeRrRLXhbjSkN7AeaGRPed9E5MxLAZ.RQge6NSyZvnUxqnO',
    'Anthony Usuario',
    'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac',
    true,
    false,
    null,
    'cus_U5fSW1oHwBOXFD',
    '2026-03-10 18:09:16.834-06',
    '2026-03-01 14:14:33.531-06',
    '2026-03-10 18:09:16.834-06',
    false,
    null,
    null
),
(
    '6540f863-6a3c-4b02-813f-f1aeceefef1d',
    'anthonyah1313@gmail.com',
    '$2a$12$q.MQT.9UvLmm0RrsjlocoeRoXplk7zV4caOfYwAOnbZPXRMT10nrO',
    'Anthony Avila',
    'd154e21d-4ca9-4b39-82ec-9ea055e80a2d',
    true,
    false,
    null,
    null,
    '2026-03-08 15:58:01.268-06',
    '2026-03-01 14:14:41.847-06',
    '2026-03-08 15:58:01.268-06',
    false,
    null,
    null
),
(
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    'anthonyah1314@gmail.com',
    '$2a$12$ygLXrpQkn3Q2sT2X3x7fgud7a4uE284r9Pm6fyzEBL1jJ2w4u4.kS',
    'Anthony Avila Basic',
    '65f485a6-0d18-4348-8f8c-0127a7c8eaa3',
    true,
    false,
    null,
    'cus_U73TtdoXRU3JP2',
    '2026-03-10 16:57:04.076-06',
    '2026-03-08 15:56:58.905-06',
    '2026-03-10 16:57:04.076-06',
    false,
    null,
    null
),
(
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    'anthonyah131@gmail.com',
    '$2a$12$gO9ZBRRBE3be9HNHq1Kfz.xFUkRQYVv0hU971I5zW7GXmSv2msloO',
    'Anthony Admin',
    'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac',
    true,
    true,
    null,
    null,
    '2026-03-10 18:17:09.658-06',
    '2026-03-01 14:14:24.325-06',
    '2026-03-10 18:17:09.658-06',
    false,
    null,
    null
);

-- ------------------------------------------------------------
-- 4) Auth support tables
-- ------------------------------------------------------------
INSERT INTO public.refresh_tokens (
    rtk_id,
    rtk_user_id,
    rtk_token_hash,
    rtk_expires_at,
    rtk_revoked_at,
    rtk_created_at
)
VALUES
    ('f3a84f0b-dcc2-4f4b-8fb2-b6f9400dfbb8', '5603f479-46fa-496a-8ed1-b64725bb7581', 'hash_refresh_001', '2026-04-01 00:00:00+00', null, '2026-03-05 10:10:00+00'),
    ('6f344ac8-b8a2-4b5f-9f78-c76ec5b63d2e', '5603f479-46fa-496a-8ed1-b64725bb7581', 'hash_refresh_002', '2026-04-15 00:00:00+00', null, '2026-03-08 14:00:00+00'),
    ('70b6a272-c96e-476d-aa21-4b131e2de828', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'hash_refresh_003', '2026-04-10 00:00:00+00', null, '2026-03-08 15:00:00+00'),
    ('1fe14d40-6f17-47fe-b41d-c895016f83e0', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'hash_refresh_004', '2026-04-11 00:00:00+00', null, '2026-03-09 19:22:00+00');

INSERT INTO public.password_reset_tokens (
    prt_id,
    prt_user_id,
    prt_code_hash,
    prt_expires_at,
    prt_used_at,
    prt_created_at
)
VALUES
    ('46d6ae92-e2f4-4738-80c5-793d17706bc3', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'otp_hash_001', '2026-03-15 06:00:00+00', null, '2026-03-15 05:45:00+00'),
    ('8b4ae1cf-11ab-4c2d-bf93-b6a20ca7f4a9', '5603f479-46fa-496a-8ed1-b64725bb7581', 'otp_hash_002', '2026-03-16 06:00:00+00', '2026-03-16 05:50:00+00', '2026-03-16 05:40:00+00'),
    ('92e237f3-44aa-4cbe-abcb-8f7258e773f4', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'otp_hash_003', '2026-03-16 08:00:00+00', null, '2026-03-16 07:43:00+00');

-- ------------------------------------------------------------
-- 5) Billing/subscriptions
-- ------------------------------------------------------------
INSERT INTO public.user_subscriptions (
    uss_id,
    uss_user_id,
    uss_plan_id,
    uss_stripe_subscription_id,
    uss_stripe_customer_id,
    uss_stripe_price_id,
    uss_status,
    uss_current_period_start,
    uss_current_period_end,
    uss_cancel_at_period_end,
    uss_canceled_at,
    uss_created_at,
    uss_updated_at
)
VALUES
(
    '15809a31-167a-4074-9e20-7471f827fae3',
    null,
    null,
    'sub_1T7RjwB5vtTuVkxpt5XJQbFe',
    'cus_U5czCqeR4CUeVt',
    'price_1T7RjwB5vtTuVkxpydSof0aq',
    'active',
    null,
    null,
    false,
    null,
    '2026-03-04 20:34:22.353-06',
    '2026-03-04 20:34:22.354-06'
),
(
    '41737c85-8d7a-46b8-b600-9cce12dad87f',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    '65f485a6-0d18-4348-8f8c-0127a7c8eaa3',
    'sub_1T8pMYB5vtTuVkxplF9wZuni',
    'cus_U73TtdoXRU3JP2',
    'price_1T7RDaB5vtTuVkxpcVMj5jLn',
    'active',
    '2026-03-08 15:59:47.000-06',
    '2026-04-08 15:59:47.000-06',
    false,
    null,
    '2026-03-08 15:59:55.209-06',
    '2026-03-08 15:59:55.210-06'
),
(
    'fa1f08a3-38cb-4c64-82e6-022a9f7efe7a',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac',
    'sub_1T7U7zB5vtTuVkxpCG99ZDDD',
    'cus_U5fSW1oHwBOXFD',
    'price_1T7RDcB5vtTuVkxpdh8opuDA',
    'active',
    '2026-03-04 23:07:13.000-06',
    '2026-04-04 23:07:13.000-06',
    false,
    null,
    '2026-03-04 23:07:22.359-06',
    '2026-03-05 21:52:25.970-06'
),
(
    '70be28aa-eeb7-444c-8765-8f1f2d7b1156',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac',
    'sub_admin_premium_001',
    null,
    'price_1T7RDcB5vtTuVkxpdh8opuDA',
    'active',
    '2026-03-01 00:00:00+00',
    '2026-04-01 00:00:00+00',
    false,
    null,
    '2026-03-01 00:05:00+00',
    '2026-03-01 00:05:00+00'
);

-- ------------------------------------------------------------
-- 6) Projects, members, payment methods
-- ------------------------------------------------------------
INSERT INTO public.projects (
    prj_id,
    prj_name,
    prj_owner_user_id,
    prj_currency_code,
    prj_description,
    prj_created_at,
    prj_updated_at,
    prj_is_deleted,
    prj_deleted_at,
    prj_deleted_by_user_id
)
VALUES
    ('5b2b11a2-dc2c-4537-845f-6c9d19f0af05', 'Carro Nissan', '5603f479-46fa-496a-8ed1-b64725bb7581', 'CRC', 'Car expenses and debt tracking.', '2026-02-26 14:51:04.733-06', '2026-03-10 11:26:12.649-06', false, null, null),
    ('27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 'Casa Nueva', '5603f479-46fa-496a-8ed1-b64725bb7581', 'USD', 'House construction and monthly operations.', '2026-02-27 11:55:36.586-06', '2026-03-10 11:55:36.586-06', false, null, null),
    ('ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 'Freelance LATAM', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'MXN', 'Freelance income and contractor expenses.', '2026-03-03 09:00:00+00', '2026-03-10 10:00:00+00', false, null, null),
    ('2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'Inversiones Globales', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'USD', 'Portfolio management.', '2026-03-04 09:00:00+00', '2026-03-10 10:00:00+00', false, null, null),
    ('c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'Viaje Japon', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'JPY', 'Trip budget and spending.', '2026-03-04 10:00:00+00', '2026-03-10 10:00:00+00', false, null, null),
    ('8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'Operaciones Admin', 'bf759a1f-a5ae-4448-87f3-b919246c3054', 'EUR', 'Administrative shared operations.', '2026-03-01 09:00:00+00', '2026-03-10 10:00:00+00', false, null, null),
    ('43a2325d-6b57-4f13-b909-c3db0f9686f8', 'Proyecto Compartido Andino', '5603f479-46fa-496a-8ed1-b64725bb7581', 'PEN', 'Cross-country collaboration project.', '2026-03-06 09:00:00+00', '2026-03-10 10:00:00+00', false, null, null),
    ('de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'UK Expansion', 'bf759a1f-a5ae-4448-87f3-b919246c3054', 'GBP', 'Expansion setup and costs in UK.', '2026-03-07 09:00:00+00', '2026-03-10 10:00:00+00', false, null, null);

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
    pmt_is_deleted,
    pmt_deleted_at,
    pmt_deleted_by_user_id
)
VALUES
    ('8d66d542-e790-41b6-a65c-815bedc78b96', '5603f479-46fa-496a-8ed1-b64725bb7581', 'Coopealianza CRC', 'bank', 'CRC', 'Coopealianza', 'CR-001', 'Primary CRC account', '2026-02-27 12:24:10.758-06', '2026-03-10 09:00:00+00', false, null, null),
    ('849f3091-e3a3-48c6-8afc-24289b7575d8', '5603f479-46fa-496a-8ed1-b64725bb7581', 'BN USD', 'bank', 'USD', 'Banco Nacional', 'US-101', 'USD savings account', '2026-02-27 15:37:21.710-06', '2026-03-10 09:00:00+00', false, null, null),
    ('12124702-0f05-4d71-87dd-a7bc01db57be', '5603f479-46fa-496a-8ed1-b64725bb7581', 'Cash Wallet CRC', 'cash', 'CRC', null, null, 'Physical cash wallet', '2026-03-01 10:00:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('0da9a4f5-ecb0-45fd-b7f4-334d7471d6fd', '5603f479-46fa-496a-8ed1-b64725bb7581', 'Card PEN', 'card', 'PEN', 'Interbank', 'PE-2200', 'Card used in Peru', '2026-03-03 10:00:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('d93ab31f-83fa-4f77-92ff-8b4ccfbdaf4d', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'Banamex MXN', 'bank', 'MXN', 'Banamex', 'MX-990', 'Main MXN account', '2026-03-03 11:00:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('65c810a2-444e-4e11-b699-47779f1f7ab2', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'Visa USD', 'card', 'USD', 'Scotiabank', 'US-8877', 'USD card for online payments', '2026-03-03 11:30:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('15790a95-375f-443e-bf58-fb699f13f430', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'Cash MX', 'cash', 'MXN', null, null, 'Cash operations', '2026-03-03 12:00:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('a1f0a327-8fb3-4f95-9578-354f13bf57d7', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'Broker USD', 'bank', 'USD', 'Interactive Brokers', 'IB-443', 'Investment account', '2026-03-04 12:30:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('8df4b5e4-f8ac-4bcc-b84b-594f79c500fd', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'Cash JPY', 'cash', 'JPY', null, null, 'Travel wallet', '2026-03-04 13:00:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('33ffb6a3-9d3f-4b0a-80d3-67385f1e4f43', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'Card EUR', 'card', 'EUR', 'N26', 'EU-1212', 'Euro card', '2026-03-04 13:30:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('a4ae2788-e434-4b4f-a376-2de3ee194f87', 'bf759a1f-a5ae-4448-87f3-b919246c3054', 'Admin Treasury EUR', 'bank', 'EUR', 'Deutsche Bank', 'EU-4500', 'Main admin account', '2026-03-01 12:00:00+00', '2026-03-10 09:00:00+00', false, null, null),
    ('ce6111b3-8f7f-4418-8c24-31ca4d50f2fe', 'bf759a1f-a5ae-4448-87f3-b919246c3054', 'Admin Cash USD', 'cash', 'USD', null, null, 'Operational petty cash', '2026-03-01 12:15:00+00', '2026-03-10 09:00:00+00', false, null, null);

INSERT INTO public.project_members (
    prm_id,
    prm_project_id,
    prm_user_id,
    prm_role,
    prm_joined_at,
    prm_created_at,
    prm_updated_at,
    prm_is_deleted,
    prm_deleted_at,
    prm_deleted_by_user_id
)
VALUES
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', '5603f479-46fa-496a-8ed1-b64725bb7581', 'owner', '2026-02-26 14:51:04.770-06', now(), now(), false, null, null),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', '5603f479-46fa-496a-8ed1-b64725bb7581', 'owner', '2026-02-27 11:55:36.593-06', now(), now(), false, null, null),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'owner', '2026-03-03 09:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'owner', '2026-03-04 09:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'owner', '2026-03-04 10:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'bf759a1f-a5ae-4448-87f3-b919246c3054', 'owner', '2026-03-01 09:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', '5603f479-46fa-496a-8ed1-b64725bb7581', 'owner', '2026-03-06 09:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'bf759a1f-a5ae-4448-87f3-b919246c3054', 'owner', '2026-03-07 09:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'viewer', '2026-03-01 14:26:47.658-06', now(), now(), false, null, null),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'editor', '2026-03-04 12:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'editor', '2026-03-06 10:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', 'viewer', '2026-03-06 11:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', '5603f479-46fa-496a-8ed1-b64725bb7581', 'viewer', '2026-03-07 11:00:00+00', now(), now(), false, null, null),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'editor', '2026-03-08 11:00:00+00', now(), now(), false, null, null);

-- ------------------------------------------------------------
-- 7) Categories and project-level payment method links
-- ------------------------------------------------------------
INSERT INTO public.categories (
    cat_id,
    cat_project_id,
    cat_name,
    cat_description,
    cat_is_default,
    cat_budget_amount,
    cat_created_at,
    cat_updated_at,
    cat_is_deleted,
    cat_deleted_at,
    cat_deleted_by_user_id
)
VALUES
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', 'General', 'Default category', true, null, now(), now(), false, null, null),
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', 'Loan', 'Debt-related payments', false, 2500000.00, now(), now(), false, null, null),
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', 'Maintenance', null, false, 1200000.00, now(), now(), false, null, null),
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', 'Fuel', null, false, 900000.00, now(), now(), false, null, null),

    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 'General', 'Default category', true, null, now(), now(), false, null, null),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 'Food', null, false, 2000.00, now(), now(), false, null, null),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 'Labor', 'Construction labor costs', false, 7000.00, now(), now(), false, null, null),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 'Materials', null, false, 12000.00, now(), now(), false, null, null),

    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 'General', 'Default category', true, null, now(), now(), false, null, null),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 'Sales', null, false, null, now(), now(), false, null, null),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 'Taxes', null, false, null, now(), now(), false, null, null),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 'Ops', null, false, 80000.00, now(), now(), false, null, null),

    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'General', 'Default category', true, null, now(), now(), false, null, null),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'Broker Fees', null, false, 900.00, now(), now(), false, null, null),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'Subscriptions', null, false, 450.00, now(), now(), false, null, null),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'Dividends', null, false, null, now(), now(), false, null, null),

    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'General', 'Default category', true, null, now(), now(), false, null, null),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'Hotels', null, false, 150000.00, now(), now(), false, null, null),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'Transport', null, false, 90000.00, now(), now(), false, null, null),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'Food', null, false, 80000.00, now(), now(), false, null, null),

    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'General', 'Default category', true, null, now(), now(), false, null, null),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'Payroll', null, false, 20000.00, now(), now(), false, null, null),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'Legal', null, false, 7000.00, now(), now(), false, null, null),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'Infrastructure', null, false, 5000.00, now(), now(), false, null, null),

    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', 'General', 'Default category', true, null, now(), now(), false, null, null),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', 'Travel', null, false, 15000.00, now(), now(), false, null, null),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', 'Vendors', null, false, 22000.00, now(), now(), false, null, null),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', 'Meetings', null, false, 6000.00, now(), now(), false, null, null),

    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'General', 'Default category', true, null, now(), now(), false, null, null),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'Compliance', null, false, 12000.00, now(), now(), false, null, null),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'Consulting', null, false, 18000.00, now(), now(), false, null, null),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'Marketing', null, false, 9000.00, now(), now(), false, null, null);

INSERT INTO public.project_payment_methods (
    ppm_id,
    ppm_project_id,
    ppm_payment_method_id,
    ppm_added_by_user_id,
    ppm_created_at
)
VALUES
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', '8d66d542-e790-41b6-a65c-815bedc78b96', '5603f479-46fa-496a-8ed1-b64725bb7581', now()),
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', '12124702-0f05-4d71-87dd-a7bc01db57be', '5603f479-46fa-496a-8ed1-b64725bb7581', now()),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', '849f3091-e3a3-48c6-8afc-24289b7575d8', '5603f479-46fa-496a-8ed1-b64725bb7581', now()),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', '8d66d542-e790-41b6-a65c-815bedc78b96', '5603f479-46fa-496a-8ed1-b64725bb7581', now()),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 'd93ab31f-83fa-4f77-92ff-8b4ccfbdaf4d', '6540f863-6a3c-4b02-813f-f1aeceefef1d', now()),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', '65c810a2-444e-4e11-b699-47779f1f7ab2', '6540f863-6a3c-4b02-813f-f1aeceefef1d', now()),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'a1f0a327-8fb3-4f95-9578-354f13bf57d7', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', now()),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', '33ffb6a3-9d3f-4b0a-80d3-67385f1e4f43', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', now()),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', '8df4b5e4-f8ac-4bcc-b84b-594f79c500fd', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', now()),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'a1f0a327-8fb3-4f95-9578-354f13bf57d7', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0', now()),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'a4ae2788-e434-4b4f-a376-2de3ee194f87', 'bf759a1f-a5ae-4448-87f3-b919246c3054', now()),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'ce6111b3-8f7f-4418-8c24-31ca4d50f2fe', 'bf759a1f-a5ae-4448-87f3-b919246c3054', now()),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', '0da9a4f5-ecb0-45fd-b7f4-334d7471d6fd', '5603f479-46fa-496a-8ed1-b64725bb7581', now()),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'a4ae2788-e434-4b4f-a376-2de3ee194f87', 'bf759a1f-a5ae-4448-87f3-b919246c3054', now());

-- ------------------------------------------------------------
-- 8) Budgets and alternative currencies
-- ------------------------------------------------------------
INSERT INTO public.project_budgets (
    pjb_id,
    pjb_project_id,
    pjb_total_budget,
    pjb_alert_percentage,
    pjb_created_at,
    pjb_updated_at,
    pjb_is_deleted,
    pjb_deleted_at,
    pjb_deleted_by_user_id
)
VALUES
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', 12000000.00, 80.00, now(), now(), false, null, null),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 25000.00, 75.00, now(), now(), false, null, null),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 450000.00, 80.00, now(), now(), false, null, null),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 15000.00, 70.00, now(), now(), false, null, null),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 500000.00, 85.00, now(), now(), false, null, null),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 60000.00, 80.00, now(), now(), false, null, null),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', 90000.00, 80.00, now(), now(), false, null, null),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 40000.00, 78.00, now(), now(), false, null, null),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 18000.00, 80.00, now() - interval '10 day', now() - interval '9 day', true, now() - interval '9 day', 'bf759a1f-a5ae-4448-87f3-b919246c3054');

INSERT INTO public.project_alternative_currencies (
    pac_id,
    pac_project_id,
    pac_currency_code,
    pac_created_at
)
VALUES
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', 'USD', now()),
    (gen_random_uuid(), '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', 'EUR', now()),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 'CRC', now()),
    (gen_random_uuid(), '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', 'EUR', now()),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 'USD', now()),
    (gen_random_uuid(), 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', 'COP', now()),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'EUR', now()),
    (gen_random_uuid(), '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51', 'MXN', now()),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'USD', now()),
    (gen_random_uuid(), 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4', 'EUR', now()),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'USD', now()),
    (gen_random_uuid(), '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'GBP', now()),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', 'USD', now()),
    (gen_random_uuid(), '43a2325d-6b57-4f13-b909-c3db0f9686f8', 'CLP', now()),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'USD', now()),
    (gen_random_uuid(), 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'EUR', now());

-- ------------------------------------------------------------
-- 9) Obligations
-- ------------------------------------------------------------
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
    obl_is_deleted,
    obl_deleted_at,
    obl_deleted_by_user_id
)
VALUES
    ('92fa7061-3601-4a6e-b95a-67b94703fbd8', '5b2b11a2-dc2c-4537-845f-6c9d19f0af05', '5603f479-46fa-496a-8ed1-b64725bb7581', 'Prestamo BAC', 'Vehicle loan', 10000000.00, 'CRC', '2026-12-01', '2026-02-28 12:39:39.346-06', now(), false, null, null),
    ('2f3b3a08-dbb9-4a80-86a4-26f7a6274955', '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4', '5603f479-46fa-496a-8ed1-b64725bb7581', 'Mortgage advance', 'House expansion financing', 15000.00, 'USD', '2026-10-15', now(), now(), false, null, null),
    ('a5f358aa-c0a5-44f2-bdcc-48ce89ce733f', 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910', '6540f863-6a3c-4b02-813f-f1aeceefef1d', 'Tax installment', 'Deferred fiscal payment', 250000.00, 'MXN', '2026-08-10', now(), now(), false, null, null),
    ('274d7486-839e-495f-b2ea-a5dff1e6f435', '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5', 'bf759a1f-a5ae-4448-87f3-b919246c3054', 'Infrastructure financing', 'Payment agreement with vendor', 30000.00, 'EUR', '2026-09-30', now(), now(), false, null, null),
    ('f3f06a7c-a9d8-4ef4-95ed-f04f2b11e21f', 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e', 'bf759a1f-a5ae-4448-87f3-b919246c3054', 'UK legal fees debt', 'Legal provider deferred invoice', 12000.00, 'GBP', '2026-11-20', now(), now(), false, null, null);

-- ------------------------------------------------------------
-- 10) Incomes (manual set + generated bulk)
-- ------------------------------------------------------------
INSERT INTO public.incomes (
    inc_id,
    inc_project_id,
    inc_category_id,
    inc_payment_method_id,
    inc_created_by_user_id,
    inc_original_amount,
    inc_original_currency,
    inc_exchange_rate,
    inc_converted_amount,
    inc_account_amount,
    inc_account_currency,
    inc_title,
    inc_description,
    inc_income_date,
    inc_receipt_number,
    inc_notes,
    inc_created_at,
    inc_updated_at,
    inc_is_deleted,
    inc_deleted_at,
    inc_deleted_by_user_id
)
VALUES
(
    '8ff5509e-8524-49f4-b53b-e0ad5421a76f',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4' AND cat_name = 'General' LIMIT 1),
    '849f3091-e3a3-48c6-8afc-24289b7575d8',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    3500.00,
    'USD',
    1.000000,
    3500.00,
    3500.00,
    'USD',
    'Owner contribution',
    'Initial capital',
    '2026-03-01',
    'INC-001',
    null,
    now(),
    now(),
    false,
    null,
    null
),
(
    '0e6538f8-d2c8-4f9f-95d8-ce4f4e146f2d',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '5b2b11a2-dc2c-4537-845f-6c9d19f0af05' AND cat_name = 'General' LIMIT 1),
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    120000.00,
    'CRC',
    1.000000,
    120000.00,
    120000.00,
    'CRC',
    'Side income',
    'Misc vehicle reimbursement',
    '2026-03-02',
    'INC-002',
    null,
    now(),
    now(),
    false,
    null,
    null
),
(
    '26c2013c-b777-40b4-ac7e-a743b6eeb774',
    'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910' AND cat_name = 'Sales' LIMIT 1),
    'd93ab31f-83fa-4f77-92ff-8b4ccfbdaf4d',
    '6540f863-6a3c-4b02-813f-f1aeceefef1d',
    48000.00,
    'MXN',
    1.000000,
    48000.00,
    48000.00,
    'MXN',
    'Consulting invoice',
    'Client LATAM monthly invoice',
    '2026-03-03',
    'INC-003',
    null,
    now(),
    now(),
    false,
    null,
    null
),
(
    '670bbf9e-cc89-47cf-9fe0-6f01048f6ec9',
    '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51' AND cat_name = 'Dividends' LIMIT 1),
    'a1f0a327-8fb3-4f95-9578-354f13bf57d7',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    900.00,
    'USD',
    1.000000,
    900.00,
    900.00,
    'USD',
    'Quarterly dividends',
    'US ETF distribution',
    '2026-03-04',
    'INC-004',
    null,
    now(),
    now(),
    false,
    null,
    null
),
(
    'b4f31f56-33f2-4ad2-a0a0-c86af15fc90f',
    'c157f98d-96f1-4f8c-bdb8-baf86b610bc4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4' AND cat_name = 'General' LIMIT 1),
    '8df4b5e4-f8ac-4bcc-b84b-594f79c500fd',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    120000.00,
    'JPY',
    1.000000,
    120000.00,
    120000.00,
    'JPY',
    'Travel refund',
    'Airline ticket refund',
    '2026-03-05',
    'INC-005',
    null,
    now(),
    now(),
    false,
    null,
    null
),
(
    '05bd8571-52b0-46f5-9dd8-03bc747f3c36',
    '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5' AND cat_name = 'General' LIMIT 1),
    'a4ae2788-e434-4b4f-a376-2de3ee194f87',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    11000.00,
    'EUR',
    1.000000,
    11000.00,
    11000.00,
    'EUR',
    'Service retainer',
    'Admin monthly service',
    '2026-03-05',
    'INC-006',
    null,
    now(),
    now(),
    false,
    null,
    null
),
-- INC-007  Cross-currency: CRC received → USD project (Casa Nueva)
(
    'a1c7e3b4-5d29-4f8a-b1e6-7c8d9e0f1a2b',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4' AND cat_name = 'Labor' LIMIT 1),
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    250000.00, 'CRC', 0.001920, 480.00, 250000.00, 'CRC',
    'Contractor reimbursement',
    'Freelance contractor returned unused advance',
    '2026-03-06', 'INC-007', null,
    now(), now(), false, null, null
),
-- INC-008  Cross-currency: USD received → MXN project (Freelance LATAM)
(
    'b2d8f4c5-6e3a-4b9c-c2f7-8d9e0a1b3c4d',
    'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910' AND cat_name = 'Sales' LIMIT 1),
    '65c810a2-444e-4e11-b699-47779f1f7ab2',
    '6540f863-6a3c-4b02-813f-f1aeceefef1d',
    1500.00, 'USD', 17.250000, 25875.00, 1500.00, 'USD',
    'Client invoice payment',
    'International client paid in USD',
    '2026-03-07', 'INC-008', null,
    now(), now(), false, null, null
),
-- INC-009  Same currency: JPY (Viaje Japón)
(
    'c3e9a5d6-7f4b-4cad-d3a8-9e0f1b2c4d5e',
    'c157f98d-96f1-4f8c-bdb8-baf86b610bc4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4' AND cat_name = 'Transport' LIMIT 1),
    '8df4b5e4-f8ac-4bcc-b84b-594f79c500fd',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    35000.00, 'JPY', 1.000000, 35000.00, 35000.00, 'JPY',
    'Train pass refund',
    'JR Pass partial refund',
    '2026-03-07', 'INC-009', null,
    now(), now(), false, null, null
),
-- INC-010  Cross-currency: EUR received → USD project (Inversiones Globales)
(
    'd4fab6e7-8a5c-4dbe-e4b9-af1a2c3d5e6f',
    '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51' AND cat_name = 'Broker Fees' LIMIT 1),
    '33ffb6a3-9d3f-4b0a-80d3-67385f1e4f43',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    450.00, 'EUR', 1.080000, 486.00, 450.00, 'EUR',
    'Broker fee rebate',
    'Annual fee rebate from broker',
    '2026-03-08', 'INC-010', null,
    now(), now(), false, null, null
),
-- INC-011  Cross-currency: EUR received → GBP project (UK Expansion)
(
    'e5abc7f8-9b6d-4ecf-f5ca-ba2b3d4e6f7a',
    'de19de16-3c83-4bb8-8c4d-bdb4f79d718e',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e' AND cat_name = 'Consulting' LIMIT 1),
    'a4ae2788-e434-4b4f-a376-2de3ee194f87',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    8500.00, 'EUR', 0.860000, 7310.00, 8500.00, 'EUR',
    'UK consulting retainer',
    'Monthly consulting engagement income',
    '2026-03-08', 'INC-011', null,
    now(), now(), false, null, null
),
-- INC-012  Same currency: PEN (Proyecto Compartido Andino)
(
    'f6bcd8a9-ac7e-4fd0-a6db-cb3c4e5f7a8b',
    '43a2325d-6b57-4f13-b909-c3db0f9686f8',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '43a2325d-6b57-4f13-b909-c3db0f9686f8' AND cat_name = 'Vendors' LIMIT 1),
    '0da9a4f5-ecb0-45fd-b7f4-334d7471d6fd',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    12500.00, 'PEN', 1.000000, 12500.00, 12500.00, 'PEN',
    'Vendor advance return',
    'Vendor returned excess payment',
    '2026-03-09', 'INC-012', null,
    now(), now(), false, null, null
),
-- INC-013  Deleted income (Casa Nueva - Materials)
(
    'a7cde9ba-bd8f-4ae1-b7ec-dc4d5f6a8b9c',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4' AND cat_name = 'Materials' LIMIT 1),
    '849f3091-e3a3-48c6-8afc-24289b7575d8',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    3200.00, 'USD', 1.000000, 3200.00, 3200.00, 'USD',
    'Materials sale (cancelled)',
    'Reversed sale — buyer returned goods',
    '2026-03-04', 'INC-013', 'deleted after reversal',
    now() - interval '5 days', now() - interval '4 days',
    true, now() - interval '4 days', '5603f479-46fa-496a-8ed1-b64725bb7581'
),
-- INC-014  Same currency: CRC (Carro Nissan - Fuel reimbursement)
(
    'b8def0cb-ce9a-4bf2-c8fd-ed5e6a7b9cad',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '5b2b11a2-dc2c-4537-845f-6c9d19f0af05' AND cat_name = 'Fuel' LIMIT 1),
    '12124702-0f05-4d71-87dd-a7bc01db57be',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    85000.00, 'CRC', 1.000000, 85000.00, 85000.00, 'CRC',
    'Fuel reimbursement',
    'Insurance fuel reimbursement',
    '2026-03-09', 'INC-014', null,
    now(), now(), false, null, null
),
-- INC-015  Same currency: EUR (Operaciones Admin - Payroll)
(
    'c9efa1dc-dfa0-4ca3-d9ae-fe6f7b8cadbe',
    '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5' AND cat_name = 'Payroll' LIMIT 1),
    'a4ae2788-e434-4b4f-a376-2de3ee194f87',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    4200.00, 'EUR', 1.000000, 4200.00, 4200.00, 'EUR',
    'Payroll refund',
    'Employee overpayment returned',
    '2026-03-09', 'INC-015', null,
    now(), now(), false, null, null
),
-- INC-016  Same currency: USD (Casa Nueva - Food)
(
    'daf0b2ed-e0b1-4db4-eabf-af7a8c9dbecf',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4' AND cat_name = 'Food' LIMIT 1),
    '849f3091-e3a3-48c6-8afc-24289b7575d8',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    180.00, 'USD', 1.000000, 180.00, 180.00, 'USD',
    'Catering refund',
    'Cancelled event catering',
    '2026-03-10', 'INC-016', null,
    now(), now(), false, null, null
),
-- INC-017  Cross-currency: USD received → JPY project (Viaje Japón)
(
    'eb01c3fe-f1c2-4ec5-fbc0-b08b9daefda0',
    'c157f98d-96f1-4f8c-bdb8-baf86b610bc4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4' AND cat_name = 'Hotels' LIMIT 1),
    'a1f0a327-8fb3-4f95-9578-354f13bf57d7',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    320.00, 'USD', 149.500000, 47840.00, 320.00, 'USD',
    'Hotel booking refund',
    'Cancelled reservation partial refund',
    '2026-03-10', 'INC-017', null,
    now(), now(), false, null, null
),
-- INC-018  Same currency: MXN (Freelance LATAM - Ops)
(
    'fc12d4af-a2d3-4fd6-acd1-c19caebfaeb1',
    'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910' AND cat_name = 'Ops' LIMIT 1),
    'd93ab31f-83fa-4f77-92ff-8b4ccfbdaf4d',
    '6540f863-6a3c-4b02-813f-f1aeceefef1d',
    35000.00, 'MXN', 1.000000, 35000.00, 35000.00, 'MXN',
    'Ops revenue',
    'Operational services rendered',
    '2026-03-10', 'INC-018', null,
    now(), now(), false, null, null
),
-- INC-019  Cross-currency: EUR → GBP project (UK Expansion - Marketing)
(
    'ad23e5b0-b3e4-4ae7-bde2-d2adbeface02',
    'de19de16-3c83-4bb8-8c4d-bdb4f79d718e',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e' AND cat_name = 'Marketing' LIMIT 1),
    'a4ae2788-e434-4b4f-a376-2de3ee194f87',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    15000.00, 'EUR', 0.860000, 12900.00, 15000.00, 'EUR',
    'Marketing grant',
    'Government marketing expansion grant',
    '2026-02-28', 'INC-019', 'grant disbursement',
    now(), now(), false, null, null
),
-- INC-020  Same currency: USD (Inversiones Globales - Dividends)
(
    'be34f6c1-c4f5-4bf8-cef3-e3becafdbf13',
    '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51' AND cat_name = 'Dividends' LIMIT 1),
    'a1f0a327-8fb3-4f95-9578-354f13bf57d7',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    1250.00, 'USD', 1.000000, 1250.00, 1250.00, 'USD',
    'Q2 Dividends',
    'Second quarter ETF dividend distribution',
    '2026-02-15', 'INC-020', null,
    now(), now(), false, null, null
);

-- Bulk income generation (120 rows across all projects with varied categories)
WITH project_cycle AS (
    SELECT
        gs,
        CASE (gs % 8)
            WHEN 0 THEN '5b2b11a2-dc2c-4537-845f-6c9d19f0af05'::uuid
            WHEN 1 THEN '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4'::uuid
            WHEN 2 THEN 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910'::uuid
            WHEN 3 THEN '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51'::uuid
            WHEN 4 THEN 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4'::uuid
            WHEN 5 THEN '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5'::uuid
            WHEN 6 THEN '43a2325d-6b57-4f13-b909-c3db0f9686f8'::uuid
            ELSE 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e'::uuid
        END AS prj_id
    FROM generate_series(1, 120) AS gs
),
data AS (
    SELECT
        gen_random_uuid() AS inc_id,
        pc.prj_id,
        (SELECT c.cat_id FROM public.categories c
         WHERE c.cat_project_id = pc.prj_id AND c.cat_is_deleted = false
         ORDER BY c.cat_name OFFSET (pc.gs % 4) LIMIT 1) AS cat_id,
        COALESCE(
            (SELECT ppm.ppm_payment_method_id FROM public.project_payment_methods ppm
             WHERE ppm.ppm_project_id = pc.prj_id ORDER BY ppm.ppm_created_at
             OFFSET (pc.gs % 2) LIMIT 1),
            (SELECT ppm.ppm_payment_method_id FROM public.project_payment_methods ppm
             WHERE ppm.ppm_project_id = pc.prj_id ORDER BY ppm.ppm_created_at LIMIT 1)
        ) AS pmt_id,
        (SELECT p.prj_owner_user_id FROM public.projects p WHERE p.prj_id = pc.prj_id) AS usr_id,
        (SELECT p.prj_currency_code FROM public.projects p WHERE p.prj_id = pc.prj_id) AS curr,
        pc.gs
    FROM project_cycle pc
)
INSERT INTO public.incomes (
    inc_id, inc_project_id, inc_category_id, inc_payment_method_id,
    inc_created_by_user_id, inc_original_amount, inc_original_currency,
    inc_exchange_rate, inc_converted_amount, inc_account_amount,
    inc_account_currency, inc_title, inc_description, inc_income_date,
    inc_receipt_number, inc_notes, inc_created_at, inc_updated_at,
    inc_is_deleted, inc_deleted_at, inc_deleted_by_user_id
)
SELECT
    d.inc_id, d.prj_id, d.cat_id, d.pmt_id, d.usr_id,
    ROUND((CASE (d.gs % 5)
        WHEN 0 THEN 100  + d.gs * 17.5
        WHEN 1 THEN 250  + d.gs * 11.3
        WHEN 2 THEN 500  + d.gs * 23.7
        WHEN 3 THEN 75   + d.gs * 6.25
        ELSE        1000 + d.gs * 31.5
    END)::numeric, 2),
    d.curr,
    1.000000,
    ROUND((CASE (d.gs % 5)
        WHEN 0 THEN 100  + d.gs * 17.5
        WHEN 1 THEN 250  + d.gs * 11.3
        WHEN 2 THEN 500  + d.gs * 23.7
        WHEN 3 THEN 75   + d.gs * 6.25
        ELSE        1000 + d.gs * 31.5
    END)::numeric, 2),
    ROUND((CASE (d.gs % 5)
        WHEN 0 THEN 100  + d.gs * 17.5
        WHEN 1 THEN 250  + d.gs * 11.3
        WHEN 2 THEN 500  + d.gs * 23.7
        WHEN 3 THEN 75   + d.gs * 6.25
        ELSE        1000 + d.gs * 31.5
    END)::numeric, 2),
    d.curr,
    'Seed income #' || d.gs,
    CASE (d.gs % 4)
        WHEN 0 THEN 'Client payment received'
        WHEN 1 THEN 'Service fee collected'
        WHEN 2 THEN 'Reimbursement processed'
        ELSE 'Revenue deposit'
    END,
    DATE '2026-01-01' + ((d.gs % 68)::int),
    'INC-AUTO-' || LPAD(d.gs::text, 3, '0'),
    CASE WHEN d.gs % 6 = 0 THEN 'batch seed' ELSE null END,
    now() - ((d.gs % 30) || ' days')::interval,
    now() - ((d.gs % 30) || ' days')::interval,
    false, null, null
FROM data d;

-- ------------------------------------------------------------
-- 11) Expenses (manual set + generated bulk)
-- ------------------------------------------------------------
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
    exp_obligation_equivalent_amount,
    exp_title,
    exp_description,
    exp_expense_date,
    exp_receipt_number,
    exp_notes,
    exp_is_template,
    exp_created_at,
    exp_updated_at,
    exp_is_deleted,
    exp_deleted_at,
    exp_deleted_by_user_id
)
VALUES
(
    'e72cc085-1eb2-481f-bfa3-28a6e6854f2e',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '5b2b11a2-dc2c-4537-845f-6c9d19f0af05' AND cat_name = 'Loan' LIMIT 1),
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '92fa7061-3601-4a6e-b95a-67b94703fbd8',
    1000000.00,
    'CRC',
    1.000000,
    1000000.00,
    1000000.00,
    'Loan payment #1',
    'Monthly BAC installment',
    '2026-03-01',
    'EXP-001',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '40ecff52-21ee-44c1-9f11-4303d00143fc',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4' AND cat_name = 'Labor' LIMIT 1),
    '849f3091-e3a3-48c6-8afc-24289b7575d8',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    1000.00,
    'USD',
    1.000000,
    1000.00,
    null,
    'Payroll contractor',
    'Main construction worker',
    '2026-03-01',
    'EXP-002',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '7c2e307c-5a18-4d9f-8c30-7dce3b0289a9',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4' AND cat_name = 'Food' LIMIT 1),
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    30000.00,
    'CRC',
    0.001900,
    57.00,
    null,
    'Food purchase CRC',
    'Converted into project USD',
    '2026-03-02',
    'EXP-003',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '6a48af56-02bc-4679-a598-800987faa470',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '5b2b11a2-dc2c-4537-845f-6c9d19f0af05' AND cat_name = 'Loan' LIMIT 1),
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '92fa7061-3601-4a6e-b95a-67b94703fbd8',
    9000000.00,
    'CRC',
    1.000000,
    9000000.00,
    9000000.00,
    'Loan principal cancel (deleted sample)',
    'Soft delete validation row',
    '2026-03-03',
    'EXP-004',
    'deleted sample row',
    false,
    now() - interval '7 days',
    now() - interval '6 days',
    true,
    now() - interval '6 days',
    '5603f479-46fa-496a-8ed1-b64725bb7581'
),
(
    'f08a4d02-cfb5-46bb-a0ba-2c84de5f6f88',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4' AND cat_name = 'Materials' LIMIT 1),
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    '2f3b3a08-dbb9-4a80-86a4-26f7a6274955',
    780000.00,
    'CRC',
    0.001920,
    1497.60,
    1497.60,
    'Mortgage advance payment #1',
    'Payment done from CRC account',
    '2026-03-04',
    'EXP-005',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '4fdccf00-fb38-4a03-a76f-8e2b5342ef3d',
    '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5' AND cat_name = 'Infrastructure' LIMIT 1),
    'ce6111b3-8f7f-4418-8c24-31ca4d50f2fe',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    '274d7486-839e-495f-b2ea-a5dff1e6f435',
    5000.00,
    'USD',
    0.920000,
    4600.00,
    4600.00,
    'Infra financing payment',
    'Converted from USD to EUR project base',
    '2026-03-05',
    'EXP-006',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '68ea1f7e-88a8-45fc-8a4f-ae02cb3f4f74',
    'de19de16-3c83-4bb8-8c4d-bdb4f79d718e',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e' AND cat_name = 'Compliance' LIMIT 1),
    'a4ae2788-e434-4b4f-a376-2de3ee194f87',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    'f3f06a7c-a9d8-4ef4-95ed-f04f2b11e21f',
    3200.00,
    'GBP',
    1.000000,
    3200.00,
    3200.00,
    'Legal debt payment #1',
    'First installment',
    '2026-03-06',
    'EXP-007',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '7532d7aa-31c2-4d75-8956-a7f9f8b67db3',
    'c157f98d-96f1-4f8c-bdb8-baf86b610bc4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4' AND cat_name = 'Hotels' LIMIT 1),
    '8df4b5e4-f8ac-4bcc-b84b-594f79c500fd',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    null,
    18000.00,
    'JPY',
    1.000000,
    18000.00,
    null,
    'Tokyo hotel',
    'Main hotel booking',
    '2026-03-06',
    'EXP-008',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '197ed017-cf9b-4d77-a03e-a7cf4b11bd11',
    '43a2325d-6b57-4f13-b909-c3db0f9686f8',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '43a2325d-6b57-4f13-b909-c3db0f9686f8' AND cat_name = 'Travel' LIMIT 1),
    '0da9a4f5-ecb0-45fd-b7f4-334d7471d6fd',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    460.00,
    'PEN',
    1.000000,
    460.00,
    null,
    'Domestic travel',
    'Field travel in Peru',
    '2026-03-06',
    'EXP-009',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '34ab2f24-e685-49e0-b9d7-622e0645f95f',
    'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910' AND cat_name = 'Ops' LIMIT 1),
    'd93ab31f-83fa-4f77-92ff-8b4ccfbdaf4d',
    '6540f863-6a3c-4b02-813f-f1aeceefef1d',
    'a5f358aa-c0a5-44f2-bdcc-48ce89ce733f',
    18500.00,
    'MXN',
    1.000000,
    18500.00,
    18500.00,
    'Tax installment payment #1',
    'Monthly tax debt payment',
    '2026-03-07',
    'EXP-010',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    '7d10dcf8-fca8-4a67-9858-77443c8db308',
    '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51' AND cat_name = 'Subscriptions' LIMIT 1),
    '33ffb6a3-9d3f-4b0a-80d3-67385f1e4f43',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    null,
    120.00,
    'EUR',
    1.080000,
    129.60,
    null,
    'Research platform annual',
    'Financial data subscription',
    '2026-03-08',
    'EXP-011',
    null,
    false,
    now(),
    now(),
    false,
    null,
    null
),
(
    'f5bccf65-e8b8-44de-a4cd-4a80dbd5f97d',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '5b2b11a2-dc2c-4537-845f-6c9d19f0af05' AND cat_name = 'Maintenance' LIMIT 1),
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    0.00 + 45000.00,
    'CRC',
    1.000000,
    45000.00,
    null,
    'Template: regular maintenance',
    'Reusable template expense',
    '2026-03-09',
    null,
    null,
    true,
    now(),
    now(),
    false,
    null,
    null
),
-- EXP-012  Cross-currency: EUR paid → GBP project (UK Expansion - Compliance)
(
    '1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d',
    'de19de16-3c83-4bb8-8c4d-bdb4f79d718e',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e' AND cat_name = 'Compliance' LIMIT 1),
    'a4ae2788-e434-4b4f-a376-2de3ee194f87',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    null,
    2200.00, 'EUR', 0.860000, 1892.00, null,
    'Regulatory filing fee',
    'UK compliance annual filing',
    '2026-03-10', 'EXP-012', null, false,
    now(), now(), false, null, null
),
-- EXP-013  Same currency: EUR (Operaciones Admin - Legal)
(
    '2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e',
    '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5' AND cat_name = 'Legal' LIMIT 1),
    'a4ae2788-e434-4b4f-a376-2de3ee194f87',
    'bf759a1f-a5ae-4448-87f3-b919246c3054',
    null,
    3750.00, 'EUR', 1.000000, 3750.00, null,
    'Legal consultation',
    'Monthly legal retainer fee',
    '2026-03-11', 'EXP-013', null, false,
    now(), now(), false, null, null
),
-- EXP-014  Cross-currency: USD paid → MXN project (Freelance LATAM - Sales)
(
    '3c4d5e6f-7a8b-4c9d-0e1f-2a3b4c5d6e7f',
    'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910' AND cat_name = 'Sales' LIMIT 1),
    '65c810a2-444e-4e11-b699-47779f1f7ab2',
    '6540f863-6a3c-4b02-813f-f1aeceefef1d',
    null,
    890.00, 'USD', 17.250000, 15352.50, null,
    'Sales commission payout',
    'Monthly partner commissions',
    '2026-03-11', 'EXP-014', null, false,
    now(), now(), false, null, null
),
-- EXP-015  Same currency: PEN (Proyecto Compartido Andino - Meetings)
(
    '4d5e6f7a-8b9c-4d0e-1f2a-3b4c5d6e7f8a',
    '43a2325d-6b57-4f13-b909-c3db0f9686f8',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '43a2325d-6b57-4f13-b909-c3db0f9686f8' AND cat_name = 'Meetings' LIMIT 1),
    '0da9a4f5-ecb0-45fd-b7f4-334d7471d6fd',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    720.00, 'PEN', 1.000000, 720.00, null,
    'Team offsite catering',
    'Quarterly planning meeting expenses',
    '2026-03-12', 'EXP-015', null, false,
    now(), now(), false, null, null
),
-- EXP-016  Deleted expense (Inversiones Globales - Broker Fees)
(
    '5e6f7a8b-9c0d-4e1f-2a3b-4c5d6e7f8a9b',
    '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51' AND cat_name = 'Broker Fees' LIMIT 1),
    'a1f0a327-8fb3-4f95-9578-354f13bf57d7',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    null,
    475.00, 'USD', 1.000000, 475.00, null,
    'Duplicate broker charge',
    'Charged in error — reversed',
    '2026-03-05', 'EXP-016', 'duplicate charge deleted',
    false,
    now() - interval '8 days', now() - interval '7 days',
    true, now() - interval '7 days', 'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0'
),
-- EXP-017  Same currency: JPY (Viaje Japón - Food)
(
    '6f7a8b9c-0d1e-4f2a-3b4c-5d6e7f8a9b0c',
    'c157f98d-96f1-4f8c-bdb8-baf86b610bc4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4' AND cat_name = 'Food' LIMIT 1),
    '8df4b5e4-f8ac-4bcc-b84b-594f79c500fd',
    'bdbc5d0f-eaa0-4700-9e8b-fc23b207bcf0',
    null,
    4500.00, 'JPY', 1.000000, 4500.00, null,
    'Ramen dinner for team',
    'Team dinner in Shinjuku',
    '2026-03-12', 'EXP-017', null, false,
    now(), now(), false, null, null
),
-- EXP-018  Cross-currency: CRC paid → USD project (Casa Nueva - General)
(
    '7a8b9c0d-1e2f-4a3b-4c5d-6e7f8a9b0c1d',
    '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4' AND cat_name = 'General' LIMIT 1),
    '8d66d542-e790-41b6-a65c-815bedc78b96',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    185000.00, 'CRC', 0.001920, 355.20, null,
    'General supplies',
    'Hardware store purchase in colones',
    '2026-03-13', 'EXP-018', null, false,
    now(), now(), false, null, null
),
-- EXP-019  Same currency: CRC (Carro Nissan - General)
(
    '8b9c0d1e-2f3a-4b4c-5d6e-7f8a9b0c1d2e',
    '5b2b11a2-dc2c-4537-845f-6c9d19f0af05',
    (SELECT cat_id FROM public.categories WHERE cat_project_id = '5b2b11a2-dc2c-4537-845f-6c9d19f0af05' AND cat_name = 'General' LIMIT 1),
    '12124702-0f05-4d71-87dd-a7bc01db57be',
    '5603f479-46fa-496a-8ed1-b64725bb7581',
    null,
    65000.00, 'CRC', 1.000000, 65000.00, null,
    'Car wash and detail',
    'Monthly vehicle detailing service',
    '2026-03-13', 'EXP-019', null, false,
    now(), now(), false, null, null
);

-- Bulk expense generation (150 rows across all projects with varied categories)
WITH project_cycle AS (
    SELECT
        gs,
        CASE (gs % 8)
            WHEN 0 THEN '5b2b11a2-dc2c-4537-845f-6c9d19f0af05'::uuid
            WHEN 1 THEN '27cfae3e-b2bd-4a1c-8f61-45cdd8cf38e4'::uuid
            WHEN 2 THEN 'ee9b7652-0f88-4f39-9ab2-dc7a6f33b910'::uuid
            WHEN 3 THEN '2b22509a-e4bc-4a4d-a5d0-9cd57f5e7d51'::uuid
            WHEN 4 THEN 'c157f98d-96f1-4f8c-bdb8-baf86b610bc4'::uuid
            WHEN 5 THEN '8d3c4f3f-7e6b-49c3-9b6d-2f48159ca7b5'::uuid
            WHEN 6 THEN '43a2325d-6b57-4f13-b909-c3db0f9686f8'::uuid
            ELSE 'de19de16-3c83-4bb8-8c4d-bdb4f79d718e'::uuid
        END AS prj_id
    FROM generate_series(1, 150) AS gs
),
data AS (
    SELECT
        gen_random_uuid() AS exp_id,
        pc.prj_id,
        (SELECT c.cat_id FROM public.categories c
         WHERE c.cat_project_id = pc.prj_id AND c.cat_is_deleted = false
         ORDER BY c.cat_name OFFSET (pc.gs % 4) LIMIT 1) AS cat_id,
        COALESCE(
            (SELECT ppm.ppm_payment_method_id FROM public.project_payment_methods ppm
             WHERE ppm.ppm_project_id = pc.prj_id ORDER BY ppm.ppm_created_at
             OFFSET (pc.gs % 2) LIMIT 1),
            (SELECT ppm.ppm_payment_method_id FROM public.project_payment_methods ppm
             WHERE ppm.ppm_project_id = pc.prj_id ORDER BY ppm.ppm_created_at LIMIT 1)
        ) AS pmt_id,
        (SELECT p.prj_owner_user_id FROM public.projects p WHERE p.prj_id = pc.prj_id) AS usr_id,
        (SELECT p.prj_currency_code FROM public.projects p WHERE p.prj_id = pc.prj_id) AS curr,
        pc.gs
    FROM project_cycle pc
)
INSERT INTO public.expenses (
    exp_id, exp_project_id, exp_category_id, exp_payment_method_id,
    exp_created_by_user_id, exp_obligation_id, exp_original_amount,
    exp_original_currency, exp_exchange_rate, exp_converted_amount,
    exp_obligation_equivalent_amount, exp_title, exp_description,
    exp_expense_date, exp_receipt_number, exp_notes, exp_is_template,
    exp_created_at, exp_updated_at, exp_is_deleted, exp_deleted_at,
    exp_deleted_by_user_id
)
SELECT
    d.exp_id, d.prj_id, d.cat_id, d.pmt_id, d.usr_id, null,
    ROUND((CASE (d.gs % 5)
        WHEN 0 THEN 25   + d.gs * 3.75
        WHEN 1 THEN 150  + d.gs * 8.50
        WHEN 2 THEN 80   + d.gs * 5.20
        WHEN 3 THEN 300  + d.gs * 12.0
        ELSE        50   + d.gs * 2.10
    END)::numeric, 2),
    d.curr,
    1.000000,
    ROUND((CASE (d.gs % 5)
        WHEN 0 THEN 25   + d.gs * 3.75
        WHEN 1 THEN 150  + d.gs * 8.50
        WHEN 2 THEN 80   + d.gs * 5.20
        WHEN 3 THEN 300  + d.gs * 12.0
        ELSE        50   + d.gs * 2.10
    END)::numeric, 2),
    null,
    'Seed expense #' || d.gs,
    CASE (d.gs % 4)
        WHEN 0 THEN 'Office supplies purchase'
        WHEN 1 THEN 'Vendor payment processed'
        WHEN 2 THEN 'Service subscription renewal'
        ELSE 'Operational cost'
    END,
    DATE '2026-01-01' + ((d.gs % 68)::int),
    'EXP-AUTO-' || LPAD(d.gs::text, 3, '0'),
    CASE WHEN d.gs % 7 = 0 THEN 'batch seed' ELSE null END,
    false,
    now() - ((d.gs % 30) || ' days')::interval,
    now() - ((d.gs % 30) || ' days')::interval,
    false, null, null
FROM data d;

-- ------------------------------------------------------------
-- 12) Transaction exchange table (expense/income conversions)
-- ------------------------------------------------------------
INSERT INTO public.transaction_currency_exchanges (
    tce_id,
    tce_expense_id,
    tce_income_id,
    tce_currency_code,
    tce_exchange_rate,
    tce_converted_amount,
    tce_created_at
)
VALUES
    (gen_random_uuid(), 'e72cc085-1eb2-481f-bfa3-28a6e6854f2e', null, 'USD', 0.001920, 1920.00, now()),
    (gen_random_uuid(), '40ecff52-21ee-44c1-9f11-4303d00143fc', null, 'CRC', 520.000000, 520000.00, now()),
    (gen_random_uuid(), '7c2e307c-5a18-4d9f-8c30-7dce3b0289a9', null, 'CRC', 526.000000, 29982.00, now()),
    (gen_random_uuid(), '4fdccf00-fb38-4a03-a76f-8e2b5342ef3d', null, 'USD', 1.090000, 5014.00, now()),
    (gen_random_uuid(), null, '8ff5509e-8524-49f4-b53b-e0ad5421a76f', 'CRC', 520.000000, 1820000.00, now()),
    (gen_random_uuid(), null, '26c2013c-b777-40b4-ac7e-a743b6eeb774', 'USD', 0.058000, 2784.00, now()),
    (gen_random_uuid(), null, 'b4f31f56-33f2-4ad2-a0a0-c86af15fc90f', 'USD', 0.006700, 804.00, now()),
    (gen_random_uuid(), null, '05bd8571-52b0-46f5-9dd8-03bc747f3c36', 'USD', 1.080000, 11880.00, now()),
    -- Cross-currency income conversions (project currency → alt currency)
    (gen_random_uuid(), null, 'a1c7e3b4-5d29-4f8a-b1e6-7c8d9e0f1a2b', 'CRC', 520.000000, 249600.00, now()),
    (gen_random_uuid(), null, 'b2d8f4c5-6e3a-4b9c-c2f7-8d9e0a1b3c4d', 'USD', 0.058000, 1500.75, now()),
    (gen_random_uuid(), null, 'd4fab6e7-8a5c-4dbe-e4b9-af1a2c3d5e6f', 'EUR', 0.920000, 447.12, now()),
    (gen_random_uuid(), null, 'e5abc7f8-9b6d-4ecf-f5ca-ba2b3d4e6f7a', 'USD', 1.270000, 9283.70, now()),
    (gen_random_uuid(), null, 'eb01c3fe-f1c2-4ec5-fbc0-b08b9daefda0', 'USD', 0.006700, 320.53, now()),
    (gen_random_uuid(), null, 'ad23e5b0-b3e4-4ae7-bde2-d2adbeface02', 'USD', 1.270000, 16383.00, now()),
    -- Cross-currency expense conversions (project currency → alt currency)
    (gen_random_uuid(), '1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d', null, 'USD', 1.270000, 2402.84, now()),
    (gen_random_uuid(), '3c4d5e6f-7a8b-4c9d-0e1f-2a3b4c5d6e7f', null, 'USD', 0.058000, 890.45, now()),
    (gen_random_uuid(), '7a8b9c0d-1e2f-4a3b-4c5d-6e7f8a9b0c1d', null, 'CRC', 520.000000, 184704.00, now());

INSERT INTO public.transaction_currency_exchanges (
    tce_id,
    tce_expense_id,
    tce_income_id,
    tce_currency_code,
    tce_exchange_rate,
    tce_converted_amount,
    tce_created_at
)
SELECT
    gen_random_uuid(),
    e.exp_id,
    null,
    pac.pac_currency_code,
    1.000000,
    e.exp_converted_amount,
    now()
FROM public.expenses e
JOIN LATERAL (
    SELECT pac_currency_code
    FROM public.project_alternative_currencies pac
    WHERE pac.pac_project_id = e.exp_project_id
    ORDER BY pac.pac_currency_code
    LIMIT 1
) pac ON true
WHERE e.exp_title LIKE 'Seed expense #%'
ON CONFLICT (tce_expense_id, tce_currency_code) DO NOTHING;

INSERT INTO public.transaction_currency_exchanges (
    tce_id,
    tce_expense_id,
    tce_income_id,
    tce_currency_code,
    tce_exchange_rate,
    tce_converted_amount,
    tce_created_at
)
SELECT
    gen_random_uuid(),
    null,
    i.inc_id,
    pac.pac_currency_code,
    1.000000,
    i.inc_converted_amount,
    now()
FROM public.incomes i
JOIN LATERAL (
    SELECT pac_currency_code
    FROM public.project_alternative_currencies pac
    WHERE pac.pac_project_id = i.inc_project_id
    ORDER BY pac.pac_currency_code DESC
    LIMIT 1
) pac ON true
WHERE i.inc_title LIKE 'Seed income #%'
ON CONFLICT (tce_income_id, tce_currency_code) DO NOTHING;

-- ------------------------------------------------------------
-- 13) Audit logs
-- ------------------------------------------------------------
INSERT INTO public.audit_logs (
    aud_id,
    aud_entity_name,
    aud_entity_id,
    aud_action_type,
    aud_performed_by_user_id,
    aud_performed_at,
    aud_old_values,
    aud_new_values
)
VALUES
    (gen_random_uuid(), 'plans', 'f59a2b7b-5edf-4e8b-9d99-d6adf8adf4ac', 'update', 'bf759a1f-a5ae-4448-87f3-b919246c3054', now() - interval '9 days', '{"pln_can_use_api": false}'::jsonb, '{"pln_can_use_api": true}'::jsonb),
    (gen_random_uuid(), 'users', '5603f479-46fa-496a-8ed1-b64725bb7581', 'update', '5603f479-46fa-496a-8ed1-b64725bb7581', now() - interval '8 days', '{"usr_last_login_at": null}'::jsonb, '{"usr_last_login_at": "2026-03-10T18:09:16Z"}'::jsonb),
    (gen_random_uuid(), 'obligations', '92fa7061-3601-4a6e-b95a-67b94703fbd8', 'associate', '5603f479-46fa-496a-8ed1-b64725bb7581', now() - interval '7 days', null, '{"expense_id": "e72cc085-1eb2-481f-bfa3-28a6e6854f2e"}'::jsonb),
    (gen_random_uuid(), 'expenses', '6a48af56-02bc-4679-a598-800987faa470', 'delete', '5603f479-46fa-496a-8ed1-b64725bb7581', now() - interval '6 days', '{"exp_is_deleted": false}'::jsonb, '{"exp_is_deleted": true}'::jsonb),
    (gen_random_uuid(), 'user_subscriptions', 'fa1f08a3-38cb-4c64-82e6-022a9f7efe7a', 'status_change', 'bf759a1f-a5ae-4448-87f3-b919246c3054', now() - interval '5 days', '{"uss_status": "incomplete"}'::jsonb, '{"uss_status": "active"}'::jsonb),
    (gen_random_uuid(), 'project_members', (SELECT prj_id FROM public.projects WHERE prj_name = 'Proyecto Compartido Andino' LIMIT 1), 'create', '5603f479-46fa-496a-8ed1-b64725bb7581', now() - interval '4 days', null, '{"member_added": true}'::jsonb);

INSERT INTO public.audit_logs (
    aud_id,
    aud_entity_name,
    aud_entity_id,
    aud_action_type,
    aud_performed_by_user_id,
    aud_performed_at,
    aud_old_values,
    aud_new_values
)
SELECT
    gen_random_uuid(),
    'expenses',
    e.exp_id,
    'create',
    e.exp_created_by_user_id,
    e.exp_created_at,
    null,
    jsonb_build_object('title', e.exp_title, 'amount', e.exp_converted_amount, 'currency', e.exp_original_currency)
FROM public.expenses e
WHERE e.exp_title LIKE 'Seed expense #%'
LIMIT 40;

INSERT INTO public.audit_logs (
    aud_id,
    aud_entity_name,
    aud_entity_id,
    aud_action_type,
    aud_performed_by_user_id,
    aud_performed_at,
    aud_old_values,
    aud_new_values
)
SELECT
    gen_random_uuid(),
    'incomes',
    i.inc_id,
    'create',
    i.inc_created_by_user_id,
    i.inc_created_at,
    null,
    jsonb_build_object('title', i.inc_title, 'amount', i.inc_converted_amount, 'currency', i.inc_original_currency)
FROM public.incomes i
WHERE i.inc_title LIKE 'Seed income #%'
LIMIT 30;

COMMIT;

-- ------------------------------------------------------------
-- Quick checks
-- ------------------------------------------------------------
-- SELECT COUNT(*) AS users_count FROM public.users;
-- SELECT COUNT(*) AS projects_count FROM public.projects;
-- SELECT COUNT(*) AS categories_count FROM public.categories;
-- SELECT COUNT(*) AS payment_methods_count FROM public.payment_methods;
-- SELECT COUNT(*) AS obligations_count FROM public.obligations;
-- SELECT COUNT(*) AS expenses_count FROM public.expenses;
-- SELECT COUNT(*) AS incomes_count FROM public.incomes;
-- SELECT COUNT(*) AS exchanges_count FROM public.transaction_currency_exchanges;
-- SELECT COUNT(*) AS audit_logs_count FROM public.audit_logs;