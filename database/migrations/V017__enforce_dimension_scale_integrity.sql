-- ==============================================================================
-- Yusay Platform — Migración V017: Integridad de escala de DimensionVersion
-- ==============================================================================
-- Cubre los gaps G1, G2, G3 y G4 de RF-021 sin alterar el modelo lógico v1.0:
-- 1. G1: toda inserción/actualización de dimension_anchor debe caer dentro de la
--    escala de su DimensionVersion, incluso en DRAFT (rango Y alineación al step).
-- 2. G2: la activación DRAFT -> ACTIVE revalida las anclas existentes contra la
--    escala resultante; sin cantidad mínima de anclas (cardinalidad 0..N intacta).
-- 3. G3: definition es inmutable en versiones ACTIVE o RETIRED (D-B2-3: una
--    sentencia que edite definition y active desde DRAFT se permite).
-- 4. G4: un cambio de escala en DRAFT revalida las anclas ya registradas.
-- Decisiones: D-B2-1 (rango + alineación al step), D-B2-2 (activación con cero
-- anclas permitida), D-B2-5 (validación de datos previos antes de instalar).
--
-- Semántica de errores:
--   - Violaciones de escala -> SQLSTATE 23514 (check_violation), igual que V015/V016.
--   - Referencia a una DimensionVersion inexistente -> se devuelve NEW y la
--     restricción de clave foránea eleva su 23503 canónico (no se sustituye).
--   - Escalas que violen los CHECK de V004 (min>=max, step<=0, endpoint): los
--     triggers ceden la palabra a esos CHECK (evitan además división por cero).
--
-- Concurrencia (coherente con el orden de bloqueos de RF-021: dimension ->
-- dimension_version -> dimension_anchor):
--   - G1 toma FOR SHARE sobre la fila padre; los UPDATE de escala/estado de V015
--     y de esta migración toman FOR UPDATE implícito. Ambos locks son mutuamente
--     excluyentes y se retienen hasta el fin de la transacción, por lo que la
--     inserción de anclas se serializa contra cambios de escala y activaciones
--     sobre la misma versión. Los SELECT internos de los disparadores (READ
--     COMMITTED) observan el estado ya commiteado tras adquirir el lock, de modo
--     que ninguna validación se ejecuta sobre un conjunto obsoleto.
--   - Ningún disparador bloquea la tabla dimension ni otras versiones: no se
--     introduce ningún orden de adquisición inverso ni ciclos de deadlock.
--   - Los disparadores de esta migración son objetos nuevos; no se reescribe
--     ninguna función de V001-V016.
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 0. Helper: pertenencia de un valor a una escala (rango + alineación al step)
--    CASE garantiza el orden de evaluación: el módulo solo se evalúa con
--    p_step > 0 (defensa ante cualquier llamada con escala inválida).
-- ------------------------------------------------------------------------------
CREATE FUNCTION yusay.fn_anchor_within_scale(
    p_value integer,
    p_min integer,
    p_max integer,
    p_step integer)
RETURNS boolean
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE
        WHEN p_min IS NULL OR p_max IS NULL OR p_step IS NULL THEN false
        WHEN p_step <= 0 THEN false
        WHEN p_min >= p_max THEN false
        WHEN p_value < p_min OR p_value > p_max THEN false
        ELSE ((p_value - p_min) % p_step) = 0
    END;
$$;

COMMENT ON FUNCTION yusay.fn_anchor_within_scale(integer, integer, integer, integer) IS
'D-B2-1: un valor pertenece a la escala si min <= value <= max y (value - min) % step = 0.';

-- ------------------------------------------------------------------------------
-- 1. D-B2-5: validación de los datos existentes ANTES de instalar los triggers
-- ------------------------------------------------------------------------------
DO $$
DECLARE
    v_bad bigint;
BEGIN
    SELECT count(*)
    INTO v_bad
    FROM yusay.dimension_anchor a
    JOIN yusay.dimension_version v
        ON v.dimension_version_id = a.dimension_version_id
    WHERE NOT yusay.fn_anchor_within_scale(a.value, v.min_value, v.max_value, v.step);

    IF v_bad > 0 THEN
        RAISE EXCEPTION 'V017: % anclaje(s) previos fuera de la escala de su versión; corregir los datos antes de instalar los triggers.', v_bad;
    END IF;
END;
$$;

-- ------------------------------------------------------------------------------
-- 2. G1: anclas dentro de la escala de su versión, incluso en DRAFT
--    El FOR SHARE propio hace el trigger auto-suficiente (y reentrante con el
--    FOR SHARE que V015 ya toma en trg_guard_dimension_anchor_immutability).
--    Si la versión padre no existe se cede el paso a la FK (23503).
-- ------------------------------------------------------------------------------
CREATE FUNCTION yusay.fn_guard_dimension_anchor_scale()
RETURNS trigger AS $$
DECLARE
    v_min integer;
    v_max integer;
    v_step integer;
BEGIN
    SELECT min_value, max_value, step
    INTO v_min, v_max, v_step
    FROM yusay.dimension_version
    WHERE dimension_version_id = NEW.dimension_version_id
    FOR SHARE;

    IF v_min IS NULL THEN
        -- Versión inexistente: la restricción de clave foránea responderá con
        -- su SQLSTATE 23503 canónico tras los triggers; no se sustituye por 23514.
        RETURN NEW;
    END IF;

    IF NOT yusay.fn_anchor_within_scale(NEW.value, v_min, v_max, v_step) THEN
        RAISE EXCEPTION 'El anclaje % está fuera de la escala [% , %] con step % de la versión %.',
            NEW.value, v_min, v_max, v_step, NEW.dimension_version_id
            USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION yusay.fn_guard_dimension_anchor_scale() IS
'G1: valida que el valor del anclaje pertenezca a la escala de su DimensionVersion en cualquier estado.';

CREATE TRIGGER trg_guard_dimension_anchor_scale
    BEFORE INSERT OR UPDATE ON yusay.dimension_anchor
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_dimension_anchor_scale();

-- ------------------------------------------------------------------------------
-- 3. G3: definition inmutable en ACTIVE/RETIRED
--    Disparador propio (UPDATE OF definition): V015 conserva íntegra su
--    fn_guard_dimension_version_immutability y esta guarda no se ejecuta en
--    actualizaciones que no toquen definition. Semántica OLD.status (D-B2-3):
--    editar definition en la misma sentencia que activa desde DRAFT se permite
--    y queda congelado el valor resultante.
-- ------------------------------------------------------------------------------
CREATE FUNCTION yusay.fn_guard_dimension_version_definition()
RETURNS trigger AS $$
BEGIN
    IF OLD.status IN ('ACTIVE', 'RETIRED')
       AND NEW.definition IS DISTINCT FROM OLD.definition THEN
        RAISE EXCEPTION 'No se permite alterar definition de una versión de dimensión en estado %.',
            OLD.status
            USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION yusay.fn_guard_dimension_version_definition() IS
'G3: definition es inmutable en versiones ACTIVE o RETIRED; una edición conjunta con la activación desde DRAFT se permite (D-B2-3).';

CREATE TRIGGER trg_guard_dimension_version_definition
    BEFORE UPDATE OF definition ON yusay.dimension_version
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_guard_dimension_version_definition();

-- ------------------------------------------------------------------------------
-- 4. G4: revalidación de anclas al modificar la escala de una versión DRAFT
--    Si la escala nueva viola los CHECK de V004 se cede la palabra a esos
--    CHECK (23514 con su mensaje) y se evita la división por cero del módulo.
--    En ACTIVE/RETIRED, trg_guard_dimension_version_immutability (orden
--    alfabético 'i' < 'v') rechaza la actualización antes de llegar aquí.
-- ------------------------------------------------------------------------------
CREATE FUNCTION yusay.fn_validate_dimension_version_anchor_scale()
RETURNS trigger AS $$
DECLARE
    v_bad integer;
BEGIN
    IF OLD.status <> 'DRAFT' THEN
        RETURN NEW;
    END IF;

    IF NEW.min_value >= NEW.max_value OR NEW.step <= 0 THEN
        RETURN NEW;
    END IF;

    IF (NEW.max_value - NEW.min_value) % NEW.step <> 0 THEN
        RETURN NEW;
    END IF;

    SELECT count(*)
    INTO v_bad
    FROM yusay.dimension_anchor
    WHERE dimension_version_id = NEW.dimension_version_id
      AND NOT yusay.fn_anchor_within_scale(value, NEW.min_value, NEW.max_value, NEW.step);

    IF v_bad > 0 THEN
        RAISE EXCEPTION 'El cambio de escala [% , %] con step % dejaría % anclaje(s) fuera de escala en la versión %.',
            NEW.min_value, NEW.max_value, NEW.step, v_bad, NEW.dimension_version_id
            USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION yusay.fn_validate_dimension_version_anchor_scale() IS
'G4: al modificar min/max/step en DRAFT, todas las anclas existentes deben seguir perteneciendo a la escala.';

CREATE TRIGGER trg_validate_dimension_version_anchor_scale
    BEFORE UPDATE OF min_value, max_value, step ON yusay.dimension_version
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_validate_dimension_version_anchor_scale();

-- ------------------------------------------------------------------------------
-- 5. G2: revalidación de anclas al activar (DRAFT -> ACTIVE), sin mínimo general
--    El guard de transición de V015 (orden alfabético 'g' < 'v') ya validó el
--    autómata de estados; solo se alcanza aquí la transición legal. Cero anclas
--    es válido (D-B2-2: cardinalidad 0..N intacta).
-- ------------------------------------------------------------------------------
CREATE FUNCTION yusay.fn_validate_dimension_version_activation()
RETURNS trigger AS $$
DECLARE
    v_bad integer;
BEGIN
    IF NOT (OLD.status = 'DRAFT' AND NEW.status = 'ACTIVE') THEN
        RETURN NEW;
    END IF;

    IF NEW.min_value >= NEW.max_value OR NEW.step <= 0 THEN
        RETURN NEW;
    END IF;

    IF (NEW.max_value - NEW.min_value) % NEW.step <> 0 THEN
        RETURN NEW;
    END IF;

    SELECT count(*)
    INTO v_bad
    FROM yusay.dimension_anchor
    WHERE dimension_version_id = NEW.dimension_version_id
      AND NOT yusay.fn_anchor_within_scale(value, NEW.min_value, NEW.max_value, NEW.step);

    IF v_bad > 0 THEN
        RAISE EXCEPTION 'No se puede activar la versión %: % anclaje(s) fuera de la escala [% , %] con step %.',
            NEW.dimension_version_id, v_bad, NEW.min_value, NEW.max_value, NEW.step
            USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION yusay.fn_validate_dimension_version_activation() IS
'G2: la activación DRAFT -> ACTIVE revalida las anclas existentes contra la escala resultante; cero anclas es válido (D-B2-2).';

CREATE TRIGGER trg_validate_dimension_version_activation
    BEFORE UPDATE OF status ON yusay.dimension_version
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_validate_dimension_version_activation();
