-- DROP SCHEMA public;

CREATE SCHEMA public AUTHORIZATION root;
-- public.currencies definition

-- Drop table

-- DROP TABLE currencies;

CREATE TABLE public.currencies (
	cur_code VARCHAR(3) NOT NULL,
	cur_name VARCHAR(100) NOT NULL,
	cur_symbol VARCHAR(10) NOT NULL,
	cur_decimal_places INT2 NOT NULL DEFAULT 2:::INT8,
	cur_is_active BOOL NOT NULL DEFAULT true,
	cur_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT currencies_pkey PRIMARY KEY (cur_code ASC),
	INDEX idx_cur_is_active (cur_is_active ASC),
	CONSTRAINT currencies_decimal_places_range CHECK (cur_decimal_places BETWEEN 0:::INT8 AND 8:::INT8)
);


-- public."plans" definition

-- Drop table

-- DROP TABLE "plans";

CREATE TABLE public.plans (
	pln_id UUID NOT NULL DEFAULT gen_random_uuid(),
	pln_name VARCHAR(100) NOT NULL,
	pln_slug VARCHAR(50) NOT NULL,
	pln_description STRING NULL,
	pln_is_active BOOL NOT NULL DEFAULT true,
	pln_display_order INT8 NOT NULL DEFAULT 0:::INT8,
	pln_can_create_projects BOOL NOT NULL DEFAULT true,
	pln_can_edit_projects BOOL NOT NULL DEFAULT true,
	pln_can_delete_projects BOOL NOT NULL DEFAULT true,
	pln_can_share_projects BOOL NOT NULL DEFAULT true,
	pln_can_export_data BOOL NOT NULL DEFAULT false,
	pln_can_use_advanced_reports BOOL NOT NULL DEFAULT false,
	pln_can_use_ocr BOOL NOT NULL DEFAULT false,
	pln_can_use_api BOOL NOT NULL DEFAULT false,
	pln_can_use_multi_currency BOOL NOT NULL DEFAULT true,
	pln_can_set_budgets BOOL NOT NULL DEFAULT true,
	pln_limits JSONB NULL,
	pln_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	pln_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	pln_monthly_price DECIMAL(18,2) NOT NULL DEFAULT 0:::DECIMAL,
	pln_currency VARCHAR(3) NOT NULL DEFAULT 'usd':::STRING,
	pln_stripe_product_id VARCHAR(255) NULL,
	pln_stripe_price_id VARCHAR(255) NULL,
	pln_stripe_payment_link_id VARCHAR(255) NULL,
	pln_stripe_payment_link_url STRING NULL,
	CONSTRAINT plans_pkey PRIMARY KEY (pln_id ASC),
	UNIQUE INDEX plans_slug_unique (pln_slug ASC),
	INDEX idx_pln_is_active (pln_is_active ASC),
	UNIQUE INDEX plans_stripe_product_id_uq (pln_stripe_product_id ASC),
	UNIQUE INDEX plans_stripe_price_id_uq (pln_stripe_price_id ASC),
	UNIQUE INDEX plans_stripe_payment_link_id_uq (pln_stripe_payment_link_id ASC)
);


-- public.stripe_webhook_events definition

-- Drop table

-- DROP TABLE stripe_webhook_events;

CREATE TABLE public.stripe_webhook_events (
	swe_id UUID NOT NULL DEFAULT gen_random_uuid(),
	swe_stripe_event_id VARCHAR(255) NOT NULL,
	swe_type VARCHAR(100) NOT NULL,
	swe_processed_successfully BOOL NOT NULL DEFAULT false,
	swe_error_message STRING NULL,
	swe_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	swe_processed_at TIMESTAMPTZ NULL,
	CONSTRAINT stripe_webhook_events_pkey PRIMARY KEY (swe_id ASC),
	UNIQUE INDEX stripe_webhook_events_event_id_uq (swe_stripe_event_id ASC),
	INDEX stripe_webhook_events_created_at_idx (swe_created_at ASC)
);


-- public."users" definition

-- Drop table

-- DROP TABLE "users";

CREATE TABLE public.users (
	usr_id UUID NOT NULL DEFAULT gen_random_uuid(),
	usr_email VARCHAR(255) NOT NULL,
	usr_password_hash STRING NULL,
	usr_full_name VARCHAR(255) NOT NULL,
	usr_plan_id UUID NOT NULL,
	usr_is_active BOOL NOT NULL DEFAULT false,
	usr_is_admin BOOL NOT NULL DEFAULT false,
	usr_avatar_url STRING NULL,
	usr_last_login_at TIMESTAMPTZ NULL,
	usr_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	usr_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	usr_is_deleted BOOL NOT NULL DEFAULT false,
	usr_deleted_at TIMESTAMPTZ NULL,
	usr_deleted_by_user_id UUID NULL,
	usr_stripe_customer_id VARCHAR(255) NULL,
	CONSTRAINT users_pkey PRIMARY KEY (usr_id ASC),
	CONSTRAINT users_deleted_by_user_id_fkey FOREIGN KEY (usr_deleted_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT users_plan_id_fkey FOREIGN KEY (usr_plan_id) REFERENCES public.plans(pln_id),
	UNIQUE INDEX users_email_unique (usr_email ASC),
	INDEX idx_usr_is_deleted (usr_is_deleted ASC),
	INDEX idx_usr_plan_id (usr_plan_id ASC),
	UNIQUE INDEX users_stripe_customer_id_uq (usr_stripe_customer_id ASC),
	CONSTRAINT users_email_format CHECK (usr_email ~* e'^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$':::STRING)
);


-- public.workspaces definition

-- Drop table

-- DROP TABLE workspaces;

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


-- public.audit_logs definition

-- Drop table

-- DROP TABLE audit_logs;

CREATE TABLE public.audit_logs (
	aud_id UUID NOT NULL DEFAULT gen_random_uuid(),
	aud_entity_name VARCHAR(100) NOT NULL,
	aud_entity_id UUID NOT NULL,
	aud_action_type VARCHAR(50) NOT NULL,
	aud_performed_by_user_id UUID NOT NULL,
	aud_performed_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	aud_old_values JSONB NULL,
	aud_new_values JSONB NULL,
	CONSTRAINT audit_logs_pkey PRIMARY KEY (aud_id ASC),
	CONSTRAINT audit_logs_user_id_fkey FOREIGN KEY (aud_performed_by_user_id) REFERENCES public.users(usr_id),
	INDEX idx_aud_entity (aud_entity_name ASC, aud_entity_id ASC),
	INDEX idx_aud_performed_by (aud_performed_by_user_id ASC),
	INDEX idx_aud_performed_at (aud_performed_at ASC),
	CONSTRAINT audit_logs_action_type_check CHECK (aud_action_type IN ('create':::STRING, 'update':::STRING, 'delete':::STRING, 'status_change':::STRING, 'associate':::STRING))
);


-- public.external_auth_providers definition

-- Drop table

-- DROP TABLE external_auth_providers;

CREATE TABLE public.external_auth_providers (
	eap_id UUID NOT NULL DEFAULT gen_random_uuid(),
	eap_user_id UUID NOT NULL,
	eap_provider VARCHAR(50) NOT NULL,
	eap_provider_user_id VARCHAR(255) NOT NULL,
	eap_provider_email VARCHAR(255) NULL,
	eap_access_token_hash STRING NULL,
	eap_refresh_token_hash STRING NULL,
	eap_token_expires_at TIMESTAMPTZ NULL,
	eap_metadata JSONB NULL,
	eap_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	eap_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	eap_is_deleted BOOL NOT NULL DEFAULT false,
	eap_deleted_at TIMESTAMPTZ NULL,
	eap_deleted_by_user_id UUID NULL,
	CONSTRAINT external_auth_providers_pkey PRIMARY KEY (eap_id ASC),
	CONSTRAINT external_auth_providers_user_id_fkey FOREIGN KEY (eap_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT external_auth_providers_deleted_by_fkey FOREIGN KEY (eap_deleted_by_user_id) REFERENCES public.users(usr_id),
	UNIQUE INDEX external_auth_providers_provider_user_uq (eap_provider ASC, eap_provider_user_id ASC),
	INDEX idx_eap_user_id (eap_user_id ASC),
	INDEX idx_eap_is_deleted (eap_is_deleted ASC),
	CONSTRAINT external_auth_providers_provider_check CHECK (eap_provider IN ('google':::STRING, 'microsoft':::STRING, 'github':::STRING, 'facebook':::STRING, 'apple':::STRING))
);


-- public.partners definition

-- Drop table

-- DROP TABLE partners;

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
	CONSTRAINT ptr_owner_fkey FOREIGN KEY (ptr_owner_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT ptr_deleted_by_fkey FOREIGN KEY (ptr_deleted_by_user_id) REFERENCES public.users(usr_id),
	INDEX idx_ptr_owner_user_id (ptr_owner_user_id ASC),
	INDEX idx_ptr_is_deleted (ptr_is_deleted ASC)
);
COMMENT ON TABLE public.partners IS e'Contactos financieros del usuario. Due\u00F1os de m\u00E9todos de pago y asignados a proyectos.';
COMMENT ON TABLE public.partners IS 'Contactos financieros del usuario. Dueños de métodos de pago y asignados a proyectos.';


-- public.password_reset_tokens definition

-- Drop table

-- DROP TABLE password_reset_tokens;

CREATE TABLE public.password_reset_tokens (
	prt_id UUID NOT NULL DEFAULT gen_random_uuid(),
	prt_user_id UUID NOT NULL,
	prt_code_hash STRING NOT NULL,
	prt_expires_at TIMESTAMPTZ NOT NULL,
	prt_used_at TIMESTAMPTZ NULL,
	prt_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT password_reset_tokens_pkey PRIMARY KEY (prt_id ASC),
	CONSTRAINT password_reset_tokens_user_id_fkey FOREIGN KEY (prt_user_id) REFERENCES public.users(usr_id),
	INDEX idx_prt_user_id (prt_user_id ASC),
	INDEX idx_prt_expires_at (prt_expires_at ASC)
);


-- public.payment_methods definition

-- Drop table

-- DROP TABLE payment_methods;

CREATE TABLE public.payment_methods (
	pmt_id UUID NOT NULL DEFAULT gen_random_uuid(),
	pmt_owner_user_id UUID NOT NULL,
	pmt_name VARCHAR(255) NOT NULL,
	pmt_type VARCHAR(50) NOT NULL,
	pmt_currency VARCHAR(3) NOT NULL,
	pmt_bank_name VARCHAR(255) NULL,
	pmt_account_number VARCHAR(100) NULL,
	pmt_description STRING NULL,
	pmt_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	pmt_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	pmt_is_deleted BOOL NOT NULL DEFAULT false,
	pmt_deleted_at TIMESTAMPTZ NULL,
	pmt_deleted_by_user_id UUID NULL,
	pmt_owner_partner_id UUID NULL,
	CONSTRAINT payment_methods_pkey PRIMARY KEY (pmt_id ASC),
	CONSTRAINT payment_methods_owner_user_id_fkey FOREIGN KEY (pmt_owner_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT payment_methods_deleted_by_user_id_fkey FOREIGN KEY (pmt_deleted_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT payment_methods_currency_fkey FOREIGN KEY (pmt_currency) REFERENCES public.currencies(cur_code),
	CONSTRAINT payment_methods_pmt_owner_partner_id_fkey FOREIGN KEY (pmt_owner_partner_id) REFERENCES public.partners(ptr_id),
	INDEX idx_pmt_owner_user_id (pmt_owner_user_id ASC),
	INDEX idx_pmt_is_deleted (pmt_is_deleted ASC),
	INDEX idx_pmt_owner_partner_id (pmt_owner_partner_id ASC) WHERE pmt_owner_partner_id IS NOT NULL,
	CONSTRAINT payment_methods_type_check CHECK (pmt_type IN ('bank':::STRING, 'cash':::STRING, 'card':::STRING))
);


-- public.projects definition

-- Drop table

-- DROP TABLE projects;

CREATE TABLE public.projects (
	prj_id UUID NOT NULL DEFAULT gen_random_uuid(),
	prj_name VARCHAR(255) NOT NULL,
	prj_owner_user_id UUID NOT NULL,
	prj_currency_code VARCHAR(3) NOT NULL,
	prj_description STRING NULL,
	prj_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	prj_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	prj_is_deleted BOOL NOT NULL DEFAULT false,
	prj_deleted_at TIMESTAMPTZ NULL,
	prj_deleted_by_user_id UUID NULL,
	prj_workspace_id UUID NULL,
	prj_partners_enabled BOOL NOT NULL DEFAULT false,
	CONSTRAINT projects_pkey PRIMARY KEY (prj_id ASC),
	CONSTRAINT projects_owner_user_id_fkey FOREIGN KEY (prj_owner_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT projects_deleted_by_user_id_fkey FOREIGN KEY (prj_deleted_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT projects_currency_fkey FOREIGN KEY (prj_currency_code) REFERENCES public.currencies(cur_code),
	CONSTRAINT projects_prj_workspace_id_fkey FOREIGN KEY (prj_workspace_id) REFERENCES public.workspaces(wks_id),
	INDEX idx_prj_owner_user_id (prj_owner_user_id ASC),
	INDEX idx_prj_is_deleted (prj_is_deleted ASC),
	INDEX idx_prj_workspace_id (prj_workspace_id ASC)
);


-- public.refresh_tokens definition

-- Drop table

-- DROP TABLE refresh_tokens;

CREATE TABLE public.refresh_tokens (
	rtk_id UUID NOT NULL DEFAULT gen_random_uuid(),
	rtk_user_id UUID NOT NULL,
	rtk_token_hash STRING NOT NULL,
	rtk_expires_at TIMESTAMPTZ NOT NULL,
	rtk_revoked_at TIMESTAMPTZ NULL,
	rtk_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT refresh_tokens_pkey PRIMARY KEY (rtk_id ASC),
	CONSTRAINT refresh_tokens_user_id_fkey FOREIGN KEY (rtk_user_id) REFERENCES public.users(usr_id),
	INDEX idx_rtk_user_id (rtk_user_id ASC),
	INDEX idx_rtk_expires_at (rtk_expires_at ASC)
);


-- public.user_subscriptions definition

-- Drop table

-- DROP TABLE user_subscriptions;

CREATE TABLE public.user_subscriptions (
	uss_id UUID NOT NULL DEFAULT gen_random_uuid(),
	uss_user_id UUID NULL,
	uss_plan_id UUID NULL,
	uss_stripe_subscription_id VARCHAR(255) NOT NULL,
	uss_stripe_customer_id VARCHAR(255) NULL,
	uss_stripe_price_id VARCHAR(255) NULL,
	uss_status VARCHAR(50) NOT NULL,
	uss_current_period_start TIMESTAMPTZ NULL,
	uss_current_period_end TIMESTAMPTZ NULL,
	uss_cancel_at_period_end BOOL NOT NULL DEFAULT false,
	uss_canceled_at TIMESTAMPTZ NULL,
	uss_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	uss_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT user_subscriptions_pkey PRIMARY KEY (uss_id ASC),
	CONSTRAINT user_subscriptions_user_fkey FOREIGN KEY (uss_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT user_subscriptions_plan_fkey FOREIGN KEY (uss_plan_id) REFERENCES public.plans(pln_id),
	UNIQUE INDEX user_subscriptions_stripe_subscription_id_uq (uss_stripe_subscription_id ASC),
	INDEX user_subscriptions_user_id_idx (uss_user_id ASC),
	INDEX user_subscriptions_customer_id_idx (uss_stripe_customer_id ASC)
);


-- public.workspace_members definition

-- Drop table

-- DROP TABLE workspace_members;

CREATE TABLE public.workspace_members (
	wkm_id UUID NOT NULL DEFAULT gen_random_uuid(),
	wkm_workspace_id UUID NOT NULL,
	wkm_user_id UUID NOT NULL,
	wkm_role VARCHAR(20) NOT NULL DEFAULT 'member':::STRING,
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
	UNIQUE INDEX uq_wkm_workspace_user_active (wkm_workspace_id ASC, wkm_user_id ASC) WHERE wkm_is_deleted = false,
	INDEX idx_wkm_workspace_id (wkm_workspace_id ASC),
	INDEX idx_wkm_user_id (wkm_user_id ASC),
	INDEX idx_wkm_is_deleted (wkm_is_deleted ASC),
	CONSTRAINT wkm_role_check CHECK (wkm_role IN ('owner':::STRING, 'member':::STRING))
);


-- public.categories definition

-- Drop table

-- DROP TABLE categories;

CREATE TABLE public.categories (
	cat_id UUID NOT NULL DEFAULT gen_random_uuid(),
	cat_project_id UUID NOT NULL,
	cat_name VARCHAR(100) NOT NULL,
	cat_description STRING NULL,
	cat_is_default BOOL NOT NULL DEFAULT false,
	cat_budget_amount DECIMAL(14,2) NULL,
	cat_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	cat_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	cat_is_deleted BOOL NOT NULL DEFAULT false,
	cat_deleted_at TIMESTAMPTZ NULL,
	cat_deleted_by_user_id UUID NULL,
	CONSTRAINT categories_pkey PRIMARY KEY (cat_id ASC),
	CONSTRAINT categories_project_id_fkey FOREIGN KEY (cat_project_id) REFERENCES public.projects(prj_id),
	CONSTRAINT categories_deleted_by_user_id_fkey FOREIGN KEY (cat_deleted_by_user_id) REFERENCES public.users(usr_id),
	UNIQUE INDEX idx_cat_project_name_active (cat_project_id ASC, cat_name ASC) WHERE cat_is_deleted = false,
	INDEX idx_cat_project_id (cat_project_id ASC),
	INDEX idx_cat_is_deleted (cat_is_deleted ASC),
	CONSTRAINT categories_budget_positive CHECK ((cat_budget_amount IS NULL) OR (cat_budget_amount > 0:::DECIMAL))
);


-- public.incomes definition

-- Drop table

-- DROP TABLE incomes;

CREATE TABLE public.incomes (
	inc_id UUID NOT NULL DEFAULT gen_random_uuid(),
	inc_project_id UUID NOT NULL,
	inc_category_id UUID NOT NULL,
	inc_payment_method_id UUID NOT NULL,
	inc_created_by_user_id UUID NOT NULL,
	inc_original_amount DECIMAL(14,2) NOT NULL,
	inc_original_currency VARCHAR(3) NOT NULL,
	inc_exchange_rate DECIMAL(18,6) NOT NULL DEFAULT 1.000000:::DECIMAL,
	inc_converted_amount DECIMAL(14,2) NOT NULL,
	inc_title VARCHAR(255) NOT NULL,
	inc_description STRING NULL,
	inc_income_date DATE NOT NULL,
	inc_receipt_number VARCHAR(100) NULL,
	inc_notes STRING NULL,
	inc_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	inc_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	inc_is_deleted BOOL NOT NULL DEFAULT false,
	inc_deleted_at TIMESTAMPTZ NULL,
	inc_deleted_by_user_id UUID NULL,
	inc_account_amount DECIMAL(14,2) NULL,
	inc_account_currency VARCHAR(3) NULL,
	inc_is_active BOOL NOT NULL DEFAULT true,
	CONSTRAINT incomes_pkey PRIMARY KEY (inc_id ASC),
	CONSTRAINT incomes_inc_project_id_fkey FOREIGN KEY (inc_project_id) REFERENCES public.projects(prj_id),
	CONSTRAINT incomes_inc_category_id_fkey FOREIGN KEY (inc_category_id) REFERENCES public.categories(cat_id),
	CONSTRAINT incomes_inc_payment_method_id_fkey FOREIGN KEY (inc_payment_method_id) REFERENCES public.payment_methods(pmt_id),
	CONSTRAINT incomes_inc_created_by_user_id_fkey FOREIGN KEY (inc_created_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT incomes_inc_original_currency_fkey FOREIGN KEY (inc_original_currency) REFERENCES public.currencies(cur_code),
	CONSTRAINT incomes_inc_deleted_by_user_id_fkey FOREIGN KEY (inc_deleted_by_user_id) REFERENCES public.users(usr_id),
	INDEX idx_inc_project_id (inc_project_id ASC),
	INDEX idx_inc_category_id (inc_category_id ASC),
	INDEX idx_inc_payment_method_id (inc_payment_method_id ASC),
	INDEX idx_inc_created_by_user_id (inc_created_by_user_id ASC),
	INDEX idx_inc_income_date (inc_income_date ASC),
	INDEX idx_inc_is_deleted (inc_is_deleted ASC),
	INDEX idx_incomes_inc_is_active (inc_is_active ASC)
);
COMMENT ON TABLE public.incomes IS 'Ingresos financieros registrados por proyecto.';
COMMENT ON TABLE public.incomes IS 'Ingresos financieros registrados por proyecto.';


-- public.obligations definition

-- Drop table

-- DROP TABLE obligations;

CREATE TABLE public.obligations (
	obl_id UUID NOT NULL DEFAULT gen_random_uuid(),
	obl_project_id UUID NOT NULL,
	obl_created_by_user_id UUID NOT NULL,
	obl_title VARCHAR(255) NOT NULL,
	obl_description STRING NULL,
	obl_total_amount DECIMAL(14,2) NOT NULL,
	obl_currency VARCHAR(3) NOT NULL,
	obl_due_date DATE NULL,
	obl_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	obl_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	obl_is_deleted BOOL NOT NULL DEFAULT false,
	obl_deleted_at TIMESTAMPTZ NULL,
	obl_deleted_by_user_id UUID NULL,
	CONSTRAINT obligations_pkey PRIMARY KEY (obl_id ASC),
	CONSTRAINT obligations_project_id_fkey FOREIGN KEY (obl_project_id) REFERENCES public.projects(prj_id),
	CONSTRAINT obligations_created_by_user_id_fkey FOREIGN KEY (obl_created_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT obligations_deleted_by_user_id_fkey FOREIGN KEY (obl_deleted_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT obligations_currency_fkey FOREIGN KEY (obl_currency) REFERENCES public.currencies(cur_code),
	INDEX idx_obl_project_id (obl_project_id ASC),
	INDEX idx_obl_created_by_user_id (obl_created_by_user_id ASC),
	INDEX idx_obl_is_deleted (obl_is_deleted ASC),
	CONSTRAINT obligations_total_amount_positive CHECK (obl_total_amount > 0:::DECIMAL)
);


-- public.project_alternative_currencies definition

-- Drop table

-- DROP TABLE project_alternative_currencies;

CREATE TABLE public.project_alternative_currencies (
	pac_id UUID NOT NULL DEFAULT gen_random_uuid(),
	pac_project_id UUID NOT NULL,
	pac_currency_code VARCHAR(3) NOT NULL,
	pac_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT project_alternative_currencies_pkey PRIMARY KEY (pac_id ASC),
	CONSTRAINT project_alternative_currencies_pac_project_id_fkey FOREIGN KEY (pac_project_id) REFERENCES public.projects(prj_id),
	CONSTRAINT project_alternative_currencies_pac_currency_code_fkey FOREIGN KEY (pac_currency_code) REFERENCES public.currencies(cur_code),
	UNIQUE INDEX uq_pac_project_currency (pac_project_id ASC, pac_currency_code ASC),
	INDEX idx_pac_project_id (pac_project_id ASC)
);
COMMENT ON TABLE public.project_alternative_currencies IS e'Monedas alternativas habilitadas por proyecto para visualizaci\u00F3n multi-divisa.';
COMMENT ON TABLE public.project_alternative_currencies IS 'Monedas alternativas habilitadas por proyecto para visualización multi-divisa.';


-- public.project_budgets definition

-- Drop table

-- DROP TABLE project_budgets;

CREATE TABLE public.project_budgets (
	pjb_id UUID NOT NULL DEFAULT gen_random_uuid(),
	pjb_project_id UUID NOT NULL,
	pjb_total_budget DECIMAL(14,2) NOT NULL,
	pjb_alert_percentage DECIMAL(5,2) NOT NULL DEFAULT 80.00:::DECIMAL,
	pjb_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	pjb_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	pjb_is_deleted BOOL NOT NULL DEFAULT false,
	pjb_deleted_at TIMESTAMPTZ NULL,
	pjb_deleted_by_user_id UUID NULL,
	CONSTRAINT project_budgets_pkey PRIMARY KEY (pjb_id ASC),
	CONSTRAINT project_budgets_project_id_fkey FOREIGN KEY (pjb_project_id) REFERENCES public.projects(prj_id),
	CONSTRAINT project_budgets_deleted_by_user_id_fkey FOREIGN KEY (pjb_deleted_by_user_id) REFERENCES public.users(usr_id),
	UNIQUE INDEX idx_pjb_project_active (pjb_project_id ASC) WHERE pjb_is_deleted = false,
	INDEX idx_pjb_project_id (pjb_project_id ASC),
	INDEX idx_pjb_is_deleted (pjb_is_deleted ASC),
	CONSTRAINT project_budgets_total_positive CHECK (pjb_total_budget > 0:::DECIMAL),
	CONSTRAINT project_budgets_alert_range CHECK (pjb_alert_percentage BETWEEN 1.00:::DECIMAL AND 100.00:::DECIMAL)
);


-- public.project_members definition

-- Drop table

-- DROP TABLE project_members;

CREATE TABLE public.project_members (
	prm_id UUID NOT NULL DEFAULT gen_random_uuid(),
	prm_project_id UUID NOT NULL,
	prm_user_id UUID NOT NULL,
	prm_role VARCHAR(20) NOT NULL,
	prm_joined_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	prm_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	prm_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	prm_is_deleted BOOL NOT NULL DEFAULT false,
	prm_deleted_at TIMESTAMPTZ NULL,
	prm_deleted_by_user_id UUID NULL,
	CONSTRAINT project_members_pkey PRIMARY KEY (prm_id ASC),
	CONSTRAINT project_members_project_id_fkey FOREIGN KEY (prm_project_id) REFERENCES public.projects(prj_id),
	CONSTRAINT project_members_user_id_fkey FOREIGN KEY (prm_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT project_members_deleted_by_user_id_fkey FOREIGN KEY (prm_deleted_by_user_id) REFERENCES public.users(usr_id),
	UNIQUE INDEX idx_prm_project_user_active (prm_project_id ASC, prm_user_id ASC) WHERE prm_is_deleted = false,
	INDEX idx_prm_project_id (prm_project_id ASC),
	INDEX idx_prm_user_id (prm_user_id ASC),
	INDEX idx_prm_is_deleted (prm_is_deleted ASC),
	CONSTRAINT project_members_role_check CHECK (prm_role IN ('owner':::STRING, 'editor':::STRING, 'viewer':::STRING))
);


-- public.project_partners definition

-- Drop table

-- DROP TABLE project_partners;

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
	UNIQUE INDEX uq_ptp_project_partner_active (ptp_project_id ASC, ptp_partner_id ASC) WHERE ptp_is_deleted = false,
	INDEX idx_ptp_project_id (ptp_project_id ASC),
	INDEX idx_ptp_partner_id (ptp_partner_id ASC),
	INDEX idx_ptp_is_deleted (ptp_is_deleted ASC)
);
COMMENT ON TABLE public.project_partners IS e'Partners asignados a un proyecto. Los m\u00E9todos de pago disponibles se derivan de estos partners.';
COMMENT ON TABLE public.project_partners IS 'Partners asignados a un proyecto. Los métodos de pago disponibles se derivan de estos partners.';


-- public.project_payment_methods definition

-- Drop table

-- DROP TABLE project_payment_methods;

CREATE TABLE public.project_payment_methods (
	ppm_id UUID NOT NULL DEFAULT gen_random_uuid(),
	ppm_project_id UUID NOT NULL,
	ppm_payment_method_id UUID NOT NULL,
	ppm_added_by_user_id UUID NOT NULL,
	ppm_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT project_payment_methods_pkey PRIMARY KEY (ppm_id ASC),
	CONSTRAINT project_payment_methods_project_id_fkey FOREIGN KEY (ppm_project_id) REFERENCES public.projects(prj_id),
	CONSTRAINT project_payment_methods_payment_method_id_fkey FOREIGN KEY (ppm_payment_method_id) REFERENCES public.payment_methods(pmt_id),
	CONSTRAINT project_payment_methods_added_by_user_id_fkey FOREIGN KEY (ppm_added_by_user_id) REFERENCES public.users(usr_id),
	UNIQUE INDEX idx_ppm_project_payment_method (ppm_project_id ASC, ppm_payment_method_id ASC),
	INDEX idx_ppm_project_id (ppm_project_id ASC),
	INDEX idx_ppm_payment_method_id (ppm_payment_method_id ASC)
);


-- public.expenses definition

-- Drop table

-- DROP TABLE expenses;

CREATE TABLE public.expenses (
	exp_id UUID NOT NULL DEFAULT gen_random_uuid(),
	exp_project_id UUID NOT NULL,
	exp_category_id UUID NOT NULL,
	exp_payment_method_id UUID NOT NULL,
	exp_created_by_user_id UUID NOT NULL,
	exp_obligation_id UUID NULL,
	exp_original_amount DECIMAL(14,2) NOT NULL,
	exp_original_currency VARCHAR(3) NOT NULL,
	exp_exchange_rate DECIMAL(18,6) NOT NULL DEFAULT 1.000000:::DECIMAL,
	exp_converted_amount DECIMAL(14,2) NOT NULL,
	exp_title VARCHAR(255) NOT NULL,
	exp_description STRING NULL,
	exp_expense_date DATE NOT NULL,
	exp_receipt_number VARCHAR(100) NULL,
	exp_notes STRING NULL,
	exp_is_template BOOL NOT NULL DEFAULT false,
	exp_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	exp_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	exp_is_deleted BOOL NOT NULL DEFAULT false,
	exp_deleted_at TIMESTAMPTZ NULL,
	exp_deleted_by_user_id UUID NULL,
	exp_obligation_equivalent_amount DECIMAL(14,2) NULL,
	exp_is_active BOOL NOT NULL DEFAULT true,
	exp_account_amount DECIMAL(14,2) NULL,
	exp_account_currency VARCHAR(3) NULL,
	CONSTRAINT expenses_pkey PRIMARY KEY (exp_id ASC),
	CONSTRAINT expenses_project_id_fkey FOREIGN KEY (exp_project_id) REFERENCES public.projects(prj_id),
	CONSTRAINT expenses_category_id_fkey FOREIGN KEY (exp_category_id) REFERENCES public.categories(cat_id),
	CONSTRAINT expenses_payment_method_id_fkey FOREIGN KEY (exp_payment_method_id) REFERENCES public.payment_methods(pmt_id),
	CONSTRAINT expenses_created_by_user_id_fkey FOREIGN KEY (exp_created_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT expenses_deleted_by_user_id_fkey FOREIGN KEY (exp_deleted_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT expenses_original_currency_fkey FOREIGN KEY (exp_original_currency) REFERENCES public.currencies(cur_code),
	CONSTRAINT expenses_obligation_id_fkey FOREIGN KEY (exp_obligation_id) REFERENCES public.obligations(obl_id),
	INDEX idx_exp_project_id (exp_project_id ASC),
	INDEX idx_exp_category_id (exp_category_id ASC),
	INDEX idx_exp_payment_method_id (exp_payment_method_id ASC),
	INDEX idx_exp_created_by_user_id (exp_created_by_user_id ASC),
	INDEX idx_exp_expense_date (exp_expense_date ASC),
	INDEX idx_exp_obligation_id (exp_obligation_id ASC),
	INDEX idx_exp_is_deleted (exp_is_deleted ASC),
	INDEX idx_exp_is_template (exp_is_template ASC),
	INDEX idx_expenses_exp_is_active (exp_is_active ASC),
	CONSTRAINT expenses_original_amount_positive CHECK (exp_original_amount > 0:::DECIMAL),
	CONSTRAINT expenses_converted_amount_positive CHECK (exp_converted_amount > 0:::DECIMAL),
	CONSTRAINT expenses_exchange_rate_positive CHECK (exp_exchange_rate > 0:::DECIMAL)
);
COMMENT ON COLUMN public.expenses.exp_obligation_equivalent_amount IS 'Amount paid expressed in obligation currency for cross-currency obligation payments.';
COMMENT ON COLUMN public.expenses.exp_account_amount IS e'Monto convertido a la moneda del m\u00E9todo de pago (account currency).';
COMMENT ON COLUMN public.expenses.exp_account_currency IS e'Moneda del m\u00E9todo de pago al momento de registrar el gasto.';

-- Column comments

COMMENT ON COLUMN public.expenses.exp_obligation_equivalent_amount IS 'Amount paid expressed in obligation currency for cross-currency obligation payments.';
COMMENT ON COLUMN public.expenses.exp_account_amount IS 'Monto convertido a la moneda del método de pago (account currency).';
COMMENT ON COLUMN public.expenses.exp_account_currency IS 'Moneda del método de pago al momento de registrar el gasto.';


-- public.income_splits definition

-- Drop table

-- DROP TABLE income_splits;

CREATE TABLE public.income_splits (
	ins_id UUID NOT NULL DEFAULT gen_random_uuid(),
	ins_income_id UUID NOT NULL,
	ins_partner_id UUID NOT NULL,
	ins_split_type VARCHAR(10) NOT NULL,
	ins_split_value DECIMAL(14,4) NOT NULL,
	ins_resolved_amount DECIMAL(14,2) NOT NULL,
	ins_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	ins_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT income_splits_pkey PRIMARY KEY (ins_id ASC),
	CONSTRAINT ins_income_fkey FOREIGN KEY (ins_income_id) REFERENCES public.incomes(inc_id) ON DELETE CASCADE,
	CONSTRAINT ins_partner_fkey FOREIGN KEY (ins_partner_id) REFERENCES public.partners(ptr_id),
	UNIQUE INDEX uq_ins_income_partner (ins_income_id ASC, ins_partner_id ASC),
	INDEX idx_ins_income_id (ins_income_id ASC),
	INDEX idx_ins_partner_id (ins_partner_id ASC),
	CONSTRAINT ins_split_type_check CHECK (ins_split_type IN ('percentage':::STRING, 'fixed':::STRING)),
	CONSTRAINT ins_split_value_positive CHECK (ins_split_value > 0:::DECIMAL),
	CONSTRAINT ins_resolved_amount_positive CHECK (ins_resolved_amount > 0:::DECIMAL)
);
COMMENT ON TABLE public.income_splits IS e'Divisi\u00F3n de un ingreso entre partners del proyecto.';
COMMENT ON TABLE public.income_splits IS 'División de un ingreso entre partners del proyecto.';


-- public.transaction_currency_exchanges definition

-- Drop table

-- DROP TABLE transaction_currency_exchanges;

CREATE TABLE public.transaction_currency_exchanges (
	tce_id UUID NOT NULL DEFAULT gen_random_uuid(),
	tce_expense_id UUID NULL,
	tce_income_id UUID NULL,
	tce_currency_code VARCHAR(3) NOT NULL,
	tce_exchange_rate DECIMAL(18,6) NOT NULL,
	tce_converted_amount DECIMAL(14,2) NOT NULL,
	tce_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT transaction_currency_exchanges_pkey PRIMARY KEY (tce_id ASC),
	CONSTRAINT transaction_currency_exchanges_tce_expense_id_fkey FOREIGN KEY (tce_expense_id) REFERENCES public.expenses(exp_id) ON DELETE CASCADE,
	CONSTRAINT transaction_currency_exchanges_tce_income_id_fkey FOREIGN KEY (tce_income_id) REFERENCES public.incomes(inc_id) ON DELETE CASCADE,
	CONSTRAINT transaction_currency_exchanges_tce_currency_code_fkey FOREIGN KEY (tce_currency_code) REFERENCES public.currencies(cur_code),
	UNIQUE INDEX uq_tce_expense_currency (tce_expense_id ASC, tce_currency_code ASC),
	UNIQUE INDEX uq_tce_income_currency (tce_income_id ASC, tce_currency_code ASC),
	INDEX idx_tce_expense_id (tce_expense_id ASC) WHERE tce_expense_id IS NOT NULL,
	INDEX idx_tce_income_id (tce_income_id ASC) WHERE tce_income_id IS NOT NULL,
	CONSTRAINT chk_tce_one_parent CHECK (((tce_expense_id IS NOT NULL)::INT8 + (tce_income_id IS NOT NULL)::INT8) = 1:::INT8)
);
COMMENT ON TABLE public.transaction_currency_exchanges IS 'Conversiones de gastos/ingresos a monedas alternativas del proyecto.';
COMMENT ON TABLE public.transaction_currency_exchanges IS 'Conversiones de gastos/ingresos a monedas alternativas del proyecto.';


-- public.expense_splits definition

-- Drop table

-- DROP TABLE expense_splits;

CREATE TABLE public.expense_splits (
	exs_id UUID NOT NULL DEFAULT gen_random_uuid(),
	exs_expense_id UUID NOT NULL,
	exs_partner_id UUID NOT NULL,
	exs_split_type VARCHAR(10) NOT NULL,
	exs_split_value DECIMAL(14,4) NOT NULL,
	exs_resolved_amount DECIMAL(14,2) NOT NULL,
	exs_created_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	exs_updated_at TIMESTAMPTZ NOT NULL DEFAULT now():::TIMESTAMPTZ,
	CONSTRAINT expense_splits_pkey PRIMARY KEY (exs_id ASC),
	CONSTRAINT exs_expense_fkey FOREIGN KEY (exs_expense_id) REFERENCES public.expenses(exp_id) ON DELETE CASCADE,
	CONSTRAINT exs_partner_fkey FOREIGN KEY (exs_partner_id) REFERENCES public.partners(ptr_id),
	UNIQUE INDEX uq_exs_expense_partner (exs_expense_id ASC, exs_partner_id ASC),
	INDEX idx_exs_expense_id (exs_expense_id ASC),
	INDEX idx_exs_partner_id (exs_partner_id ASC),
	CONSTRAINT exs_split_type_check CHECK (exs_split_type IN ('percentage':::STRING, 'fixed':::STRING)),
	CONSTRAINT exs_split_value_positive CHECK (exs_split_value > 0:::DECIMAL),
	CONSTRAINT exs_resolved_amount_positive CHECK (exs_resolved_amount > 0:::DECIMAL)
);
COMMENT ON TABLE public.expense_splits IS e'Divisi\u00F3n del costo de un gasto entre partners del proyecto.';
COMMENT ON TABLE public.expense_splits IS 'División del costo de un gasto entre partners del proyecto.';