/*
 * File:    BusinessRuleException.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: The one exception type services throw when a business rule is
 *          violated. Carries the machine-readable code, title, detail and
 *          HTTP status that ExceptionHandlingMiddleware turns into the
 *          problem object in docs/response-format.md.
 */
namespace SmartSolar.Api.Helpers;

public class BusinessRuleException : Exception
{
    public string Code { get; }
    public string Title { get; }
    public string Detail { get; }
    public int StatusCode { get; }

    // Captures the full problem contract in one place so the service layer
    // never has to know how the response is formatted.
    public BusinessRuleException(string code, string title, string detail, int statusCode)
        : base(detail)
    {
        Code = code;
        Title = title;
        Detail = detail;
        StatusCode = statusCode;
    }
}
