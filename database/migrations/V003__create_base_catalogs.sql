-- ==============================================================================
-- Yusay Platform — Migración V003: Catálogos Base
-- ==============================================================================
-- Entidades:
--   1. yusay.topic (R-026 / TOPIC)
--   2. yusay.context_tag (R-022 / CONTEXT_TAG)
--   3. yusay.dimension (R-017 / DIMENSION)
--   4. yusay.instrument (R-006 / INSTRUMENT)
--
-- Restricciones e integridad según el diseño físico v1.0 aprobado:
--   - PKs (UUID v4 raíces centralizadas en PostgreSQL):
--       * pk_topic (IDF-14)
--       * pk_context_tag (IDF-13)
--       * pk_dimension (IDF-10)
--       * pk_instrument (IDF-04)
--   - AKs (Códigos únicos canónicos en cada catálogo):
--       * uq_topic_code (AK-009)
--       * uq_context_tag_code (AK-008)
--       * uq_dimension_code (AK-006)
--       * uq_instrument_code (AK-002)
--   - CHECKs de valores y estados:
--       * ck_context_tag_status (ACTIVE, RETIRED)
--       * ck_topic_code_nonempty (code ~ '^[a-zA-Z0-9_-]+$')
--       * ck_context_tag_code_nonempty (code ~ '^[a-zA-Z0-9_-]+$')
--       * ck_dimension_code_nonempty (code ~ '^[a-zA-Z0-9_-]+$')
--       * ck_instrument_code_nonempty (code ~ '^[a-zA-Z0-9_-]+$')
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.topic (R-026 / Vocabulario controlado editorial)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.topic (
    topic_id uuid NOT NULL DEFAULT gen_random_uuid(),
    code text NOT NULL,
    name text NOT NULL,
    description text NULL,
    CONSTRAINT pk_topic PRIMARY KEY (topic_id),
    CONSTRAINT uq_topic_code UNIQUE (code),
    CONSTRAINT ck_topic_code_nonempty CHECK (code ~ '^[a-zA-Z0-9_-]+$')
);

COMMENT ON TABLE yusay.topic IS 'Vocabulario controlado para clasificar contenido y conceptos de forma transversal.';
COMMENT ON COLUMN yusay.topic.topic_id IS 'Identificador único del tema (UUID v4 raíz IDF-14).';
COMMENT ON COLUMN yusay.topic.code IS 'Código canónico único del tema (sensible a mayúsculas).';
COMMENT ON COLUMN yusay.topic.name IS 'Nombre descriptivo del tema.';
COMMENT ON COLUMN yusay.topic.description IS 'Descripción opcional del alcance temático.';

-- ------------------------------------------------------------------------------
-- 2. Tabla: yusay.context_tag (R-022 / Etiquetas de contexto para seguimiento)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.context_tag (
    context_tag_id uuid NOT NULL DEFAULT gen_random_uuid(),
    code text NOT NULL,
    name text NOT NULL,
    description text NULL,
    status text NOT NULL DEFAULT 'ACTIVE',
    CONSTRAINT pk_context_tag PRIMARY KEY (context_tag_id),
    CONSTRAINT uq_context_tag_code UNIQUE (code),
    CONSTRAINT ck_context_tag_status CHECK (status IN ('ACTIVE', 'RETIRED')),
    CONSTRAINT ck_context_tag_code_nonempty CHECK (code ~ '^[a-zA-Z0-9_-]+$')
);

COMMENT ON TABLE yusay.context_tag IS 'Etiquetas de contexto para contextualizar registros de seguimiento personal (CheckIns).';
COMMENT ON COLUMN yusay.context_tag.context_tag_id IS 'Identificador único de la etiqueta de contexto (UUID v4 raíz IDF-13).';
COMMENT ON COLUMN yusay.context_tag.code IS 'Código canónico único de la etiqueta.';
COMMENT ON COLUMN yusay.context_tag.name IS 'Nombre descriptivo de la etiqueta.';
COMMENT ON COLUMN yusay.context_tag.description IS 'Descripción contextual opcional.';
COMMENT ON COLUMN yusay.context_tag.status IS 'Estado operativo de la etiqueta: ACTIVE o RETIRED.';

-- ------------------------------------------------------------------------------
-- 3. Tabla: yusay.dimension (R-017 / Identidad de dimensión de seguimiento)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.dimension (
    dimension_id uuid NOT NULL DEFAULT gen_random_uuid(),
    code text NOT NULL,
    name text NOT NULL,
    description text NOT NULL,
    CONSTRAINT pk_dimension PRIMARY KEY (dimension_id),
    CONSTRAINT uq_dimension_code UNIQUE (code),
    CONSTRAINT ck_dimension_code_nonempty CHECK (code ~ '^[a-zA-Z0-9_-]+$')
);

COMMENT ON TABLE yusay.dimension IS 'Identidad estable de una dimensión de seguimiento, separada de su versión de escala.';
COMMENT ON COLUMN yusay.dimension.dimension_id IS 'Identificador único de la dimensión (UUID v4 raíz IDF-10).';
COMMENT ON COLUMN yusay.dimension.code IS 'Código canónico único de la dimensión.';
COMMENT ON COLUMN yusay.dimension.name IS 'Nombre representativo de la dimensión.';
COMMENT ON COLUMN yusay.dimension.description IS 'Descripción obligatoria del constructo evaluado.';

-- ------------------------------------------------------------------------------
-- 4. Tabla: yusay.instrument (R-006 / Identidad de instrumento psicométrico)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.instrument (
    instrument_id uuid NOT NULL DEFAULT gen_random_uuid(),
    code text NOT NULL,
    name text NOT NULL,
    description text NOT NULL,
    purpose text NOT NULL,
    CONSTRAINT pk_instrument PRIMARY KEY (instrument_id),
    CONSTRAINT uq_instrument_code UNIQUE (code),
    CONSTRAINT ck_instrument_code_nonempty CHECK (code ~ '^[a-zA-Z0-9_-]+$')
);

COMMENT ON TABLE yusay.instrument IS 'Identidad estable del instrumento psicométrico, separada de su definición versionada.';
COMMENT ON COLUMN yusay.instrument.instrument_id IS 'Identificador único del instrumento (UUID v4 raíz IDF-04).';
COMMENT ON COLUMN yusay.instrument.code IS 'Código canónico único del instrumento (e.g. WHO-5, PHQ-9).';
COMMENT ON COLUMN yusay.instrument.name IS 'Nombre oficial del instrumento.';
COMMENT ON COLUMN yusay.instrument.description IS 'Descripción general del instrumento psicométrico.';
COMMENT ON COLUMN yusay.instrument.purpose IS 'Propósito validado del instrumento.';
