namespace Yusay.Api.Contracts.Administration;

// B5a de RF-021: edición administrativa con semántica PATCH — un campo ausente o null
// conserva el valor almacenado. code no viaja en el contrato porque es inmutable tras el
// alta; un cuerpo vacío o idéntico al estado almacenado responde 400 (sin cambios
// efectivos).
public sealed record UpdateDimensionRequest(
    string? Name = null,
    string? Description = null);
