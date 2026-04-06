using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend.Contracts;

/// <summary>Minimal API helpers returning <see cref="ApiErrorResponse"/> JSON.</summary>
public static class ApiErrorResults
{
	public static IResult BadRequest(string code, string message, object? details = null) =>
		Results.Json(new ApiErrorResponse(code, message, details), statusCode: StatusCodes.Status400BadRequest);

	public static IResult NotFound(string code, string message, object? details = null) =>
		Results.Json(new ApiErrorResponse(code, message, details), statusCode: StatusCodes.Status404NotFound);

	public static IResult Conflict(string code, string message, object? details = null) =>
		Results.Json(new ApiErrorResponse(code, message, details), statusCode: StatusCodes.Status409Conflict);
}
