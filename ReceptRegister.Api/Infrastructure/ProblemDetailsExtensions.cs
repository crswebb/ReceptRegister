using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ReceptRegister.Api.Infrastructure;

public static class ProblemDetailsExtensions
{
    public static IResult NotFoundProblem(string detail, string? instance = null) => Results.Problem(
        title: "Resource not found",
        type: "https://receptregister/errors/not-found",
        statusCode: StatusCodes.Status404NotFound,
        detail: detail,
        instance: instance);

    public static IResult ConflictProblem(string detail, string? instance = null) => Results.Problem(
        title: "Conflict",
        type: "https://receptregister/errors/conflict",
        statusCode: StatusCodes.Status409Conflict,
        detail: detail,
        instance: instance);

    public static IResult ValidationProblem(IDictionary<string,string[]> errors, string? detail = null) => Results.ValidationProblem(
        errors: errors,
        type: "https://receptregister/errors/validation",
        title: "One or more validation errors occurred.",
        detail: detail);
}

public class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var arg in context.Arguments)
        {
            if (arg is null) continue;
            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(arg);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(arg, validationContext, results, true))
            {
                var dict = results
                    .SelectMany(r => r.MemberNames.Select(m => (Member: m, Error: r.ErrorMessage ?? "Invalid")))
                    .GroupBy(x => x.Member)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Error).ToArray());
                return ProblemDetailsExtensions.ValidationProblem(dict);
            }
        }
        return await next(context);
    }
}