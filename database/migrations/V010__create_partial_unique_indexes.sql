-- ==============================================================================
-- Yusay Platform — Migración V010: Índices Únicos Parciales de Integridad
-- ==============================================================================
-- Mecanismos implementados:
--   1. MP-PHYS-004: Máximo una versión publicada por instrumento
--      - Objeto: uxp_instrument_version_single_published
--      - Tabla: yusay.instrument_version (instrument_id) WHERE (status = 'PUBLISHED')
--   2. MP-PHYS-004: Máximo una versión activa por dimensión
--      - Objeto: uxp_dimension_version_single_active
--      - Tabla: yusay.dimension_version (dimension_id) WHERE (status = 'ACTIVE')
--   3. MP-PHYS-005: Máximo un intento en progreso por usuario e instrumento
--      - Objeto: uxp_assessment_attempt_single_in_progress
--      - Tabla: yusay.assessment_attempt (user_id, instrument_id) WHERE (status = 'IN_PROGRESS')
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Índice: uxp_instrument_version_single_published (MP-PHYS-004)
-- ------------------------------------------------------------------------------
CREATE UNIQUE INDEX uxp_instrument_version_single_published 
ON yusay.instrument_version (instrument_id) 
WHERE (status = 'PUBLISHED');

COMMENT ON INDEX yusay.uxp_instrument_version_single_published IS 
'Garantiza físicamente que exista como máximo una versión en estado PUBLISHED por instrumento (MP-PHYS-004).';

-- ------------------------------------------------------------------------------
-- 2. Índice: uxp_dimension_version_single_active (MP-PHYS-004)
-- ------------------------------------------------------------------------------
CREATE UNIQUE INDEX uxp_dimension_version_single_active 
ON yusay.dimension_version (dimension_id) 
WHERE (status = 'ACTIVE');

COMMENT ON INDEX yusay.uxp_dimension_version_single_active IS 
'Garantiza físicamente que exista como máximo una versión en estado ACTIVE por dimensión (MP-PHYS-004).';

-- ------------------------------------------------------------------------------
-- 3. Índice: uxp_assessment_attempt_single_in_progress (MP-PHYS-005)
-- ------------------------------------------------------------------------------
CREATE UNIQUE INDEX uxp_assessment_attempt_single_in_progress 
ON yusay.assessment_attempt (user_id, instrument_id) 
WHERE (status = 'IN_PROGRESS');

COMMENT ON INDEX yusay.uxp_assessment_attempt_single_in_progress IS 
'Garantiza físicamente que un usuario tenga como máximo un intento de evaluación en estado IN_PROGRESS por instrumento (MP-PHYS-005).';
