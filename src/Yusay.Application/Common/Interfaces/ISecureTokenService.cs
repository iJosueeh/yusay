namespace Yusay.Application.Common.Interfaces;

public interface ISecureTokenService
{
    string GenerateToken(int byteLength = 32);
    string HashToken(string token);
}
