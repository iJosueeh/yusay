-- ==============================================================================
-- Yusay Platform — Migración V012: Normalización de Nomenclatura user_account
-- ==============================================================================
-- Renombra yusay.app_user a yusay.user_account conservando user_id, datos y relaciones.
-- Normaliza nombres de índices, restricciones primarias, de verificación y referencias foráneas.
-- ==============================================================================

-- 1. Renombrar la tabla principal
ALTER TABLE yusay.app_user RENAME TO user_account;

-- 2. Renombrar restricciones de clave primaria y CHECKs en user_account
ALTER TABLE yusay.user_account RENAME CONSTRAINT pk_app_user TO pk_user_account;
ALTER TABLE yusay.user_account RENAME CONSTRAINT ck_app_user_status TO ck_user_account_status;
ALTER TABLE yusay.user_account RENAME CONSTRAINT ck_app_user_email_format TO ck_user_account_email_format;

-- 3. Renombrar índice único canónico de correo
ALTER INDEX yusay.uq_app_user_email RENAME TO uq_user_account_email;

-- 4. Renombrar claves foráneas dependientes en las 7 tablas relacionadas
ALTER TABLE yusay.user_credential
    RENAME CONSTRAINT fk_user_credential_app_user TO fk_user_credential_user_account;

ALTER TABLE yusay.administrator
    RENAME CONSTRAINT fk_administrator_app_user TO fk_administrator_user_account;

ALTER TABLE yusay.email_verification_token
    RENAME CONSTRAINT fk_email_verification_token_app_user TO fk_email_verification_token_user_account;

ALTER TABLE yusay.password_reset_token
    RENAME CONSTRAINT fk_password_reset_token_app_user TO fk_password_reset_token_user_account;

ALTER TABLE yusay.assessment_attempt
    RENAME CONSTRAINT fk_assessment_attempt_app_user TO fk_assessment_attempt_user_account;

ALTER TABLE yusay.check_in
    RENAME CONSTRAINT fk_check_in_app_user TO fk_check_in_user_account;

ALTER TABLE yusay.audit_event
    RENAME CONSTRAINT fk_audit_event_app_user TO fk_audit_event_user_account;

-- 5. Actualizar documentación en catálogo
COMMENT ON TABLE yusay.user_account IS 'Identidad de la cuenta personal, correo, verificación, confirmación adulta y estado de acceso.';
