-- ==============================================================================
-- Yusay Platform — Migración V011: Integridad Declarativa de Auditoría y Perfiles
-- ==============================================================================
-- Entidad: yusay.audit_event (R-032 / AUDIT_EVENT)
-- Mecanismo normativo: MP-PHYS-003 (Auditoría, perfiles N/F/D/C/E/T/P y catálogos cerrados)
--
-- Restricciones declarativas implementadas:
--   1. ck_audit_event_valid_combination:
--      Valida exactamente la matriz de las 28 combinaciones normativas aprobadas
--      de la tupla (actor_kind, action, target_type) según el diccionario D06.
--
--   2. ck_audit_event_metadata_profile:
--      Valida la estructura y coherencia del payload jsonb según el perfil de la acción:
--        - Perfil N (22 acciones): metadata IS NULL
--        - Perfil F (SIGN_IN_FAILED): metadata IS NULL OR {reason_code: CREDENTIALS_NOT_ACCEPTED}
--        - Perfil D (AUTHORIZATION_DENIED): metadata IS NULL OR {reason_code IN (...)}
--        - Perfil C (CATALOG_UPDATED no RESOURCE): metadata IS NULL OR {changed_fields: array}
--        - Perfil E (CATALOG_UPDATED RESOURCE): metadata IS NULL OR {changed_fields?: array, correction_kind?: text}
--        - Perfil T (EDITORIAL_ASSOCIATION_*): metadata IS NOT NULL con {topic_id: uuid v4}
--        - Perfil P (COMPATIBILITY_DECLARED): metadata IS NOT NULL con {version_a_id: uuid v4, version_b_id: uuid v4}
--          y orden canónico estricto (version_a_id < version_b_id).
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Restricción: ck_audit_event_valid_combination
-- ------------------------------------------------------------------------------
ALTER TABLE yusay.audit_event
ADD CONSTRAINT ck_audit_event_valid_combination CHECK (
    -- 1. USER_REGISTERED (USER, USER)
    (action = 'USER_REGISTERED' AND actor_kind = 'USER' AND target_type = 'USER')
    -- 2. EMAIL_VERIFICATION_TOKEN_ISSUED (USER|ANONYMOUS, USER)
    OR (action = 'EMAIL_VERIFICATION_TOKEN_ISSUED' AND actor_kind IN ('USER', 'ANONYMOUS') AND target_type = 'USER')
    -- 3. EMAIL_VERIFIED (USER|ANONYMOUS, USER)
    OR (action = 'EMAIL_VERIFIED' AND actor_kind IN ('USER', 'ANONYMOUS') AND target_type = 'USER')
    -- 4. PASSWORD_RESET_TOKEN_ISSUED (USER|ANONYMOUS, USER)
    OR (action = 'PASSWORD_RESET_TOKEN_ISSUED' AND actor_kind IN ('USER', 'ANONYMOUS') AND target_type = 'USER')
    -- 5. PASSWORD_RESET_COMPLETED (USER|ANONYMOUS, USER)
    OR (action = 'PASSWORD_RESET_COMPLETED' AND actor_kind IN ('USER', 'ANONYMOUS') AND target_type = 'USER')
    -- 6. PASSWORD_CHANGED (USER, USER)
    OR (action = 'PASSWORD_CHANGED' AND actor_kind = 'USER' AND target_type = 'USER')
    -- 7. SIGN_IN_SUCCEEDED (USER, AUTHENTICATION)
    OR (action = 'SIGN_IN_SUCCEEDED' AND actor_kind = 'USER' AND target_type = 'AUTHENTICATION')
    -- 8. SIGN_IN_FAILED (ANONYMOUS, AUTHENTICATION)
    OR (action = 'SIGN_IN_FAILED' AND actor_kind = 'ANONYMOUS' AND target_type = 'AUTHENTICATION')
    -- 9. SIGN_OUT (USER, AUTHENTICATION)
    OR (action = 'SIGN_OUT' AND actor_kind = 'USER' AND target_type = 'AUTHENTICATION')
    -- 10. AUTHORIZATION_DENIED (USER|ADMINISTRATOR|ANONYMOUS, AUTHENTICATION)
    OR (action = 'AUTHORIZATION_DENIED' AND actor_kind IN ('USER', 'ADMINISTRATOR', 'ANONYMOUS') AND target_type = 'AUTHENTICATION')
    -- 11. USER_BLOCKED (ADMINISTRATOR, USER)
    OR (action = 'USER_BLOCKED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'USER')
    -- 12. USER_UNBLOCKED (ADMINISTRATOR, USER)
    OR (action = 'USER_UNBLOCKED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'USER')
    -- 13. USER_DELETED (USER, USER)
    OR (action = 'USER_DELETED' AND actor_kind = 'USER' AND target_type = 'USER')
    -- 14. CATALOG_CREATED (ADMINISTRATOR, 7 catálogos)
    OR (action = 'CATALOG_CREATED' AND actor_kind = 'ADMINISTRATOR' AND target_type IN (
        'INSTRUMENT', 'INSTRUMENT_VERSION', 'DIMENSION', 'DIMENSION_VERSION', 'CONTEXT_TAG', 'TOPIC', 'RESOURCE'
    ))
    -- 15. CATALOG_UPDATED - Perfil C (ADMINISTRATOR, 6 catálogos no-RESOURCE)
    OR (action = 'CATALOG_UPDATED' AND actor_kind = 'ADMINISTRATOR' AND target_type IN (
        'INSTRUMENT', 'INSTRUMENT_VERSION', 'DIMENSION', 'DIMENSION_VERSION', 'CONTEXT_TAG', 'TOPIC'
    ))
    -- 16. CATALOG_UPDATED - Perfil E (ADMINISTRATOR, RESOURCE)
    OR (action = 'CATALOG_UPDATED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'RESOURCE')
    -- 17. INSTRUMENT_VERSION_READY (ADMINISTRATOR, INSTRUMENT_VERSION)
    OR (action = 'INSTRUMENT_VERSION_READY' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'INSTRUMENT_VERSION')
    -- 18. INSTRUMENT_VERSION_RETURNED_TO_DRAFT (ADMINISTRATOR, INSTRUMENT_VERSION)
    OR (action = 'INSTRUMENT_VERSION_RETURNED_TO_DRAFT' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'INSTRUMENT_VERSION')
    -- 19. INSTRUMENT_VERSION_PUBLISHED (ADMINISTRATOR, INSTRUMENT_VERSION)
    OR (action = 'INSTRUMENT_VERSION_PUBLISHED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'INSTRUMENT_VERSION')
    -- 20. INSTRUMENT_VERSION_RETIRED (ADMINISTRATOR, INSTRUMENT_VERSION)
    OR (action = 'INSTRUMENT_VERSION_RETIRED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'INSTRUMENT_VERSION')
    -- 21. DIMENSION_VERSION_ACTIVATED (ADMINISTRATOR, DIMENSION_VERSION)
    OR (action = 'DIMENSION_VERSION_ACTIVATED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'DIMENSION_VERSION')
    -- 22. DIMENSION_VERSION_RETIRED (ADMINISTRATOR, DIMENSION_VERSION)
    OR (action = 'DIMENSION_VERSION_RETIRED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'DIMENSION_VERSION')
    -- 23. CONTEXT_TAG_ACTIVATED (ADMINISTRATOR, CONTEXT_TAG)
    OR (action = 'CONTEXT_TAG_ACTIVATED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'CONTEXT_TAG')
    -- 24. CONTEXT_TAG_RETIRED (ADMINISTRATOR, CONTEXT_TAG)
    OR (action = 'CONTEXT_TAG_RETIRED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'CONTEXT_TAG')
    -- 25. RESOURCE_PUBLISHED (ADMINISTRATOR, RESOURCE)
    OR (action = 'RESOURCE_PUBLISHED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'RESOURCE')
    -- 26. RESOURCE_RETIRED (ADMINISTRATOR, RESOURCE)
    OR (action = 'RESOURCE_RETIRED' AND actor_kind = 'ADMINISTRATOR' AND target_type = 'RESOURCE')
    -- 27. EDITORIAL_ASSOCIATION_ADDED (ADMINISTRATOR, 4 catálogos)
    OR (action = 'EDITORIAL_ASSOCIATION_ADDED' AND actor_kind = 'ADMINISTRATOR' AND target_type IN (
        'RESOURCE', 'INSTRUMENT', 'INTERPRETATION', 'DIMENSION'
    ))
    -- 28. EDITORIAL_ASSOCIATION_REMOVED (ADMINISTRATOR, 4 catálogos)
    OR (action = 'EDITORIAL_ASSOCIATION_REMOVED' AND actor_kind = 'ADMINISTRATOR' AND target_type IN (
        'RESOURCE', 'INSTRUMENT', 'INTERPRETATION', 'DIMENSION'
    ))
    -- 29. COMPATIBILITY_DECLARED (ADMINISTRATOR, 2 compatibilidades)
    OR (action = 'COMPATIBILITY_DECLARED' AND actor_kind = 'ADMINISTRATOR' AND target_type IN (
        'INSTRUMENT_VERSION_COMPATIBILITY', 'DIMENSION_VERSION_COMPATIBILITY'
    ))
);

-- ------------------------------------------------------------------------------
-- 2. Restricción: ck_audit_event_metadata_profile
-- ------------------------------------------------------------------------------
ALTER TABLE yusay.audit_event
ADD CONSTRAINT ck_audit_event_metadata_profile CHECK (
    -- Prohibición universal de JSON 'null'::jsonb
    (metadata IS NULL OR jsonb_typeof(metadata) <> 'null')
    AND (
        -- A. Acciones de Perfil N (22 acciones): metadata debe ser obligatoriamente NULL
        (
            action IN (
                'USER_REGISTERED',
                'EMAIL_VERIFICATION_TOKEN_ISSUED',
                'EMAIL_VERIFIED',
                'PASSWORD_RESET_TOKEN_ISSUED',
                'PASSWORD_RESET_COMPLETED',
                'PASSWORD_CHANGED',
                'SIGN_IN_SUCCEEDED',
                'SIGN_OUT',
                'USER_BLOCKED',
                'USER_UNBLOCKED',
                'USER_DELETED',
                'CATALOG_CREATED',
                'INSTRUMENT_VERSION_READY',
                'INSTRUMENT_VERSION_RETURNED_TO_DRAFT',
                'INSTRUMENT_VERSION_PUBLISHED',
                'INSTRUMENT_VERSION_RETIRED',
                'DIMENSION_VERSION_ACTIVATED',
                'DIMENSION_VERSION_RETIRED',
                'CONTEXT_TAG_ACTIVATED',
                'CONTEXT_TAG_RETIRED',
                'RESOURCE_PUBLISHED',
                'RESOURCE_RETIRED'
            )
            AND metadata IS NULL
        )

        -- B. Acción de Perfil F (SIGN_IN_FAILED): metadata opcional, clave exclusiva reason_code = CREDENTIALS_NOT_ACCEPTED
        OR (
            action = 'SIGN_IN_FAILED'
            AND (
                metadata IS NULL
                OR (
                    jsonb_typeof(metadata) = 'object'
                    AND (metadata - 'reason_code') = '{}'::jsonb
                    AND metadata->>'reason_code' = 'CREDENTIALS_NOT_ACCEPTED'
                )
            )
        )

        -- C. Acción de Perfil D (AUTHORIZATION_DENIED): metadata opcional, clave exclusiva reason_code en catálogo cerrado
        OR (
            action = 'AUTHORIZATION_DENIED'
            AND (
                metadata IS NULL
                OR (
                    jsonb_typeof(metadata) = 'object'
                    AND (metadata - 'reason_code') = '{}'::jsonb
                    AND metadata->>'reason_code' IN (
                        'AUTHENTICATION_REQUIRED',
                        'ACCOUNT_NOT_ACTIVE',
                        'EMAIL_NOT_VERIFIED',
                        'OPERATION_NOT_AUTHORIZED'
                    )
                )
            )
        )

        -- D. Acción de Perfil C (CATALOG_UPDATED en catálogos generales no-RESOURCE):
        --    metadata opcional, clave permitida changed_fields como array jsonb
        OR (
            action = 'CATALOG_UPDATED'
            AND target_type <> 'RESOURCE'
            AND (
                metadata IS NULL
                OR (
                    jsonb_typeof(metadata) = 'object'
                    AND (metadata - 'changed_fields') = '{}'::jsonb
                    AND jsonb_typeof(metadata->'changed_fields') = 'array'
                )
            )
        )

        -- E. Acción de Perfil E (CATALOG_UPDATED en RESOURCE):
        --    metadata opcional, claves permitidas changed_fields (array) y correction_kind (catálogo cerrado)
        OR (
            action = 'CATALOG_UPDATED'
            AND target_type = 'RESOURCE'
            AND (
                metadata IS NULL
                OR (
                    jsonb_typeof(metadata) = 'object'
                    AND (metadata - 'changed_fields' - 'correction_kind') = '{}'::jsonb
                    AND (NOT (metadata ? 'changed_fields') OR jsonb_typeof(metadata->'changed_fields') = 'array')
                    AND (
                        NOT (metadata ? 'correction_kind')
                        OR metadata->>'correction_kind' IN (
                            'SPELLING',
                            'PUNCTUATION',
                            'FORMAT',
                            'SURFACE_CLARITY',
                            'SAME_CONTENT_LINK_REPAIR'
                        )
                    )
                )
            )
        )

        -- F. Acciones de Perfil T (EDITORIAL_ASSOCIATION_ADDED, EDITORIAL_ASSOCIATION_REMOVED):
        --    metadata OBLIGATORIA, clave topic_id obligatoria con formato canónico UUID v4
        OR (
            action IN ('EDITORIAL_ASSOCIATION_ADDED', 'EDITORIAL_ASSOCIATION_REMOVED')
            AND metadata IS NOT NULL
            AND jsonb_typeof(metadata) = 'object'
            AND (metadata - 'topic_id') = '{}'::jsonb
            AND (metadata->>'topic_id') ~ '^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
        )

        -- G. Acción de Perfil P (COMPATIBILITY_DECLARED):
        --    metadata OBLIGATORIA, claves version_a_id y version_b_id obligatorias con formato UUID v4
        --    y orden canónico estricto (version_a_id < version_b_id)
        OR (
            action = 'COMPATIBILITY_DECLARED'
            AND metadata IS NOT NULL
            AND jsonb_typeof(metadata) = 'object'
            AND (metadata - 'version_a_id' - 'version_b_id') = '{}'::jsonb
            AND (metadata->>'version_a_id') ~ '^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
            AND (metadata->>'version_b_id') ~ '^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
            AND (metadata->>'version_a_id') < (metadata->>'version_b_id')
        )
    )
);

COMMENT ON CONSTRAINT ck_audit_event_valid_combination ON yusay.audit_event IS
'Garantiza que la combinación (actor_kind, action, target_type) pertenezca estrictamente a las 28 filas normativas de D06.';

COMMENT ON CONSTRAINT ck_audit_event_metadata_profile ON yusay.audit_event IS
'Valida la obligatoriedad, ausencia y claves permitidas del payload jsonb según los perfiles N/F/D/C/E/T/P de MP-PHYS-003.';
