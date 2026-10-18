using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class InstrumentVersionReference
{
    public Guid InstrumentVersionId { get; }
    public int ReferenceOrder { get; }
    public string Citation { get; }
    public string? Url { get; }

    public InstrumentVersionReference(
        Guid instrumentVersionId,
        int referenceOrder,
        string citation,
        string? url)
    {
        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("La referencia debe pertenecer a una versión válida (UUID no vacío).");
        }

        if (referenceOrder <= 0)
        {
            throw new DomainException($"El orden de la referencia debe ser mayor a cero (recibido: {referenceOrder}).");
        }

        if (string.IsNullOrWhiteSpace(citation))
        {
            throw new DomainException("La cita bibliográfica no puede estar vacía.");
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new DomainException($"La URL de la referencia ('{url}') no es válida.");
            }
        }

        InstrumentVersionId = instrumentVersionId;
        ReferenceOrder = referenceOrder;
        Citation = citation.Trim();
        Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }
}
