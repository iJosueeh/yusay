-- ==============================================================================
-- Yusay Platform — Migración V005: Scoring e Interpretación Psicométrica
-- ==============================================================================
-- Entidades:
--   1. yusay.scoring_definition (R-011 / SCORING_DEFINITION)
--   2. yusay.scoring_contribution (R-012 / SCORING_CONTRIBUTION)
--   3. yusay.interpretation (R-015 / INTERPRETATION)
--
-- Restricciones e integridad según el diseño físico v1.0 aprobado:
--   - PKs:
--       * pk_scoring_definition (instrument_version_id uuid — PK compartida 1:1 con instrument_version)
--       * pk_scoring_contribution (option_id uuid — PK compartida 1:1 con answer_option)
--       * pk_interpretation (interpretation_id uuid IDF-09 — UUID v4 raíz)
--   - URAs:
--       * uq_interpretation_ref_instrument_version_id_interpretation_id (URA-005)
--         Clave compuesta requerida por la FK normativa FK-021 (assessment_result -> interpretation)
--   - FKs:
--       * fk_scoring_definition_instrument_version -> instrument_version (FK-009, ON DELETE RESTRICT)
--       * fk_scoring_contribution_scoring_definition -> scoring_definition (FK-010, ON DELETE RESTRICT)
--       * fk_scoring_contribution_question_version -> question(instrument_version_id, question_id) (FK-011, ON DELETE RESTRICT)
--       * fk_scoring_contribution_option_question -> answer_option(question_id, option_id) (FK-012, ON DELETE RESTRICT)
--       * fk_interpretation_instrument_version -> instrument_version (FK-018, ON DELETE RESTRICT)
--   - CHECKs normativos:
--       * ck_scoring_definition_method (method = 'SUM')
--       * ck_interpretation_bounds (lower_bound <= upper_bound)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.scoring_definition (R-011)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.scoring_definition (
    instrument_version_id uuid NOT NULL,
    method text NOT NULL DEFAULT 'SUM',
    CONSTRAINT pk_scoring_definition PRIMARY KEY (instrument_version_id),
    CONSTRAINT fk_scoring_definition_instrument_version FOREIGN KEY (instrument_version_id)
        REFERENCES yusay.instrument_version (instrument_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_scoring_definition_method CHECK (method = 'SUM')
);

COMMENT ON TABLE yusay.scoring_definition IS 'Método de cálculo de puntuación psicométrica asociado a una versión concreta (1:1).';
COMMENT ON COLUMN yusay.scoring_definition.instrument_version_id IS 'Versión psicométrica (reutiliza exactamente instrument_version_id).';
COMMENT ON COLUMN yusay.scoring_definition.method IS 'Método de cálculo algorítmico soportado en el MVP: exclusivamente SUM.';

-- ------------------------------------------------------------------------------
-- 2. Tabla: yusay.scoring_contribution (R-012)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.scoring_contribution (
    option_id uuid NOT NULL,
    question_id uuid NOT NULL,
    instrument_version_id uuid NOT NULL,
    contribution integer NOT NULL,
    CONSTRAINT pk_scoring_contribution PRIMARY KEY (option_id),
    CONSTRAINT fk_scoring_contribution_scoring_definition FOREIGN KEY (instrument_version_id)
        REFERENCES yusay.scoring_definition (instrument_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_scoring_contribution_question_version FOREIGN KEY (instrument_version_id, question_id)
        REFERENCES yusay.question (instrument_version_id, question_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_scoring_contribution_option_question FOREIGN KEY (question_id, option_id)
        REFERENCES yusay.answer_option (question_id, option_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.scoring_contribution IS 'Puntuación o contribución numérica con signo de una opción al total SUM de la versión.';
COMMENT ON COLUMN yusay.scoring_contribution.option_id IS 'Opción de respuesta valorada (reutiliza exactamente option_id).';
COMMENT ON COLUMN yusay.scoring_contribution.question_id IS 'Pregunta a la que pertenece la opción.';
COMMENT ON COLUMN yusay.scoring_contribution.instrument_version_id IS 'Versión del instrumento a la que pertenece la pregunta y el método.';
COMMENT ON COLUMN yusay.scoring_contribution.contribution IS 'Valor entero con signo atribuido al seleccionar la opción (soporta scoring invertido).';

-- ------------------------------------------------------------------------------
-- 3. Tabla: yusay.interpretation (R-015)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.interpretation (
    interpretation_id uuid NOT NULL DEFAULT gen_random_uuid(),
    instrument_version_id uuid NOT NULL,
    label text NOT NULL,
    description text NOT NULL,
    limitations text NULL,
    lower_bound integer NOT NULL,
    upper_bound integer NOT NULL,
    CONSTRAINT pk_interpretation PRIMARY KEY (interpretation_id),
    CONSTRAINT fk_interpretation_instrument_version FOREIGN KEY (instrument_version_id)
        REFERENCES yusay.instrument_version (instrument_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT uq_interpretation_ref_instrument_version_id_interpretation_id UNIQUE (instrument_version_id, interpretation_id),
    CONSTRAINT ck_interpretation_bounds CHECK (lower_bound <= upper_bound)
);

COMMENT ON TABLE yusay.interpretation IS 'Interpretación oficial respaldada para un rango inclusivo de puntuaciones en una versión.';
COMMENT ON COLUMN yusay.interpretation.interpretation_id IS 'Identificador único de la interpretación (UUID v4 raíz IDF-09).';
COMMENT ON COLUMN yusay.interpretation.instrument_version_id IS 'Versión psicométrica a la que corresponde esta interpretación.';
COMMENT ON COLUMN yusay.interpretation.label IS 'Etiqueta normativa o categoría diagnóstica/orientativa (e.g. Bienestar adecuado, Sintomatología severa).';
COMMENT ON COLUMN yusay.interpretation.description IS 'Explicación detallada orientativa según el manual del instrumento.';
COMMENT ON COLUMN yusay.interpretation.limitations IS 'Aclaraciones psicométricas sobre falsos positivos o limitaciones de la categoría.';
COMMENT ON COLUMN yusay.interpretation.lower_bound IS 'Límite numérico inferior inclusivo del rango de puntuación.';
COMMENT ON COLUMN yusay.interpretation.upper_bound IS 'Límite numérico superior inclusivo del rango de puntuación.';
