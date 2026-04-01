-- ============================================================
-- Migration: Add pln_can_use_partners to plans table
-- Date: 2026-04-01
-- ============================================================
-- Adds a boolean permission flag that controls whether a plan
-- allows enabling the partners/splits feature on projects.
-- Default: false (Free plan). Set true for Basic and Premium.
--
-- NOTE: DDL and DML must run in separate transactions in
-- CockroachDB. Run each block independently.
-- ============================================================

-- Step 1: Add the column (run alone)
ALTER TABLE public.plans
    ADD COLUMN IF NOT EXISTS pln_can_use_partners BOOLEAN NOT NULL DEFAULT false;

-- Step 2: Apply correct values per plan slug (run after Step 1 commits)
BEGIN;
UPDATE public.plans SET pln_can_use_partners = false WHERE pln_slug = 'free';
UPDATE public.plans SET pln_can_use_partners = true  WHERE pln_slug = 'basic';
UPDATE public.plans SET pln_can_use_partners = true  WHERE pln_slug = 'premium';
COMMIT;
