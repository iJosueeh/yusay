-- ==============================================================================
-- Yusay Platform — Migración V007: Compatibilidad entre Versiones
-- ==============================================================================
-- Entidades:
--   1. yusay.instrument_version_compatibility (R-024 / INSTRUMENT_VERSION_COMPATIBILITY)
--   2. yusay.dimension_version_compatibility (R-025 / DIMENSION_VERSION_COMPATIBILITY)
--
-- Restricciones e integridad según el diseño físico v1.0 aprobado:
--   - PKs:
--       * pk_instrument_version_compatibility (version_a_id, version_b_id)
--       * pk_dimension_version_compatibility (version_a_id, version_b_id)
--   - FKs compuestas (Pertenencia estricta al mismo catálogo común padre con ON DELETE RESTRICT):
--       * fk_instrument_version_compatibility_version_a -> instrument_version(instrument_id, instrument_version_id) (FK-029)
--       * fk_instrument_version_compatibility_version_b -> instrument_version(instrument_id, instrument_version_id) (FK-030)
--       * fk_dimension_version_compatibility_version_a -> dimension_version(dimension_id, dimension_version_id) (FK-031)
--       * fk_dimension_version_compatibility_version_b -> dimension_version(dimension_id, dimension_version_id) (FK-032)
--   - CHECKs normativos de orden canónico total y exclusión de autorreferencia:
--       * ck_instrument_version_compatibility_canonical_pair (version_a_id < version_b_id)
--       * ck_dimension_version_compatibility_canonical_pair (version_a_id < version_b_id)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.instrument_version_compatibility (R-024)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.instrument_version_compatibility (
    version_a_id uuid NOT NULL,
    version_b_id uuid NOT NULL,
    instrument_id uuid NOT NULL,
    rationale text NOT NULL,
    reference text NULL,
    CONSTRAINT pk_instrument_version_compatibility PRIMARY KEY (version_a_id, version_b_id),
    CONSTRAINT fk_instrument_version_compatibility_version_a FOREIGN KEY (instrument_id, version_a_id)
        REFERENCES yusay.instrument_version (instrument_id, instrument_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_instrument_version_compatibility_version_b FOREIGN KEY (instrument_id, version_b_id)
        REFERENCES yusay.instrument_version (instrument_id, instrument_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_instrument_version_compatibility_canonical_pair CHECK (version_a_id < version_b_id)
);

COMMENT ON TABLE yusay.instrument_version_compatibility IS 'Declaración positiva y justificada de compatibilidad longitudinal entre dos versiones de un mismo instrumento.';
COMMENT ON COLUMN yusay.instrument_version_compatibility.version_a_id IS 'Versión inicial según el orden canónico estricto de 16 octetos sin signo (version_a_id < version_b_id).';
COMMENT ON COLUMN yusay.instrument_version_compatibility.version_b_id IS 'Segunda versión del par canónico ordenado.';
COMMENT ON COLUMN yusay.instrument_version_compatibility.instrument_id IS 'Instrumento psicométrico común al que ambas versiones deben pertenecer de forma obligatoria.';
COMMENT ON COLUMN yusay.instrument_version_compatibility.rationale IS 'Justificación metodológica documentada de la compatibilidad longitudinal.';
COMMENT ON COLUMN yusay.instrument_version_compatibility.reference IS 'Referencia bibliográfica formal o técnica de respaldo (opcional).';

-- ------------------------------------------------------------------------------
-- 2. Tabla: yusay.dimension_version_compatibility (R-025)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.dimension_version_compatibility (
    version_a_id uuid NOT NULL,
    version_b_id uuid NOT NULL,
    dimension_id uuid NOT NULL,
    rationale text NOT NULL,
    reference text NULL,
    CONSTRAINT pk_dimension_version_compatibility PRIMARY KEY (version_a_id, version_b_id),
    CONSTRAINT fk_dimension_version_compatibility_version_a FOREIGN KEY (dimension_id, version_a_id)
        REFERENCES yusay.dimension_version (dimension_id, dimension_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_dimension_version_compatibility_version_b FOREIGN KEY (dimension_id, version_b_id)
        REFERENCES yusay.dimension_version (dimension_id, dimension_version_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_dimension_version_compatibility_canonical_pair CHECK (version_a_id < version_b_id)
);

COMMENT ON TABLE yusay.dimension_version_compatibility IS 'Declaración positiva y justificada de compatibilidad longitudinal entre dos versiones de una misma dimensión.';
COMMENT ON COLUMN yusay.dimension_version_compatibility.version_a_id IS 'Versión inicial según el orden canónico estricto de 16 octetos sin signo (version_a_id < version_b_id).';
COMMENT ON COLUMN yusay.dimension_version_compatibility.version_b_id IS 'Segunda versión del par canónico ordenado.';
COMMENT ON COLUMN yusay.dimension_version_compatibility.dimension_id IS 'Dimensión de seguimiento común a la que ambas versiones deben pertenecer obligatoriamente.';
COMMENT ON COLUMN yusay.dimension_version_compatibility.rationale IS 'Justificación metodológica documentada de la compatibilidad de escalas.';
COMMENT ON COLUMN yusay.dimension_version_compatibility.reference IS 'Referencia bibliográfica formal o técnica de respaldo (opcional).';
