-- Fase 3c: Liquidaciones directas entre partners
-- Pagos directos entre partners para saldar deudas. No afectan métodos de pago del proyecto.

CREATE TABLE public.partner_settlements (
  pst_id UUID NOT NULL DEFAULT gen_random_uuid(),
  pst_project_id UUID NOT NULL,
  pst_from_partner_id UUID NOT NULL,
  pst_to_partner_id UUID NOT NULL,
  pst_amount DECIMAL(14,2) NOT NULL,
  pst_currency VARCHAR(3) NOT NULL,
  pst_exchange_rate DECIMAL(18,6) NOT NULL DEFAULT 1.000000,
  pst_converted_amount DECIMAL(14,2) NOT NULL,
  pst_settlement_date DATE NOT NULL,
  pst_description VARCHAR(500) NULL,
  pst_notes TEXT NULL,
  pst_created_by_user_id UUID NOT NULL,
  pst_created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  pst_updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  pst_is_deleted BOOL NOT NULL DEFAULT false,
  pst_deleted_at TIMESTAMPTZ NULL,
  pst_deleted_by_user_id UUID NULL,
  CONSTRAINT partner_settlements_pkey PRIMARY KEY (pst_id ASC),
  CONSTRAINT pst_project_fkey FOREIGN KEY (pst_project_id) REFERENCES public.projects(prj_id),
  CONSTRAINT pst_from_partner_fkey FOREIGN KEY (pst_from_partner_id) REFERENCES public.partners(ptr_id),
  CONSTRAINT pst_to_partner_fkey FOREIGN KEY (pst_to_partner_id) REFERENCES public.partners(ptr_id),
  CONSTRAINT pst_currency_fkey FOREIGN KEY (pst_currency) REFERENCES public.currencies(cur_code),
  CONSTRAINT pst_created_by_fkey FOREIGN KEY (pst_created_by_user_id) REFERENCES public.users(usr_id),
  CONSTRAINT pst_deleted_by_fkey FOREIGN KEY (pst_deleted_by_user_id) REFERENCES public.users(usr_id),
  CONSTRAINT pst_different_partners CHECK (pst_from_partner_id != pst_to_partner_id),
  CONSTRAINT pst_amount_positive CHECK (pst_amount > 0)
);

CREATE INDEX idx_pst_project_id ON public.partner_settlements (pst_project_id ASC);
CREATE INDEX idx_pst_from_partner_id ON public.partner_settlements (pst_from_partner_id ASC);
CREATE INDEX idx_pst_to_partner_id ON public.partner_settlements (pst_to_partner_id ASC);
CREATE INDEX idx_pst_date ON public.partner_settlements (pst_settlement_date ASC);
CREATE INDEX idx_pst_is_deleted ON public.partner_settlements (pst_is_deleted ASC);

COMMENT ON TABLE public.partner_settlements IS
  'Pagos directos entre partners para saldar deudas. No afectan métodos de pago del proyecto.';
