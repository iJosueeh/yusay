# Runbook — Inicialización del catálogo inicial de dimensiones

- **Bloque:** B8a de RF-021 (procedimiento operativo, no código).
- **Estado:** APTO PARA EJECUCIÓN CONTROLADA en entorno de pruebas. El aprovisionamiento del primer administrador queda **PENDIENTE de [OQ-ARCH-018](../06-decisions/README.md#oq-arch-018)** (ver [Prerrequisitos](#prerrequisitos)).
- **Fuente de contenido:** [OQ-PROD-006](../02-product/product-definition.md#oq-prod-006) (RESOLVED). Códigos, descripciones, escala, anclas y reglas de `definition` se recuperan literalmente de esa resolución; no se añade contenido.
- **Verificación automatizada:** `tests/Yusay.IntegrationTests/Http/InitialDimensionCatalogHttpTests.cs` ejecuta este mismo flujo end-to-end por HTTP y lo valida en verde dentro de la suite de integración.

## Propósito

Crear mediante la API de administración las cuatro dimensiones iniciales del MVP (`mood`, `stress`, `energy`, `sleep`), con su única versión `version = 1` (escala 1–5, `step = 1`) y sus cinco anclas literales, activar cada versión y verificar el resultado en las lecturas públicas y administrativas, en la auditoría y en un registro real de CheckIn que conserve la `DimensionVersion` exacta de cada medición.

Este procedimiento **no** escribe datos de catálogo por SQL: toda creación pasa por la API para que la auditoría normativa quede completa (RN-021 y RF-021). Tampoco implementa el reemplazo atómico de versiones (VF-03), que permanece pendiente por decisión.

## Prerrequisitos

- Aplicación Yusay.Api desplegada y migraciones aplicadas (incluidas V017 y V018).
- Un token de acceso JWT de un usuario con la política `administrator` habilitada.

> **PENDIENTE — OQ-ARCH-018 (OPEN).** La gobernanza de la habilitación del primer administrador (identidad operativa, procedimiento de elevación, trazabilidad, retención de 365 días conforme a OQ-NFR-004 y manejo de referencias a cuentas eliminadas) no está resuelta. **Este runbook no incluye comandos SQL ni procedimientos de elevación** porque no existe ninguno aprobado; no se debe improvisar ninguno. Las pruebas automatizadas insertan la fila de administrador directamente en la base de la colección como *fixture* de prueba, práctica restringida a entorno de pruebas y que no constituye el procedimiento de producción.

## Contenido aprobado del catálogo (OQ-PROD-006)

Cada dimensión se crea con exactamente una versión inicial `version = 1`, `min_value = 1`, `max_value = 5`, `step = 1` y cinco anclas, una por valor. Las escalas son ordinales, subjetivas y no clínicas. Toda `definition` describe explícitamente la percepción medida, la referencia temporal, el carácter ordinal y subjetivo de la escala 1–5, la orientación de los valores y el alcance no clínico (composición D2). `definition` es texto libre (Q4 resuelta): la redacción del procedimiento es la composición fiel de esos cinco elementos a partir de OQ-PROD-006, sin introducir hechos nuevos.

| Code | Name | Description | Anclas (valor «etiqueta») |
|---|---|---|---|
| `mood` | Mood | estado de ánimo percibido | 1 «Muy negativo», 2 «Negativo», 3 «Ni negativo ni positivo», 4 «Positivo», 5 «Muy positivo» |
| `stress` | Stress | estrés percibido | 1 «Nada de estrés», 2 «Poco estrés», 3 «Estrés moderado», 4 «Mucho estrés», 5 «Estrés muy intenso» |
| `energy` | Energy | energía percibida | 1 «Muy baja», 2 «Baja», 3 «Moderada», 4 «Alta», 5 «Muy alta» |
| `sleep` | Sleep | calidad del último periodo de sueño percibida, no duración | 1 «Muy mala», 2 «Mala», 3 «Regular», 4 «Buena», 5 «Muy buena» |

Orientación de los valores (derivada de las anclas aprobadas): `mood`, `energy` y `sleep` indican mejor estado con valor más alto; `stress` indica mayor intensidad percibida con valor más alto.

Referencia temporal: para `mood`, `stress` y `energy`, la percepción en el instante del registro del CheckIn, sin ventana adicional aprobada en OQ-PROD-006; para `sleep`, el último periodo de sueño previo al registro.

## Identificadores a capturar

Ejecutar los pasos en orden y anotar cada identificador, porque los pasos de verificación y de registro del CheckIn los requieren:

| Identificador | Origen | Uso posterior |
|---|---|---|
| `dimensionId` (por code) | respuesta `201` de `POST /admin/dimensions` | versión, activación, lecturas, CheckIn |
| `dimensionVersionId` (por dimensión) | respuesta `201` de `POST /admin/dimensions/{id}/versions` | activación y verificación de mediciones |
| `accessToken` | inicio de sesión del administrador | todas las llamadas autenticadas |
| `checkInId` | respuesta `201` de `POST /check-ins` | lectura posterior y verificación de mediciones |

## Paso 1 — Crear la dimensión

Repetir para los cuatro códigos de la tabla. Ejemplo con `mood`:

```http
POST /admin/dimensions
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "code": "mood",
  "name": "Mood",
  "description": "estado de ánimo percibido"
}
```

Respuesta esperada: `201 Created` (sin cabecera `Location`):

```json
{
  "dimensionId": "<dimensionId-mood>",
  "code": "mood",
  "name": "Mood",
  "description": "estado de ánimo percibido"
}
```

## Paso 2 — Crear la versión 1 con sus cinco anclas

`POST /admin/dimensions/<dimensionId-mood>/versions`:

```json
{
  "version": 1,
  "definition": "Percepción medida: estado de ánimo percibido. Referencia temporal: la percepción en el instante del registro del CheckIn, sin ventana adicional. Escala ordinal y subjetiva de 1 a 5, no clínica. Orientación de los valores: 1 «Muy negativo» a 5 «Muy positivo», con valor más alto para un ánimo más positivo. Alcance no clínico: no constituye un diagnóstico ni una evaluación clínica.",
  "minValue": 1,
  "maxValue": 5,
  "step": 1,
  "anchors": [
    { "value": 1, "label": "Muy negativo" },
    { "value": 2, "label": "Negativo" },
    { "value": 3, "label": "Ni negativo ni positivo" },
    { "value": 4, "label": "Positivo" },
    { "value": 5, "label": "Muy positivo" }
  ]
}
```

Respuesta esperada: `201 Created` con `status = "DRAFT"`, la misma `definition`, la escala y las cinco anclas. Anotar `dimensionVersionId`.

Las `definition` de `stress`, `energy` y `sleep` reproducen los mismos cinco elementos con su percepción, su referencia temporal (`sleep`: el último periodo de sueño previo al registro) y su orientación derivada de las anclas aprobadas; los textos íntegros están en la prueba automatizada citada arriba, que actúa como fuente ejecutable de este runbook.

## Paso 3 — Activar la versión

`POST /admin/dimensions/<dimensionId>/versions/<dimensionVersionId>/activation`. Sin cuerpo.

Respuesta esperada: `200 OK` con `status = "ACTIVE"`, conservando `version = 1`, la escala, la `definition` y las cinco anclas.

Repetir los pasos 1–3 para los cuatro códigos.

## Paso 4 — Verificar las lecturas

Con el mismo token (la política `administrator` autentica también las lecturas públicas):

1. `GET /dimensions` → `200 OK`. Debe aparecer cada una de las cuatro dimensiones con exactamente una versión en `ACTIVE`, escala 1–5, `step = 1` y sus cinco anclas.
2. `GET /dimensions/{dimensionId}` → `200 OK` por cada dimensión, con el mismo contenido. Una dimensión sin versión `ACTIVE` no es visible en esta ruta.
3. `GET /admin/dimensions` → `200 OK`. Las cuatro dimensiones deben aparecer con su versión `ACTIVE` (la vista administrativa incluye también versiones `DRAFT`; aquí solo existe la versión 1).

## Paso 5 — Verificar la auditoría

Cada paso de creación y activación debe producir exactamente una fila en `yusay.audit_event`, sin eventos adicionales. Verificación de referencia (solo lectura):

```sql
SELECT action, actor_kind, actor_user_id, target_type, target_identifier, metadata
FROM yusay.audit_event
WHERE target_identifier IN ('<dimensionId>', '<dimensionVersionId>')
ORDER BY target_identifier, occurred_at;
```

Comportamiento esperado:

| Paso | `action` | `target_type` | `actor_kind` | `metadata` |
|---|---|---|---|---|
| Crear dimensión | `CATALOG_CREATED` | `DIMENSION` | `ADMINISTRATOR` | `NULL` |
| Crear versión | `CATALOG_CREATED` | `DIMENSION_VERSION` | `ADMINISTRATOR` | `NULL` |
| Activar versión | `DIMENSION_VERSION_ACTIVATED` | `DIMENSION_VERSION` | `ADMINISTRATOR` | `NULL` |

`actor_user_id` es el administrador que ejecuta el procedimiento. Las tres acciones siguen el perfil N de V011 (metadata `NULL`).

## Paso 6 — Registrar un CheckIn real (cierre del flujo)

El catálogo activo habilita el registro de tracking. `POST /check-ins`:

```json
{
  "measurements": [
    { "dimensionId": "<dimensionId-mood>",   "value": 4 },
    { "dimensionId": "<dimensionId-stress>", "value": 2 },
    { "dimensionId": "<dimensionId-energy>", "value": 3 },
    { "dimensionId": "<dimensionId-sleep>",  "value": 5 }
  ]
}
```

Respuesta esperada: `201 Created` con `checkInId` y `revision = 1`. Lectura posterior `GET /check-ins/{checkInId}` → `200 OK` con cuatro mediciones; cada una debe conservar la `dimensionVersionId` de la versión activa de su dimensión (RN-020). El valor `0` o cualquier valor fuera de 1–5 devolvería `400` porque respeta la definición de su versión.

## Tratamiento de los 409 y reejecución controlada

La API **no** ofrece idempotencia por clave: los 409 son la señal de detección de estado. Ante un 409, no repetir con otros códigos ni forzar el estado; verificar y continuar.

| Situación | Respuesta | Interpretación | Acción |
|---|---|---|---|
| Repetir el paso 1 con un code ya existente | `409 Conflict`, detalle «Ya existe una Dimensión con el código proporcionado.» | La dimensión ya fue creada en una ejecución previa | Recuperar su `dimensionId` con `GET /admin/dimensions` y continuar desde el paso siguiente |
| Repetir el paso 2 con `version = 1` ya existente | `409 Conflict`, detalle «Ya existe una versión de la Dimensión con el número proporcionado.» | La versión ya fue creada | Recuperar su `dimensionVersionId` con `GET /admin/dimensions` y continuar |
| Activar cuando ya existe una versión `ACTIVE` | `409 Conflict` (otra versión activa) | La activación ya se completó | No retirar ni reactivar: el reemplazo atómico (VF-03) está pendiente |

En ningún caso un 409 añade filas de catálogo ni eventos de auditoría adicionales: la reejecución es un control de estado, no una reescritura. La reejecución controlada consiste, precisamente, en volver a enviar los mismos pasos, confirmar que cada uno responde 409 con su mensaje esperado, comprobar que los conteos por code y por dimensión no cambian y que la auditoría de cada target conserva exactamente su fila (dimensión) o sus dos filas (versión). Si la verificación confirma el catálogo completo, el procedimiento termina sin ejecutar ningún paso de escritura.

Un entorno nuevo (base sin las cuatro dimensiones) ejecuta el procedimiento completo de cero; un entorno parcialmente inicializado se recupera identificando, con `GET /admin/dimensions`, qué pasos quedaron pendientes. No existe endpoint de borrado ni de corrección de catálogo, por lo que el código no debe variarse entre ejecuciones.

## Fuera de alcance de este runbook

- Aprovisionamiento del primer administrador: **PENDIENTE de OQ-ARCH-018**.
- Retiro y reemplazo de versiones (VF-03 / reemplazo atómico): pendiente por decisión.
- Semillas de catálogo por migración: prohibidas; RF-021 exige creación por API con auditoría.
