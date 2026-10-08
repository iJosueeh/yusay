DO $$
DECLARE
    v_user_1 uuid := gen_random_uuid();
    v_user_2 uuid := gen_random_uuid();
    v_hash_1 text := 'hash_unique_token_test_abc';
    v_dup_error boolean := false;
BEGIN
    RAISE NOTICE '=== TEST V013: Iniciar verificación de unicidad de tokens e índices ===';

    -- Crear usuarios de prueba
    INSERT INTO yusay.user_account (user_id, email, adult_confirmed_at, status)
    VALUES (v_user_1, 'test_v013_1@yusay.local', clock_timestamp(), 'ACTIVE'),
           (v_user_2, 'test_v013_2@yusay.local', clock_timestamp(), 'ACTIVE');

    -- 1. Test unicidad en email_verification_token
    INSERT INTO yusay.email_verification_token (user_id, token_hash, expires_at)
    VALUES (v_user_1, v_hash_1, clock_timestamp() + interval '1 hour');

    BEGIN
        INSERT INTO yusay.email_verification_token (user_id, token_hash, expires_at)
        VALUES (v_user_2, v_hash_1, clock_timestamp() + interval '1 hour');
    EXCEPTION WHEN unique_violation THEN
        v_dup_error := true;
    END;

    IF NOT v_dup_error THEN
        RAISE EXCEPTION 'FALLO: Se esperaba violación de unicidad en email_verification_token.token_hash';
    END IF;

    -- 2. Test unicidad en password_reset_token
    v_dup_error := false;
    INSERT INTO yusay.password_reset_token (user_id, token_hash, expires_at)
    VALUES (v_user_1, v_hash_1, clock_timestamp() + interval '1 hour');

    BEGIN
        INSERT INTO yusay.password_reset_token (user_id, token_hash, expires_at)
        VALUES (v_user_2, v_hash_1, clock_timestamp() + interval '1 hour');
    EXCEPTION WHEN unique_violation THEN
        v_dup_error := true;
    END;

    IF NOT v_dup_error THEN
        RAISE EXCEPTION 'FALLO: Se esperaba violación de unicidad en password_reset_token.token_hash';
    END IF;

    -- 3. Verificar existencia de los índices
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_check_in_user_recorded') THEN
        RAISE EXCEPTION 'FALLO: idx_check_in_user_recorded no existe';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_assessment_attempt_user_started') THEN
        RAISE EXCEPTION 'FALLO: idx_assessment_attempt_user_started no existe';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_audit_event_occurred_at') THEN
        RAISE EXCEPTION 'FALLO: idx_audit_event_occurred_at no existe';
    END IF;

    -- Limpieza de datos de prueba
    DELETE FROM yusay.user_account WHERE user_id IN (v_user_1, v_user_2);

    RAISE NOTICE '=== TEST V013: Unicidad y definición de índices verificadas exitosamente ===';
END $$;

-- 4. Pruebas de rendimiento con EXPLAIN ANALYZE
EXPLAIN ANALYZE
SELECT check_in_id, recorded_at
FROM yusay.check_in
WHERE user_id = '00000000-0000-0000-0000-000000000000'::uuid
ORDER BY recorded_at DESC
LIMIT 10;

EXPLAIN ANALYZE
SELECT attempt_id, started_at
FROM yusay.assessment_attempt
WHERE user_id = '00000000-0000-0000-0000-000000000000'::uuid
ORDER BY started_at DESC
LIMIT 10;

EXPLAIN ANALYZE
SELECT audit_event_id, occurred_at
FROM yusay.audit_event
ORDER BY occurred_at DESC
LIMIT 50;
