DO $$
DECLARE
    v_user_id uuid := '11111111-1111-4111-8111-111111111111';
    v_cnt int;
BEGIN
    RAISE NOTICE '=== TEST V012: Iniciar verificación de user_account ===';

    -- 1. Verificar existencia de user_account y ausencia de app_user
    IF NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'yusay' AND tablename = 'user_account') THEN
        RAISE EXCEPTION 'FALLO: yusay.user_account no existe';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'yusay' AND tablename = 'app_user') THEN
        RAISE EXCEPTION 'FALLO: yusay.app_user todavía existe';
    END IF;

    -- 2. Verificar PK e índice único de correo
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_user_account') THEN
        RAISE EXCEPTION 'FALLO: pk_user_account no encontrada';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'uq_user_account_email') THEN
        RAISE EXCEPTION 'FALLO: uq_user_account_email no encontrado';
    END IF;

    -- 3. Verificar las 7 FKs renombradas
    SELECT count(*) INTO v_cnt FROM pg_constraint WHERE conname IN (
        'fk_user_credential_user_account',
        'fk_administrator_user_account',
        'fk_email_verification_token_user_account',
        'fk_password_reset_token_user_account',
        'fk_assessment_attempt_user_account',
        'fk_check_in_user_account',
        'fk_audit_event_user_account'
    );
    IF v_cnt <> 7 THEN
        RAISE EXCEPTION 'FALLO: Se esperaban 7 FKs renombradas, encontradas %', v_cnt;
    END IF;

    -- 4. Prueba funcional de inserción e integridad relacional
    INSERT INTO yusay.user_account (user_id, email, adult_confirmed_at, status)
    VALUES (v_user_id, 'test_v012@yusay.local', clock_timestamp(), 'ACTIVE');

    INSERT INTO yusay.user_credential (user_id, password_hash)
    VALUES (v_user_id, '$argon2id$v=19$m=65536,t=3,p=4$dummyhash');

    INSERT INTO yusay.administrator (user_id)
    VALUES (v_user_id);

    INSERT INTO yusay.email_verification_token (user_id, token_hash, expires_at)
    VALUES (v_user_id, 'hash123', clock_timestamp() + interval '1 hour');

    INSERT INTO yusay.password_reset_token (user_id, token_hash, expires_at)
    VALUES (v_user_id, 'hash456', clock_timestamp() + interval '1 hour');

    -- Auditoría con actor_user_id (comb: USER, USER_REGISTERED, USER, metadata NULL)
    INSERT INTO yusay.audit_event (
        audit_event_id, occurred_at, actor_user_id, actor_kind, action, target_type, target_identifier, metadata
    ) VALUES (
        gen_random_uuid(), clock_timestamp(), v_user_id, 'USER',
        'USER_REGISTERED', 'USER', v_user_id::text, NULL
    );

    -- 5. Comprobar eliminación y comportamiento ON DELETE CASCADE / SET NULL
    DELETE FROM yusay.user_account WHERE user_id = v_user_id;

    IF EXISTS (SELECT 1 FROM yusay.user_credential WHERE user_id = v_user_id) THEN
        RAISE EXCEPTION 'FALLO: user_credential no fue eliminado en cascada';
    END IF;

    IF EXISTS (SELECT 1 FROM yusay.audit_event WHERE target_identifier = v_user_id::text AND actor_user_id IS NOT NULL) THEN
        RAISE EXCEPTION 'FALLO: audit_event.actor_user_id no fue seteado a NULL';
    END IF;

    -- Limpieza del evento de prueba
    DELETE FROM yusay.audit_event WHERE target_identifier = v_user_id::text;

    RAISE NOTICE '=== TEST V012: Completado con éxito sin discrepancias ===';
END $$;
