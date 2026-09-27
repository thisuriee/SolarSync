/*
 * File:    OperatingScheduleDto.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: One opening-hours row of a microgrid node, on the wire. The stored
 *          shape is embedded in the station document (docs/db-schema.md §2),
 *          because a schedule row has no life outside its station. Times stay
 *          strings in "HH:mm" so they survive a round trip through an HTML
 *          <input type="time"> without a timezone being applied to what is a
 *          wall-clock opening time, not an instant.
 */
using System.ComponentModel.DataAnnotations;
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Dtos;

public class OperatingScheduleDto
{
    // 0 = Sunday through 6 = Saturday, matching System.DayOfWeek so the
    // clients can map it without a lookup table.
    [Range(0, 6, ErrorMessage = "dayOfWeek must be 0 (Sunday) to 6 (Saturday).")]
    public int DayOfWeek { get; set; }

    [Required]
    [RegularExpression(@"^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "openTime must be HH:mm in 24-hour form.")]
    public string OpenTime { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "closeTime must be HH:mm in 24-hour form.")]
    public string CloseTime { get; set; } = string.Empty;

    // Projects a stored schedule row outward.
    public static OperatingScheduleDto FromEntry(OperatingScheduleEntry entry)
    {
        return new OperatingScheduleDto
        {
            DayOfWeek = entry.DayOfWeek,
            OpenTime = entry.OpenTime,
            CloseTime = entry.CloseTime
        };
    }

    // Maps an inbound row onto the embedded document shape.
    public OperatingScheduleEntry ToEntry()
    {
        return new OperatingScheduleEntry
        {
            DayOfWeek = DayOfWeek,
            OpenTime = OpenTime,
            CloseTime = CloseTime
        };
    }
}
