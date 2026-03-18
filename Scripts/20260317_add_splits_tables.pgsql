-- Fase 3a: Tablas de splits para gastos e ingresos
-- División del costo/ingreso entre partners del proyecto

CREATE TABLE public.expense_splits (
  exs_id UUID NOT NULL DEFAULT gen_random_uuid(),
  exs_expense_id UUID NOT NULL,
  exs_partner_id UUID NOT NULL,
  exs_split_type VARCHAR(10) NOT NULL,
  exs_split_value DECIMAL(14,4) NOT NULL,
  exs_resolved_amount DECIMAL(14,2) NOT NULL,
  exs_created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  exs_updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CONSTRAINT expense_splits_pkey PRIMARY KEY (exs_id),
  CONSTRAINT exs_expense_fkey FOREIGN KEY (exs_expense_id)
    REFERENCES public.expenses(exp_id) ON DELETE CASCADE,
  CONSTRAINT exs_partner_fkey FOREIGN KEY (exs_partner_id)
    REFERENCES public.partners(ptr_id),
  CONSTRAINT exs_split_type_check CHECK (exs_split_type IN ('percentage', 'fixed')),
  CONSTRAINT exs_split_value_positive CHECK (exs_split_value > 0),
  CONSTRAINT exs_resolved_amount_positive CHECK (exs_resolved_amount > 0)
);

CREATE UNIQUE INDEX uq_exs_expense_partner ON public.expense_splits (exs_expense_id, exs_partner_id);
CREATE INDEX idx_exs_expense_id ON public.expense_splits (exs_expense_id);
CREATE INDEX idx_exs_partner_id ON public.expense_splits (exs_partner_id);

COMMENT ON TABLE public.expense_splits IS 'División del costo de un gasto entre partners del proyecto.';

-- ──────────────────────────────────────────────────────────────

CREATE TABLE public.income_splits (
  ins_id UUID NOT NULL DEFAULT gen_random_uuid(),
  ins_income_id UUID NOT NULL,
  ins_partner_id UUID NOT NULL,
  ins_split_type VARCHAR(10) NOT NULL,
  ins_split_value DECIMAL(14,4) NOT NULL,
  ins_resolved_amount DECIMAL(14,2) NOT NULL,
  ins_created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  ins_updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CONSTRAINT income_splits_pkey PRIMARY KEY (ins_id),
  CONSTRAINT ins_income_fkey FOREIGN KEY (ins_income_id)
    REFERENCES public.incomes(inc_id) ON DELETE CASCADE,
  CONSTRAINT ins_partner_fkey FOREIGN KEY (ins_partner_id)
    REFERENCES public.partners(ptr_id),
  CONSTRAINT ins_split_type_check CHECK (ins_split_type IN ('percentage', 'fixed')),
  CONSTRAINT ins_split_value_positive CHECK (ins_split_value > 0),
  CONSTRAINT ins_resolved_amount_positive CHECK (ins_resolved_amount > 0)
);

CREATE UNIQUE INDEX uq_ins_income_partner ON public.income_splits (ins_income_id, ins_partner_id);
CREATE INDEX idx_ins_income_id ON public.income_splits (ins_income_id);
CREATE INDEX idx_ins_partner_id ON public.income_splits (ins_partner_id);

COMMENT ON TABLE public.income_splits IS 'División de un ingreso entre partners del proyecto.';
