BEGIN;

DO $$
DECLARE
    v_inst_id uuid := gen_random_uuid();
    v_ver_id uuid := gen_random_uuid();
    v_q1 uuid := gen_random_uuid();
    v_q2_opt uuid := gen_random_uuid();
    v_opt1_a uuid := gen_random_uuid();
    v_opt1_b uuid := gen_random_uuid();
    v_opt2_a uuid := gen_random_uuid();
    v_opt2_b uuid := gen_random_uuid();
    v_err boolean;
BEGIN
    RAISE NOTICE '=== TEST V016: Iniciar validación exhaustiva de publicación de instrumentos ===';

    -- 1. Setup instrumento y versión en READY
    INSERT INTO yusay.instrument (instrument_id, code, name, description, purpose)
    VALUES (v_inst_id, 'INST_V016_TEST', 'Inst V016 Test', 'Desc', 'Wellness eval');

    INSERT INTO yusay.instrument_version (
        instrument_id, instrument_version_id, version, status,
        source_description, population, limitations
    ) VALUES (
        v_inst_id, v_ver_id, 1, 'READY', 'Source', 'Adults', 'None'
    );

    -- CASO 1: Publicar sin preguntas ni scoring -> Debe fallar por falta de scoring SUM
    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba fallo por falta de scoring_definition';
    END IF;

    -- Agregar scoring definition
    INSERT INTO yusay.scoring_definition (instrument_version_id, method)
    VALUES (v_ver_id, 'SUM');

    -- CASO 2: Publicar sin preguntas -> Debe fallar
    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba fallo por falta de preguntas';
    END IF;

    -- Agregar Pregunta 1
    INSERT INTO yusay.question (question_id, instrument_version_id, position, prompt, required)
    VALUES (v_q1, v_ver_id, 1, 'Pregunta 1', true);

    -- CASO 3: Publicar con pregunta sin opciones suficientes (< 2) -> Debe fallar
    INSERT INTO yusay.answer_option (option_id, question_id, position, label)
    VALUES (v_opt1_a, v_q1, 1, 'Opción A');

    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba fallo por tener menos de 2 opciones';
    END IF;

    -- Agregar segunda opción a Pregunta 1
    INSERT INTO yusay.answer_option (option_id, question_id, position, label)
    VALUES (v_opt1_b, v_q1, 2, 'Opción B');

    -- CASO 4: Publicar sin scoring_contribution para las opciones -> Debe fallar
    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba fallo por falta de scoring_contribution';
    END IF;

    -- Asignar contribuciones a Pregunta 1: Opción A = 0, Opción B = 10
    INSERT INTO yusay.scoring_contribution (option_id, question_id, instrument_version_id, contribution)
    VALUES (v_opt1_a, v_q1, v_ver_id, 0),
           (v_opt1_b, v_q1, v_ver_id, 10);

    -- CASO 5: Agregar Pregunta 2 OPCIONAL (required = false) con opciones [2, 5]
    -- Puntuación mínima si no responde = 0. Máxima = 5.
    -- Rango total esperado: Min = 0 + 0 = 0. Max = 10 + 5 = 15.
    INSERT INTO yusay.question (question_id, instrument_version_id, position, prompt, required)
    VALUES (v_q2_opt, v_ver_id, 2, 'Pregunta Opcional 2', false);

    INSERT INTO yusay.answer_option (option_id, question_id, position, label)
    VALUES (v_opt2_a, v_q2_opt, 1, 'Opción 2A'),
           (v_opt2_b, v_q2_opt, 2, 'Opción 2B');

    INSERT INTO yusay.scoring_contribution (option_id, question_id, instrument_version_id, contribution)
    VALUES (v_opt2_a, v_q2_opt, v_ver_id, 2),
           (v_opt2_b, v_q2_opt, v_ver_id, 5);

    -- CASO 6: Interpretaciones con discontinuidad (GAP) -> [0, 5] y [8, 15] (falta 6, 7)
    INSERT INTO yusay.interpretation (interpretation_id, instrument_version_id, label, description, lower_bound, upper_bound)
    VALUES (gen_random_uuid(), v_ver_id, 'Bajo', 'Desc', 0, 5),
           (gen_random_uuid(), v_ver_id, 'Alto', 'Desc', 8, 15);

    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba fallo por discontinuidad (gap) en interpretaciones';
    END IF;

    -- Limpiar interpretaciones erróneas
    DELETE FROM yusay.interpretation WHERE instrument_version_id = v_ver_id;

    -- CASO 7: Interpretaciones con solapamiento (OVERLAP) -> [0, 8] y [8, 15] (8 repetido)
    INSERT INTO yusay.interpretation (interpretation_id, instrument_version_id, label, description, lower_bound, upper_bound)
    VALUES (gen_random_uuid(), v_ver_id, 'Bajo', 'Desc', 0, 8),
           (gen_random_uuid(), v_ver_id, 'Alto', 'Desc', 8, 15);

    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba fallo por solapamiento (overlap) en interpretaciones';
    END IF;

    -- Limpiar interpretaciones erróneas
    DELETE FROM yusay.interpretation WHERE instrument_version_id = v_ver_id;

    -- CASO 8: Interpretaciones contiguas pero que NO cubren el rango total calculado [0, 15]
    -- Ej. [0, 10] (falta 11-15)
    INSERT INTO yusay.interpretation (interpretation_id, instrument_version_id, label, description, lower_bound, upper_bound)
    VALUES (gen_random_uuid(), v_ver_id, 'Rango Incompleto', 'Desc', 0, 10);

    v_err := false;
    BEGIN
        UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;
    EXCEPTION WHEN check_violation THEN
        v_err := true;
    END;
    IF NOT v_err THEN
        RAISE EXCEPTION 'FALLO: Se esperaba fallo por cobertura incompleta del rango [0, 15]';
    END IF;

    -- Limpiar interpretaciones erróneas
    DELETE FROM yusay.interpretation WHERE instrument_version_id = v_ver_id;

    -- CASO 9: Cobertura EXACTA y CONTINUA del rango calculado [0, 15]:
    -- Interp 1: [0, 7]
    -- Interp 2: [8, 15]
    INSERT INTO yusay.interpretation (interpretation_id, instrument_version_id, label, description, lower_bound, upper_bound)
    VALUES (gen_random_uuid(), v_ver_id, 'Nivel A', 'Desc A', 0, 7),
           (gen_random_uuid(), v_ver_id, 'Nivel B', 'Desc B', 8, 15);

    -- Publicación debe ser EXITOSA
    UPDATE yusay.instrument_version SET status = 'PUBLISHED' WHERE instrument_version_id = v_ver_id;

    IF (SELECT status FROM yusay.instrument_version WHERE instrument_version_id = v_ver_id) <> 'PUBLISHED' THEN
        RAISE EXCEPTION 'FALLO: El estado de la versión debía ser PUBLISHED';
    END IF;

    RAISE NOTICE '=== TEST V016: Todas las validaciones de publicación ejecutadas y verificadas con éxito ===';
END $$;

ROLLBACK;
