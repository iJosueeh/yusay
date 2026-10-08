using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Yusay.Application.Common.Interfaces;
namespace Yusay.Infrastructure.Identity.Services;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private readonly Argon2Options _options;

    public Argon2idPasswordHasher(Argon2Options? options = null)
    {
        _options = options ?? new Argon2Options();
    }

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("La contraseña no puede ser nula ni vacía.", nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(_options.SaltSize);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = _options.DegreeOfParallelism,
            MemorySize = _options.MemorySize,
            Iterations = _options.Iterations
        };

        byte[] hash = argon2.GetBytes(_options.HashSize);

        string saltBase64 = Convert.ToBase64String(salt);
        string hashBase64 = Convert.ToBase64String(hash);

        return $"$argon2id$v=19$m={_options.MemorySize},t={_options.Iterations},p={_options.DegreeOfParallelism}${saltBase64}${hashBase64}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            string[] parts = passwordHash.Split('$');
            if (parts.Length != 6)
            {
                return false;
            }

            if (parts[1] != "argon2id" || parts[2] != "v=19")
            {
                return false;
            }

            string[] paramPairs = parts[3].Split(',');
            int memory = 0;
            int iterations = 0;
            int parallelism = 0;

            foreach (var pair in paramPairs)
            {
                string[] kv = pair.Split('=');
                if (kv.Length != 2)
                {
                    return false;
                }

                if (kv[0] == "m" && int.TryParse(kv[1], out int mVal))
                {
                    memory = mVal;
                }
                else if (kv[0] == "t" && int.TryParse(kv[1], out int tVal))
                {
                    iterations = tVal;
                }
                else if (kv[0] == "p" && int.TryParse(kv[1], out int pVal))
                {
                    parallelism = pVal;
                }
                else
                {
                    return false;
                }
            }

            if (memory <= 0 || iterations <= 0 || parallelism <= 0)
            {
                return false;
            }

            byte[] salt = Convert.FromBase64String(parts[4]);
            byte[] expectedHash = Convert.FromBase64String(parts[5]);

            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = parallelism,
                MemorySize = memory,
                Iterations = iterations
            };

            byte[] computedHash = argon2.GetBytes(expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
