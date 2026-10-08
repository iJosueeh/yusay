-- ==============================================================================
-- Yusay Platform — Migración Inicial V001: Esquema Base y Privilegios Mínimos
-- ==============================================================================
-- Motor: PostgreSQL 18
-- Documentación Normativa:
--   - docs/05-data/physical-model-v1/01-contexto-y-alcance.md
--   - docs/05-data/physical-model-v1/05-privacidad-eliminacion-y-operacion.md (MP-PHYS-014)
-- ==============================================================================

-- 1. Asegurar existencia del esquema unificado del producto
CREATE SCHEMA IF NOT EXISTS yusay;

-- 2. Asegurar existencia de roles técnicos en entornos gestionados (Neon DB, Supabase, etc.)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'yusay_app') THEN
        CREATE ROLE yusay_app;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'yusay_worker') THEN
        CREATE ROLE yusay_worker;
    END IF;
END
$$;

-- 3. Otorgar permisos de uso sobre el esquema a los roles técnicos de aplicación y mantenimiento
GRANT USAGE ON SCHEMA yusay TO yusay_app;
GRANT USAGE ON SCHEMA yusay TO yusay_worker;

-- 3. Configurar privilegios por defecto para todas las futuras tablas creadas por el rol migrador
-- Rol de Aplicación (yusay_app): Exclusivamente DML (SELECT, INSERT, UPDATE, DELETE). Sin permisos DDL.
ALTER DEFAULT PRIVILEGES IN SCHEMA yusay
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO yusay_app;

-- Rol de Workers (yusay_worker): Lectura y purga de registros caducados (SELECT, DELETE).
ALTER DEFAULT PRIVILEGES IN SCHEMA yusay
    GRANT SELECT, DELETE ON TABLES TO yusay_worker;

-- Secuencias asociadas para el rol de aplicación
ALTER DEFAULT PRIVILEGES IN SCHEMA yusay
    GRANT USAGE, SELECT ON SEQUENCES TO yusay_app;

-- 4. Comentario normativo sobre el esquema
COMMENT ON SCHEMA yusay IS 'Esquema relacional normativo del MVP de Yusay (v1.0).';
