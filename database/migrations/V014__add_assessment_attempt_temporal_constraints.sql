-- ==============================================================================
-- Yusay Platform — Migración V014: Coherencia Temporal en Intentos de Evaluación
-- ==============================================================================
-- Valida la coherencia estricta entre estados y marcas temporales en assessment_attempt:
-- - IN_PROGRESS: ended_at debe ser obligatoriamente NULL.
-- - Estados terminales (SUBMITTED, EXPIRED, CANCELLED): ended_at debe ser NOT NULL
--   y posterior o igual al inicio (ended_at >= started_at).
-- ==============================================================================

ALTER TABLE yusay.assessment_attempt
    ADD CONSTRAINT ck_assessment_attempt_ended_at_consistency CHECK (
        (status = 'IN_PROGRESS' AND ended_at IS NULL)
        OR
        (status IN ('SUBMITTED', 'EXPIRED', 'CANCELLED') AND ended_at IS NOT NULL AND ended_at >= started_at)
    );
