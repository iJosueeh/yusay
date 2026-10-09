using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Emailing;

/// <summary>
/// Implementación PROVISIONAL sin proveedor externo: acepta el token de recuperación de
/// contraseña y lo descarta sin enviar ningún correo. NO APTA PARA PRODUCCIÓN — solo permite
/// ejercitar el flujo completo (emisión, entrega observada mediante dobles en pruebas y
/// consumo del token) hasta incorporar un proveedor real con semánticas de reintento.
/// </summary>
public sealed class NullPasswordResetEmailSender : IPasswordResetEmailSender
{
    public Task SendPasswordResetTokenAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
