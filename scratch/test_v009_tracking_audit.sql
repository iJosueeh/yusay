-- ==============================================================================
-- Test Suite: Migración V009 (Seguimiento Diario y Auditoría)
-- ==============================================================================
\set ON_ERROR_STOP on

BEGIN;

-- 1. Setup: Crear datos base para las pruebas (User, Dimension, DimensionVersion, ContextTag)
DO $$
DECLARE
    v_user_id uuid := '11111111-1111-4111-8111-111111111111';
    v_user2_id uuid := '22222222-2222-4222-8222-222222222222';
    v_dim_id uuid := '33333333-3333-4333-8333-333333333333';
    v_dim_version_id uuid := '44444444-4444-4444-8444-444444444444';
    v_tag_id uuid := '55555555-5555-4555-8555-555555555555';
    v_check_in_id uuid := '66666666-6666-4666-8666-666666666666';
    v_audit_id uuid := '77777777-7777-4777-8777-777777777777';
BEGIN
    -- Usuario de prueba 1
    INSERT INTO yusay.app_user (user_id, email, created_at, adult_confirmed_at, status)
    VALUES (v_user_id, 'tracker@yusay.test', clock_timestamp(), clock_timestamp(), 'ACTIVE');

    -- Usuario de prueba 2
    INSERT INTO yusay.app_user (user_id, email, created_at, adult_confirmed_at, status)
    VALUES (v_user2_id, 'actor@yusay.test', clock_timestamp(), clock_timestamp(), 'ACTIVE');

    -- Dimensión de catálogo
    INSERT INTO yusay.dimension (dimension_id, code, name, description)
    VALUES (v_dim_id, 'ENERGY', 'Nivel de Energía', 'Escala de energía percibida');

    -- Versión activa de la dimensión
    INSERT INTO yusay.dimension_version (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
    VALUES (v_dim_version_id, v_dim_id, 1, 'Definición v1 de Energía', 1, 10, 1, 'ACTIVE');

    -- ContextTag
    INSERT INTO yusay.context_tag (context_tag_id, code, name, description, status)
    VALUES (v_tag_id, 'WORK', 'Trabajo', 'Entorno laboral', 'ACTIVE');

    -- 2. Test Positivo: Inserción válida de CheckIn
    INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at, note)
    VALUES (v_check_in_id, v_user_id, clock_timestamp(), clock_timestamp(), 'Me siento bien');

    -- 3. Test Positivo: Inserción de Measurement vinculada
    INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
    VALUES (v_check_in_id, v_dim_id, v_dim_version_id, 8);

    -- 4. Test Positivo: Inserción de CheckInContextTag vinculada
    INSERT INTO yusay.check_in_context_tag (check_in_id, context_tag_id)
    VALUES (v_check_in_id, v_tag_id);

    -- 5. Test Positivo: Inserción de AuditEvent con actor_user_id y jsonb metadata
    INSERT INTO yusay.audit_event (audit_event_id, actor_user_id, actor_kind, action, target_type, target_identifier, metadata)
    VALUES (
        v_audit_id, 
        v_user2_id, 
        'USER', 
        'USER_REGISTERED', 
        'USER', 
        v_user2_id::text, 
        '{"note": "Registro inicial de cuenta"}'::jsonb
    );

    RAISE NOTICE 'Inserciones válidas completadas exitosamente.';
END $$;

-- 6. Test Negativo: PK de CheckIn duplicada
DO $$
BEGIN
    INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at)
    VALUES ('66666666-6666-4666-8666-666666666666', '11111111-1111-4111-8111-111111111111', clock_timestamp(), clock_timestamp());
    RAISE EXCEPTION 'Fallo: Se permitió PK duplicada en check_in';
EXCEPTION
    WHEN unique_violation THEN
        RAISE NOTICE 'Aprobado: pk_check_in rechazó duplicado.';
END $$;

-- 7. Test Negativo: Ventana temporal inválida (recorded_at > created_at)
DO $$
BEGIN
    INSERT INTO yusay.check_in (user_id, recorded_at, created_at)
    VALUES ('11111111-1111-4111-8111-111111111111', clock_timestamp() + interval '1 hour', clock_timestamp());
    RAISE EXCEPTION 'Fallo: Se permitió recorded_at futuro superior a created_at';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_check_in_recorded_window rechazó recorded_at > created_at.';
END $$;

-- 8. Test Negativo: Ventana temporal inválida (recorded_at < created_at - 168h)
DO $$
BEGIN
    INSERT INTO yusay.check_in (user_id, recorded_at, created_at)
    VALUES ('11111111-1111-4111-8111-111111111111', clock_timestamp() - interval '170 hours', clock_timestamp());
    RAISE EXCEPTION 'Fallo: Se permitió recorded_at anterior a 168 horas previas';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_check_in_recorded_window rechazó recorded_at < created_at - 168h.';
END $$;

-- 9. Test Negativo: revision no positiva (revision <= 0)
DO $$
BEGIN
    INSERT INTO yusay.check_in (user_id, recorded_at, created_at, revision)
    VALUES ('11111111-1111-4111-8111-111111111111', clock_timestamp(), clock_timestamp(), 0);
    RAISE EXCEPTION 'Fallo: Se permitió revision = 0';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_check_in_revision_positive rechazó revision <= 0.';
END $$;

-- 10. Test Negativo: Measurement PK duplicada (dos mediciones de la misma dimension en un check_in)
DO $$
BEGIN
    INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
    VALUES ('66666666-6666-4666-8666-666666666666', '33333333-3333-4333-8333-333333333333', '44444444-4444-4444-8444-444444444444', 5);
    RAISE EXCEPTION 'Fallo: Se permitió duplicar dimension_id en el mismo check_in';
EXCEPTION
    WHEN unique_violation THEN
        RAISE NOTICE 'Aprobado: pk_measurement rechazó dimensión duplicada en check_in.';
END $$;

-- 11. Test Negativo: Measurement FK compuesta inválida (dimensión no corresponde a dimension_version)
DO $$
DECLARE
    v_dim_otra uuid := '88888888-8888-4888-8888-888888888888';
BEGIN
    INSERT INTO yusay.dimension (dimension_id, code, name, description)
    VALUES (v_dim_otra, 'MOOD', 'Estado de Ánimo', 'Otra dimensión');

    -- Intentar asociar v_dim_otra con la versión de v_dim_id (44444444-...)
    INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
    VALUES ('66666666-6666-4666-8666-666666666666', v_dim_otra, '44444444-4444-4444-8444-444444444444', 7);
    RAISE EXCEPTION 'Fallo: Se permitió Measurement con dimensión cruzada y version ajena';
EXCEPTION
    WHEN foreign_key_violation THEN
        RAISE NOTICE 'Aprobado: fk_measurement_dimension_version rechazó versión no perteneciente a dimensión.';
END $$;

-- 12. Test Negativo: ContextTag duplicado en el mismo check_in
DO $$
BEGIN
    INSERT INTO yusay.check_in_context_tag (check_in_id, context_tag_id)
    VALUES ('66666666-6666-4666-8666-666666666666', '55555555-5555-4555-8555-555555555555');
    RAISE EXCEPTION 'Fallo: Se permitió duplicar context_tag_id en check_in';
EXCEPTION
    WHEN unique_violation THEN
        RAISE NOTICE 'Aprobado: pk_check_in_context_tag rechazó duplicado de etiqueta.';
END $$;

-- 13. Test Negativo: AuditEvent actor_kind inválido
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_kind, action, target_type)
    VALUES ('HACKER', 'SIGN_IN_FAILED', 'AUTHENTICATION');
    RAISE EXCEPTION 'Fallo: Se permitió actor_kind inválido';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_actor_kind rechazó actor_kind no perteneciente al catálogo.';
END $$;

-- 14. Test Negativo: AuditEvent action vacía
DO $$
BEGIN
    INSERT INTO yusay.audit_event (actor_kind, action, target_type)
    VALUES ('USER', '   ', 'USER');
    RAISE EXCEPTION 'Fallo: Se permitió action vacía o sólo espacios';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'Aprobado: ck_audit_event_action_not_empty rechazó acción en blanco.';
END $$;

-- 15. Test Privacidad y Cascada: Eliminación de usuario tracker
-- Debe purgar en cascada check_in, measurement y check_in_context_tag
DO $$
DECLARE
    v_ci_count integer;
    v_m_count integer;
    v_ct_count integer;
BEGIN
    DELETE FROM yusay.app_user WHERE user_id = '11111111-1111-4111-8111-111111111111';

    SELECT count(*) INTO v_ci_count FROM yusay.check_in WHERE check_in_id = '66666666-6666-4666-8666-666666666666';
    SELECT count(*) INTO v_m_count FROM yusay.measurement WHERE check_in_id = '66666666-6666-4666-8666-666666666666';
    SELECT count(*) INTO v_ct_count FROM yusay.check_in_context_tag WHERE check_in_id = '66666666-6666-4666-8666-666666666666';

    IF v_ci_count <> 0 OR v_m_count <> 0 OR v_ct_count <> 0 THEN
        RAISE EXCEPTION 'Fallo: La cascada de eliminación de usuario no purgó el seguimiento completo.';
    END IF;

    RAISE NOTICE 'Aprobado: Eliminación de app_user purgó en cascada check_in, measurement y check_in_context_tag.';
END $$;

-- 16. Test Privacidad y Desvinculación: Eliminación de usuario actor
-- En audit_event, actor_user_id debe pasar a NULL (ON DELETE SET NULL), preservando el evento
DO $$
DECLARE
    v_event_actor_is_null boolean;
    v_event_count integer;
BEGIN
    DELETE FROM yusay.app_user WHERE user_id = '22222222-2222-4222-8222-222222222222';

    SELECT count(*), bool_and(actor_user_id IS NULL) 
    INTO v_event_count, v_event_actor_is_null
    FROM yusay.audit_event 
    WHERE audit_event_id = '77777777-7777-4777-8777-777777777777';

    IF v_event_count <> 1 THEN
        RAISE EXCEPTION 'Fallo: El evento de auditoría desapareció tras borrar el usuario.';
    END IF;

    IF NOT v_event_actor_is_null THEN
        RAISE EXCEPTION 'Fallo: actor_user_id no fue desvinculado a NULL.';
    END IF;

    RAISE NOTICE 'Aprobado: Eliminación de app_user desvinculó actor_user_id a NULL preservando el evento de auditoría.';
END $$;

-- 17. Test Restricción de borrado de Catálogos (ON DELETE RESTRICT en dimension_version)
DO $$
DECLARE
    v_u_tmp uuid := '99999999-9999-4999-8999-999999999999';
    v_ci_tmp uuid := 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
BEGIN
    INSERT INTO yusay.app_user (user_id, email, created_at, adult_confirmed_at, status)
    VALUES (v_u_tmp, 'temp@yusay.test', clock_timestamp(), clock_timestamp(), 'ACTIVE');

    INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at)
    VALUES (v_ci_tmp, v_u_tmp, clock_timestamp(), clock_timestamp());

    INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
    VALUES (v_ci_tmp, '33333333-3333-4333-8333-333333333333', '44444444-4444-4444-8444-444444444444', 9);

    -- Intentar borrar dimension_version referenciada
    BEGIN
        DELETE FROM yusay.dimension_version WHERE dimension_version_id = '44444444-4444-4444-8444-444444444444';
        RAISE EXCEPTION 'Fallo: Se permitió borrar dimension_version referenciada por una medición';
    EXCEPTION
        WHEN foreign_key_violation OR SQLSTATE '23001' THEN
            RAISE NOTICE 'Aprobado: fk_measurement_dimension_version impidió eliminar versión con mediciones históricas (RESTRICT).';
    END;
END $$;

-- Revertir todos los datos de prueba
ROLLBACK;
