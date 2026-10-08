-- ==============================================================================
-- Yusay Platform — Migración V016: Validación Transaccional de Publicación
-- ==============================================================================
-- Valida la completitud y coherencia psicométrica estricta de una instrument_version
-- al momento de transición al estado PUBLISHED:
-- 1. Existencia de scoring_definition con método SUM.
-- 2. Existencia de al menos 1 pregunta (ítem).
-- 3. Al menos 2 opciones de respuesta por cada pregunta.
-- 4. Contribución de scoring definida para cada opción de respuesta.
-- 5. Cálculo preciso de límites de puntuación [Score_min, Score_max] respetando
--    la obligatoriedad (required = true: [min(contrib), max(contrib)];
--    required = false: [min(0, min(contrib)), max(0, max(contrib))]).
-- 6. Cobertura continua y sin solapamientos (partición exhaustiva) del rango completo
--    de puntuaciones posibles por parte del catálogo de interpretaciones.
-- ==============================================================================

CREATE OR REPLACE FUNCTION yusay.fn_validate_instrument_version_publication()
RETURNS trigger AS $$
DECLARE
    v_q_count int;
    v_opt_missing int;
    v_contrib_missing int;
    v_interp_count int;
    v_interp_overlap int;
    v_min_score int;
    v_max_score int;
    v_interp_min int;
    v_interp_max int;
    v_has_scoring_def boolean;
BEGIN
    IF NEW.status = 'PUBLISHED' AND OLD.status <> 'PUBLISHED' THEN
        -- 1. Definición de scoring SUM obligatoria
        SELECT EXISTS (
            SELECT 1 FROM yusay.scoring_definition
            WHERE instrument_version_id = NEW.instrument_version_id
              AND method = 'SUM'
        ) INTO v_has_scoring_def;

        IF NOT v_has_scoring_def THEN
            RAISE EXCEPTION 'No se puede publicar la versión %: falta definición de scoring SUM.',
                NEW.instrument_version_id
                USING ERRCODE = 'check_violation';
        END IF;

        -- 2. Al menos 1 pregunta
        SELECT count(*) INTO v_q_count
        FROM yusay.question
        WHERE instrument_version_id = NEW.instrument_version_id;

        IF v_q_count = 0 THEN
            RAISE EXCEPTION 'No se puede publicar la versión %: no contiene ninguna pregunta.',
                NEW.instrument_version_id
                USING ERRCODE = 'check_violation';
        END IF;

        -- 3. Al menos 2 opciones por pregunta
        SELECT count(*) INTO v_opt_missing
        FROM yusay.question q
        WHERE q.instrument_version_id = NEW.instrument_version_id
          AND (
              SELECT count(*)
              FROM yusay.answer_option ao
              WHERE ao.question_id = q.question_id
          ) < 2;

        IF v_opt_missing > 0 THEN
            RAISE EXCEPTION 'No se puede publicar la versión %: % pregunta(s) tienen menos de 2 opciones de respuesta.',
                NEW.instrument_version_id, v_opt_missing
                USING ERRCODE = 'check_violation';
        END IF;

        -- 4. Contribución de scoring en cada opción
        SELECT count(*) INTO v_contrib_missing
        FROM yusay.question q
        JOIN yusay.answer_option ao ON ao.question_id = q.question_id
        LEFT JOIN yusay.scoring_contribution sc ON sc.option_id = ao.option_id
        WHERE q.instrument_version_id = NEW.instrument_version_id
          AND sc.option_id IS NULL;

        IF v_contrib_missing > 0 THEN
            RAISE EXCEPTION 'No se puede publicar la versión %: % opción(es) carecen de scoring_contribution.',
                NEW.instrument_version_id, v_contrib_missing
                USING ERRCODE = 'check_violation';
        END IF;

        -- 5. Cálculo exacto de límites de puntuación según reglas de obligatoriedad
        WITH q_bounds AS (
            SELECT
                q.question_id,
                q.required,
                MIN(sc.contribution) AS opt_min,
                MAX(sc.contribution) AS opt_max
            FROM yusay.question q
            JOIN yusay.answer_option ao ON ao.question_id = q.question_id
            JOIN yusay.scoring_contribution sc ON sc.option_id = ao.option_id
            WHERE q.instrument_version_id = NEW.instrument_version_id
            GROUP BY q.question_id, q.required
        )
        SELECT
            COALESCE(SUM(CASE WHEN required THEN opt_min ELSE LEAST(0, opt_min) END), 0),
            COALESCE(SUM(CASE WHEN required THEN opt_max ELSE GREATEST(0, opt_max) END), 0)
        INTO v_min_score, v_max_score
        FROM q_bounds;

        -- 6. Validar interpretaciones (cobertura continua exacta)
        SELECT count(*) INTO v_interp_count
        FROM yusay.interpretation
        WHERE instrument_version_id = NEW.instrument_version_id;

        IF v_interp_count = 0 THEN
            RAISE EXCEPTION 'No se puede publicar la versión %: no tiene interpretaciones registradas.',
                NEW.instrument_version_id
                USING ERRCODE = 'check_violation';
        END IF;

        -- Continuidad y ausencia de solapamientos / huecos
        WITH ordered_interps AS (
            SELECT
                lower_bound,
                upper_bound,
                LAG(upper_bound) OVER (ORDER BY lower_bound) AS prev_upper
            FROM yusay.interpretation
            WHERE instrument_version_id = NEW.instrument_version_id
        )
        SELECT count(*) INTO v_interp_overlap
        FROM ordered_interps
        WHERE prev_upper IS NOT NULL AND lower_bound <> prev_upper + 1;

        IF v_interp_overlap > 0 THEN
            RAISE EXCEPTION 'No se puede publicar la versión %: las interpretaciones presentan solapamientos o discontinuidades.',
                NEW.instrument_version_id
                USING ERRCODE = 'check_violation';
        END IF;

        -- Extremos del rango de interpretaciones vs puntuaciones posibles
        SELECT MIN(lower_bound), MAX(upper_bound)
        INTO v_interp_min, v_interp_max
        FROM yusay.interpretation
        WHERE instrument_version_id = NEW.instrument_version_id;

        IF v_interp_min <> v_min_score OR v_interp_max <> v_max_score THEN
            RAISE EXCEPTION 'No se puede publicar la versión %: el rango de interpretaciones [%, %] no coincide con el rango de puntuaciones posibles [%, %].',
                NEW.instrument_version_id, v_interp_min, v_interp_max, v_min_score, v_max_score
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_validate_instrument_version_publication
    BEFORE UPDATE OF status ON yusay.instrument_version
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_validate_instrument_version_publication();
