-- ==============================================================================
-- Yusay Platform — Migración V004: Versiones y Escalas
-- ==============================================================================
-- Entidades:
--   1. yusay.instrument_version (R-007 / INSTRUMENT_VERSION)
--   2. yusay.instrument_version_reference (R-008 / INSTRUMENT_VERSION_REFERENCE)
--   3. yusay.question (R-009 / QUESTION)
--   4. yusay.answer_option (R-010 / ANSWER_OPTION)
--   5. yusay.dimension_version (R-018 / DIMENSION_VERSION)
--   6. yusay.dimension_anchor (R-019 / DIMENSION_ANCHOR)
--
-- Restricciones e integridad según el diseño físico v1.0 aprobado:
--   - PKs:
--       * pk_instrument_version (instrument_version_id uuid IDF-05)
--       * pk_instrument_version_reference (instrument_version_id, reference_order)
--       * pk_question (question_id uuid IDF-06)
--       * pk_answer_option (option_id uuid IDF-07)
--       * pk_dimension_version (dimension_version_id uuid IDF-11)
--       * pk_dimension_anchor (dimension_version_id, value)
--   - AKs:
--       * uq_instrument_version_instrument_id_version (AK-003)
--       * uq_question_instrument_version_id_position (AK-004)
--       * uq_answer_option_question_id_position (AK-005)
--       * uq_dimension_version_dimension_id_version (AK-007)
--   - URAs (Claves únicas para respaldar FKs compuestas normativas):
--       * uq_instrument_version_ref_instrument_id_instrument_version_id (URA-001)
--       * uq_question_ref_instrument_version_id_question_id (URA-002)
--       * uq_answer_option_ref_question_id_option_id (URA-003)
--       * uq_dimension_version_ref_dimension_id_dimension_version_id (URA-006)
--   - FKs (Políticas ON DELETE RESTRICT para proteger catálogos y definiciones):
--       * fk_instrument_version_instrument -> instrument (FK-005)
--       * fk_instrument_version_reference_instrument_version -> instrument_version (FK-006)
--       * fk_question_instrument_version -> instrument_version (FK-007)
--       * fk_answer_option_question -> question (FK-008)
--       * fk_dimension_version_dimension -> dimension (FK-022)
--       * fk_dimension_anchor_dimension_version -> dimension_version (FK-023)
--   - CHECKs normativos:
--       * ck_instrument_version_version_positive (version > 0)
--       * ck_instrument_version_status (DRAFT, READY, PUBLISHED, RETIRED)
--       * ck_instrument_version_reference_reference_order_positive (reference_order > 0)
--       * ck_question_position_positive (position > 0)
--       * ck_answer_option_position_positive (position > 0)
--       * ck_dimension_version_version_positive (version > 0)
--       * ck_dimension_version_status (DRAFT, ACTIVE, RETIRED)
--       * ck_dimension_version_bounds (min_value < max_value)
--       * ck_dimension_version_step_positive (step > 0)
--       * ck_dimension_version_scale_endpoint ((max_value - min_value) % step = 0)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.instrument_version (R-007)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.instrument_version (
    instrument_version_id uuid NOT NULL DEFAULT gen_random_uuid(),
    instrument_id uuid NOT NULL,
    version integer NOT NULL,
    status text NOT NULL DEFAULT 'DRAFT',
    source_description text NOT NULL,
    population text NOT NULL,
    administration_conditions text NULL,
    license_information text NULL,
    limitations text NOT NULL,
    CONSTRAINT pk_instrument_version PRIMARY KEY (instrument_version_id),
    CONSTRAINT fk_instrument_version_instrument FOREIGN KEY (instrument_id)
        REFERENCES yusay.instrument (instrument_id)
        ON DELETE RESTRICT,
    CONSTRAINT uq_instrument_version_instrument_id_version UNIQUE (instrument_id, version),
    CONSTRAINT uq_instrument_version_ref_instrument_id_instrument_version_id UNIQUE (instrument_id, instrument_version_id),
    CONSTRAINT ck_instrument_version_version_positive CHECK (version > 0),
    CONSTRAINT ck_instrument_version_status CHECK (status IN ('DRAFT', 'READY', 'PUBLISHED', 'RETIRED'))
);

COMMENT ON TABLE yusay.instrument_version IS 'Definición identificable y reproducible de una versión concreta de instrumento psicométrico.';
COMMENT ON COLUMN yusay.instrument_version.instrument_version_id IS 'Identificador único de la versión (UUID v4 raíz IDF-05).';
COMMENT ON COLUMN yusay.instrument_version.instrument_id IS 'Instrumento al que pertenece la versión.';
COMMENT ON COLUMN yusay.instrument_version.version IS 'Número ordinal de versión (entero positivo > 0).';
COMMENT ON COLUMN yusay.instrument_version.status IS 'Estado del ciclo editorial: DRAFT, READY, PUBLISHED o RETIRED.';
COMMENT ON COLUMN yusay.instrument_version.source_description IS 'Descripción metodológica y bibliográfica de la fuente.';
COMMENT ON COLUMN yusay.instrument_version.population IS 'Población objetivo y muestra de validación psicométrica.';
COMMENT ON COLUMN yusay.instrument_version.administration_conditions IS 'Condiciones requeridas para la administración del instrumento.';
COMMENT ON COLUMN yusay.instrument_version.license_information IS 'Términos de licenciamiento y uso de la escala.';
COMMENT ON COLUMN yusay.instrument_version.limitations IS 'Limitaciones de aplicación e interpretación psicométrica.';

-- ------------------------------------------------------------------------------
-- 2. Tabla: yusay.instrument_version_reference (R-008)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.instrument_version_reference (
    instrument_version_id uuid NOT NULL,
    reference_order integer NOT NULL,
    citation text NOT NULL,
    url text NULL,
    CONSTRAINT pk_instrument_version_reference PRIMARY KEY (instrument_version_id, reference_order),
    CONSTRAINT fk_instrument_version_reference_instrument_version FOREIGN KEY (instrument_version_id)
        REFERENCES yusay.instrument_version (instrument_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_instrument_version_reference_reference_order_positive CHECK (reference_order > 0)
);

COMMENT ON TABLE yusay.instrument_version_reference IS 'Referencias bibliográficas de respaldo de la versión psicométrica.';
COMMENT ON COLUMN yusay.instrument_version_reference.instrument_version_id IS 'Versión psicométrica respaldada.';
COMMENT ON COLUMN yusay.instrument_version_reference.reference_order IS 'Orden ordinal de citación dentro de la versión (> 0).';
COMMENT ON COLUMN yusay.instrument_version_reference.citation IS 'Cita textual normalizada (e.g. APA / Vancouver).';
COMMENT ON COLUMN yusay.instrument_version_reference.url IS 'Enlace digital persistente (DOI / URL) opcional.';

-- ------------------------------------------------------------------------------
-- 3. Tabla: yusay.question (R-009)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.question (
    question_id uuid NOT NULL DEFAULT gen_random_uuid(),
    instrument_version_id uuid NOT NULL,
    position integer NOT NULL,
    prompt text NOT NULL,
    required boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_question PRIMARY KEY (question_id),
    CONSTRAINT fk_question_instrument_version FOREIGN KEY (instrument_version_id)
        REFERENCES yusay.instrument_version (instrument_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT uq_question_instrument_version_id_position UNIQUE (instrument_version_id, position),
    CONSTRAINT uq_question_ref_instrument_version_id_question_id UNIQUE (instrument_version_id, question_id),
    CONSTRAINT ck_question_position_positive CHECK (position > 0)
);

COMMENT ON TABLE yusay.question IS 'Ítems o preguntas que componen una versión de instrumento.';
COMMENT ON COLUMN yusay.question.question_id IS 'Identificador único de la pregunta (UUID v4 raíz IDF-06).';
COMMENT ON COLUMN yusay.question.instrument_version_id IS 'Versión de instrumento a la que pertenece.';
COMMENT ON COLUMN yusay.question.position IS 'Posición ordinal del ítem dentro del instrumento (> 0).';
COMMENT ON COLUMN yusay.question.prompt IS 'Enunciado de la pregunta presentado al usuario.';
COMMENT ON COLUMN yusay.question.required IS 'Indicador de obligatoriedad de respuesta (true para publicar en MVP).';

-- ------------------------------------------------------------------------------
-- 4. Tabla: yusay.answer_option (R-010)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.answer_option (
    option_id uuid NOT NULL DEFAULT gen_random_uuid(),
    question_id uuid NOT NULL,
    position integer NOT NULL,
    label text NOT NULL,
    CONSTRAINT pk_answer_option PRIMARY KEY (option_id),
    CONSTRAINT fk_answer_option_question FOREIGN KEY (question_id)
        REFERENCES yusay.question (question_id)
        ON DELETE RESTRICT,
    CONSTRAINT uq_answer_option_question_id_position UNIQUE (question_id, position),
    CONSTRAINT uq_answer_option_ref_question_id_option_id UNIQUE (question_id, option_id),
    CONSTRAINT ck_answer_option_position_positive CHECK (position > 0)
);

COMMENT ON TABLE yusay.answer_option IS 'Opciones estructuradas de respuesta para cada pregunta psicométrica.';
COMMENT ON COLUMN yusay.answer_option.option_id IS 'Identificador único de la opción de respuesta (UUID v4 raíz IDF-07).';
COMMENT ON COLUMN yusay.answer_option.question_id IS 'Pregunta a la que pertenece la opción.';
COMMENT ON COLUMN yusay.answer_option.position IS 'Posición de presentación dentro de la pregunta (> 0).';
COMMENT ON COLUMN yusay.answer_option.label IS 'Etiqueta textual legible de la opción.';

-- ------------------------------------------------------------------------------
-- 5. Tabla: yusay.dimension_version (R-018)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.dimension_version (
    dimension_version_id uuid NOT NULL DEFAULT gen_random_uuid(),
    dimension_id uuid NOT NULL,
    version integer NOT NULL,
    definition text NOT NULL,
    min_value integer NOT NULL,
    max_value integer NOT NULL,
    step integer NOT NULL,
    status text NOT NULL DEFAULT 'DRAFT',
    CONSTRAINT pk_dimension_version PRIMARY KEY (dimension_version_id),
    CONSTRAINT fk_dimension_version_dimension FOREIGN KEY (dimension_id)
        REFERENCES yusay.dimension (dimension_id)
        ON DELETE RESTRICT,
    CONSTRAINT uq_dimension_version_dimension_id_version UNIQUE (dimension_id, version),
    CONSTRAINT uq_dimension_version_ref_dimension_id_dimension_version_id UNIQUE (dimension_id, dimension_version_id),
    CONSTRAINT ck_dimension_version_version_positive CHECK (version > 0),
    CONSTRAINT ck_dimension_version_status CHECK (status IN ('DRAFT', 'ACTIVE', 'RETIRED')),
    CONSTRAINT ck_dimension_version_bounds CHECK (min_value < max_value),
    CONSTRAINT ck_dimension_version_step_positive CHECK (step > 0),
    CONSTRAINT ck_dimension_version_scale_endpoint CHECK (((max_value - min_value) % step) = 0)
);

COMMENT ON TABLE yusay.dimension_version IS 'Definición histórica y matemática de la escala de una dimensión de seguimiento.';
COMMENT ON COLUMN yusay.dimension_version.dimension_version_id IS 'Identificador único de la versión de escala (UUID v4 raíz IDF-11).';
COMMENT ON COLUMN yusay.dimension_version.dimension_id IS 'Dimensión de seguimiento asociada.';
COMMENT ON COLUMN yusay.dimension_version.version IS 'Número ordinal de versión (> 0).';
COMMENT ON COLUMN yusay.dimension_version.definition IS 'Definición constructiva y criterios operativos de la escala.';
COMMENT ON COLUMN yusay.dimension_version.min_value IS 'Extremo inferior entero de la escala.';
COMMENT ON COLUMN yusay.dimension_version.max_value IS 'Extremo superior entero de la escala.';
COMMENT ON COLUMN yusay.dimension_version.step IS 'Incremento discreto entero entre niveles de la escala (> 0).';
COMMENT ON COLUMN yusay.dimension_version.status IS 'Estado de la versión de escala: DRAFT, ACTIVE o RETIRED.';

-- ------------------------------------------------------------------------------
-- 6. Tabla: yusay.dimension_anchor (R-019)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.dimension_anchor (
    dimension_version_id uuid NOT NULL,
    value integer NOT NULL,
    label text NOT NULL,
    CONSTRAINT pk_dimension_anchor PRIMARY KEY (dimension_version_id, value),
    CONSTRAINT fk_dimension_anchor_dimension_version FOREIGN KEY (dimension_version_id)
        REFERENCES yusay.dimension_version (dimension_version_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.dimension_anchor IS 'Anclajes y etiquetas descriptivas asociadas a valores específicos de la escala.';
COMMENT ON COLUMN yusay.dimension_anchor.dimension_version_id IS 'Versión de dimensión a la que corresponde el anclaje.';
COMMENT ON COLUMN yusay.dimension_anchor.value IS 'Valor numérico entero de la escala que recibe la etiqueta descriptiva.';
COMMENT ON COLUMN yusay.dimension_anchor.label IS 'Texto descriptivo asociado al punto de anclaje de la escala.';
