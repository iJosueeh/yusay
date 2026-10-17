-- ==============================================================================
-- Yusay Platform — Migración V020: Extensión Física Operacional administrative_provisioning_event (B8b-3A)
-- ==============================================================================
-- Motor: PostgreSQL 18
-- Documentación Normativa:
--   - docs/05-data/physical-model-v1/01-contexto-y-alcance.md (MP-PHYS-017)
--   - docs/05-data/physical-model-v1/02-decisiones-y-mapeo-fisico.md
--   - docs/05-data/physical-model-v1/03-integridad-e-indices.md (MP-PHYS-017)
--   - docs/05-data/physical-model-v1/04-transacciones-y-concurrencia.md (MP-PHYS-017)
--   - docs/05-data/physical-model-v1/05-privacidad-eliminacion-y-operacion.md (MP-PHYS-014)
--   - docs/06-decisions/README.md (OQ-ARCH-018 RESOLVED; OQ-NFR-004 y VF-04 OPEN)
--
-- Alcance y Reglas de Gobernanza:
--   1. Tabla operacional en PostgreSQL (Alternativa R1) para la trazabilidad fuera de
--      banda del aprovisionamiento y revocación en yusay.administrator.
--   2. Preserva estrictamente el modelo lógico normativo congelado v1.0 en sus 32
--      relaciones (R-001..R-032) y 147 columnas. No constituye una relación R-033.
--   3. target_user_id opera como referencia desacoplada sin FK para no bloquear la
--      eliminación física de cuentas personales (VF-04 permanece OPEN).
--   4. Neutraliza los permisos derivados de ALTER DEFAULT PRIVILEGES de V001 sobre
--      yusay_app y yusay_worker, así como cualquier privilegio sobre PUBLIC.
--   5. yusay_admin_ops recibe SELECT global e INSERT acotado por columna; se prohíbe
--      terminantemente el privilegio INSERT global sobre la tabla.
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.administrative_provisioning_event
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.administrative_provisioning_event (
    event_id uuid NOT NULL,
    target_user_id uuid NOT NULL,
    action text NOT NULL,
    operator_identifier text NOT NULL,
    ticket_ref text NULL,
    db_user text NOT NULL DEFAULT CURRENT_USER,
    client_addr inet NULL DEFAULT inet_client_addr(),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),

    CONSTRAINT pk_administrative_provisioning_event
        PRIMARY KEY (event_id),

    CONSTRAINT ck_administrative_provisioning_event_action
        CHECK (action IN ('ADMINISTRATOR_PROVISIONED', 'ADMINISTRATOR_REVOKED')),

    CONSTRAINT ck_administrative_provisioning_event_operator_not_empty
        CHECK (length(trim(operator_identifier)) > 0)
);

-- ------------------------------------------------------------------------------
-- 2. Índice para auditoría y trazabilidad por cuenta objetivo
-- ------------------------------------------------------------------------------
CREATE INDEX idx_administrative_provisioning_event_target_user
    ON yusay.administrative_provisioning_event (target_user_id);

-- ------------------------------------------------------------------------------
-- 3. Documentación en catálogo del motor
-- ------------------------------------------------------------------------------
COMMENT ON TABLE yusay.administrative_provisioning_event IS
'Bitácora relacional física inmutable de aprovisionamiento administrativo fuera de banda (MP-PHYS-017 / OQ-ARCH-018). Extensión operacional fuera del modelo lógico congelado v1.0.';

COMMENT ON COLUMN yusay.administrative_provisioning_event.event_id IS
'Identificador UUID v4 provisto por el runner antes del DML; actúa como clave primaria y token único de idempotencia determinista.';

COMMENT ON COLUMN yusay.administrative_provisioning_event.target_user_id IS
'Identificador de la cuenta objetivo habilitada o revocada. Referencia desacoplada sin FK hacia user_account para no bloquear la eliminación física de cuentas (VF-04 permanece OPEN).';

COMMENT ON COLUMN yusay.administrative_provisioning_event.action IS
'Acción operacional ejecutada: ADMINISTRATOR_PROVISIONED o ADMINISTRATOR_REVOKED.';

COMMENT ON COLUMN yusay.administrative_provisioning_event.operator_identifier IS
'Identificador autodeclarado del operador o servicio responsable fuera de banda (atribución personal no verificada por el motor relacional).';

COMMENT ON COLUMN yusay.administrative_provisioning_event.ticket_ref IS
'Referencia opcional de ticket de cambio o justificación externa (e.g. RFC o ticket de seguridad).';

COMMENT ON COLUMN yusay.administrative_provisioning_event.db_user IS
'Rol técnico autenticado en PostgreSQL que ejecutó la transacción (CURRENT_USER). Certifica el rol de base de datos, no una identidad humana verificada.';

COMMENT ON COLUMN yusay.administrative_provisioning_event.client_addr IS
'Dirección IP de origen de la conexión de administración capturada por el motor.';

COMMENT ON COLUMN yusay.administrative_provisioning_event.occurred_at IS
'Instante UTC generado automáticamente por clock_timestamp() en el momento de la inserción de la fila, no el instante de confirmación (COMMIT) de la transacción.';

-- ------------------------------------------------------------------------------
-- 4. Neutralización estricta de ALTER DEFAULT PRIVILEGES de V001 y de PUBLIC
-- ------------------------------------------------------------------------------
REVOKE ALL PRIVILEGES ON yusay.administrative_provisioning_event FROM yusay_app;
REVOKE ALL PRIVILEGES ON yusay.administrative_provisioning_event FROM yusay_worker;
REVOKE ALL PRIVILEGES ON yusay.administrative_provisioning_event FROM PUBLIC;

-- ------------------------------------------------------------------------------
-- 5. Privilegios mínimos acotados por columna para yusay_admin_ops
-- ------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'yusay_admin_ops') THEN
        GRANT USAGE ON SCHEMA yusay TO yusay_admin_ops;
        GRANT SELECT ON yusay.administrative_provisioning_event TO yusay_admin_ops;

        -- Concesión acotada por columna: prohíbe explícitamente manipular db_user, client_addr u occurred_at
        GRANT INSERT (
            event_id,
            target_user_id,
            action,
            operator_identifier,
            ticket_ref
        ) ON yusay.administrative_provisioning_event TO yusay_admin_ops;

        -- UPDATE y DELETE permanecen categóricamente no concedidos
    ELSE
        RAISE NOTICE '[V020] El rol yusay_admin_ops no existe: concesiones sobre administrative_provisioning_event omitidas. Crearlo fuera de Flyway (database/init/01-init-roles.sh o canal manual del despliegue) y reproducir las concesiones.';
    END IF;
END
$$;
