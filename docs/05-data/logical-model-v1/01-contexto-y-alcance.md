# Contexto y alcance

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

Yusay es una aplicación web de seguimiento personal estructurado del bienestar para adultos de 18 años o más. Assessment, Tracking y Guidance conservan sus responsabilidades; Guidance no es una entidad persistida. Los límites clínicos y de producto permanecen en el [alcance previo](../../02-product/scope.md) y las [limitaciones](../../02-product/limitations.md).

Esta capa documenta el modelo lógico aprobado de 32 relaciones como línea base FROZEN. La congelación incluye relaciones, atributos, claves, cardinalidades, dominios, integridad, estados/transiciones, publicación/versionado, concurrencia, privacidad/eliminación/retención, auditoría y decisiones funcionales aprobadas. No comprende diseño físico o implementación. Los cambios posteriores se rigen por el [control de cambios del dictamen](12-dictamen-modelo-logico-v1.md#condiciones-para-cambios-posteriores).

## Aclaración aprobada de AJ-01

Cada InstrumentVersion publicable debe producir una interpretación oficial válida para cada puntuación alcanzable. ASSESSMENT_RESULT.interpretation_id es obligatorio. Si la fuente del instrumento no proporciona interpretaciones respaldadas, no se inventan: la publicación queda bloqueada hasta resolver esa incompatibilidad.

Esta aclaración se registra en la capa nueva. El [modelo conceptual anterior](../../03-domain/conceptual-model.md#dr-dom-005) conserva su texto histórico; la diferencia se registra en [pendientes y riesgos](11-pendientes-y-riesgos.md).

Véase el [índice y estado de fuentes](00-indice.md).
