DO $$
DECLARE
    v_user_id uuid := gen_random_uuid();
    v_inst_id uuid := gen_random_uuid();
    v_ver_id uuid := gen_random_uuid();
    v_attempt_1 uuid := gen_random_uuid();
    v_attempt_2 uuid := gen_random_uuid();
    v_now timestamptz := clock_timestamp();
    v_err boolean;
BEGIN
    RAISE NOTICE '=== TEST V014: Verificación de restricciones temporales de assessment_attempt ===';

    -- 1. Setup entidades necesarias
    INSERT INTO yusay.user_account (user_id, email, adult_confirmed_at, status)
    VALUES (v_user_id, 'test_v014@yusay.local', v_now, 'ACTIVE');

    INSERT INTO yusay.instrument (instrument_id, code, name, description, purpose)
    VALUES (v_inst_id, 'INST_TEST_V014', 'Instrument Test V014', 'Description', 'Evaluation of wellness');

    INSERT INTO yusay.instrument_version (
        instrument_id, instrument_version_id, version,
        status, source_description, population, limitations
    ) VALUES (
        v_inst_id, v_ver_id, 1,
        'PUBLISHED', 'Source desc', 'General adult population', 'None'
    );

    -- 2. Caso Válido: IN_PROGRESS con ended_at NULL
    INSERT INTO yusay.assessment_attempt (
        attempt_id, user_id, instrument_id, instrument_version_id,
        status, started_at, expires_at, ended_at
    ) VALUES (
        v_attempt_1, v_user_id, v_inst_id, v_ver_id,
        'IN_PROGRESS', v_now, v_now + interval '1 hour', NULL
    );

    -- 3. Caso Inválido: IN_PROGRESS con ended_at NOT NULL -> debe fallar
    v_err := false;
    BEGIN
        INSERT INTO yusay.assessment_attempt (
            attempt_id, user_id, instrument_id, instrument_version_id,
            status, started_at, expires_at, ended_at
        ) VALUES (
            gen_random_uuid(), v_user_id, v_inst_id, v_ver_id,
            'IN_PROGRESS', v_now, v_now + interval '1 hour', v_now + interval '10 minutes'
        );
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba check_violation para IN_PROGRESS con ended_at NOT NULL';
    END IF;

    -- 4. Caso Inválido: SUBMITTED con ended_at NULL -> debe fallar
    v_err := false;
    BEGIN
        INSERT INTO yusay.assessment_attempt (
            attempt_id, user_id, instrument_id, instrument_version_id,
            status, started_at, expires_at, ended_at
        ) VALUES (
            gen_random_uuid(), v_user_id, v_inst_id, v_ver_id,
            'SUBMITTED', v_now, v_now + interval '1 hour', NULL
        );
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba check_violation para SUBMITTED con ended_at NULL';
    END IF;

    -- 5. Caso Inválido: SUBMITTED con ended_at anterior a started_at -> debe fallar
    v_err := false;
    BEGIN
        INSERT INTO yusay.assessment_attempt (
            attempt_id, user_id, instrument_id, instrument_version_id,
            status, started_at, expires_at, ended_at
        ) VALUES (
            gen_random_uuid(), v_user_id, v_inst_id, v_ver_id,
            'SUBMITTED', v_now, v_now + interval '1 hour', v_now - interval '10 minutes'
        );
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba check_violation para SUBMITTED con ended_at < started_at';
    END IF;

    -- 6. Caso Válido: Transición de IN_PROGRESS a SUBMITTED con ended_at >= started_at
    UPDATE yusay.assessment_attempt
    SET status = 'SUBMITTED',
        ended_at = v_now + interval '30 minutes'
    WHERE attempt_id = v_attempt_1;

    -- 7. Casos Válidos: EXPIRED y CANCELLED
    INSERT INTO yusay.assessment_attempt (
        attempt_id, user_id, instrument_id, instrument_version_id,
        status, started_at, expires_at, ended_at
    ) VALUES
    (gen_random_uuid(), v_user_id, v_inst_id, v_ver_id, 'EXPIRED', v_now, v_now + interval '1 hour', v_now + interval '1 hour 5 minutes'),
    (gen_random_uuid(), v_user_id, v_inst_id, v_ver_id, 'CANCELLED', v_now, v_now + interval '1 hour', v_now + interval '5 minutes');

    -- Limpieza de entidades de prueba
    DELETE FROM yusay.assessment_attempt WHERE instrument_version_id = v_ver_id;
    DELETE FROM yusay.instrument_version WHERE instrument_version_id = v_ver_id;
    DELETE FROM yusay.instrument WHERE instrument_id = v_inst_id;
    DELETE FROM yusay.user_account WHERE user_id = v_user_id;

    RAISE NOTICE '=== TEST V014: Todas las reglas temporales validadas exitosamente ===';
END $$;
