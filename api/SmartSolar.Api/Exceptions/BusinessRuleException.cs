/*
 * File:    BusinessRuleException.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Business rule violation for the QR fulfilment vertical. Carries the
 *          machine-readable code, title, detail and HTTP status that the
 *          response contract in docs/response-format.md requires.
 */
namespace SmartSolar.Api.Exceptions;

public class BusinessRuleException : Exception
{
    public string Code { get; }
    public string Title { get; }
    public string Detail { get; }
    public int Status { get; }

    public BusinessRuleException(string code, string title, string detail, int status)
        : base(detail)
    {
        Code = code;
        Title = title;
        Detail = detail;
        Status = status;
    }
}
