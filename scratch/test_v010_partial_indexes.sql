-- ==============================================================================
-- Test Suite: Migración V010 (Índices Únicos Parciales)
-- ==============================================================================
\set ON_ERROR_STOP on

BEGIN;

-- ------------------------------------------------------------------------------
-- 1. Setup: Crear Instrumento, Dimensión y Usuario base
-- ------------------------------------------------------------------------------
DO $$
DECLARE
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_dim_id uuid := '22222222-2222-4222-8222-222222222222';
    v_user_id uuid := '33333333-3333-4333-8333-333333333333';
BEGIN
    INSERT INTO yusay.instrument (instrument_id, code, name, description, purpose)
    VALUES (v_inst_id, 'PHQ9', 'Patient Health Questionnaire-9', 'Depresión', 'Tamizaje de síntomas depresivos');

    INSERT INTO yusay.dimension (dimension_id, code, name, description)
    VALUES (v_dim_id, 'SLEEP', 'Calidad de Sueño', 'Registro subjetivo de descanso');

    INSERT INTO yusay.app_user (user_id, email, created_at, adult_confirmed_at, status)
    VALUES (v_user_id, 'patient1@yusay.test', clock_timestamp(), clock_timestamp(), 'ACTIVE');

    RAISE NOTICE 'Setup completado con éxito.';
END $$;

-- ------------------------------------------------------------------------------
-- 2. Test: uxp_instrument_version_single_published
-- ------------------------------------------------------------------------------
-- 2.1 Permitir múltiples versiones en DRAFT o RETIRED para el mismo instrument_id
DO $$
DECLARE
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_v1 uuid := 'aaaaaaaa-1111-4aaa-8aaa-aaaaaaaaaaaa';
    v_v2 uuid := 'aaaaaaaa-2222-4aaa-8aaa-aaaaaaaaaaaa';
    v_v3 uuid := 'aaaaaaaa-3333-4aaa-8aaa-aaaaaaaaaaaa';
BEGIN
    INSERT INTO yusay.instrument_version (instrument_version_id, instrument_id, version, source_description, population, administration_conditions, license_information, limitations, status)
    VALUES (v_v1, v_inst_id, 1, 'Fuente 1', 'Adultos', 'Autoaplicado', 'Libre', 'Ninguna', 'DRAFT');

    INSERT INTO yusay.instrument_version (instrument_version_id, instrument_id, version, source_description, population, administration_conditions, license_information, limitations, status)
    VALUES (v_v2, v_inst_id, 2, 'Fuente 2', 'Adultos', 'Autoaplicado', 'Libre', 'Ninguna', 'DRAFT');

    INSERT INTO yusay.instrument_version (instrument_version_id, instrument_id, version, source_description, population, administration_conditions, license_information, limitations, status)
    VALUES (v_v3, v_inst_id, 3, 'Fuente 3', 'Adultos', 'Autoaplicado', 'Libre', 'Ninguna', 'RETIRED');

    RAISE NOTICE 'Aprobado: Se permitieron múltiples versiones en DRAFT y RETIRED para el mismo instrumento.';
END $$;

-- 2.2 Permitir exactamente UNA versión en PUBLISHED
DO $$
DECLARE
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_v4 uuid := 'aaaaaaaa-4444-4aaa-8aaa-aaaaaaaaaaaa';
BEGIN
    INSERT INTO yusay.instrument_version (instrument_version_id, instrument_id, version, source_description, population, administration_conditions, license_information, limitations, status)
    VALUES (v_v4, v_inst_id, 4, 'Fuente 4', 'Adultos', 'Autoaplicado', 'Libre', 'Ninguna', 'PUBLISHED');

    RAISE NOTICE 'Aprobado: Se permitió exactamente una versión en PUBLISHED.';
END $$;

-- 2.3 Rechazar una SEGUNDA versión en PUBLISHED (violación de índice parcial)
DO $$
DECLARE
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_v5 uuid := 'aaaaaaaa-5555-4aaa-8aaa-aaaaaaaaaaaa';
BEGIN
    INSERT INTO yusay.instrument_version (instrument_version_id, instrument_id, version, source_description, population, administration_conditions, license_information, limitations, status)
    VALUES (v_v5, v_inst_id, 5, 'Fuente 5', 'Adultos', 'Autoaplicado', 'Libre', 'Ninguna', 'PUBLISHED');

    RAISE EXCEPTION 'Fallo: Se permitió una segunda versión en PUBLISHED para el mismo instrumento.';
EXCEPTION
    WHEN unique_violation THEN
        RAISE NOTICE 'Aprobado: uxp_instrument_version_single_published rechazó segunda versión PUBLISHED concurrente.';
END $$;

-- 2.4 Transición atómica válida: RETIRED de la anterior y PUBLISHED de la nueva
DO $$
DECLARE
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_v4 uuid := 'aaaaaaaa-4444-4aaa-8aaa-aaaaaaaaaaaa';
    v_v5 uuid := 'aaaaaaaa-5555-4aaa-8aaa-aaaaaaaaaaaa';
BEGIN
    -- Retirar v4
    UPDATE yusay.instrument_version SET status = 'RETIRED' WHERE instrument_version_id = v_v4;

    -- Publicar v5
    INSERT INTO yusay.instrument_version (instrument_version_id, instrument_id, version, source_description, population, administration_conditions, license_information, limitations, status)
    VALUES (v_v5, v_inst_id, 5, 'Fuente 5', 'Adultos', 'Autoaplicado', 'Libre', 'Ninguna', 'PUBLISHED');

    RAISE NOTICE 'Aprobado: Reemplazo válido de versión PUBLISHED tras retirar la previa.';
END $$;

-- ------------------------------------------------------------------------------
-- 3. Test: uxp_dimension_version_single_active
-- ------------------------------------------------------------------------------
-- 3.1 Permitir múltiples versiones en DRAFT o RETIRED para la misma dimensión
DO $$
DECLARE
    v_dim_id uuid := '22222222-2222-4222-8222-222222222222';
    v_dv1 uuid := 'bbbbbbbb-1111-4bbb-8bbb-bbbbbbbbbbbb';
    v_dv2 uuid := 'bbbbbbbb-2222-4bbb-8bbb-bbbbbbbbbbbb';
    v_dv3 uuid := 'bbbbbbbb-3333-4bbb-8bbb-bbbbbbbbbbbb';
BEGIN
    INSERT INTO yusay.dimension_version (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
    VALUES (v_dv1, v_dim_id, 1, 'Def 1', 1, 5, 1, 'DRAFT');

    INSERT INTO yusay.dimension_version (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
    VALUES (v_dv2, v_dim_id, 2, 'Def 2', 1, 5, 1, 'DRAFT');

    INSERT INTO yusay.dimension_version (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
    VALUES (v_dv3, v_dim_id, 3, 'Def 3', 1, 5, 1, 'RETIRED');

    RAISE NOTICE 'Aprobado: Se permitieron múltiples versiones en DRAFT y RETIRED para la misma dimensión.';
END $$;

-- 3.2 Permitir exactamente UNA versión en ACTIVE
DO $$
DECLARE
    v_dim_id uuid := '22222222-2222-4222-8222-222222222222';
    v_dv4 uuid := 'bbbbbbbb-4444-4bbb-8bbb-bbbbbbbbbbbb';
BEGIN
    INSERT INTO yusay.dimension_version (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
    VALUES (v_dv4, v_dim_id, 4, 'Def 4', 1, 5, 1, 'ACTIVE');

    RAISE NOTICE 'Aprobado: Se permitió exactamente una versión en ACTIVE.';
END $$;

-- 3.3 Rechazar una SEGUNDA versión en ACTIVE (violación de índice parcial)
DO $$
DECLARE
    v_dim_id uuid := '22222222-2222-4222-8222-222222222222';
    v_dv5 uuid := 'bbbbbbbb-5555-4bbb-8bbb-bbbbbbbbbbbb';
BEGIN
    INSERT INTO yusay.dimension_version (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
    VALUES (v_dv5, v_dim_id, 5, 'Def 5', 1, 5, 1, 'ACTIVE');

    RAISE EXCEPTION 'Fallo: Se permitió una segunda versión en ACTIVE para la misma dimensión.';
EXCEPTION
    WHEN unique_violation THEN
        RAISE NOTICE 'Aprobado: uxp_dimension_version_single_active rechazó segunda versión ACTIVE concurrente.';
END $$;

-- 3.4 Transición atómica válida: RETIRED de la anterior y ACTIVE de la nueva
DO $$
DECLARE
    v_dim_id uuid := '22222222-2222-4222-8222-222222222222';
    v_dv4 uuid := 'bbbbbbbb-4444-4bbb-8bbb-bbbbbbbbbbbb';
    v_dv5 uuid := 'bbbbbbbb-5555-4bbb-8bbb-bbbbbbbbbbbb';
BEGIN
    -- Retirar v4
    UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = v_dv4;

    -- Activar v5
    INSERT INTO yusay.dimension_version (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
    VALUES (v_dv5, v_dim_id, 5, 'Def 5', 1, 5, 1, 'ACTIVE');

    RAISE NOTICE 'Aprobado: Reemplazo válido de versión ACTIVE tras retirar la previa.';
END $$;

-- ------------------------------------------------------------------------------
-- 4. Test: uxp_assessment_attempt_single_in_progress
-- ------------------------------------------------------------------------------
-- 4.1 Permitir un intento IN_PROGRESS para un (user_id, instrument_id)
DO $$
DECLARE
    v_user_id uuid := '33333333-3333-4333-8333-333333333333';
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_v5 uuid := 'aaaaaaaa-5555-4aaa-8aaa-aaaaaaaaaaaa';
    v_att1 uuid := 'cccccccc-1111-4ccc-8ccc-cccccccccccc';
BEGIN
    INSERT INTO yusay.assessment_attempt (attempt_id, user_id, instrument_id, instrument_version_id, status, started_at, expires_at)
    VALUES (v_att1, v_user_id, v_inst_id, v_v5, 'IN_PROGRESS', clock_timestamp(), clock_timestamp() + interval '720 hours');

    RAISE NOTICE 'Aprobado: Se insertó el primer intento en estado IN_PROGRESS.';
END $$;

-- 4.2 Rechazar un SEGUNDO intento IN_PROGRESS para el mismo (user_id, instrument_id)
DO $$
DECLARE
    v_user_id uuid := '33333333-3333-4333-8333-333333333333';
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_v5 uuid := 'aaaaaaaa-5555-4aaa-8aaa-aaaaaaaaaaaa';
    v_att2 uuid := 'cccccccc-2222-4ccc-8ccc-cccccccccccc';
BEGIN
    INSERT INTO yusay.assessment_attempt (attempt_id, user_id, instrument_id, instrument_version_id, status, started_at, expires_at)
    VALUES (v_att2, v_user_id, v_inst_id, v_v5, 'IN_PROGRESS', clock_timestamp(), clock_timestamp() + interval '720 hours');

    RAISE EXCEPTION 'Fallo: Se permitió un segundo intento IN_PROGRESS concurrente para el mismo instrumento.';
EXCEPTION
    WHEN unique_violation THEN
        RAISE NOTICE 'Aprobado: uxp_assessment_attempt_single_in_progress rechazó segundo intento IN_PROGRESS.';
END $$;

-- 4.3 Permitir múltiples intentos en estados terminales (SUBMITTED, EXPIRED, CANCELLED)
DO $$
DECLARE
    v_user_id uuid := '33333333-3333-4333-8333-333333333333';
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_v5 uuid := 'aaaaaaaa-5555-4aaa-8aaa-aaaaaaaaaaaa';
    v_att1 uuid := 'cccccccc-1111-4ccc-8ccc-cccccccccccc';
    v_att3 uuid := 'cccccccc-3333-4ccc-8ccc-cccccccccccc';
    v_att4 uuid := 'cccccccc-4444-4ccc-8ccc-cccccccccccc';
    v_att5 uuid := 'cccccccc-5555-4ccc-8ccc-cccccccccccc';
BEGIN
    -- Finalizar intento 1 como SUBMITTED
    UPDATE yusay.assessment_attempt SET status = 'SUBMITTED', ended_at = clock_timestamp() WHERE attempt_id = v_att1;

    -- Insertar intento previo en EXPIRED
    INSERT INTO yusay.assessment_attempt (attempt_id, user_id, instrument_id, instrument_version_id, status, started_at, expires_at, ended_at)
    VALUES (v_att3, v_user_id, v_inst_id, v_v5, 'EXPIRED', clock_timestamp() - interval '800 hours', clock_timestamp() - interval '80 hours', clock_timestamp() - interval '80 hours');

    -- Insertar intento previo en CANCELLED
    INSERT INTO yusay.assessment_attempt (attempt_id, user_id, instrument_id, instrument_version_id, status, started_at, expires_at, ended_at)
    VALUES (v_att4, v_user_id, v_inst_id, v_v5, 'CANCELLED', clock_timestamp() - interval '100 hours', clock_timestamp() + interval '620 hours', clock_timestamp() - interval '90 hours');

    -- Insertar nuevo intento en IN_PROGRESS (ahora que no hay ninguno en curso)
    INSERT INTO yusay.assessment_attempt (attempt_id, user_id, instrument_id, instrument_version_id, status, started_at, expires_at)
    VALUES (v_att5, v_user_id, v_inst_id, v_v5, 'IN_PROGRESS', clock_timestamp(), clock_timestamp() + interval '720 hours');

    RAISE NOTICE 'Aprobado: Se permitieron múltiples intentos en estados terminales y un nuevo intento IN_PROGRESS.';
END $$;

-- 4.4 Permitir intentos IN_PROGRESS para un DIFERENTE usuario o diferente instrumento
DO $$
DECLARE
    v_user2_id uuid := '44444444-4444-4444-8444-444444444444';
    v_inst_id uuid := '11111111-1111-4111-8111-111111111111';
    v_v5 uuid := 'aaaaaaaa-5555-4aaa-8aaa-aaaaaaaaaaaa';
    v_att6 uuid := 'cccccccc-6666-4ccc-8ccc-cccccccccccc';
BEGIN
    INSERT INTO yusay.app_user (user_id, email, created_at, adult_confirmed_at, status)
    VALUES (v_user2_id, 'patient2@yusay.test', clock_timestamp(), clock_timestamp(), 'ACTIVE');

    INSERT INTO yusay.assessment_attempt (attempt_id, user_id, instrument_id, instrument_version_id, status, started_at, expires_at)
    VALUES (v_att6, v_user2_id, v_inst_id, v_v5, 'IN_PROGRESS', clock_timestamp(), clock_timestamp() + interval '720 hours');

    RAISE NOTICE 'Aprobado: Se permitió intento IN_PROGRESS para otro usuario sobre el mismo instrumento.';
END $$;

-- Revertir todos los cambios de prueba
ROLLBACK;
