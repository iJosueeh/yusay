using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Identity.Services;

public sealed class SecureTokenService : ISecureTokenService
{
    public string GenerateToken(int byteLength = 32)
    {
        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), "La longitud en bytes debe ser estrictamente positiva.");
        }

        byte[] randomBytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64Url.EncodeToString(randomBytes);
    }

    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("El secreto del token no puede ser nulo ni vacío.", nameof(token));
        }

        byte[] inputBytes = Encoding.UTF8.GetBytes(token);
        byte[] hashBytes = SHA256.HashData(inputBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
