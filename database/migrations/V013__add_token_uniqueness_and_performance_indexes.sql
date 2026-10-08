-- ==============================================================================
-- Yusay Platform — Migración V013: Unicidad de Tokens e Índices de Rendimiento
-- ==============================================================================
-- 1. Unicidad estricta de hashes de tokens en verificación y recuperación.
-- 2. Índices compuestos para consultas de alta frecuencia:
--    - check_in (user_id, recorded_at DESC)
--    - assessment_attempt (user_id, started_at DESC)
--    - audit_event (occurred_at DESC) para purgas y líneas de tiempo.
-- ==============================================================================

-- 1. Unicidad de tokens criptográficos
ALTER TABLE yusay.email_verification_token
    ADD CONSTRAINT uq_email_verification_token_hash UNIQUE (token_hash);

ALTER TABLE yusay.password_reset_token
    ADD CONSTRAINT uq_password_reset_token_hash UNIQUE (token_hash);

-- 2. Índices compuestos de rendimiento
CREATE INDEX idx_check_in_user_recorded
    ON yusay.check_in (user_id, recorded_at DESC);

CREATE INDEX idx_assessment_attempt_user_started
    ON yusay.assessment_attempt (user_id, started_at DESC);

CREATE INDEX idx_audit_event_occurred_at
    ON yusay.audit_event (occurred_at DESC);
