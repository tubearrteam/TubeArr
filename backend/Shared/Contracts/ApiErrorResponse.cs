namespace TubeArr.Backend.Contracts;

/// <summary>Standard API error body with a stable machine-readable code.</summary>
public sealed record ApiErrorResponse(string Code, string Message, object? Details = null);
