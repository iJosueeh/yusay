-- ==============================================================================
-- Yusay Platform — Migración V006: Contenido Editorial y Asociaciones Temáticas
-- ==============================================================================
-- Entidades:
--   1. yusay.resource (R-027 / RESOURCE)
--   2. yusay.resource_topic (R-028 / RESOURCE_TOPIC)
--   3. yusay.instrument_topic (R-029 / INSTRUMENT_TOPIC)
--   4. yusay.interpretation_topic (R-030 / INTERPRETATION_TOPIC)
--   5. yusay.dimension_topic (R-031 / DIMENSION_TOPIC)
--
-- Restricciones e integridad según el diseño físico v1.0 aprobado:
--   - PKs:
--       * pk_resource (resource_id uuid IDF-15)
--       * pk_resource_topic (resource_id, topic_id)
--       * pk_instrument_topic (instrument_id, topic_id)
--       * pk_interpretation_topic (interpretation_id, topic_id)
--       * pk_dimension_topic (dimension_id, topic_id)
--   - FKs (Políticas ON DELETE RESTRICT normativas para proteger catálogo y taxonomía):
--       * fk_resource_topic_resource -> resource (FK-033)
--       * fk_resource_topic_topic -> topic (FK-034)
--       * fk_instrument_topic_instrument -> instrument (FK-035)
--       * fk_instrument_topic_topic -> topic (FK-036)
--       * fk_interpretation_topic_interpretation -> interpretation (FK-037)
--       * fk_interpretation_topic_topic -> topic (FK-038)
--       * fk_dimension_topic_dimension -> dimension (FK-039)
--       * fk_dimension_topic_topic -> topic (FK-040)
--   - CHECKs normativos:
--       * ck_resource_type (ARTICLE, EXTERNAL_LINK)
--       * ck_resource_status (DRAFT, PUBLISHED, RETIRED)
--       * ck_resource_title_nonempty (length(trim(title)) > 0)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.resource (R-027)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.resource (
    resource_id uuid NOT NULL DEFAULT gen_random_uuid(),
    type text NOT NULL,
    status text NOT NULL DEFAULT 'DRAFT',
    title text NOT NULL,
    summary text NULL,
    body text NULL,
    external_url text NULL,
    CONSTRAINT pk_resource PRIMARY KEY (resource_id),
    CONSTRAINT ck_resource_type CHECK (type IN ('ARTICLE', 'EXTERNAL_LINK')),
    CONSTRAINT ck_resource_status CHECK (status IN ('DRAFT', 'PUBLISHED', 'RETIRED')),
    CONSTRAINT ck_resource_title_nonempty CHECK (length(trim(title)) > 0)
);

COMMENT ON TABLE yusay.resource IS 'Contenido informativo o psicoeducativo con ciclo editorial independiente de evaluaciones.';
COMMENT ON COLUMN yusay.resource.resource_id IS 'Identificador único del recurso (UUID v4 raíz IDF-15).';
COMMENT ON COLUMN yusay.resource.type IS 'Naturaleza técnica del recurso: ARTICLE o EXTERNAL_LINK (inmutable desde publicación).';
COMMENT ON COLUMN yusay.resource.status IS 'Estado del ciclo editorial: DRAFT, PUBLISHED o RETIRED.';
COMMENT ON COLUMN yusay.resource.title IS 'Título público del recurso (obligatorio y no vacío desde DRAFT).';
COMMENT ON COLUMN yusay.resource.summary IS 'Resumen editorial opcional para vistas previas.';
COMMENT ON COLUMN yusay.resource.body IS 'Cuerpo editorial completo del artículo (requerido para ARTICLE en PUBLISHED).';
COMMENT ON COLUMN yusay.resource.external_url IS 'URL persistente externa segura (requerido para EXTERNAL_LINK en PUBLISHED).';

-- ------------------------------------------------------------------------------
-- 2. Tabla: yusay.resource_topic (R-028)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.resource_topic (
    resource_id uuid NOT NULL,
    topic_id uuid NOT NULL,
    CONSTRAINT pk_resource_topic PRIMARY KEY (resource_id, topic_id),
    CONSTRAINT fk_resource_topic_resource FOREIGN KEY (resource_id)
        REFERENCES yusay.resource (resource_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_resource_topic_topic FOREIGN KEY (topic_id)
        REFERENCES yusay.topic (topic_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.resource_topic IS 'Asociación muchos a muchos entre recursos editoriales y temas taxonómicos.';
COMMENT ON COLUMN yusay.resource_topic.resource_id IS 'Recurso clasificado.';
COMMENT ON COLUMN yusay.resource_topic.topic_id IS 'Tema asociado.';

-- ------------------------------------------------------------------------------
-- 3. Tabla: yusay.instrument_topic (R-029)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.instrument_topic (
    instrument_id uuid NOT NULL,
    topic_id uuid NOT NULL,
    CONSTRAINT pk_instrument_topic PRIMARY KEY (instrument_id, topic_id),
    CONSTRAINT fk_instrument_topic_instrument FOREIGN KEY (instrument_id)
        REFERENCES yusay.instrument (instrument_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_instrument_topic_topic FOREIGN KEY (topic_id)
        REFERENCES yusay.topic (topic_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.instrument_topic IS 'Vínculo editorial entre el instrumento psicométrico y temas para orientación.';
COMMENT ON COLUMN yusay.instrument_topic.instrument_id IS 'Instrumento psicométrico.';
COMMENT ON COLUMN yusay.instrument_topic.topic_id IS 'Tema taxonómico asociado.';

-- ------------------------------------------------------------------------------
-- 4. Tabla: yusay.interpretation_topic (R-030)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.interpretation_topic (
    interpretation_id uuid NOT NULL,
    topic_id uuid NOT NULL,
    CONSTRAINT pk_interpretation_topic PRIMARY KEY (interpretation_id, topic_id),
    CONSTRAINT fk_interpretation_topic_interpretation FOREIGN KEY (interpretation_id)
        REFERENCES yusay.interpretation (interpretation_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_interpretation_topic_topic FOREIGN KEY (topic_id)
        REFERENCES yusay.topic (topic_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.interpretation_topic IS 'Vínculo entre interpretaciones oficiales de resultados y temas para derivación de Guidance.';
COMMENT ON COLUMN yusay.interpretation_topic.interpretation_id IS 'Interpretación oficial de rango psicométrico.';
COMMENT ON COLUMN yusay.interpretation_topic.topic_id IS 'Tema taxonómico asociado.';

-- ------------------------------------------------------------------------------
-- 5. Tabla: yusay.dimension_topic (R-031)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.dimension_topic (
    dimension_id uuid NOT NULL,
    topic_id uuid NOT NULL,
    CONSTRAINT pk_dimension_topic PRIMARY KEY (dimension_id, topic_id),
    CONSTRAINT fk_dimension_topic_dimension FOREIGN KEY (dimension_id)
        REFERENCES yusay.dimension (dimension_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_dimension_topic_topic FOREIGN KEY (topic_id)
        REFERENCES yusay.topic (topic_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.dimension_topic IS 'Vínculo editorial entre la dimensión de seguimiento y temas para orientación.';
COMMENT ON COLUMN yusay.dimension_topic.dimension_id IS 'Dimensión de seguimiento.';
COMMENT ON COLUMN yusay.dimension_topic.topic_id IS 'Tema taxonómico asociado.';
