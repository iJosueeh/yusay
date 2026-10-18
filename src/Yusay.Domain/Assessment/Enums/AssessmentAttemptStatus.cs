namespace Yusay.Domain.Assessment.Enums;

public enum AssessmentAttemptStatus
{
    InProgress,
    Submitted,
    Expired,
    Cancelled
}

public static class AssessmentAttemptStatusExtensions
{
    public static string ToDbValue(this AssessmentAttemptStatus status) => status switch
    {
        AssessmentAttemptStatus.InProgress => "IN_PROGRESS",
        AssessmentAttemptStatus.Submitted => "SUBMITTED",
        AssessmentAttemptStatus.Expired => "EXPIRED",
        AssessmentAttemptStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado de intento no soportado.")
    };

    public static AssessmentAttemptStatus FromDbValue(string dbValue) => dbValue switch
    {
        "IN_PROGRESS" => AssessmentAttemptStatus.InProgress,
        "SUBMITTED" => AssessmentAttemptStatus.Submitted,
        "EXPIRED" => AssessmentAttemptStatus.Expired,
        "CANCELLED" => AssessmentAttemptStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(dbValue), dbValue, $"Estado de intento desconocido: '{dbValue}'.")
    };
}
