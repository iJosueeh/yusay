#!/bin/sh
set -e

echo "==> [Yusay Init] Configurando roles técnicos con privilegios mínimos y esquema base..."

# Roles arquitectónicos normativos (MP-PHYS-014)
MIGRATOR_USER="yusay_migrator"
APP_USER="yusay_app"
WORKER_USER="yusay_worker"
BACKUP_USER="yusay_backup"

# Contraseña unificada para desarrollo local con soporte para sobreescritura individual
DEFAULT_PASS="${DB_PASSWORD:-postgres_dev_password}"
MIGRATOR_PASS="${MIGRATOR_DB_PASSWORD:-$DEFAULT_PASS}"
APP_PASS="${APP_DB_PASSWORD:-$DEFAULT_PASS}"
WORKER_PASS="${WORKER_DB_PASSWORD:-$DEFAULT_PASS}"
BACKUP_PASS="${BACKUP_DB_PASSWORD:-$DEFAULT_PASS}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- 1. Rol de Migración (DDL exclusivo en esquema yusay)
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '${MIGRATOR_USER}') THEN
            CREATE ROLE ${MIGRATOR_USER} WITH LOGIN PASSWORD '${MIGRATOR_PASS}';
            RAISE NOTICE 'Rol % creado con éxito.', '${MIGRATOR_USER}';
        END IF;
    END
    \$\$;

    -- 2. Rol de Aplicación (DML mínimo: SELECT, INSERT, UPDATE, DELETE)
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '${APP_USER}') THEN
            CREATE ROLE ${APP_USER} WITH LOGIN PASSWORD '${APP_PASS}';
            RAISE NOTICE 'Rol % creado con éxito.', '${APP_USER}';
        END IF;
    END
    \$\$;

    -- 3. Rol de Workers (Mantenimiento y purgas por lotes: SELECT, DELETE)
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '${WORKER_USER}') THEN
            CREATE ROLE ${WORKER_USER} WITH LOGIN PASSWORD '${WORKER_PASS}';
            RAISE NOTICE 'Rol % creado con éxito.', '${WORKER_USER}';
        END IF;
    END
    \$\$;

    -- 4. Rol de Backup (Solo lectura mediante pg_read_all_data)
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '${BACKUP_USER}') THEN
            CREATE ROLE ${BACKUP_USER} WITH LOGIN PASSWORD '${BACKUP_PASS}';
            RAISE NOTICE 'Rol % creado con éxito.', '${BACKUP_USER}';
        END IF;
    END
    \$\$;

    -- 5. Privilegios de conexión a la base de datos
    GRANT CONNECT, CREATE ON DATABASE ${POSTGRES_DB} TO ${MIGRATOR_USER};
    GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO ${APP_USER};
    GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO ${WORKER_USER};
    GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO ${BACKUP_USER};
    GRANT pg_read_all_data TO ${BACKUP_USER};

    -- 6. Creación previa del esquema yusay asignando propiedad al rol de migraciones
    CREATE SCHEMA IF NOT EXISTS yusay;
    ALTER SCHEMA yusay OWNER TO ${MIGRATOR_USER};
    GRANT USAGE, CREATE ON SCHEMA yusay TO ${MIGRATOR_USER};

    -- 7. Configuración estricta de search_path (MP-PHYS-014)
    ALTER ROLE ${MIGRATOR_USER} SET search_path = yusay, pg_temp;
    ALTER ROLE ${APP_USER} SET search_path = yusay, pg_temp;
    ALTER ROLE ${WORKER_USER} SET search_path = yusay, pg_temp;

    -- 8. Extensiones criptográficas en public
    CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;
EOSQL

echo "==> [Yusay Init] Inicialización completada exitosamente."
