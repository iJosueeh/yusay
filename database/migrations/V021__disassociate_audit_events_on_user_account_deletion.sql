-- ==============================================================================
-- Yusay Platform — Migración V021: Desvinculación de Eventos de Auditoría tras Supresión de Cuenta (RF-004b / VF-04)
-- ==============================================================================
-- Motor: PostgreSQL 18
-- Documentación Normativa:
--   - docs/05-data/logical-model-v1/08-privacidad-eliminacion-retencion.md (VF-04)
--   - docs/05-data/physical-model-v1/05-privacidad-eliminacion-y-operacion.md (MP-PHYS-010)
--
-- Reglas de Gobernanza:
--   1. Desvincula exclusivamente audit_event.target_identifier cuando target_type = 'USER'
--      y target_identifier = OLD.user_id::text.
--   2. Función con SECURITY DEFINER y search_path seguro (pg_catalog, pg_temp) con
--      referencias explícitas a yusay.* para prevenir secuestro de búsqueda.
--   3. Se revoca EXECUTE sobre la función a PUBLIC. No se concede EXECUTE a yusay_app,
--      garantizando que la función solo se invoque como efecto colateral del trigger
--      y no de manera directa por la aplicación.
-- ==============================================================================

-- 1. Función disparadora segura con SECURITY DEFINER y search_path restringido
CREATE OR REPLACE FUNCTION yusay.fn_disassociate_audit_events_on_user_account_deletion()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    UPDATE yusay.audit_event
    SET target_identifier = NULL
    WHERE target_type = 'USER'
      AND target_identifier = OLD.user_id::text;

    RETURN OLD;
END;
$$;

-- 2. Restringir privilegios de ejecución sobre la función disparadora
REVOKE ALL ON FUNCTION yusay.fn_disassociate_audit_events_on_user_account_deletion() FROM PUBLIC;

-- 3. Disparador en yusay.user_account tras eliminación física
CREATE TRIGGER trg_disassociate_audit_events_after_user_deletion
    AFTER DELETE ON yusay.user_account
    FOR EACH ROW
    EXECUTE FUNCTION yusay.fn_disassociate_audit_events_on_user_account_deletion();

-- 4. Comentario normativo
COMMENT ON FUNCTION yusay.fn_disassociate_audit_events_on_user_account_deletion() IS
'Función disparadora restringida (SECURITY DEFINER) para desvincular target_identifier en audit_event al eliminar físicamente una cuenta en user_account (VF-04 / RF-004b).';
