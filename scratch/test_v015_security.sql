-- Test como postgres / yusay_migrator para setup
BEGIN;

DO $$
DECLARE
    v_user_id uuid := gen_random_uuid();
    v_event_id uuid := gen_random_uuid();
BEGIN
    RAISE NOTICE '=== TEST V015 SECURITY: Iniciando pruebas de privilegios mínimos en audit_event ===';

    -- Crear usuario con rol yusay_app
    INSERT INTO yusay.user_account (user_id, email, adult_confirmed_at, status)
    VALUES (v_user_id, 'sec_test@yusay.local', clock_timestamp(), 'ACTIVE');

    -- Insertar evento de auditoría como usuario
    INSERT INTO yusay.audit_event (
        audit_event_id, occurred_at, actor_user_id, actor_kind, action, target_type, target_identifier, metadata
    ) VALUES (
        v_event_id, clock_timestamp(), v_user_id, 'USER',
        'USER_REGISTERED', 'USER', v_user_id::text, NULL
    );
END $$;

COMMIT;

-- Probar operaciones con el rol yusay_app
\c yusay yusay_app

-- 1. yusay_app puede insertar evento
INSERT INTO yusay.audit_event (
    audit_event_id, occurred_at, actor_user_id, actor_kind, action, target_type, target_identifier, metadata
) VALUES (
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', clock_timestamp(), NULL, 'ANONYMOUS',
    'EMAIL_VERIFICATION_TOKEN_ISSUED', 'USER', 'unknown', NULL
);

-- 2. yusay_app NO puede hacer UPDATE
DO $$
DECLARE
    v_err boolean := false;
BEGIN
    BEGIN
        UPDATE yusay.audit_event SET action = 'HACKED' WHERE audit_event_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
    EXCEPTION WHEN insufficient_privilege THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: yusay_app no debió tener permiso de UPDATE en audit_event';
    END IF;
    RAISE NOTICE 'CONFIRMADO: yusay_app no tiene permiso de UPDATE en audit_event';
END $$;

-- 3. yusay_app NO puede hacer DELETE directo
DO $$
DECLARE
    v_err boolean := false;
BEGIN
    BEGIN
        DELETE FROM yusay.audit_event WHERE audit_event_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
    EXCEPTION WHEN insufficient_privilege THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: yusay_app no debió tener permiso de DELETE directo en audit_event';
    END IF;
    RAISE NOTICE 'CONFIRMADO: yusay_app no tiene permiso de DELETE en audit_event';
END $$;

-- 4. yusay_app eliminando user_account dispara ON DELETE SET NULL en audit_event sin error
DELETE FROM yusay.user_account WHERE email = 'sec_test@yusay.local';

-- Probar operaciones con el rol yusay_worker
\c yusay yusay_worker

-- 5. yusay_worker NO puede hacer INSERT
DO $$
DECLARE
    v_err boolean := false;
BEGIN
    BEGIN
        INSERT INTO yusay.audit_event (
            audit_event_id, occurred_at, actor_user_id, actor_kind, action, target_type, target_identifier, metadata
        ) VALUES (
            gen_random_uuid(), clock_timestamp(), NULL, 'ANONYMOUS',
            'EMAIL_VERIFICATION_TOKEN_ISSUED', 'USER', 'unknown', NULL
        );
    EXCEPTION WHEN insufficient_privilege THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: yusay_worker no debió tener permiso de INSERT en audit_event';
    END IF;
    RAISE NOTICE 'CONFIRMADO: yusay_worker no tiene permiso de INSERT en audit_event';
END $$;

-- 6. yusay_worker SI puede purgar registros viejos (DELETE)
DELETE FROM yusay.audit_event WHERE audit_event_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

-- Limpieza final con postgres
\c yusay postgres
DELETE FROM yusay.audit_event WHERE target_identifier = 'sec_test@yusay.local' OR actor_kind = 'USER';
