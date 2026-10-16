-- ==============================================================================
-- Yusay Platform — Migración V018: Aritmética de DimensionScale a 64 bits
-- ==============================================================================
-- Corrige la divergencia entre las restricciones físicas de dimension_version
-- y el value object DimensionScale del dominio (aritmética de 64 bits, OQ-DOM-008):
-- la evaluación en int4 de (max_value - min_value) % step elevaba SQLSTATE 22003
-- (integer out of range) con escalas de rango extremo que el dominio acepta, y el
-- módulo por cero del CHECK elevaba 22012 (division_by_zero) cuando step = 0.
-- Alcance (corrección mínima aprobada, posterior a V017):
-- 1. ck_dimension_version_scale_endpoint: se conservan nombre y columnas; la
--    evaluación pasa a bigint y un CASE interno protege step <= 0, de modo que
--    ningún CHECK de escala puede elevar un error de ejecución para ninguna
--    tripla de int4 y la respuesta canónica 23514 de
--    ck_dimension_version_step_positive ya no depende del orden (no especificado)
--    de evaluación de constraints.
-- 2. Las funciones de V017 que realizan la misma aritmética se redefinen con
--    CREATE OR REPLACE (firma, atributos, guardas, mensajes y ERRCODE intactos;
--    solo la resta y el módulo se evalúan en bigint):
--      * fn_anchor_within_scale (G1/D-B2-1): desbordamiento de (p_value - p_min)
--        al insertar anclas en escalas de rango >= 2^31.
--      * fn_validate_dimension_version_anchor_scale (G4): desbordamiento de
--        (NEW.max_value - NEW.min_value) al cambiar la escala en DRAFT — ya
--        alcanzable hoy mediante UPDATE.
--      * fn_validate_dimension_version_activation (G2): mismo desbordamiento al
--        activar una versión de escala extrema.
-- Supersesión: esta migración reemplaza las definiciones de V004 (constraint) y
-- V017 (las tres funciones) sin alterar sus ficheros históricos. Los triggers no
-- se recrean: CREATE OR REPLACE conserva el OID de cada función, de modo que
-- pg_trigger.tgfoid sigue resolviendo a los cuerpos nuevos. No se tocan
-- ck_dimension_version_bounds, ck_dimension_version_step_positive ni la guarda G3.
-- Compatibilidad: las filas existentes satisfacen el nuevo CHECK (el residuo en
-- bigint coincide con el de int4 para toda fila que evaluaba sin error) y el
-- conjunto aceptado solo se amplía; ninguna sentencia válida previa cambia de
-- resultado. No hay datos que validar ni migrar, ni semillas que alterar.
-- Concurrencia: el DROP/ADD CONSTRAINT toma ACCESS EXCLUSIVE sobre
-- dimension_version (tabla catálogo, sin escritores concurrentes hasta B5) durante
-- la transacción de la migración; otras sesiones ven las definiciones anteriores
-- hasta el commit (DDL transaccional). La migración es atómica: cualquier fallo
-- la revierte por completo al estado anterior.
-- Reversibilidad: forward-only, como el resto de migraciones. Un revert exige el
-- swap inverso del CHECK y de las tres funciones, y solo es viable mientras no
-- exista ninguna versión con escala extrema (el CHECK antiguo elevaría 22003 al
-- validarse contra dichas filas). Irreversible en la práctica una vez materializada
-- una escala extrema; reversión entonces solo eliminando primero esas versiones.
-- Esta migración no implementa B5 ni B6, no altera status ni transiciones de
-- estado (B6 reservado) y no modifica dominio, aplicación ni documentación.
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. ck_dimension_version_scale_endpoint: resto y módulo en bigint con guarda
--    interna de step. CASE garantiza el orden de evaluación (mismo idioma que
--    V017 en fn_anchor_within_scale): con step <= 0 el CHECK responde true y
--    cede a ck_dimension_version_step_positive, que responde su 23514 canónico.
-- ------------------------------------------------------------------------------
ALTER TABLE yusay.dimension_version
    DROP CONSTRAINT ck_dimension_version_scale_endpoint;

ALTER TABLE yusay.dimension_version
    ADD CONSTRAINT ck_dimension_version_scale_endpoint
    CHECK (
        CASE
            WHEN step > 0 THEN ((max_value::bigint - min_value::bigint) % step::bigint) = 0
            ELSE true
        END
    );

COMMENT ON CONSTRAINT ck_dimension_version_scale_endpoint ON yusay.dimension_version IS
'Extremos alcanzables: (max_value - min_value) divisible entre step, evaluado en bigint (V018); con step <= 0 cede a ck_dimension_version_step_positive.';

-- ------------------------------------------------------------------------------
-- 2. G1/D-B2-1: fn_anchor_within_scale — mismas guardas CASE de V017 (NULL,
--    step <= 0, min >= max y rango, en ese orden, antes del módulo); el módulo
--    se evalúa en bigint para no desbordar (p_value - p_min) en escalas de
--    rango extremo. OR REPLACE conserva el OID: trg_guard_dimension_anchor_scale
--    y las llamadas desde G2/G4 resuelven al cuerpo nuevo sin recreación.
-- ------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION yusay.fn_anchor_within_scale(
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
        ELSE ((p_value::bigint - p_min::bigint) % p_step::bigint) = 0
    END;
$$;

COMMENT ON FUNCTION yusay.fn_anchor_within_scale(integer, integer, integer, integer) IS
'D-B2-1/V018: un valor pertenece a la escala si min <= value <= max y (value - min) % step = 0, con el módulo evaluado en bigint.';

-- ------------------------------------------------------------------------------
-- 3. G4: fn_validate_dimension_version_anchor_scale — idéntica a V017 salvo el
--    endpoint inline, ahora en bigint: con escalas extremas válidas el trigger
--    difiere a los CHECK de V004 en lugar de elevar 22003 en su propia resta.
-- ------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION yusay.fn_validate_dimension_version_anchor_scale()
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

    IF (NEW.max_value::bigint - NEW.min_value::bigint) % NEW.step::bigint <> 0 THEN
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
'G4/V018: al modificar min/max/step en DRAFT, todas las anclas existentes deben seguir perteneciendo a la escala (endpoint evaluado en bigint).';

-- ------------------------------------------------------------------------------
-- 4. G2: fn_validate_dimension_version_activation — idéntica a V017 salvo el
--    endpoint inline en bigint: la activación de una versión de escala extrema
--    revalida sus anclas sin desbordar en la resta de extremos.
-- ------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION yusay.fn_validate_dimension_version_activation()
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

    IF (NEW.max_value::bigint - NEW.min_value::bigint) % NEW.step::bigint <> 0 THEN
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
'G2/V018: la activación DRAFT -> ACTIVE revalida las anclas existentes contra la escala resultante con endpoint en bigint; cero anclas es válido (D-B2-2).';
