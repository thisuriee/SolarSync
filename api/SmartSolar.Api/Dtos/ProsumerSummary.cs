// -----------------------------------------------------------------------------
// File Name:        ProsumerSummary.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Safe projection of a prosumer for the QR verification
//                   response. Carries no credential field, so the UsersDetail
//                   document itself never leaves the API.
// -----------------------------------------------------------------------------
namespace SmartSolar.Api.Dtos;

public class ProsumerSummary
{
    // Business primary key of the prosumer.
    public string Nic { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
