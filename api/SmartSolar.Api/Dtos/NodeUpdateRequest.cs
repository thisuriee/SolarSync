/*
 * File:    NodeUpdateRequest.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Body of PUT /api/nodes/{id} — a full resource update, so it carries
 *          the same editable fields as the create request.
 *
 *          It has no status property on purpose. Activation and deactivation
 *          are state transitions with their own rules and their own PATCH
 *          routes (docs/api-contract.md §3); allowing status through here
 *          would be a second way to change it, and one that skips the
 *          active-reservation check. createdBy and createdAt are likewise
 *          absent and are preserved from the stored document.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class NodeUpdateRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string StationName { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 3)]
    public string AddressLine { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "lat must be between -90 and 90.")]
    public double Lat { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "lng must be between -180 and 180.")]
    public double Lng { get; set; }

    [Range(0.0001, 1_000_000.0, ErrorMessage = "capacityKWh must be greater than 0.")]
    public double CapacityKWh { get; set; }

    [Range(1, 1000, ErrorMessage = "totalBatterySlots must be at least 1.")]
    public int TotalBatterySlots { get; set; }

    [Required, MinLength(1, ErrorMessage = "operatingSchedule must contain at least one day.")]
    public List<OperatingScheduleDto> OperatingSchedule { get; set; } = [];
}
