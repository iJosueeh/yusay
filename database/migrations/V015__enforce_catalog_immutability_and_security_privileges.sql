-- ==============================================================================
-- Yusay Platform — Migración V015: Inmutabilidad de Catálogos y Mínimo Privilegio
-- ==============================================================================
-- 1. Transiciones estrictas de ciclo de vida (instrument_version y dimension_version).
-- 2. Inmutabilidad de preguntas, opciones, scoring, interpretaciones y anclajes en versiones publicadas/activas/retiradas.
-- 3. Bloqueo de concurrencia segura (FOR SHARE en mutaciones hijas frente a FOR UPDATE en publicación).
-- 4. Prohibición de reasociación de claves foráneas entre versiones.
-- 5. Privilegios mínimos: audit_event como append-only para yusay_app (sin UPDATE/DELETE manual).
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Funciones y disparadores de transición de estado
-- ------------------------------------------------------------------------------

-- 1.1 Transición de instrument_version
CREATE OR REPLACE FUNCTION yusay.fn_guard_instrument_version_status_transition()
RETURNS trigger AS $$
BEGIN
    IF OLD.status = NEW.status THEN
        RETURN NEW;
    END IF;

    -- Validar transiciones permitidas del autómata de estados:
    -- DRAFT -> READY
    -- READY -> DRAFT (retorno editorial)
    -- READY -> PUBLISHED (publicación formal)
    -- PUBLISHED -> RETIRED (retiro)
    IF (OLD.status = 'DRAFT' AND NEW.status = 'READY')
       OR (OLD.status = 'READY' AND NEW.status IN ('DRAFT', 'PUBLISHED'))
       OR (OLD.status = 'PUBLISHED' AND NEW.status = 'RETIRED') THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION 'Transición de estado inválida para instrument_version %: no se permite de % a %.',
        OLD.instrument_version_id, OLD.status, NEW.status
        USING ERRCODE = 'check_violation';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_instrument_version_status
    BEFORE UPDATE OF status ON yusay.instrument_version
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_instrument_version_status_transition();

-- 1.2 Transición de dimension_version
CREATE OR REPLACE FUNCTION yusay.fn_guard_dimension_version_status_transition()
RETURNS trigger AS $$
BEGIN
    IF OLD.status = NEW.status THEN
        RETURN NEW;
    END IF;

    -- DRAFT -> ACTIVE -> RETIRED (terminal)
    IF (OLD.status = 'DRAFT' AND NEW.status = 'ACTIVE')
       OR (OLD.status = 'ACTIVE' AND NEW.status = 'RETIRED') THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION 'Transición de estado inválida para dimension_version %: no se permite de % a %.',
        OLD.dimension_version_id, OLD.status, NEW.status
        USING ERRCODE = 'check_violation';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_dimension_version_status
    BEFORE UPDATE OF status ON yusay.dimension_version
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_dimension_version_status_transition();

-- ------------------------------------------------------------------------------
-- 2. Inmutabilidad de instrument_version y dimension_version al bloquearse
-- ------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION yusay.fn_guard_instrument_version_immutability()
RETURNS trigger AS $$
BEGIN
    IF OLD.status IN ('PUBLISHED', 'RETIRED') THEN
        IF OLD.instrument_id <> NEW.instrument_id OR OLD.version <> NEW.version THEN
            RAISE EXCEPTION 'No se permite alterar la identidad (instrument_id, version) de una versión publicada o retirada.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_instrument_version_immutability
    BEFORE UPDATE ON yusay.instrument_version
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_instrument_version_immutability();

CREATE OR REPLACE FUNCTION yusay.fn_guard_dimension_version_immutability()
RETURNS trigger AS $$
BEGIN
    IF OLD.status IN ('ACTIVE', 'RETIRED') THEN
        IF OLD.dimension_id <> NEW.dimension_id
           OR OLD.version <> NEW.version
           OR OLD.min_value <> NEW.min_value
           OR OLD.max_value <> NEW.max_value
           OR OLD.step <> NEW.step THEN
            RAISE EXCEPTION 'No se permite alterar la escala ni identidad de una versión de dimensión activa o retirada.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_dimension_version_immutability
    BEFORE UPDATE ON yusay.dimension_version
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_dimension_version_immutability();

-- ------------------------------------------------------------------------------
-- 3. Inmutabilidad de entidades hijas de instrument_version
--    (Incluye concurrencia FOR SHARE en versión padre y prohibición de reasociación)
-- ------------------------------------------------------------------------------

-- 3.1 question
CREATE OR REPLACE FUNCTION yusay.fn_guard_question_immutability()
RETURNS trigger AS $$
DECLARE
    v_ver_id uuid;
    v_status text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_ver_id := OLD.instrument_version_id;
    ELSE
        v_ver_id := NEW.instrument_version_id;
        IF TG_OP = 'UPDATE' AND OLD.instrument_version_id <> NEW.instrument_version_id THEN
            RAISE EXCEPTION 'No se permite reasociar una pregunta a otra instrument_version.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT status INTO v_status
    FROM yusay.instrument_version
    WHERE instrument_version_id = v_ver_id
    FOR SHARE;

    IF v_status IN ('PUBLISHED', 'RETIRED') THEN
        RAISE EXCEPTION 'Operación rechazada: no se permite alterar preguntas en versión % con estado %.',
            v_ver_id, v_status
            USING ERRCODE = 'check_violation';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_question_immutability
    BEFORE INSERT OR UPDATE OR DELETE ON yusay.question
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_question_immutability();

-- 3.2 answer_option
CREATE OR REPLACE FUNCTION yusay.fn_guard_answer_option_immutability()
RETURNS trigger AS $$
DECLARE
    v_qid uuid;
    v_ver_id uuid;
    v_status text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_qid := OLD.question_id;
    ELSE
        v_qid := NEW.question_id;
        IF TG_OP = 'UPDATE' AND OLD.question_id <> NEW.question_id THEN
            RAISE EXCEPTION 'No se permite reasociar una opción de respuesta a otra pregunta.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT q.instrument_version_id, iv.status
    INTO v_ver_id, v_status
    FROM yusay.question q
    JOIN yusay.instrument_version iv ON iv.instrument_version_id = q.instrument_version_id
    WHERE q.question_id = v_qid
    FOR SHARE OF iv;

    IF v_status IN ('PUBLISHED', 'RETIRED') THEN
        RAISE EXCEPTION 'Operación rechazada: no se permite alterar opciones en versión % con estado %.',
            v_ver_id, v_status
            USING ERRCODE = 'check_violation';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_answer_option_immutability
    BEFORE INSERT OR UPDATE OR DELETE ON yusay.answer_option
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_answer_option_immutability();

-- 3.3 scoring_definition
CREATE OR REPLACE FUNCTION yusay.fn_guard_scoring_definition_immutability()
RETURNS trigger AS $$
DECLARE
    v_ver_id uuid;
    v_status text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_ver_id := OLD.instrument_version_id;
    ELSE
        v_ver_id := NEW.instrument_version_id;
        IF TG_OP = 'UPDATE' AND OLD.instrument_version_id <> NEW.instrument_version_id THEN
            RAISE EXCEPTION 'No se permite reasociar una definición de scoring a otra instrument_version.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT status INTO v_status
    FROM yusay.instrument_version
    WHERE instrument_version_id = v_ver_id
    FOR SHARE;

    IF v_status IN ('PUBLISHED', 'RETIRED') THEN
        RAISE EXCEPTION 'Operación rechazada: no se permite alterar scoring_definition en versión % con estado %.',
            v_ver_id, v_status
            USING ERRCODE = 'check_violation';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_scoring_definition_immutability
    BEFORE INSERT OR UPDATE OR DELETE ON yusay.scoring_definition
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_scoring_definition_immutability();

-- 3.4 scoring_contribution
CREATE OR REPLACE FUNCTION yusay.fn_guard_scoring_contribution_immutability()
RETURNS trigger AS $$
DECLARE
    v_ver_id uuid;
    v_status text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_ver_id := OLD.instrument_version_id;
    ELSE
        v_ver_id := NEW.instrument_version_id;
        IF TG_OP = 'UPDATE' AND (
            OLD.instrument_version_id <> NEW.instrument_version_id
            OR OLD.question_id <> NEW.question_id
            OR OLD.option_id <> NEW.option_id
        ) THEN
            RAISE EXCEPTION 'No se permite reasociar referencias de scoring_contribution.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT status INTO v_status
    FROM yusay.instrument_version
    WHERE instrument_version_id = v_ver_id
    FOR SHARE;

    IF v_status IN ('PUBLISHED', 'RETIRED') THEN
        RAISE EXCEPTION 'Operación rechazada: no se permite alterar scoring_contribution en versión % con estado %.',
            v_ver_id, v_status
            USING ERRCODE = 'check_violation';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_scoring_contribution_immutability
    BEFORE INSERT OR UPDATE OR DELETE ON yusay.scoring_contribution
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_scoring_contribution_immutability();

-- 3.5 interpretation
CREATE OR REPLACE FUNCTION yusay.fn_guard_interpretation_immutability()
RETURNS trigger AS $$
DECLARE
    v_ver_id uuid;
    v_status text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_ver_id := OLD.instrument_version_id;
    ELSE
        v_ver_id := NEW.instrument_version_id;
        IF TG_OP = 'UPDATE' AND OLD.instrument_version_id <> NEW.instrument_version_id THEN
            RAISE EXCEPTION 'No se permite reasociar una interpretación a otra instrument_version.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT status INTO v_status
    FROM yusay.instrument_version
    WHERE instrument_version_id = v_ver_id
    FOR SHARE;

    IF v_status IN ('PUBLISHED', 'RETIRED') THEN
        RAISE EXCEPTION 'Operación rechazada: no se permite alterar interpretaciones en versión % con estado %.',
            v_ver_id, v_status
            USING ERRCODE = 'check_violation';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_interpretation_immutability
    BEFORE INSERT OR UPDATE OR DELETE ON yusay.interpretation
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_interpretation_immutability();

-- 3.6 instrument_version_reference
CREATE OR REPLACE FUNCTION yusay.fn_guard_instrument_version_reference_immutability()
RETURNS trigger AS $$
DECLARE
    v_ver_id uuid;
    v_status text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_ver_id := OLD.instrument_version_id;
    ELSE
        v_ver_id := NEW.instrument_version_id;
        IF TG_OP = 'UPDATE' AND OLD.instrument_version_id <> NEW.instrument_version_id THEN
            RAISE EXCEPTION 'No se permite reasociar una referencia bibliográfica a otra instrument_version.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT status INTO v_status
    FROM yusay.instrument_version
    WHERE instrument_version_id = v_ver_id
    FOR SHARE;

    IF v_status IN ('PUBLISHED', 'RETIRED') THEN
        RAISE EXCEPTION 'Operación rechazada: no se permite alterar referencias en versión % con estado %.',
            v_ver_id, v_status
            USING ERRCODE = 'check_violation';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_instrument_version_reference_immutability
    BEFORE INSERT OR UPDATE OR DELETE ON yusay.instrument_version_reference
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_instrument_version_reference_immutability();

-- ------------------------------------------------------------------------------
-- 4. Inmutabilidad de dimension_anchor
-- ------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION yusay.fn_guard_dimension_anchor_immutability()
RETURNS trigger AS $$
DECLARE
    v_dim_ver_id uuid;
    v_status text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_dim_ver_id := OLD.dimension_version_id;
    ELSE
        v_dim_ver_id := NEW.dimension_version_id;
        IF TG_OP = 'UPDATE' AND OLD.dimension_version_id <> NEW.dimension_version_id THEN
            RAISE EXCEPTION 'No se permite reasociar un anclaje a otra dimension_version.'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT status INTO v_status
    FROM yusay.dimension_version
    WHERE dimension_version_id = v_dim_ver_id
    FOR SHARE;

    IF v_status IN ('ACTIVE', 'RETIRED') THEN
        RAISE EXCEPTION 'Operación rechazada: no se permite alterar anclajes en dimension_version % con estado %.',
            v_dim_ver_id, v_status
            USING ERRCODE = 'check_violation';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guard_dimension_anchor_immutability
    BEFORE INSERT OR UPDATE OR DELETE ON yusay.dimension_anchor
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_dimension_anchor_immutability();

-- ------------------------------------------------------------------------------
-- 5. Seguridad y Mínimo Privilegio en audit_event
-- ------------------------------------------------------------------------------

-- yusay_app: Append-only para eventos de auditoría (revocar UPDATE y DELETE manual)
REVOKE UPDATE, DELETE ON yusay.audit_event FROM yusay_app;

-- yusay_worker: Exclusivamente lectura y purga de retención histórica (sin INSERT/UPDATE)
REVOKE INSERT, UPDATE ON yusay.audit_event FROM yusay_worker;
