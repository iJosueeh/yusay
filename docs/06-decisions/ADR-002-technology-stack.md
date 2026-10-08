# ADR-002: Technology Stack (Application, Persistence, and Development Platform)

## Status

**ACCEPTED**

- **Identificador:** ADR-002.
- **Estado anterior:** PENDING / PROPOSED.
- **Decisión aprobada:** Adopción del stack tecnológico consolidado para el MVP de Yusay:
  * **Frontend:** Next.js (React 19, TypeScript) con Tailwind CSS y componentes accesibles (Radix UI / shadcn/ui).
  * **Backend:** ASP.NET Core sobre .NET 10 LTS, estructurado como **monolito modular**.
  * **Persistencia:** Micro-ORM Dapper sobre driver nativo Npgsql.
  * **Base de datos:** PostgreSQL 18 (conforme a [ADR-001](ADR-001-database-engine.md) y [diseño físico v1.0](../05-data/physical-model-v1/00-indice.md)).
  * **Migraciones:** Flyway CLI con scripts SQL nativos versionados.
  * **Testing:** xUnit + Testcontainers (PostgreSQL 18 en contenedor).
  * **Desarrollo local y orquestación:** Docker Compose.
- **Fecha de aprobación:** 2026-10-08.
- **Autoridad:** responsable del proyecto.
- **Alcance:** Arquitectura de aplicación, persistencia, frontend y plataforma de desarrollo para el MVP de Yusay.

---

## Context

Yusay surge como una plataforma de seguimiento estructurado del bienestar personal a través de evaluaciones psicométricas validadas, registros periódicos (CheckIns) y recursos informativos. El proyecto se rige por contratos normativos estrictos:
- **Baseline conceptual v0.1:** Cerrado.
- **Modelo lógico v1.0:** FAVORABLE / APPROVED / FROZEN (32 relaciones, 147 atributos, 32 PK, 9 AK, 6 URA, 41 FK con 13 compuestas).
- **Modelo físico v1.0 en PostgreSQL 18:** APROBADO CON CONDICIONES DE IMPLEMENTACIÓN (88 restricciones nombradas, 16 mecanismos `MP-PHYS`, almacenamiento en esquema `yusay.`, soporte nativo para `uuid`, `jsonb`, `timestamptz`, índices funcionales y únicos parciales `uxp_*`).

El sistema demanda conciliar dos naturalezas de interacción distintas:
1. **Contenido público abierto e indexable:** Catálogo editorial de recursos psicoeducativos, explicaciones metodológicas, propósitos de instrumentos y guías informativas de bienestar, donde el posicionamiento (SEO), el rendimiento en la carga inicial y la accesibilidad (WCAG 2.1 AA) son prioritarios.
2. **Funcionalidades privadas reactivas y transaccionales:** Autoevaluaciones de múltiples ítems con validación estricta de completitud, registros diarios de seguimiento (CheckIn) con ventanas temporales críticas (168h), cálculo atómico de puntuaciones (`SUM`), auditoría inmutable de 7 perfiles en `jsonb` y estrictos flujos de privacidad y derecho al olvido (`DP-TRANS-001`).

---

## Decision Drivers

1. **Soberanía y fidelidad al modelo relacional:** El mecanismo de acceso a datos no debe entorpecer ni reinterpretar las 41 FKs (13 compuestas), el esquema calificado `yusay.`, los bloqueos pesimistas `SELECT ... FOR UPDATE` ni los índices únicos parciales. Prohibición de ORMs pesados basados en estados mutables o proxies perezosos.
2. **Arquitectura híbrida de frontend (Público indexable vs. Privado interactivo):** Necesidad de renderizado del lado del servidor (SSR/SSG) para contenido informativo y SEO, combinado con interfaces de cliente ricas y accesibles para las evaluaciones y paneles de seguimiento.
3. **Centralización estricta de reglas de negocio y seguridad:** Las reglas de dominio, validación metodológica, invariantes de estado, auditoría y políticas de seguridad deben residir indefectiblemente en el backend, desacopladas de la interfaz de usuario.
4. **Mantenibilidad y rendimiento operacional:** Código fuertemente tipado, alta concurrencia de I/O con bajo consumo de memoria, y pruebas de integración confiables y reproducibles contra el motor real de base de datos.
5. **Simplicidad arquitectónica (No Accidental Complexity):** Preferencia por un monolito modular bien delimitado frente a arquitecturas distribuidas prematuras (microservicios).

---

## Options Considered

### 1. Stack Full-Stack TypeScript (Node.js / Fastify + Kysely + Next.js)
* **Ventajas:** Un solo lenguaje de extremo a extremo, compartición directa de esquemas de validación y DTOs, Kysely como query builder tipado sin sobrecarga de ORM.
* **Desventajas:** Menor madurez en el ecosistema empresarial para orquestación de transacciones complejas; tipado no nominal y menor robustez nativa de utilidades de seguridad y autorización comparado con frameworks maduros.

### 2. Stack JVM Empresarial (Spring Boot 3 + jOOQ + React Vite SPA)
* **Ventajas:** jOOQ inspecciona directamente el esquema DDL y genera metamodelos tipados; transacciones declarativas muy maduras.
* **Desventajas:** Alta huella de memoria RAM (~400-600 MB por nodo), tiempos de compilación y arranque lentos, alta verbosidad y arquitectura SPA pura que dificulta la indexación SEO del contenido público sin infraestructura de prerenderizado adicional.

### 3. Stack C# / .NET 10 LTS (ASP.NET Core + Dapper + Npgsql + Next.js) — **SELECCIONADO**
* **Ventajas:** Rendimiento de I/O de primer nivel (TechEmpower), consumo contenido de memoria (~120-180 MB), driver `Npgsql` con soporte nativo inigualable para PostgreSQL 18, Dapper como ejecutor SQL transparente sin capas opacas de ORM, arquitectura modular limpia en C# y Next.js resolviendo con solvencia tanto el contenido público como la aplicación privada.

---

## Decision

Se aprueba formalmente el siguiente **Stack Tecnológico Consolidado para el MVP de Yusay**:

### 1. Frontend: Next.js + React + TypeScript
* **Tecnología:** Next.js (App Router, React 19) con TypeScript, Tailwind CSS y componentes accesibles basados en primitivas Radix UI / shadcn/ui.
* **Justificación de la arquitectura:**
  - **Contenido público indexable:** Las páginas de información sobre bienestar, guías de salud emocional, recursos psicoeducativos y fichas descriptivas de instrumentos se renderizan mediante **Server-Side Rendering (SSR) y Static Site Generation (SSG)**, garantizando indexabilidad en motores de búsqueda (SEO), accesibilidad universal (WCAG 2.1 AA) y rendimiento óptimo en la primera carga en dispositivos móviles.
  - **Funcionalidades privadas interactivas:** Los paneles de usuario, cuestionarios dinámicos paso a paso, CheckIns de seguimiento diario y visualizaciones de series temporales operan como componentes de cliente reactivos protegidos tras la sesión del usuario.

### 2. Backend: ASP.NET Core sobre .NET 10 LTS (Monolito Modular)
* **Tecnología:** ASP.NET Core Web API estructurado como un **monolito modular** desacoplado por contextos delimitados (Identidad, Evaluaciones, Seguimiento, Contenido, Compatibilidad y Auditoría).
* **Centralización de seguridad y negocio:**
  - Todas las reglas de negocio, cálculo determinista de scores acumulados, guardas de publicación en catálogo (`MP-PHYS-001`), y transiciones de estado de intentos residen **exclusivamente en el backend**.
  - Autenticación mediante tokens JWT firmados criptográficamente, hash de contraseñas con **Argon2id**, generación de secretos CSPRNG para tokens de verificación y reset (SHA-256), y cumplimiento de la comparación temporal corregida (`to_timestamp(token.iat) >= date_trunc('second', password_changed_at)` para `MP-PHYS-015`).
  - La revocación individual selectiva para `SIGN_OUT` se gestiona en la capa de aplicación/caché (denylist en memoria o Redis con TTL de corta duración), sin alterar las tablas relacionales.

### 3. Persistencia: Dapper + Npgsql sobre PostgreSQL 18
* **Tecnología:** Driver oficial **Npgsql** y micro-ORM **Dapper**.
* **Justificación:**
  - Cero abstracción de entidades mutables: Dapper ejecuta directamente sentencias SQL parametrizadas escritas a mano, mapeando resultados a `records` inmutables de C#.
  - Respeto absoluto del diseño físico: soporte transparente para las 13 claves compuestas, el esquema `yusay.`, bloqueos pesimistas (`SELECT ... FOR UPDATE`), serialización canónica de los 7 perfiles `jsonb` de auditoría y guardas diferibles (`INITIALLY DEFERRED` de `MP-PHYS-006`).

### 4. Migraciones: Flyway CLI
* **Tecnología:** Flyway con scripts SQL nativos versionados (`migrations/V001__*.sql`).
* **Justificación:** Independencia absoluta del esquema respecto a frameworks o lenguajes; control atómico de DDL transaccional en PostgreSQL 18.

### 5. Testing: xUnit + Testcontainers
* **Tecnología:** xUnit con **Testcontainers para .NET**.
* **Justificación:** Las pruebas de integración de repositorios y transacciones se ejecutan contra contenedores Docker reales de **PostgreSQL 18.x**, garantizando la validación fidedigna de las 41 FKs, los índices únicos parciales `uxp_*` y los triggers de consistencia diferida antes del despliegue.

### 6. Plataforma de desarrollo local: Docker Compose
* **Tecnología:** Docker Compose definiendo servicios reproducibles para PostgreSQL 18, Flyway runner y dependencias locales de desarrollo.

---

## Consequences

### Favorables
1. **Fidelidad y respeto al modelo relacional:** No hay capas ORM (como Entity Framework o Hibernate) que intenten suplantar las restricciones de integridad, claves compuestas o índices parciales definidos en PostgreSQL 18.
2. **Dualidad óptima en la experiencia de usuario:** Next.js cubre con eficiencia tanto la divulgación pública indexable y accesible de contenidos de bienestar como la aplicación web transaccional privada.
3. **Alto rendimiento y bajo consumo:** La combinación de ASP.NET Core sobre .NET 10 y Dapper ofrece una de las tasas de transacciones por segundo más altas del mercado con un uso contenido de memoria RAM (<200 MB por nodo).
4. **Verificación fidedigna:** Testcontainers asegura que ningún cambio en el acceso a datos se valide contra bases de datos en memoria simplificadas (como SQLite o H2), sino contra el PostgreSQL 18 normativo.

### Negativas y responsabilidades
1. **Disciplina en SQL manual:** Al usar Dapper, las consultas SQL no se generan a partir de modelos de clases; los cambios en los 147 atributos del modelo físico deben reflejarse manualmente en las cadenas SQL, requiriendo cobertura rigurosa mediante tests de integración.
2. **Mantenimiento de dos ecosistemas:** El stack requiere mantener herramientas de desarrollo en C# (.NET SDK) para el backend y Node.js/npm para el frontend Next.js.
3. **Gestión de estado y caché efímera:** La revocación de tokens individuales en `SIGN_OUT` (`MP-PHYS-015`) debe ser implementada y mantenida rigurosamente en la capa de middleware de ASP.NET Core (memoria distribuida o Redis).

---

## Approval Limits

- La adopción de este stack tecnológico **no altera en modo alguno el modelo conceptual v0.1, el modelo lógico v1.0 ni el diseño físico v1.0 aprobados**.
- No se autoriza la modificación de esquemas, relaciones, atributos, estados ni de las 41 claves foráneas.
- Este ADR no aprueba proveedores de nube específicos ni contratos de hosting definitivo (mantiene `OQ-ARCH-011` abierto para dimensionamiento futuro).
- Cierra formalmente las preguntas arquitectónicas de backend (`OQ-ARCH-007`), frontend (`OQ-ARCH-008`), persistencia (`OQ-ARCH-009`) y establece las bases técnicas de autenticación (`OQ-ARCH-010`).

---

[Índice de Decisiones](README.md) · [ADR-001](ADR-001-database-engine.md) · [Arquitectura](../04-architecture/README.md) · [Diseño Físico](../05-data/physical-model-v1/00-indice.md).
