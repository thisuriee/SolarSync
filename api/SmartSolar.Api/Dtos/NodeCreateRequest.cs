/*
 * File:    NodeCreateRequest.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Body of POST /api/nodes. Deliberately has NO status, createdBy or
 *          timestamp property: all four are set server-side, so a client
 *          editing the request has no field to set them with. Latitude and
 *          longitude arrive as two plain numbers and are mapped into GeoJSON
 *          [longitude, latitude] inside NodeService — the storage order is an
 *          API concern, not something two clients should each get right.
 *          The checks here produce the automatic 400 with a field map; the
 *          same bounds are re-checked in NodeService, which is where the rule
 *          actually lives.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class NodeCreateRequest
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

    // Exclusive lower bound is enforced in NodeService; DataAnnotations Range
    // is inclusive, so the smallest value accepted here is a hair above zero.
    [Range(0.0001, 1_000_000.0, ErrorMessage = "capacityKWh must be greater than 0.")]
    public double CapacityKWh { get; set; }

    [Range(1, 1000, ErrorMessage = "totalBatterySlots must be at least 1.")]
    public int TotalBatterySlots { get; set; }

    [Required, MinLength(1, ErrorMessage = "operatingSchedule must contain at least one day.")]
    public List<OperatingScheduleDto> OperatingSchedule { get; set; } = [];
}
