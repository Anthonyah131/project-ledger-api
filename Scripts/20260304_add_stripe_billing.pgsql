-- ============================================================
-- Stripe billing support (plans + payment links + subscriptions)
-- Date: 2026-03-04
-- Target: PostgreSQL / CockroachDB
-- ============================================================

ALTER TABLE public.plans
    ADD COLUMN IF NOT EXISTS pln_monthly_price numeric(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS pln_currency varchar(3) NOT NULL DEFAULT 'usd',
    ADD COLUMN IF NOT EXISTS pln_stripe_product_id varchar(255),
    ADD COLUMN IF NOT EXISTS pln_stripe_price_id varchar(255),
    ADD COLUMN IF NOT EXISTS pln_stripe_payment_link_id varchar(255),
    ADD COLUMN IF NOT EXISTS pln_stripe_payment_link_url text;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS usr_stripe_customer_id varchar(255);

CREATE TABLE IF NOT EXISTS public.user_subscriptions (
    uss_id                      uuid            NOT NULL DEFAULT gen_random_uuid(),
    uss_user_id                 uuid,
    uss_plan_id                 uuid,
    uss_stripe_subscription_id  varchar(255)    NOT NULL,
    uss_stripe_customer_id      varchar(255),
    uss_stripe_price_id         varchar(255),
    uss_status                  varchar(50)     NOT NULL,
    uss_current_period_start    timestamptz,
    uss_current_period_end      timestamptz,
    uss_cancel_at_period_end    boolean         NOT NULL DEFAULT false,
    uss_canceled_at             timestamptz,
    uss_created_at              timestamptz     NOT NULL DEFAULT now(),
    uss_updated_at              timestamptz     NOT NULL DEFAULT now(),
    CONSTRAINT user_subscriptions_pkey PRIMARY KEY (uss_id),
    CONSTRAINT user_subscriptions_user_fkey FOREIGN KEY (uss_user_id) REFERENCES public.users (usr_id),
    CONSTRAINT user_subscriptions_plan_fkey FOREIGN KEY (uss_plan_id) REFERENCES public.plans (pln_id)
);

CREATE TABLE IF NOT EXISTS public.stripe_webhook_events (
    swe_id                      uuid            NOT NULL DEFAULT gen_random_uuid(),
    swe_stripe_event_id         varchar(255)    NOT NULL,
    swe_type                    varchar(100)    NOT NULL,
    swe_processed_successfully  boolean         NOT NULL DEFAULT false,
    swe_error_message           text,
    swe_created_at              timestamptz     NOT NULL DEFAULT now(),
    swe_processed_at            timestamptz,
    CONSTRAINT stripe_webhook_events_pkey PRIMARY KEY (swe_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS plans_stripe_product_id_uq
    ON public.plans (pln_stripe_product_id);

CREATE UNIQUE INDEX IF NOT EXISTS plans_stripe_price_id_uq
    ON public.plans (pln_stripe_price_id);

CREATE UNIQUE INDEX IF NOT EXISTS plans_stripe_payment_link_id_uq
    ON public.plans (pln_stripe_payment_link_id);

CREATE UNIQUE INDEX IF NOT EXISTS users_stripe_customer_id_uq
    ON public.users (usr_stripe_customer_id);

CREATE UNIQUE INDEX IF NOT EXISTS user_subscriptions_stripe_subscription_id_uq
    ON public.user_subscriptions (uss_stripe_subscription_id);

CREATE INDEX IF NOT EXISTS user_subscriptions_user_id_idx
    ON public.user_subscriptions (uss_user_id);

CREATE INDEX IF NOT EXISTS user_subscriptions_customer_id_idx
    ON public.user_subscriptions (uss_stripe_customer_id);

CREATE UNIQUE INDEX IF NOT EXISTS stripe_webhook_events_event_id_uq
    ON public.stripe_webhook_events (swe_stripe_event_id);

CREATE INDEX IF NOT EXISTS stripe_webhook_events_created_at_idx
    ON public.stripe_webhook_events (swe_created_at);
