-- ============================================================
-- Migration: OCR monthly plan limits by subscription tier
-- Date: 2026-03-08
-- ============================================================
-- Goal:
--  - free    : OCR disabled + monthly limit 0
--  - basic   : OCR enabled  + monthly limit 10
--  - premium : OCR enabled  + monthly limit unlimited (null)
--
-- Notes:
--  - Uses pln_slug to target plans.
--  - Keeps existing pln_limits keys and only upserts the OCR limit key.
-- ============================================================

UPDATE public.plans
SET
    pln_can_use_ocr = CASE pln_slug
        WHEN 'free' THEN false
        WHEN 'basic' THEN true
        WHEN 'premium' THEN true
        ELSE pln_can_use_ocr
    END,
    pln_limits = CASE pln_slug
        WHEN 'free' THEN
            jsonb_set(
                COALESCE(pln_limits, '{}'::jsonb),
                '{max_document_reads_per_month}',
                '0'::jsonb,
                true)
        WHEN 'basic' THEN
            jsonb_set(
                COALESCE(pln_limits, '{}'::jsonb),
                '{max_document_reads_per_month}',
                '10'::jsonb,
                true)
        WHEN 'premium' THEN
            jsonb_set(
                COALESCE(pln_limits, '{}'::jsonb),
                '{max_document_reads_per_month}',
                'null'::jsonb,
                true)
        ELSE pln_limits
    END,
    pln_updated_at = NOW()
WHERE pln_slug IN ('free', 'basic', 'premium');
