using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Emailing;

/// <summary>
/// Implementación PROVISIONAL sin proveedor externo: acepta el token de verificación y lo
/// descarta sin enviar ningún correo. NO APTA PARA PRODUCCIÓN — solo permite ejercitar el
/// flujo completo de registro y verificación hasta incorporar un proveedor real de correo
/// con semánticas de reintento (outbox pendiente).
/// </summary>
public sealed class NullEmailVerificationSender : IEmailVerificationSender
{
    public Task SendVerificationTokenAsync(
        string recipientEmail,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
