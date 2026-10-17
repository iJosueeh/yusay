-- ==============================================================================
-- Yusay Platform — Migración V019: Mínimo privilegio sobre yusay.administrator (B8b-1)
-- ==============================================================================
-- Motor: PostgreSQL 18
-- Documentación Normativa:
--   - docs/05-data/physical-model-v1/05-privacidad-eliminacion-y-operacion.md (MP-PHYS-014)
--   - docs/06-decisions/README.md (OQ-ARCH-017 RESOLVED; OQ-ARCH-018 sigue OPEN)
--
-- Objetivo (B8b-1, endurecimiento de privilegios sin código productivo nuevo):
--   1. yusay_app conserva SOLO SELECT sobre yusay.administrator: es el privilegio
--      que exige AdministratorAuthorizationHandler (política Administrator,
--      OQ-ARCH-017). INSERT, UPDATE y DELETE se revocan: la API no escribe la
--      habilitación administrativa por ningún camino del código.
--   2. yusay_worker queda sin privilegios sobre yusay.administrator: su alcance
--      aprobado (MP-PHYS-014) es purga de intentos caducados y retención de
--      auditoría (audit_event); no existe dependencia legítima sobre la
--      habilitación administrativa (verificado en migraciones, código y scripts).
--   3. yusay_admin_ops (rol operativo de alta/baja fuera de banda, conforme a
--      OQ-ARCH-018(a)) recibe SELECT, INSERT y DELETE, sin UPDATE y sin
--      WITH GRANT OPTION.
--
-- Entorno sin el rol (compatibilidad explícita, sin crear roles desde Flyway):
--   yusay_migrator no tiene CREATEROLE (MP-PHYS-014), por lo que esta migración
--   NO crea yusay_admin_ops. Si el rol aún no existe, sus concesiones se omiten
--   con un NOTICE visible en el log de Flyway: crearlo fuera de Flyway
--   (database/init/01-init-roles.sh en inicialización local, o el canal manual
--   del despliegue) y reproducir después las concesiones de este bloque. La
--   configuración incompleta no se oculta.
--
-- Pendientes declarados (fuera de alcance de B8b-1):
--   - Procedimiento operacional de alta/baja de administradores y su bitácora:
--     pendientes de B8b-2. OQ-ARCH-018, OQ-NFR-004 y hosting siguen OPEN.
--   - Enmienda documental de MP-PHYS-014 (lista de roles y declaración general
--     de DML de yusay_app): pendiente de la resolución de OQ-ARCH-018.
--   - ALTER DEFAULT PRIVILEGES de V001 no se modifica (no autorizado en B8b-1).
--   - Eliminación de cuentas de usuario: no implementada (VF-04 pendiente).
--
-- Efecto sobre decisiones aprobadas: la política Administrator, el manejo de JWT
-- y el 403 por revocación (OQ-ARCH-017) no cambian; tampoco cambia el
-- comportamiento de la API en ejecución (nunca escribe administrator y
-- audit_event conserva su INSERT de yusay_app, precedente V015).
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. yusay_app: solo lectura sobre la habilitación administrativa
--    (precedente: revocaciones puntuales sobre tablas concretas en V015).
-- ------------------------------------------------------------------------------
REVOKE INSERT, UPDATE, DELETE ON yusay.administrator FROM yusay_app;

-- ------------------------------------------------------------------------------
-- 2. yusay_worker: sin privilegios sobre administrator (sin dependencia legítima;
--    su purga opera sobre audit_event y los intentos caducados).
-- ------------------------------------------------------------------------------
REVOKE ALL PRIVILEGES ON yusay.administrator FROM yusay_worker;

-- ------------------------------------------------------------------------------
-- 3. yusay_admin_ops: concesiones mínimas solo si el rol ya existe.
--    Flyway no crea roles; la ausencia se hace explícita con NOTICE.
-- ------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'yusay_admin_ops') THEN
        GRANT USAGE ON SCHEMA yusay TO yusay_admin_ops;
        GRANT SELECT, INSERT, DELETE ON yusay.administrator TO yusay_admin_ops;
        -- Sin UPDATE y sin WITH GRANT OPTION (ambos ausentes por diseño).
    ELSE
        RAISE NOTICE '[V019] El rol yusay_admin_ops no existe: concesiones sobre yusay.administrator omitidas. Crearlo fuera de Flyway (database/init/01-init-roles.sh o canal manual del despliegue) y reproducir las concesiones de este bloque. Procedimiento de alta/baja y bitácora operacional: pendientes de B8b-2; OQ-ARCH-018 sigue OPEN.';
    END IF;
END
$$;
