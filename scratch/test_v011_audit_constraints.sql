-- ==============================================================================
-- Test Suite: Migración V011 (Auditoría Combinaciones y Perfiles JSONB)
-- ==============================================================================
\set ON_ERROR_STOP on

BEGIN;

-- ------------------------------------------------------------------------------
-- 1. Setup: Crear Usuario y Administrador de prueba
-- ------------------------------------------------------------------------------
DO $$
DECLARE
    v_user_id uuid := '11111111-1111-4111-8111-111111111111';
    v_admin_id uuid := '22222222-2222-4222-8222-222222222222';
BEGIN
    INSERT INTO yusay.app_user (user_id, email, created_at, adult_confirmed_at, status)
    VALUES (v_user_id, 'user1@yusay.test', clock_timestamp(), clock_timestamp(), 'ACTIVE');

    INSERT INTO yusay.app_user (user_id, email, created_at, adult_confirmed_at, status)
    VALUES (v_admin_id, 'admin1@yusay.test', clock_timestamp(), clock_timestamp(), 'ACTIVE');

    INSERT INTO yusay.administrator (user_id)
    VALUES (v_admin_id);

    RAISE NOTICE 'Setup de usuarios completado con éxito.';
END $$;

-- ------------------------------------------------------------------------------
-- 2. Pruebas Positivas: Casos válidos para los 7 perfiles N/F/D/C/E/T/P
-- ------------------------------------------------------------------------------
DO $$
DECLARE
    v_user_id uuid := '11111111-1111-4111-8111-111111111111';
    v_admin_id uuid := '22222222-2222-4222-8222-222222222222';
    v_topic_id uuid := '33333333-3333-4333-8333-333333333333';
    v_v_a_id uuid := '44444444-4444-4444-8444-444444444444';
    v_v_b_id uuid := '55555555-5555-4555-8555-555555555555';
BEGIN
    -- Perfil N: USER_REGISTERED sin metadata
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES (v_user_id, 'USER', 'USER_REGISTERED', 'USER', v_user_id::text, NULL);

    -- Perfil N: CATALOG_CREATED por ADMIN sin metadata
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES (v_admin_id, 'ADMINISTRATOR', 'CATALOG_CREATED', 'INSTRUMENT', '66666666-6666-4666-8666-666666666666', NULL);

    -- Perfil F: SIGN_IN_FAILED por ANONYMOUS con reason_code válido
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES (NULL, 'ANONYMOUS', 'SIGN_IN_FAILED', 'AUTHENTICATION', '{"reason_code": "CREDENTIALS_NOT_ACCEPTED"}'::jsonb);

    -- Perfil F: SIGN_IN_FAILED sin metadata (opcional)
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES (NULL, 'ANONYMOUS', 'SIGN_IN_FAILED', 'AUTHENTICATION', NULL);

    -- Perfil D: AUTHORIZATION_DENIED con reason_code válido
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES (v_user_id, 'USER', 'AUTHORIZATION_DENIED', 'AUTHENTICATION', '{"reason_code": "ACCOUNT_NOT_ACTIVE"}'::jsonb);

    -- Perfil C: CATALOG_UPDATED sobre INSTRUMENT con changed_fields array
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES (v_admin_id, 'ADMINISTRATOR', 'CATALOG_UPDATED', 'INSTRUMENT', '66666666-6666-4666-8666-666666666666', '{"changed_fields": ["name", "description"]}'::jsonb);

    -- Perfil E: CATALOG_UPDATED sobre RESOURCE con changed_fields y correction_kind
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES (v_admin_id, 'ADMINISTRATOR', 'CATALOG_UPDATED', 'RESOURCE', '77777777-7777-4777-8777-777777777777', '{"changed_fields": ["body"], "correction_kind": "SPELLING"}'::jsonb);

    -- Perfil T: EDITORIAL_ASSOCIATION_ADDED con topic_id canónico obligatorio
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES (v_admin_id, 'ADMINISTRATOR', 'EDITORIAL_ASSOCIATION_ADDED', 'RESOURCE', '77777777-7777-4777-8777-777777777777', jsonb_build_object('topic_id', v_topic_id::text));

    -- Perfil P: COMPATIBILITY_DECLARED con version_a_id y version_b_id canónicos (a < b)
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES (
        v_admin_id, 
        'ADMINISTRATOR', 
        'COMPATIBILITY_DECLARED', 
        'INSTRUMENT_VERSION_COMPATIBILITY', 
        NULL, 
        jsonb_build_object('version_a_id', v_v_a_id::text, 'version_b_id', v_v_b_id::text)
    );

    RAISE NOTICE 'Aprobado: Todos los perfiles N, F, D, C, E, T, P fueron insertados válidamente.';
END $$;

-- ------------------------------------------------------------------------------
-- 3. Pruebas Negativas: Combinaciones inválidas de (actor_kind, action, target_type)
-- ------------------------------------------------------------------------------
-- 3.1 Intento de usuario ejecutando acción de administrador (USER en CATALOG_CREATED)
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES ('11111111-1111-4111-8111-111111111111', 'USER', 'CATALOG_CREATED', 'INSTRUMENT', NULL);
    RAISE EXCEPTION 'Fallo: Se permitió USER para acción exclusiva de ADMINISTRATOR';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_valid_combination rechazó USER en CATALOG_CREATED.';
END $$;

-- 3.2 Intento de acción con target_type prohibido (USER_REGISTERED con AUTHENTICATION)
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES ('11111111-1111-4111-8111-111111111111', 'USER', 'USER_REGISTERED', 'AUTHENTICATION', NULL);
    RAISE EXCEPTION 'Fallo: Se permitió destino inválido para USER_REGISTERED';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_valid_combination rechazó target_type inválido.';
END $$;

-- 3.3 Intento de destino de bienestar privado prohibido (e.g. ATTEMPT o ASSESSMENT_RESULT)
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES ('11111111-1111-4111-8111-111111111111', 'USER', 'USER_REGISTERED', 'ATTEMPT', NULL);
    RAISE EXCEPTION 'Fallo: Se permitió registrar ATTEMPT en auditoría';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_valid_combination rechazó destino de bienestar privado.';
END $$;

-- ------------------------------------------------------------------------------
-- 4. Pruebas Negativas: Violaciones de Perfiles JSONB
-- ------------------------------------------------------------------------------
-- 4.1 Perfil N: Intento de agregar metadata en acción N (e.g. USER_REGISTERED con payload)
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES ('11111111-1111-4111-8111-111111111111', 'USER', 'USER_REGISTERED', 'USER', '{"foo": "bar"}'::jsonb);
    RAISE EXCEPTION 'Fallo: Se permitió metadata en acción de perfil N';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_metadata_profile rechazó metadata en perfil N.';
END $$;

-- 4.2 Prohibición de JSON 'null'::jsonb
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES ('11111111-1111-4111-8111-111111111111', 'USER', 'USER_REGISTERED', 'USER', 'null'::jsonb);
    RAISE EXCEPTION 'Fallo: Se permitió jsonb null literal';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_metadata_profile rechazó null::jsonb.';
END $$;

-- 4.3 Perfil F: clave no autorizada o reason_code inválido
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_kind, action, target_type, metadata)
    VALUES ('ANONYMOUS', 'SIGN_IN_FAILED', 'AUTHENTICATION', '{"reason_code": "INVALID_PASSWORD"}'::jsonb);
    RAISE EXCEPTION 'Fallo: Se permitió reason_code no autorizado en perfil F';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_metadata_profile rechazó reason_code inválido en perfil F.';
END $$;

-- 4.4 Perfil D: clave adicional no autorizada (e.g. email filtrado)
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_kind, action, target_type, metadata)
    VALUES ('ANONYMOUS', 'AUTHORIZATION_DENIED', 'AUTHENTICATION', '{"reason_code": "AUTHENTICATION_REQUIRED", "email": "test@test.com"}'::jsonb);
    RAISE EXCEPTION 'Fallo: Se permitió clave adicional no permitida en perfil D';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_metadata_profile rechazó clave espuria en perfil D.';
END $$;

-- 4.5 Perfil T: ausencia de metadata obligatoria
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES ('22222222-2222-4222-8222-222222222222', 'ADMINISTRATOR', 'EDITORIAL_ASSOCIATION_ADDED', 'RESOURCE', '77777777-7777-4777-8777-777777777777', NULL);
    RAISE EXCEPTION 'Fallo: Se permitió EDITORIAL_ASSOCIATION_ADDED sin metadata';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_metadata_profile exigió metadata en perfil T.';
END $$;

-- 4.6 Perfil T: topic_id con formato no-UUID
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES ('22222222-2222-4222-8222-222222222222', 'ADMINISTRATOR', 'EDITORIAL_ASSOCIATION_ADDED', 'RESOURCE', '77777777-7777-4777-8777-777777777777', '{"topic_id": "not-a-uuid"}'::jsonb);
    RAISE EXCEPTION 'Fallo: Se permitió topic_id no UUID en perfil T';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_metadata_profile validó formato UUID v4 canónico en perfil T.';
END $$;

-- 4.7 Perfil P: par no canónico (a >= b o invertido)
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES (
        '22222222-2222-4222-8222-222222222222', 
        'ADMINISTRATOR', 
        'COMPATIBILITY_DECLARED', 
        'INSTRUMENT_VERSION_COMPATIBILITY', 
        '{"version_a_id": "55555555-5555-4555-8555-555555555555", "version_b_id": "44444444-4444-4444-8444-444444444444"}'::jsonb
    );
    RAISE EXCEPTION 'Fallo: Se permitió par invertido (a > b) en perfil P';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_metadata_profile rechazó par no ordenado canónicamente en perfil P.';
END $$;

-- 4.8 Perfil P: autorreferencia (a = b)
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_user_id, actor_kind, action, target_type, metadata)
    VALUES (
        '22222222-2222-4222-8222-222222222222', 
        'ADMINISTRATOR', 
        'COMPATIBILITY_DECLARED', 
        'INSTRUMENT_VERSION_COMPATIBILITY', 
        '{"version_a_id": "44444444-4444-4444-8444-444444444444", "version_b_id": "44444444-4444-4444-8444-444444444444"}'::jsonb
    );
    RAISE EXCEPTION 'Fallo: Se permitió autorreferencia (a = b) en perfil P';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_metadata_profile rechazó autorreferencia en perfil P.';
END $$;

ROLLBACK;
