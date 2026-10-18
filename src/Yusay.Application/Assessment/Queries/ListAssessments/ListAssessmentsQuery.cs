namespace Yusay.Application.Assessment.Queries.ListAssessments;

public sealed record ListAssessmentsQuery(
    int? Limit,
    string? Cursor);
