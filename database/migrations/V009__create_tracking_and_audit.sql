-- ==============================================================================
-- Yusay Platform — Migración V009: Seguimiento Diario y Auditoría de Seguridad
-- ==============================================================================
-- Entidades:
--   1. yusay.check_in (R-020 / CHECK_IN)
--   2. yusay.measurement (R-021 / MEASUREMENT)
--   3. yusay.check_in_context_tag (R-023 / CHECK_IN_CONTEXT_TAG)
--   4. yusay.audit_event (R-032 / AUDIT_EVENT)
--
-- Restricciones e integridad según el diseño físico v1.0 aprobado:
--   - PKs:
--       * pk_check_in (check_in_id uuid IDF-12)
--       * pk_measurement (check_in_id, dimension_id)
--       * pk_check_in_context_tag (check_in_id, context_tag_id)
--       * pk_audit_event (audit_event_id uuid IDF-16)
--   - FKs simples y compuestas:
--       * fk_check_in_app_user -> app_user(user_id) (FK-024, ON DELETE CASCADE)
--       * fk_measurement_check_in -> check_in(check_in_id) (FK-025, ON DELETE CASCADE)
--       * fk_measurement_dimension_version -> dimension_version(dimension_id, dimension_version_id) (FK-026, ON DELETE RESTRICT, usa URA-006)
--       * fk_check_in_context_tag_check_in -> check_in(check_in_id) (FK-027, ON DELETE CASCADE)
--       * fk_check_in_context_tag_context_tag -> context_tag(context_tag_id) (FK-028, ON DELETE RESTRICT)
--       * fk_audit_event_app_user -> app_user(user_id) (FK-041, ON DELETE SET NULL - Desvinculación por privacidad)
--   - CHECKs normativos:
--       * ck_check_in_revision_positive (revision > 0)
--       * ck_check_in_recorded_window (recorded_at >= created_at - interval '168 hours' AND recorded_at <= created_at)
--       * ck_audit_event_actor_kind (actor_kind IN ('USER', 'ADMINISTRATOR', 'ANONYMOUS'))
--       * ck_audit_event_action_not_empty (length(trim(action)) > 0)
--       * ck_audit_event_target_type_not_empty (length(trim(target_type)) > 0)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.check_in (R-020)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.check_in (
    check_in_id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    recorded_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NULL,
    revision integer NOT NULL DEFAULT 1,
    note text NULL,
    CONSTRAINT pk_check_in PRIMARY KEY (check_in_id),
    CONSTRAINT fk_check_in_app_user FOREIGN KEY (user_id)
        REFERENCES yusay.app_user (user_id)
        ON DELETE CASCADE,
    CONSTRAINT ck_check_in_revision_positive CHECK (revision > 0),
    CONSTRAINT ck_check_in_recorded_window CHECK (
        recorded_at >= (created_at - interval '168 hours')
        AND recorded_at <= created_at
    )
);

COMMENT ON TABLE yusay.check_in IS 'Registro personal de seguimiento multidimensional con nota opcional y control de concurrencia optimista.';
COMMENT ON COLUMN yusay.check_in.check_in_id IS 'Identificador único del check-in (UUID v4 raíz IDF-12).';
COMMENT ON COLUMN yusay.check_in.user_id IS 'Usuario propietario del registro de seguimiento.';
COMMENT ON COLUMN yusay.check_in.recorded_at IS 'Instante UTC de ocurrencia subjetiva del seguimiento, acotado a [created_at - 168h, created_at].';
COMMENT ON COLUMN yusay.check_in.created_at IS 'Instante UTC inmutable de creación técnica del registro.';
COMMENT ON COLUMN yusay.check_in.updated_at IS 'Instante UTC de la última edición confirmada; ausente (NULL) antes de la primera edición.';
COMMENT ON COLUMN yusay.check_in.revision IS 'Versión de concurrencia optimista; inicia en 1 e incrementa en 1 por edición atómica confirmada.';
COMMENT ON COLUMN yusay.check_in.note IS 'Nota personal libre y confidencial asociada al check-in (opcional).';

-- ------------------------------------------------------------------------------
-- 2. Tabla: yusay.measurement (R-021)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.measurement (
    check_in_id uuid NOT NULL,
    dimension_id uuid NOT NULL,
    dimension_version_id uuid NOT NULL,
    value integer NOT NULL,
    CONSTRAINT pk_measurement PRIMARY KEY (check_in_id, dimension_id),
    CONSTRAINT fk_measurement_check_in FOREIGN KEY (check_in_id)
        REFERENCES yusay.check_in (check_in_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_measurement_dimension_version FOREIGN KEY (dimension_id, dimension_version_id)
        REFERENCES yusay.dimension_version (dimension_id, dimension_version_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.measurement IS 'Medición de un valor entero sobre una dimensión específica de seguimiento ligada a su versión histórica.';
COMMENT ON COLUMN yusay.measurement.check_in_id IS 'Check-in al que pertenece la medición.';
COMMENT ON COLUMN yusay.measurement.dimension_id IS 'Dimensión de seguimiento evaluada (máximo una medición por dimensión en el check-in).';
COMMENT ON COLUMN yusay.measurement.dimension_version_id IS 'Versión histórica exacta de la escala de la dimensión (coherencia garantizada por FK compuesta hacia URA-006).';
COMMENT ON COLUMN yusay.measurement.value IS 'Valor entero reportado dentro de la escala activa histórica.';

-- ------------------------------------------------------------------------------
-- 3. Tabla: yusay.check_in_context_tag (R-023)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.check_in_context_tag (
    check_in_id uuid NOT NULL,
    context_tag_id uuid NOT NULL,
    CONSTRAINT pk_check_in_context_tag PRIMARY KEY (check_in_id, context_tag_id),
    CONSTRAINT fk_check_in_context_tag_check_in FOREIGN KEY (check_in_id)
        REFERENCES yusay.check_in (check_in_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_check_in_context_tag_context_tag FOREIGN KEY (context_tag_id)
        REFERENCES yusay.context_tag (context_tag_id)
        ON DELETE RESTRICT
);

COMMENT ON TABLE yusay.check_in_context_tag IS 'Asociación muchos a muchos entre un check-in de seguimiento y etiquetas de contexto activas.';
COMMENT ON COLUMN yusay.check_in_context_tag.check_in_id IS 'Check-in asociado.';
COMMENT ON COLUMN yusay.check_in_context_tag.context_tag_id IS 'Etiqueta de contexto vinculada (solo etiquetas activas al momento de vincular).';

-- ------------------------------------------------------------------------------
-- 4. Tabla: yusay.audit_event (R-032)
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.audit_event (
    audit_event_id uuid NOT NULL DEFAULT gen_random_uuid(),
    actor_user_id uuid NULL,
    actor_kind text NOT NULL,
    action text NOT NULL,
    target_type text NOT NULL,
    target_identifier text NULL,
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    metadata jsonb NULL,
    CONSTRAINT pk_audit_event PRIMARY KEY (audit_event_id),
    CONSTRAINT fk_audit_event_app_user FOREIGN KEY (actor_user_id)
        REFERENCES yusay.app_user (user_id)
        ON DELETE SET NULL,
    CONSTRAINT ck_audit_event_actor_kind CHECK (actor_kind IN ('USER', 'ADMINISTRATOR', 'ANONYMOUS')),
    CONSTRAINT ck_audit_event_action_not_empty CHECK (length(trim(action)) > 0),
    CONSTRAINT ck_audit_event_target_type_not_empty CHECK (length(trim(target_type)) > 0)
);

COMMENT ON TABLE yusay.audit_event IS 'Registro estructurado de auditoría de seguridad y catálogo, excluyendo estrictamente datos privados de bienestar.';
COMMENT ON COLUMN yusay.audit_event.audit_event_id IS 'Identificador único opaco del evento de auditoría (UUID v4 raíz IDF-16).';
COMMENT ON COLUMN yusay.audit_event.actor_user_id IS 'Usuario actor del evento; desvinculado a NULL al eliminarse la cuenta por privacidad (ON DELETE SET NULL).';
COMMENT ON COLUMN yusay.audit_event.actor_kind IS 'Naturaleza del actor en el momento del evento: USER, ADMINISTRATOR o ANONYMOUS.';
COMMENT ON COLUMN yusay.audit_event.action IS 'Acción administrativa o de seguridad ejecutada (catálogo cerrado de 28 acciones normativas).';
COMMENT ON COLUMN yusay.audit_event.target_type IS 'Tipo de entidad o categoría destino del evento (catálogo cerrado de 12 tipos normativos).';
COMMENT ON COLUMN yusay.audit_event.target_identifier IS 'Identificador opcional del destino como texto canónico (sin FK física directa, sujeto a desvinculación).';
COMMENT ON COLUMN yusay.audit_event.occurred_at IS 'Instante UTC de ocurrencia del evento; retención máxima de 180 períodos de 24 horas.';
COMMENT ON COLUMN yusay.audit_event.metadata IS 'Carga estructurada jsonb conforme a los perfiles N/F/D/C/E/T/P, sin información privada de bienestar.';
