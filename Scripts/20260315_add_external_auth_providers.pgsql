-- Migration: 20260315_add_external_auth_providers
-- Creates the external_auth_providers table for Google OAuth linking.
-- Safe to run multiple times (IF NOT EXISTS).

CREATE TABLE IF NOT EXISTS public.external_auth_providers (
	eap_id UUID NOT NULL DEFAULT gen_random_uuid(),
	eap_user_id UUID NOT NULL,
	eap_provider VARCHAR(50) NOT NULL,
	eap_provider_user_id VARCHAR(255) NOT NULL,
	eap_provider_email VARCHAR(255) NULL,
	eap_access_token_hash STRING NULL,
	eap_refresh_token_hash STRING NULL,
	eap_token_expires_at TIMESTAMPTZ NULL,
	eap_metadata JSONB NULL,
	eap_created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
	eap_updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
	eap_is_deleted BOOL NOT NULL DEFAULT false,
	eap_deleted_at TIMESTAMPTZ NULL,
	eap_deleted_by_user_id UUID NULL,
	CONSTRAINT external_auth_providers_pkey PRIMARY KEY (eap_id ASC),
	CONSTRAINT external_auth_providers_user_id_fkey FOREIGN KEY (eap_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT external_auth_providers_deleted_by_fkey FOREIGN KEY (eap_deleted_by_user_id) REFERENCES public.users(usr_id),
	CONSTRAINT external_auth_providers_provider_check CHECK (eap_provider IN ('google', 'microsoft', 'github', 'facebook', 'apple'))
);

CREATE UNIQUE INDEX IF NOT EXISTS external_auth_providers_provider_user_uq
    ON public.external_auth_providers (eap_provider ASC, eap_provider_user_id ASC);

CREATE INDEX IF NOT EXISTS idx_eap_user_id
    ON public.external_auth_providers (eap_user_id ASC);

CREATE INDEX IF NOT EXISTS idx_eap_is_deleted
    ON public.external_auth_providers (eap_is_deleted ASC);
