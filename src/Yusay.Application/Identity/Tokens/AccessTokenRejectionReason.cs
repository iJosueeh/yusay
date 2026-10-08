namespace Yusay.Application.Identity.Tokens;

public enum AccessTokenRejectionReason
{
    Malformed = 0,
    InvalidSignature = 1,
    InvalidAlgorithm = 2,
    InvalidIssuer = 3,
    InvalidAudience = 4,
    Expired = 5,
    MissingIssuedAt = 6,
    MissingCredentialVersion = 7
}
