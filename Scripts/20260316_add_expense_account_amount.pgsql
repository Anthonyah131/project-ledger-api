-- Migración: agregar campos account_amount y account_currency a expenses
-- Fecha: 2026-03-16
-- Motivo: Los gastos necesitan registrar el monto en la moneda del método de pago,
--         igual que los ingresos (inc_account_amount / inc_account_currency), para
--         mostrar totales correctos en el resumen del método de pago.

ALTER TABLE public.expenses
    ADD COLUMN IF NOT EXISTS exp_account_amount DECIMAL(14,2) NULL,
    ADD COLUMN IF NOT EXISTS exp_account_currency VARCHAR(3) NULL;

COMMENT ON COLUMN public.expenses.exp_account_amount IS 'Monto convertido a la moneda del método de pago (account currency).';
COMMENT ON COLUMN public.expenses.exp_account_currency IS 'Moneda del método de pago al momento de registrar el gasto.';
