/*
 * File:    NearbyNodeQuery.cs
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: Query string of GET /api/nodes/nearby, bound as an object so the
 *          position can actually be required.
 *
 *          Bound as plain doubles, an absent lat or lng arrives as 0 — a real
 *          coordinate in the Gulf of Guinea — and the search runs from there
 *          and truthfully reports no nodes nearby. A client that dropped a
 *          parameter would get an empty map and no indication why. Nullable
 *          properties with [Required] turn that into a 400 naming the missing
 *          field.
 *
 *          The bounds here produce the automatic field-level 400; NodeService
 *          checks the same values again, and that copy is the rule.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class NearbyNodeQuery
{
    [Required(ErrorMessage = "lat is required.")]
    [Range(-90.0, 90.0, ErrorMessage = "lat must be between -90 and 90.")]
    public double? Lat { get; set; }

    [Required(ErrorMessage = "lng is required.")]
    [Range(-180.0, 180.0, ErrorMessage = "lng must be between -180 and 180.")]
    public double? Lng { get; set; }

    // Optional. Absent means the service's default search radius, so the
    // smallest useful request is a position and nothing else.
    [Range(0.0001, 500.0, ErrorMessage = "radiusKm must be greater than 0 and at most 500.")]
    public double? RadiusKm { get; set; }
}
