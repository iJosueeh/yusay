-- ==============================================================================
-- Yusay Platform — Migración V002: Módulo de Identidad
-- ==============================================================================
-- Entidades:
--   1. yusay.app_user (R-001 / USER)
--   2. yusay.user_credential (R-002 / USER_CREDENTIAL)
--   3. yusay.administrator (R-003 / ADMINISTRATOR)
--   4. yusay.email_verification_token (R-004 / EMAIL_VERIFICATION_TOKEN)
--   5. yusay.password_reset_token (R-005 / PASSWORD_RESET_TOKEN)
--
-- Restricciones e integridad según el diseño físico v1.0 aprobado:
--   - PKs: pk_app_user, pk_user_credential, pk_administrator,
--          pk_email_verification_token, pk_password_reset_token
--   - FKs: fk_user_credential_app_user, fk_administrator_app_user,
--          fk_email_verification_token_app_user, fk_password_reset_token_app_user
--          (Todas ON DELETE CASCADE por ser dependencias personales / tokens efímeros)
--   - AK / Unicidad canónica de email: uq_app_user_email (índice único funcional LOWER(email) COLLATE "C")
--   - CHECKs de formato y estados:
--       * ck_app_user_status (ACTIVE, BLOCKED)
--       * ck_app_user_email_format (sintaxis y longitud estándar RFC 5321)
--       * ck_email_verification_token_expiry (created_at < expires_at)
--       * ck_password_reset_token_expiry (created_at < expires_at)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Tabla: yusay.app_user
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.app_user (
    user_id uuid NOT NULL DEFAULT gen_random_uuid(),
    email text NOT NULL,
    email_verified_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    adult_confirmed_at timestamptz NOT NULL,
    status text NOT NULL DEFAULT 'ACTIVE',
    CONSTRAINT pk_app_user PRIMARY KEY (user_id),
    CONSTRAINT ck_app_user_status CHECK (status IN ('ACTIVE', 'BLOCKED')),
    CONSTRAINT ck_app_user_email_format CHECK (
        email ~ '^[a-zA-Z0-9._+-]+@[a-zA-Z0-9.-]+$'
        AND email NOT LIKE '.%'
        AND email NOT LIKE '%.'
        AND email NOT LIKE '%..%'
        AND email NOT LIKE '%@%@%'
        AND length(email) <= 254
    )
);

-- AK-001: Correo canónico único sin distinción de mayúsculas (OQ-PHYS-004 / MP-PHYS-002)
CREATE UNIQUE INDEX uq_app_user_email ON yusay.app_user (lower(email) COLLATE "C");

-- Comentarios explicativos
COMMENT ON TABLE yusay.app_user IS 'Identidad de la cuenta personal, correo, verificación, confirmación adulta y estado de acceso.';
COMMENT ON COLUMN yusay.app_user.user_id IS 'Identificador opaco y estable de la cuenta (UUID v4 raíz IDF-01).';
COMMENT ON COLUMN yusay.app_user.email IS 'Correo electrónico canónico del usuario; inmutable en el MVP.';
COMMENT ON COLUMN yusay.app_user.email_verified_at IS 'Instante UTC de verificación efectiva del correo electrónico.';
COMMENT ON COLUMN yusay.app_user.created_at IS 'Instante UTC de creación de la cuenta.';
COMMENT ON COLUMN yusay.app_user.adult_confirmed_at IS 'Instante UTC de confirmación obligatoria de mayoría de edad (≥ 18 años).';
COMMENT ON COLUMN yusay.app_user.status IS 'Estado operativo de la cuenta: ACTIVE o BLOCKED.';

-- ------------------------------------------------------------------------------
-- 2. Tabla: yusay.user_credential
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.user_credential (
    user_id uuid NOT NULL,
    password_hash text NOT NULL,
    password_changed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT pk_user_credential PRIMARY KEY (user_id),
    CONSTRAINT fk_user_credential_app_user FOREIGN KEY (user_id)
        REFERENCES yusay.app_user (user_id)
        ON DELETE CASCADE
);

COMMENT ON TABLE yusay.user_credential IS 'Credencial criptográfica de autenticación (clave 1:1 compartida con app_user).';
COMMENT ON COLUMN yusay.user_credential.user_id IS 'Identificador de la cuenta (reutiliza exactamente user_id de app_user sin regeneración).';
COMMENT ON COLUMN yusay.user_credential.password_hash IS 'Hash seguro de contraseña (Argon2id/bcrypt procesado por backend).';
COMMENT ON COLUMN yusay.user_credential.password_changed_at IS 'Instante UTC del último cambio confirmado de contraseña.';

-- ------------------------------------------------------------------------------
-- 3. Tabla: yusay.administrator
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.administrator (
    user_id uuid NOT NULL,
    CONSTRAINT pk_administrator PRIMARY KEY (user_id),
    CONSTRAINT fk_administrator_app_user FOREIGN KEY (user_id)
        REFERENCES yusay.app_user (user_id)
        ON DELETE CASCADE
);

COMMENT ON TABLE yusay.administrator IS 'Habilitación de rol administrativo sobre una cuenta existente sin crear identidad personal duplicada.';
COMMENT ON COLUMN yusay.administrator.user_id IS 'Identificador de la cuenta habilitada como administrador (reutiliza user_id).';

-- ------------------------------------------------------------------------------
-- 4. Tabla: yusay.email_verification_token
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.email_verification_token (
    verification_token_id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    token_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    CONSTRAINT pk_email_verification_token PRIMARY KEY (verification_token_id),
    CONSTRAINT fk_email_verification_token_app_user FOREIGN KEY (user_id)
        REFERENCES yusay.app_user (user_id)
        ON DELETE CASCADE,
    CONSTRAINT ck_email_verification_token_expiry CHECK (created_at < expires_at)
);

COMMENT ON TABLE yusay.email_verification_token IS 'Tokens efímeros de uso único para verificación de correo (vigencia 24h, IDF-02).';
COMMENT ON COLUMN yusay.email_verification_token.verification_token_id IS 'Identificador único del token (UUID v4 raíz IDF-02).';
COMMENT ON COLUMN yusay.email_verification_token.user_id IS 'Cuenta propietaria del token.';
COMMENT ON COLUMN yusay.email_verification_token.token_hash IS 'Hash SHA-256 del token opaco emitido.';
COMMENT ON COLUMN yusay.email_verification_token.created_at IS 'Instante UTC de emisión del token.';
COMMENT ON COLUMN yusay.email_verification_token.expires_at IS 'Instante UTC de expiración del token (created_at + 24 horas).';

-- ------------------------------------------------------------------------------
-- 5. Tabla: yusay.password_reset_token
-- ------------------------------------------------------------------------------
CREATE TABLE yusay.password_reset_token (
    reset_token_id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    token_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    CONSTRAINT pk_password_reset_token PRIMARY KEY (reset_token_id),
    CONSTRAINT fk_password_reset_token_app_user FOREIGN KEY (user_id)
        REFERENCES yusay.app_user (user_id)
        ON DELETE CASCADE,
    CONSTRAINT ck_password_reset_token_expiry CHECK (created_at < expires_at)
);

COMMENT ON TABLE yusay.password_reset_token IS 'Tokens efímeros de uso único para recuperación de contraseña (vigencia 30m, IDF-03).';
COMMENT ON COLUMN yusay.password_reset_token.reset_token_id IS 'Identificador único del token (UUID v4 raíz IDF-03).';
COMMENT ON COLUMN yusay.password_reset_token.user_id IS 'Cuenta propietaria del token.';
COMMENT ON COLUMN yusay.password_reset_token.token_hash IS 'Hash SHA-256 del token opaco emitido.';
COMMENT ON COLUMN yusay.password_reset_token.created_at IS 'Instante UTC de emisión del token.';
COMMENT ON COLUMN yusay.password_reset_token.expires_at IS 'Instante UTC de expiración del token (created_at + 30 minutos).';
