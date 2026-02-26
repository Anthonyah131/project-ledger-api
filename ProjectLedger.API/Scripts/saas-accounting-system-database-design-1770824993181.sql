-- ============================================================
-- SaaS Accounting System – Database Schema  (MVP Profesional)
-- PostgreSQL 16+ / CockroachDB compatible
-- ============================================================
--
-- Principios de diseño:
--   • UUID como PK en todas las tablas (sin SERIAL/secuencias).
--   • gen_random_uuid() via extensión pgcrypto.
--   • Eliminación lógica (*_is_deleted + *_deleted_at + *_deleted_by_user_id).
--   • NOT NULL estricto en campos obligatorios.
--   • CHECK constraints en tipos, montos y formatos.
--   • NUMERIC/DECIMAL para montos financieros.
--   • Sin ON DELETE CASCADE (control desde la aplicación).
--   • Auditoría completa vía audit_logs (JSONB).
--   • Multi-currency con tipo de cambio manual.
--   • Catálogo de monedas (currencies) con PK natural ISO 4217.
--   • Compatibilidad CockroachDB (sin features exclusivas de PG).
--   • Módulo de obligaciones (deudas) con estado calculado dinámicamente.
--   • Presupuestos opcionales a nivel de proyecto y categoría.
--   • Sin flujo de estados manuales en gastos (solo soft delete).
-- ============================================================
-- ============================================================
-- 0. EXTENSIONS
-- ============================================================
-- pgcrypto proporciona gen_random_uuid() en PostgreSQL.
-- CockroachDB tiene gen_random_uuid() built-in, por lo que esta
-- extensión puede NO ser necesaria. Si da error, comenta esta línea.
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================
-- 1. CURRENCIES  (catálogo de monedas habilitadas)
-- ============================================================
-- PK natural: cur_code (ISO 4217, ej: 'CRC', 'USD', 'EUR').
-- UUID no aplica aquí: el código ISO ya ES el identificador canónico.
-- Todas las columnas de moneda en el schema referencian esta tabla,
-- garantizando que ningún registro puede usar un código inexistente.
-- cur_is_active: permite deshabilitar monedas sin eliminarlas.
-- cur_decimal_places: controla el formato de redondeo en la app.
-- ============================================================
CREATE TABLE public.currencies (
    cur_code            varchar(3)      NOT NULL,                -- PK · Código ISO 4217 (ej: 'CRC', 'USD', 'EUR')
    cur_name            varchar(100)    NOT NULL,                -- Nombre completo (ej: 'Costa Rican Colón')
    cur_symbol          varchar(10)     NOT NULL,                -- Símbolo de visualización (ej: '₡', '$', '€')
    cur_decimal_places  smallint        NOT NULL DEFAULT 2,      -- Decimales estándar (0 para CRC/JPY, 2 para USD/EUR)
    cur_is_active       boolean         NOT NULL DEFAULT true,   -- ¿Moneda disponible para usar en proyectos y gastos?
    cur_created_at      timestamptz     NOT NULL DEFAULT now(),  -- Fecha de inserción en el catálogo
    CONSTRAINT currencies_pkey PRIMARY KEY (cur_code),
    CONSTRAINT currencies_decimal_places_range CHECK (cur_decimal_places BETWEEN 0 AND 8)
);

-- Seed: monedas más comunes. Agregar las que necesites.
INSERT INTO public.currencies (cur_code, cur_name, cur_symbol, cur_decimal_places) VALUES
    ('USD', 'US Dollar',            '$',  2),
    ('EUR', 'Euro',                 '€',  2),
    ('CRC', 'Costa Rican Colón',    '₡',  0),
    ('MXN', 'Mexican Peso',         '$',  2),
    ('COP', 'Colombian Peso',       '$',  0),
    ('BRL', 'Brazilian Real',       'R$', 2),
    ('ARS', 'Argentine Peso',       '$',  2),
    ('CLP', 'Chilean Peso',         '$',  0),
    ('PEN', 'Peruvian Sol',         'S/', 2),
    ('GBP', 'British Pound',        '£',  2),
    ('JPY', 'Japanese Yen',         '¥',  0),
    ('CAD', 'Canadian Dollar',      '$',  2),
    ('AUD', 'Australian Dollar',    '$',  2),
    ('CHF', 'Swiss Franc',          'Fr', 2),
    ('CNY', 'Chinese Yuan',         '¥',  2);

-- ============================================================
-- 2. USERS
-- ============================================================
-- Req 1.1: usuario nuevo se crea desactivado (usr_is_active DEFAULT false).
-- Req 2:   usr_is_admin distingue admin global vs usuario normal.
-- Req 12:  usr_plan_id referencia al plan asignado (FK → plans).
-- ============================================================
CREATE TABLE public.users (
    usr_id              uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del usuario
    usr_email           varchar(255)    NOT NULL,                            -- Correo electrónico; único, formato validado vía CHECK
    usr_password_hash   text,                                               -- Hash de contraseña (bcrypt/argon2); NULL si usa solo OAuth
    usr_full_name       varchar(255)    NOT NULL,                            -- Nombre completo del usuario
    usr_plan_id         uuid            NOT NULL,                            -- FK → plans · Plan de suscripción asignado
    usr_is_active       boolean         NOT NULL DEFAULT false,              -- ¿Cuenta activa? Nuevo usuario inicia desactivado
    usr_is_admin        boolean         NOT NULL DEFAULT false,              -- ¿Es administrador global del sistema?
    usr_avatar_url      text,                                               -- URL del avatar de perfil (opcional)
    usr_last_login_at   timestamptz,                                        -- Último inicio de sesión (opcional)
    usr_created_at      timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    usr_updated_at      timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    usr_is_deleted      boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = eliminado
    usr_deleted_at      timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminado)
    usr_deleted_by_user_id uuid,                                            -- FK → users (self) · Admin/usuario que eliminó (NULL = no eliminado)
    CONSTRAINT users_pkey PRIMARY KEY (usr_id),
    CONSTRAINT users_email_unique UNIQUE (usr_email),
    CONSTRAINT users_email_format CHECK (
        usr_email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$'
    ),
    CONSTRAINT users_deleted_by_user_id_fkey FOREIGN KEY (usr_deleted_by_user_id) REFERENCES public.users (usr_id)
    -- usr_plan_id FK se agrega después de crear tabla plans
);

-- ============================================================
-- 3. PLANS  (Req 12 – Planes del sistema)
-- ============================================================
-- Define los planes disponibles (free, pro, enterprise, etc.).
-- Los precios se manejarán con Stripe en el futuro (fuera de DB).
-- Permisos y límites están directamente en esta tabla.
-- ============================================================
CREATE TABLE public.plans (
    pln_id              uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del plan
    pln_name            varchar(100)    NOT NULL,                            -- Nombre visible del plan (ej: "Free", "Pro")
    pln_slug            varchar(50)     NOT NULL,                            -- Slug URL-friendly (ej: "free", "pro"); UNIQUE
    pln_description     text,                                               -- Descripción del plan (opcional)
    pln_is_active       boolean         NOT NULL DEFAULT true,              -- ¿Plan disponible para nuevas asignaciones?
    pln_display_order   integer         NOT NULL DEFAULT 0,                 -- Orden de visualización en la interfaz
    
    -- ========== PERMISOS (FEATURES) ==========
    pln_can_create_projects         boolean NOT NULL DEFAULT true,          -- Permiso: crear proyectos
    pln_can_edit_projects           boolean NOT NULL DEFAULT true,          -- Permiso: editar proyectos
    pln_can_delete_projects         boolean NOT NULL DEFAULT true,          -- Permiso: eliminar proyectos
    pln_can_share_projects          boolean NOT NULL DEFAULT true,          -- Permiso: compartir proyectos con otros usuarios
    pln_can_export_data             boolean NOT NULL DEFAULT false,         -- Permiso: exportar datos (CSV, PDF, Excel)
    pln_can_use_advanced_reports    boolean NOT NULL DEFAULT false,         -- Permiso: reportes avanzados
    pln_can_use_ocr                 boolean NOT NULL DEFAULT false,         -- Permiso: reconocimiento óptico de recibos (OCR)
    pln_can_use_api                 boolean NOT NULL DEFAULT false,         -- Permiso: acceso a la API pública
    pln_can_use_multi_currency      boolean NOT NULL DEFAULT true,          -- Permiso: multi-moneda en proyectos
    pln_can_set_budgets             boolean NOT NULL DEFAULT true,          -- Permiso: configurar presupuestos
    
    -- ========== LÍMITES NUMÉRICOS (JSONB) ==========
    -- Ejemplo de estructura:
    -- {
    --   "max_projects": 3,
    --   "max_expenses_per_month": 50,
    --   "max_categories_per_project": 10,
    --   "max_payment_methods": 5,
    --   "max_team_members_per_project": 2
    -- }
    -- NULL o campo con "unlimited": true significa sin límite
    pln_limits          jsonb,                                              -- Límites numéricos del plan en JSON (opcional; NULL = sin límites)
    
    pln_created_at      timestamptz     NOT NULL DEFAULT now(),             -- Fecha de creación del registro
    pln_updated_at      timestamptz     NOT NULL DEFAULT now(),             -- Fecha de última actualización

    CONSTRAINT plans_pkey           PRIMARY KEY (pln_id),
    CONSTRAINT plans_slug_unique    UNIQUE (pln_slug)
);

-- Agregar FK de users → plans ahora que existe la tabla
ALTER TABLE public.users
    ADD CONSTRAINT users_plan_id_fkey FOREIGN KEY (usr_plan_id) REFERENCES public.plans (pln_id);


-- ============================================================
-- 4. REFRESH TOKENS  (Req 13 – JWT Auth)
-- ============================================================
CREATE TABLE public.refresh_tokens (
    rtk_id          uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del token
    rtk_user_id     uuid            NOT NULL,                            -- FK → users · Usuario dueño del token
    rtk_token_hash  text            NOT NULL,                            -- Hash SHA-256 del refresh token
    rtk_expires_at  timestamptz     NOT NULL,                            -- Fecha/hora de expiración del token
    rtk_revoked_at  timestamptz,                                         -- Fecha de revocación; NULL = token vigente (opcional)
    rtk_created_at  timestamptz     NOT NULL DEFAULT now(),              -- Fecha de emisión del token
    CONSTRAINT refresh_tokens_pkey PRIMARY KEY (rtk_id),
    CONSTRAINT refresh_tokens_user_id_fkey FOREIGN KEY (rtk_user_id) REFERENCES public.users (usr_id)
);


-- ============================================================
-- 5. PASSWORD_RESET_TOKENS  (Restablecimiento de contraseña vía OTP)
-- ============================================================
-- Almacena códigos OTP de un solo uso para el flujo de reset de contraseña.
-- El código nunca se guarda en texto plano; solo su hash SHA-256.
-- Al generar un nuevo código, la app debe marcar los anteriores como usados
-- (prt_used_at) o dejar que expiren naturalmente.
--
-- Flujo:
--   1. POST /auth/forgot-password → genera OTP (6 dígitos),
--      guarda hash + expiración, envía OTP por email al usuario.
--   2. POST /auth/reset-password  → recibe OTP + nueva contraseña,
--      hashea el código, busca token válido (no usado, no expirado),
--      actualiza password en users, marca prt_used_at = now().
-- ============================================================
CREATE TABLE public.password_reset_tokens (
    prt_id          uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del token
    prt_user_id     uuid            NOT NULL,                            -- FK → users · Usuario que solicitó el reset
    prt_code_hash   text            NOT NULL,                            -- Hash SHA-256 del código OTP (nunca en texto plano)
    prt_expires_at  timestamptz     NOT NULL,                            -- Fecha/hora de expiración del código OTP
    prt_used_at     timestamptz,                                         -- Fecha en que fue utilizado; NULL = aún no usado
    prt_created_at  timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    CONSTRAINT password_reset_tokens_pkey PRIMARY KEY (prt_id),
    CONSTRAINT password_reset_tokens_user_id_fkey FOREIGN KEY (prt_user_id) REFERENCES public.users (usr_id)
);

-- ============================================================
-- 6. EXTERNAL_AUTH_PROVIDERS  (OAuth – Google, Microsoft, etc.)
-- ============================================================
-- Vincula usuarios con cuentas externas (OAuth).
-- Permite login con Google, Microsoft, GitHub, Facebook, etc.
-- Un usuario puede tener múltiples providers vinculados.
-- ============================================================
CREATE TABLE public.external_auth_providers (
    eap_id                  uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del vínculo OAuth
    eap_user_id             uuid            NOT NULL,                            -- FK → users · Usuario local vinculado
    eap_provider            varchar(50)     NOT NULL,                            -- Proveedor: 'google', 'microsoft', 'github', etc.
    eap_provider_user_id    varchar(255)    NOT NULL,                            -- ID del usuario en el proveedor externo
    eap_provider_email      varchar(255),                                        -- Email reportado por el proveedor (opcional)
    eap_access_token_hash   text,                                               -- Hash del access token encriptado (opcional)
    eap_refresh_token_hash  text,                                               -- Hash del refresh token encriptado (opcional)
    eap_token_expires_at    timestamptz,                                        -- Expiración del token del proveedor (opcional)
    eap_metadata            jsonb,                                              -- Claims y datos de perfil del proveedor (opcional)
    eap_created_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del vínculo
    eap_updated_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    eap_is_deleted          boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = vínculo eliminado
    eap_deleted_at          timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminado)
    eap_deleted_by_user_id  uuid,                                               -- FK → users · Quién desvinculó (NULL = no eliminado)

    CONSTRAINT external_auth_providers_pkey             PRIMARY KEY (eap_id),
    CONSTRAINT external_auth_providers_user_id_fkey     FOREIGN KEY (eap_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT external_auth_providers_deleted_by_fkey  FOREIGN KEY (eap_deleted_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT external_auth_providers_provider_check   CHECK (
        eap_provider IN ('google', 'microsoft', 'github', 'facebook', 'apple')
    ),
    CONSTRAINT external_auth_providers_provider_user_uq UNIQUE (eap_provider, eap_provider_user_id)
);

-- ============================================================
-- 7. PROJECTS  (Req 3)
-- ============================================================
-- prj_deleted_at / prj_deleted_by_user_id → auditoría de soft delete.
-- ============================================================
CREATE TABLE public.projects (
    prj_id                  uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del proyecto
    prj_name                varchar(255)    NOT NULL,                            -- Nombre del proyecto
    prj_owner_user_id       uuid            NOT NULL,                            -- FK → users · Propietario/creador del proyecto
    prj_currency_code       varchar(3)      NOT NULL,                            -- Moneda base del proyecto (ISO 4217, ej: "USD")
    prj_description         text,                                               -- Descripción del proyecto (opcional)
    prj_created_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    prj_updated_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    prj_is_deleted          boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = eliminado
    prj_deleted_at          timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminado)
    prj_deleted_by_user_id  uuid,                                               -- FK → users · Quién eliminó (NULL = no eliminado)
    CONSTRAINT projects_pkey PRIMARY KEY (prj_id),
    CONSTRAINT projects_owner_user_id_fkey FOREIGN KEY (prj_owner_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT projects_deleted_by_user_id_fkey FOREIGN KEY (prj_deleted_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT projects_currency_fkey FOREIGN KEY (prj_currency_code) REFERENCES public.currencies (cur_code)
);

-- ============================================================
-- 8. PROJECT_MEMBERS  (Req 3.3)
-- ============================================================
CREATE TABLE public.project_members (
    prm_id          uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único de la membresía
    prm_project_id  uuid            NOT NULL,                            -- FK → projects · Proyecto al que pertenece
    prm_user_id     uuid            NOT NULL,                            -- FK → users · Usuario miembro
    prm_role        varchar(20)     NOT NULL,                            -- Rol: 'owner', 'editor', 'viewer'
    prm_joined_at   timestamptz     NOT NULL DEFAULT now(),              -- Fecha en que se unió al proyecto
    prm_created_at  timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    prm_updated_at  timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    prm_is_deleted  boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = miembro removido
    prm_deleted_at  timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminado)
    prm_deleted_by_user_id uuid,                                        -- FK → users · Quién removió al miembro (NULL = no eliminado)
    CONSTRAINT project_members_pkey PRIMARY KEY (prm_id),
    CONSTRAINT project_members_project_id_fkey FOREIGN KEY (prm_project_id) REFERENCES public.projects (prj_id),
    CONSTRAINT project_members_user_id_fkey FOREIGN KEY (prm_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT project_members_deleted_by_user_id_fkey FOREIGN KEY (prm_deleted_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT project_members_role_check CHECK (prm_role IN ('owner', 'editor', 'viewer'))
);

-- Partial UNIQUE: permite re-invitar a un miembro previamente eliminado (soft-delete)
CREATE UNIQUE INDEX idx_prm_project_user_active
    ON public.project_members (prm_project_id, prm_user_id)
    WHERE prm_is_deleted = false;

-- ============================================================
-- 9. CATEGORIES  (Req 4)
-- ============================================================
-- cat_is_default: marca la categoría "General" creada automáticamente.
-- cat_budget_amount: presupuesto opcional por categoría (NULL = sin presupuesto).
-- cat_deleted_at / cat_deleted_by_user_id → auditoría de soft delete.
-- ============================================================
CREATE TABLE public.categories (
    cat_id                  uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único de la categoría
    cat_project_id          uuid            NOT NULL,                            -- FK → projects · Proyecto al que pertenece
    cat_name                varchar(100)    NOT NULL,                            -- Nombre de la categoría
    cat_description         text,                                               -- Descripción de la categoría (opcional)
    cat_is_default          boolean         NOT NULL DEFAULT false,              -- ¿Es la categoría "General" creada automáticamente?
    cat_budget_amount       numeric(14, 2),                                     -- Presupuesto asignado a la categoría (opcional; NULL = sin presupuesto)
    cat_created_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    cat_updated_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    cat_is_deleted          boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = eliminada
    cat_deleted_at          timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminada)
    cat_deleted_by_user_id  uuid,                                               -- FK → users · Quién eliminó (NULL = no eliminada)
    CONSTRAINT categories_pkey PRIMARY KEY (cat_id),
    CONSTRAINT categories_project_id_fkey FOREIGN KEY (cat_project_id) REFERENCES public.projects (prj_id),
    CONSTRAINT categories_deleted_by_user_id_fkey FOREIGN KEY (cat_deleted_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT categories_budget_positive CHECK (cat_budget_amount IS NULL OR cat_budget_amount > 0)
);

-- Partial UNIQUE: permite recrear una categoría con el mismo nombre tras soft-delete
CREATE UNIQUE INDEX idx_cat_project_name_active
    ON public.categories (cat_project_id, cat_name)
    WHERE cat_is_deleted = false;

-- ============================================================
-- 10. PAYMENT_METHODS  (Req 8)
-- ============================================================
-- Métodos de pago pertenecen al USUARIO (no al proyecto).
-- Req 8.2: permite ver movimientos de una cuenta cruzando proyectos.
-- ============================================================
CREATE TABLE public.payment_methods (
    pmt_id              uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del método de pago
    pmt_owner_user_id   uuid            NOT NULL,                            -- FK → users · Usuario propietario de la cuenta
    pmt_name            varchar(255)    NOT NULL,                            -- Nombre descriptivo (ej: "Banco X - Ahorro")
    pmt_type            varchar(50)     NOT NULL,                            -- Tipo: 'bank', 'cash', 'card'
    pmt_currency        varchar(3)      NOT NULL,                            -- Moneda de la cuenta (ISO 4217, ej: "USD")
    pmt_bank_name       varchar(255),                                       -- Nombre del banco o emisor (opcional)
    pmt_account_number  varchar(100),                                       -- Número de cuenta o tarjeta (opcional)
    pmt_description     text,                                               -- Nota descriptiva adicional (opcional)
    pmt_created_at      timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    pmt_updated_at      timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    pmt_is_deleted      boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = eliminado
    pmt_deleted_at      timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminado)
    pmt_deleted_by_user_id uuid,                                            -- FK → users · Quién eliminó (NULL = no eliminado)
    CONSTRAINT payment_methods_pkey PRIMARY KEY (pmt_id),
    CONSTRAINT payment_methods_owner_user_id_fkey FOREIGN KEY (pmt_owner_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT payment_methods_deleted_by_user_id_fkey FOREIGN KEY (pmt_deleted_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT payment_methods_type_check CHECK (pmt_type IN ('bank', 'cash', 'card')),
    CONSTRAINT payment_methods_currency_fkey FOREIGN KEY (pmt_currency) REFERENCES public.currencies (cur_code)
);

-- ============================================================
-- 11. EXPENSES  (Req 5 + Req 7 + Req 9)
-- ============================================================
-- LÓGICA DE CONVERSIÓN DE MONEDA:
--   El proyecto tiene una moneda base (prj_currency_code).
--   Cada gasto guarda:
--     • exp_original_amount / exp_original_currency → lo que realmente se pagó.
--     • exp_exchange_rate   → tasa ingresada manualmente por el usuario
--                             (unidades de moneda del proyecto por 1 unidad original).
--                             Ejemplo: proyecto en CRC, gasto en USD → rate = 520.00
--                             (1 USD = 520 CRC). Si la moneda es igual, rate = 1.
--     • exp_converted_amount → monto en la moneda del proyecto.
--                             Calculado en app: exp_original_amount * exp_exchange_rate.
--   La validación de consistencia (original * rate ≈ converted) se hace en la app,
--   no con triggers, para mantener compatibilidad con CockroachDB.
--
-- MONEDA ALTERNATIVA (visualización opcional):
--   exp_alt_currency / exp_alt_exchange_rate / exp_alt_amount permiten guardar
--   el equivalente del gasto en una segunda moneda (ej: ver el gasto en CRC y en USD).
--   Los tres campos son NULL o los tres tienen valor (CHECK de consistencia).
--   Calculado en app: exp_converted_amount * exp_alt_exchange_rate = exp_alt_amount.
--
-- CAMBIO: Se eliminó exp_status y flujo de estados (simplificación MVP).
--         Solo se maneja soft delete para eliminar gastos.
-- exp_is_template: gastos frecuentes (Req 7).
-- exp_obligation_id: permite asociar un gasto como pago/abono de una deuda.
--   NULL = gasto normal | NOT NULL = pago de obligación.
--   Regla de negocio (app-level): exp_is_template = true NO puede tener obligation.
-- ============================================================
CREATE TABLE public.expenses (
    exp_id                  uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del gasto
    exp_project_id          uuid            NOT NULL,                            -- FK → projects · Proyecto al que pertenece
    exp_category_id         uuid            NOT NULL,                            -- FK → categories · Categoría del gasto
    exp_payment_method_id   uuid            NOT NULL,                            -- FK → payment_methods · Método de pago utilizado
    exp_created_by_user_id  uuid            NOT NULL,                            -- FK → users · Usuario que registró el gasto
    exp_obligation_id       uuid,                                               -- FK → obligations · NULL = gasto normal; NOT NULL = pago de deuda
    -- Montos y moneda
    exp_original_amount     numeric(14, 2)  NOT NULL,                            -- Monto en la moneda original (CHECK > 0)
    exp_original_currency   varchar(3)      NOT NULL,                            -- Moneda original del gasto (ISO 4217, 3 caracteres)
    exp_exchange_rate       numeric(18, 6)  NOT NULL DEFAULT 1.000000,           -- Tipo de cambio aplicado (CHECK > 0)
    exp_converted_amount    numeric(14, 2)  NOT NULL,                            -- Monto convertido a moneda del proyecto (CHECK > 0)
    -- Datos descriptivos
    exp_title               varchar(255)    NOT NULL,                            -- Título/concepto del gasto
    exp_description         text,                                               -- Descripción detallada (opcional)
    exp_expense_date        date            NOT NULL,                            -- Fecha en que se realizó el gasto
    exp_receipt_number      varchar(100),                                        -- Número de recibo o factura (opcional)
    exp_notes               text,                                               -- Notas adicionales (opcional)
    -- Plantilla
    exp_is_template         boolean         NOT NULL DEFAULT false,              -- ¿Es plantilla de gasto frecuente? (app: true → no puede tener obligation)
    -- Moneda alternativa (visualización opcional)
    exp_alt_currency        varchar(3),                                          -- Moneda alternativa para visualización (opcional; NULL = sin equivalente)
    exp_alt_exchange_rate   numeric(18, 6),                                      -- Tasa usada: unidades alt por 1 unidad de moneda del proyecto (CHECK > 0 si presente)
    exp_alt_amount          numeric(14, 2),                                      -- Monto equivalente en exp_alt_currency; calculado en app (CHECK > 0 si presente)
    -- Timestamps y soft delete
    exp_created_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    exp_updated_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    exp_is_deleted          boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = eliminado
    exp_deleted_at          timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminado)
    exp_deleted_by_user_id  uuid,                                               -- FK → users · Quién eliminó (NULL = no eliminado)
    CONSTRAINT expenses_pkey PRIMARY KEY (exp_id),
    CONSTRAINT expenses_project_id_fkey FOREIGN KEY (exp_project_id) REFERENCES public.projects (prj_id),
    CONSTRAINT expenses_category_id_fkey FOREIGN KEY (exp_category_id) REFERENCES public.categories (cat_id),
    CONSTRAINT expenses_payment_method_id_fkey FOREIGN KEY (exp_payment_method_id) REFERENCES public.payment_methods (pmt_id),
    CONSTRAINT expenses_created_by_user_id_fkey FOREIGN KEY (exp_created_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT expenses_deleted_by_user_id_fkey FOREIGN KEY (exp_deleted_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT expenses_original_amount_positive CHECK (exp_original_amount > 0),
    CONSTRAINT expenses_converted_amount_positive CHECK (exp_converted_amount > 0),
    CONSTRAINT expenses_exchange_rate_positive CHECK (exp_exchange_rate > 0),
    CONSTRAINT expenses_original_currency_fkey FOREIGN KEY (exp_original_currency) REFERENCES public.currencies (cur_code),
    -- Moneda alternativa: los 3 campos deben estar todos presentes o todos ausentes
    CONSTRAINT expenses_alt_fields_consistency CHECK (
        (exp_alt_currency IS NULL) = (exp_alt_exchange_rate IS NULL) AND
        (exp_alt_currency IS NULL) = (exp_alt_amount IS NULL)
    ),
    CONSTRAINT expenses_alt_currency_fkey FOREIGN KEY (exp_alt_currency) REFERENCES public.currencies (cur_code),
    CONSTRAINT expenses_alt_exchange_rate_positive CHECK (exp_alt_exchange_rate IS NULL OR exp_alt_exchange_rate > 0),
    CONSTRAINT expenses_alt_amount_positive CHECK (exp_alt_amount IS NULL OR exp_alt_amount > 0)
);

-- ============================================================
-- 12. OBLIGATIONS  (Módulo de deudas)
-- ============================================================
-- Representa una deuda/obligación financiera dentro de un proyecto.
-- El ESTADO NO se persiste en la DB; se calcula dinámicamente en la
-- aplicación según:
--   • SUM(exp_converted_amount) de expenses asociados (no eliminados)
--   • obl_due_date vs fecha actual
--   • Saldo pendiente = obl_total_amount - SUM(pagos)
--
-- Estados calculados (app-level, NO almacenados):
--   open           → sin pagos registrados
--   partially_paid → pagos parciales (sum < total)
--   paid           → sum >= total_amount
--   overdue        → due_date < now() AND saldo > 0
--
-- Reglas de negocio (validadas en aplicación, NO con triggers):
--   • SUM de pagos NO puede superar obl_total_amount.
--   • No se permiten pagos si la obligación ya está pagada.
--   • Eliminar lógicamente un gasto asociado recalcula el saldo.
--   • La moneda del pago debe ser coherente con la obligación.
-- ============================================================
CREATE TABLE public.obligations (
    obl_id                  uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único de la obligación/deuda
    obl_project_id          uuid            NOT NULL,                            -- FK → projects · Proyecto al que pertenece
    obl_created_by_user_id  uuid            NOT NULL,                            -- FK → users · Usuario que registró la obligación
    obl_title               varchar(255)    NOT NULL,                            -- Título descriptivo de la deuda
    obl_description         text,                                               -- Descripción detallada (opcional)
    obl_total_amount        numeric(14, 2)  NOT NULL,                            -- Monto total de la deuda (CHECK > 0)
    obl_currency            varchar(3)      NOT NULL,                            -- Moneda de la obligación (ISO 4217, 3 caracteres)
    obl_due_date            date,                                               -- Fecha de vencimiento (opcional; NULL = sin vencimiento)
    obl_created_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    obl_updated_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    obl_is_deleted          boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = eliminada
    obl_deleted_at          timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminada)
    obl_deleted_by_user_id  uuid,                                               -- FK → users · Quién eliminó (NULL = no eliminada)
    CONSTRAINT obligations_pkey PRIMARY KEY (obl_id),
    CONSTRAINT obligations_project_id_fkey FOREIGN KEY (obl_project_id) REFERENCES public.projects (prj_id),
    CONSTRAINT obligations_created_by_user_id_fkey FOREIGN KEY (obl_created_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT obligations_deleted_by_user_id_fkey FOREIGN KEY (obl_deleted_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT obligations_total_amount_positive CHECK (obl_total_amount > 0),
    CONSTRAINT obligations_currency_fkey FOREIGN KEY (obl_currency) REFERENCES public.currencies (cur_code)
);

-- Agregar FK de expenses → obligations ahora que la tabla existe
ALTER TABLE public.expenses
    ADD CONSTRAINT expenses_obligation_id_fkey FOREIGN KEY (exp_obligation_id) REFERENCES public.obligations (obl_id);

-- ============================================================
-- 13. AUDIT_LOGS  (Req 6)
-- ============================================================
-- Registro de auditoría para toda entidad relevante.
-- aud_old_values / aud_new_values → snapshots JSONB.
-- ============================================================
CREATE TABLE public.audit_logs (
    aud_id                      uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del registro de auditoría
    aud_entity_name             varchar(100)    NOT NULL,                            -- Nombre de la entidad afectada (ej: "expenses", "obligations")
    aud_entity_id               uuid            NOT NULL,                            -- ID del registro afectado
    aud_action_type             varchar(50)     NOT NULL,                            -- Tipo de acción: 'create', 'update', 'delete', 'status_change', 'associate'
    aud_performed_by_user_id    uuid            NOT NULL,                            -- FK → users · Usuario que realizó la acción
    aud_performed_at            timestamptz     NOT NULL DEFAULT now(),              -- Fecha/hora en que se realizó la acción
    aud_old_values              jsonb,                                              -- Snapshot JSONB del estado anterior (NULL en create)
    aud_new_values              jsonb,                                              -- Snapshot JSONB del estado nuevo (NULL en delete)
    CONSTRAINT audit_logs_pkey PRIMARY KEY (aud_id),
    CONSTRAINT audit_logs_user_id_fkey FOREIGN KEY (aud_performed_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT audit_logs_action_type_check CHECK (
        aud_action_type IN ('create', 'update', 'delete', 'status_change', 'associate')
    )
);
-- Nota: 'associate' cubre la acción de vincular un expense a una obligation.

-- ============================================================
-- 14. PROJECT_BUDGETS  (Req 10)
-- ============================================================
-- Presupuesto global del proyecto con umbral de alerta.
-- Opcional: un proyecto puede no tener presupuesto.
-- Un solo presupuesto activo por proyecto (partial UNIQUE index).
-- ============================================================
CREATE TABLE public.project_budgets (
    pjb_id                  uuid            NOT NULL DEFAULT gen_random_uuid(),  -- PK · Identificador único del presupuesto
    pjb_project_id          uuid            NOT NULL,                            -- FK → projects · Proyecto al que aplica
    pjb_total_budget        numeric(14, 2)  NOT NULL,                            -- Monto total del presupuesto (CHECK > 0)
    pjb_alert_percentage    numeric(5, 2)   NOT NULL DEFAULT 80.00,              -- Umbral de alerta (1-100%); al alcanzar se notifica
    pjb_created_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de creación del registro
    pjb_updated_at          timestamptz     NOT NULL DEFAULT now(),              -- Fecha de última actualización
    pjb_is_deleted          boolean         NOT NULL DEFAULT false,              -- Borrado lógico; true = eliminado
    pjb_deleted_at          timestamptz,                                        -- Fecha del borrado lógico (NULL = no eliminado)
    pjb_deleted_by_user_id  uuid,                                               -- FK → users · Quién eliminó (NULL = no eliminado)
    CONSTRAINT project_budgets_pkey PRIMARY KEY (pjb_id),
    CONSTRAINT project_budgets_project_id_fkey FOREIGN KEY (pjb_project_id) REFERENCES public.projects (prj_id),
    CONSTRAINT project_budgets_deleted_by_user_id_fkey FOREIGN KEY (pjb_deleted_by_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT project_budgets_total_positive CHECK (pjb_total_budget > 0),
    CONSTRAINT project_budgets_alert_range CHECK (
        pjb_alert_percentage BETWEEN 1.00
        AND 100.00
    )
);

-- Partial UNIQUE: un solo presupuesto activo por proyecto
CREATE UNIQUE INDEX idx_pjb_project_active
    ON public.project_budgets (pjb_project_id)
    WHERE pjb_is_deleted = false;

-- ============================================================
-- 15. INDICES
-- ============================================================
-- currencies
CREATE INDEX idx_cur_is_active ON public.currencies (cur_is_active);

-- users  (usr_email ya tiene UNIQUE index implícito)
CREATE INDEX idx_usr_is_deleted ON public.users (usr_is_deleted);

CREATE INDEX idx_usr_plan_id ON public.users (usr_plan_id);

-- plans  (pln_slug ya tiene UNIQUE index implícito)
CREATE INDEX idx_pln_is_active ON public.plans (pln_is_active);

-- refresh_tokens
CREATE INDEX idx_rtk_user_id ON public.refresh_tokens (rtk_user_id);

CREATE INDEX idx_rtk_expires_at ON public.refresh_tokens (rtk_expires_at);

-- password_reset_tokens
CREATE INDEX idx_prt_user_id ON public.password_reset_tokens (prt_user_id);

CREATE INDEX idx_prt_expires_at ON public.password_reset_tokens (prt_expires_at);

-- external_auth_providers  (provider+provider_user_id ya tiene UNIQUE index implícito)
CREATE INDEX idx_eap_user_id ON public.external_auth_providers (eap_user_id);

CREATE INDEX idx_eap_is_deleted ON public.external_auth_providers (eap_is_deleted);

-- projects
CREATE INDEX idx_prj_owner_user_id ON public.projects (prj_owner_user_id);

CREATE INDEX idx_prj_is_deleted ON public.projects (prj_is_deleted);

-- project_members
CREATE INDEX idx_prm_project_id ON public.project_members (prm_project_id);

CREATE INDEX idx_prm_user_id ON public.project_members (prm_user_id);

CREATE INDEX idx_prm_is_deleted ON public.project_members (prm_is_deleted);

-- categories
CREATE INDEX idx_cat_project_id ON public.categories (cat_project_id);

CREATE INDEX idx_cat_is_deleted ON public.categories (cat_is_deleted);

-- payment_methods
CREATE INDEX idx_pmt_owner_user_id ON public.payment_methods (pmt_owner_user_id);

CREATE INDEX idx_pmt_is_deleted ON public.payment_methods (pmt_is_deleted);

-- expenses  (todas las FK + date + obligation)
CREATE INDEX idx_exp_project_id ON public.expenses (exp_project_id);

CREATE INDEX idx_exp_category_id ON public.expenses (exp_category_id);

CREATE INDEX idx_exp_payment_method_id ON public.expenses (exp_payment_method_id);

CREATE INDEX idx_exp_created_by_user_id ON public.expenses (exp_created_by_user_id);

CREATE INDEX idx_exp_expense_date ON public.expenses (exp_expense_date);

CREATE INDEX idx_exp_obligation_id ON public.expenses (exp_obligation_id);

CREATE INDEX idx_exp_is_deleted ON public.expenses (exp_is_deleted);

CREATE INDEX idx_exp_is_template ON public.expenses (exp_is_template);

CREATE INDEX idx_exp_alt_currency ON public.expenses (exp_alt_currency) WHERE exp_alt_currency IS NOT NULL;

-- obligations
CREATE INDEX idx_obl_project_id ON public.obligations (obl_project_id);

CREATE INDEX idx_obl_created_by_user_id ON public.obligations (obl_created_by_user_id);

CREATE INDEX idx_obl_is_deleted ON public.obligations (obl_is_deleted);

-- audit_logs
CREATE INDEX idx_aud_entity ON public.audit_logs (aud_entity_name, aud_entity_id);

CREATE INDEX idx_aud_performed_by ON public.audit_logs (aud_performed_by_user_id);

CREATE INDEX idx_aud_performed_at ON public.audit_logs (aud_performed_at);

-- project_budgets
CREATE INDEX idx_pjb_project_id ON public.project_budgets (pjb_project_id);

CREATE INDEX idx_pjb_is_deleted ON public.project_budgets (pjb_is_deleted);

-- ============================================================
-- FIN DEL SCHEMA
-- ============================================================