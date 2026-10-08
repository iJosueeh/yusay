-- ==============================================================================
-- Yusay Platform — Migración V008: Ejecución de Evaluaciones Psicométricas
-- ==============================================================================
-- Entidades:
--   1. yusay.assessment_attempt (R-013 / ASSESSMENT_ATTEMPT)
--   2. yusay.answer (R-014 / ANSWER)
--   3. yusay.assessment_result (R-016 / ASSESSMENT_RESULT)
--
-- Restricciones e integridad según el diseño físico v1.0 aprobado:
--   - PKs:
--       * pk_assessment_attempt (attempt_id uuid IDF-08)
--       * pk_answer (attempt_id, question_id)
--       * pk_assessment_result (attempt_id uuid — PK compartida 1:1 con assessment_attempt)
--   - URAs:
--       * uq_assessment_attempt_ref_attempt_id_instrument_version_id (URA-004)
--         Clave compuesta requerida por FK-015 (answer) y FK-020 (assessment_result)
--   - FKs simples y compuestas:
--       * fk_assessment_attempt_app_user -> app_user(user_id) (FK-013, ON DELETE CASCADE)
--       * fk_assessment_attempt_instrument_version -> instrument_version(instrument_id, instrument_version_id) (FK-014, ON DELETE RESTRICT)
--       * fk_answer_attempt_version -> assessment_attempt(attempt_id, instrument_version_id) (FK-015, ON DELETE CASCADE)
--       * fk_answer_question_version -> question(instrument_version_id, question_id) (FK-016, ON DELETE RESTRICT)
--       * fk_answer_selected_option -> answer_option(question_id, option_id) (FK-017, ON DELETE RESTRICT)
--       * fk_assessment_result_attempt -> assessment_attempt(attempt_id) (FK-019, ON DELETE CASCADE)
--       * fk_assessment_result_attempt_version -> assessment_attempt(attempt_id, instrument_version_id) (FK-020, ON DELETE CASCADE)
--       * fk_assessment_result_interpretation_version -> interpretation(instrument_version_id, interpretation_id) (FK-021, ON DELETE RESTRICT)
--   - CHECKs normativos:
--       * ck_assessment_attempt_status (IN_PROGRESS, SUBMITTED, EXPIRED, CANCELLED)
--       * ck_assessment_attempt_expiry (started_at < expires_at)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.assessment_attempt (R-013)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.assessment_attempt (
    attempt_id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    instrument_id uuid NOT NULL,
    instrument_version_id uuid NOT NULL,
    status text NOT NULL DEFAULT 'IN_PROGRESS',
    started_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    ended_at timestamptz NULL,
    CONSTRAINT pk_assessment_attempt PRIMARY KEY (attempt_id),
    CONSTRAINT fk_assessment_attempt_app_user FOREIGN KEY (user_id)
        REFERENCES yusay.app_user (user_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_assessment_attempt_instrument_version FOREIGN KEY (instrument_id, instrument_version_id)
        REFERENCES yusay.instrument_version (instrument_id, instrument_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT uq_assessment_attempt_ref_attempt_id_instrument_version_id UNIQUE (attempt_id, instrument_version_id),
    CONSTRAINT ck_assessment_attempt_status CHECK (status IN ('IN_PROGRESS', 'SUBMITTED', 'EXPIRED', 'CANCELLED')),
    CONSTRAINT ck_assessment_attempt_expiry CHECK (started_at < expires_at)
);

COMMENT ON TABLE yusay.assessment_attempt IS 'Ejecución personal de un instrumento ligada a su versión exacta desde el inicio.';
COMMENT ON COLUMN yusay.assessment_attempt.attempt_id IS 'Identificador único del intento (UUID v4 raíz IDF-08).';
COMMENT ON COLUMN yusay.assessment_attempt.user_id IS 'Usuario propietario del intento.';
COMMENT ON COLUMN yusay.assessment_attempt.instrument_id IS 'Instrumento psicométrico ejecutado.';
COMMENT ON COLUMN yusay.assessment_attempt.instrument_version_id IS 'Versión histórica exacta del instrumento.';
COMMENT ON COLUMN yusay.assessment_attempt.status IS 'Estado operativo del intento: IN_PROGRESS, SUBMITTED, EXPIRED o CANCELLED.';
COMMENT ON COLUMN yusay.assessment_attempt.started_at IS 'Instante UTC de inicio del intento.';
COMMENT ON COLUMN yusay.assessment_attempt.expires_at IS 'Instante UTC límite de vigencia para envío (started_at + 720h).';
COMMENT ON COLUMN yusay.assessment_attempt.ended_at IS 'Instante UTC de terminación efectiva (envío, expiración o cancelación).';

-- ------------------------------------------------------------------------------
-- 2. Tabla: yusay.answer (R-014)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.answer (
    attempt_id uuid NOT NULL,
    instrument_version_id uuid NOT NULL,
    question_id uuid NOT NULL,
    option_id uuid NOT NULL,
    CONSTRAINT pk_answer PRIMARY KEY (attempt_id, question_id),
    CONSTRAINT fk_answer_attempt_version FOREIGN KEY (attempt_id, instrument_version_id)
        REFERENCES yusay.assessment_attempt (attempt_id, instrument_version_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_answer_question_version FOREIGN KEY (instrument_version_id, question_id)
        REFERENCES yusay.question (instrument_version_id, question_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_answer_selected_option FOREIGN KEY (question_id, option_id)
        REFERENCES yusay.answer_option (question_id, option_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.answer IS 'Selección de una opción por pregunta dentro de un intento psicométrico.';
COMMENT ON COLUMN yusay.answer.attempt_id IS 'Intento al que pertenece la respuesta.';
COMMENT ON COLUMN yusay.answer.instrument_version_id IS 'Versión histórica del instrumento (coherencia garantizada por FK compuesta).';
COMMENT ON COLUMN yusay.answer.question_id IS 'Pregunta respondida.';
COMMENT ON COLUMN yusay.answer.option_id IS 'Opción seleccionada por el usuario.';

-- ------------------------------------------------------------------------------
-- 3. Tabla: yusay.assessment_result (R-016)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.assessment_result (
    attempt_id uuid NOT NULL,
    instrument_version_id uuid NOT NULL,
    score integer NOT NULL,
    interpretation_id uuid NOT NULL,
    calculated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT pk_assessment_result PRIMARY KEY (attempt_id),
    CONSTRAINT fk_assessment_result_attempt FOREIGN KEY (attempt_id)
        REFERENCES yusay.assessment_attempt (attempt_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_assessment_result_attempt_version FOREIGN KEY (attempt_id, instrument_version_id)
        REFERENCES yusay.assessment_attempt (attempt_id, instrument_version_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_assessment_result_interpretation_version FOREIGN KEY (instrument_version_id, interpretation_id)
        REFERENCES yusay.interpretation (instrument_version_id, interpretation_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.assessment_result IS 'Resultado histórico inmutable de un intento enviado con score oficial e interpretación.';
COMMENT ON COLUMN yusay.assessment_result.attempt_id IS 'Intento de evaluación evaluado (reutiliza exactamente attempt_id 1:1).';
COMMENT ON COLUMN yusay.assessment_result.instrument_version_id IS 'Versión histórica del instrumento sobre la que se calculó el resultado.';
COMMENT ON COLUMN yusay.assessment_result.score IS 'Puntuación total obtenida mediante cálculo atómico SUM.';
COMMENT ON COLUMN yusay.assessment_result.interpretation_id IS 'Interpretación oficial correspondiente al rango de score.';
COMMENT ON COLUMN yusay.assessment_result.calculated_at IS 'Instante UTC de cálculo del resultado.';
