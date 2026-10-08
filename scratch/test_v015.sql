BEGIN;

DO $$
DECLARE
    v_inst_id uuid := gen_random_uuid();
    v_ver_id uuid := gen_random_uuid();
    v_other_ver_id uuid := gen_random_uuid();
    v_qid uuid := gen_random_uuid();
    v_opt_id uuid := gen_random_uuid();
    v_interp_id uuid := gen_random_uuid();
    v_dim_id uuid := gen_random_uuid();
    v_dim_ver_id uuid := gen_random_uuid();
    v_now timestamptz := clock_timestamp();
    v_err boolean;
BEGIN
    RAISE NOTICE '=== TEST V015: Iniciar validación de transiciones e inmutabilidad ===';

    -- 1. Setup entidades base
    INSERT INTO yusay.instrument (instrument_id, code, name, description, purpose)
    VALUES (v_inst_id, 'INST_V015_TEST', 'Inst V015 Test', 'Desc', 'Purpose');

    INSERT INTO yusay.instrument_version (
        instrument_id, instrument_version_id, version, status,
        source_description, population, limitations
    ) VALUES 
        (v_inst_id, v_ver_id, 1, 'DRAFT', 'Src', 'Pop', 'Lim'),
        (v_inst_id, v_other_ver_id, 2, 'DRAFT', 'Src 2', 'Pop 2', 'Lim 2');

    INSERT INTO yusay.dimension (dimension_id, code, name, description)
    VALUES (v_dim_id, 'DIM_V015_TEST', 'Dim V015 Test', 'Desc');

    INSERT INTO yusay.dimension_version (
        dimension_id, dimension_version_id, version, definition, status,
        min_value, max_value, step
    ) VALUES (
        v_dim_id, v_dim_ver_id, 1, 'Scale definition', 'DRAFT', 0, 10, 1
    );

    -- 2. Transiciones válidas e inválidas de instrument_version
    -- Inválida: DRAFT -> PUBLISHED directamente
    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba check_violation en transición directa DRAFT -> PUBLISHED';
    END IF;

    -- Válida: DRAFT -> READY -> DRAFT -> READY -> PUBLISHED
    UPDATE yusay.instrument_version SET status = 'READY' WHERE instrument_version_id = v_ver_id;
    UPDATE yusay.instrument_version SET status = 'DRAFT' WHERE instrument_version_id = v_ver_id;

    -- Agregar contenido en DRAFT (debe permitirlo)
    INSERT INTO yusay.question (question_id, instrument_version_id, position, prompt, required)
    VALUES (v_qid, v_ver_id, 1, 'Pregunta 1', true);

    INSERT INTO yusay.answer_option (option_id, question_id, position, label)
    VALUES (v_opt_id, v_qid, 1, 'Opción 1');

    INSERT INTO yusay.scoring_definition (instrument_version_id, method)
    VALUES (v_ver_id, 'SUM');

    INSERT INTO yusay.scoring_contribution (option_id, question_id, instrument_version_id, contribution)
    VALUES (v_opt_id, v_qid, v_ver_id, 5);

    INSERT INTO yusay.interpretation (interpretation_id, instrument_version_id, label, description, lower_bound, upper_bound)
    VALUES (v_interp_id, v_ver_id, 'Normal', 'Desc', 0, 10);

    -- Transición a READY y luego a PUBLISHED
    UPDATE yusay.instrument_version SET status = 'READY' WHERE instrument_version_id = v_ver_id;
    UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;

    -- Inválida: PUBLISHED -> READY
    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'READY' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba check_violation en transición PUBLISHED -> READY';
    END IF;

    -- 3. Inmutabilidad de entidades hijas en versión PUBLISHED
    -- Modificar pregunta
    v_err := false;
    BEGIN
        UPDATE yusay.question SET prompt = 'Texto modificado' WHERE question_id = v_qid;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba rechazo al actualizar pregunta en versión PUBLISHED';
    END IF;

    -- Modificar opción de respuesta
    v_err := false;
    BEGIN
        UPDATE yusay.answer_option SET label = 'Opción modificada' WHERE option_id = v_opt_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba rechazo al actualizar answer_option en versión PUBLISHED';
    END IF;

    -- Insertar nueva pregunta
    v_err := false;
    BEGIN
        INSERT INTO yusay.question (question_id, instrument_version_id, position, prompt, required)
        VALUES (gen_random_uuid(), v_ver_id, 2, 'Pregunta 2', true);
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba rechazo al insertar pregunta en versión PUBLISHED';
    END IF;

    -- Eliminar pregunta
    v_err := false;
    BEGIN
        DELETE FROM yusay.question WHERE question_id = v_qid;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba rechazo al eliminar pregunta en versión PUBLISHED';
    END IF;

    -- 4. Prohibición de reasociación de versión
    v_err := false;
    BEGIN
        UPDATE yusay.question SET instrument_version_id = v_other_ver_id WHERE question_id = v_qid;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba rechazo al reasociar instrument_version_id';
    END IF;

    -- 5. Transiciones e inmutabilidad de dimension_version
    -- DRAFT -> ACTIVE
    INSERT INTO yusay.dimension_anchor (dimension_version_id, value, label)
    VALUES (v_dim_ver_id, 0, 'Mínimo'), (v_dim_ver_id, 10, 'Máximo');

    UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = v_dim_ver_id;

    -- Modificar anclaje en ACTIVE
    v_err := false;
    BEGIN
        UPDATE yusay.dimension_anchor SET label = 'Cambiado' WHERE dimension_version_id = v_dim_ver_id AND value = 0;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba rechazo al modificar dimension_anchor en versión ACTIVE';
    END IF;

    -- Modificar escala en ACTIVE
    v_err := false;
    BEGIN
        UPDATE yusay.dimension_version SET max_value = 20 WHERE dimension_version_id = v_dim_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba rechazo al modificar escala de dimension_version ACTIVE';
    END IF;

    -- Inválida: ACTIVE -> DRAFT
    v_err := false;
    BEGIN
        UPDATE yusay.dimension_version SET status = 'DRAFT' WHERE dimension_version_id = v_dim_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba check_violation en transición ACTIVE -> DRAFT';
    END IF;

    -- Válida: ACTIVE -> RETIRED
    UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = v_dim_ver_id;

    -- Inválida: RETIRED -> ACTIVE
    v_err := false;
    BEGIN
        UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = v_dim_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba check_violation en transición RETIRED -> ACTIVE';
    END IF;

    RAISE NOTICE '=== TEST V015: Transiciones e inmutabilidad probadas exitosamente ===';
END $$;

ROLLBACK;
