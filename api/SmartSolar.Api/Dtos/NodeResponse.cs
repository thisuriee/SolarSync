/*
 * File:    NodeResponse.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: The outward shape of a SolarStationInfo document. Two things it
 *          does deliberately:
 *
 *          It flattens the stored GeoJSON point back into lat and lng. The
 *          [longitude, latitude] ordering is a MongoDB storage requirement,
 *          and exposing it would push that detail into the React form and the
 *          Android marker, where indexing the wrong element is silent and puts
 *          every Sri Lankan node in the Indian Ocean.
 *
 *          It is the only mapping from the SolarStation document to anything a
 *          client receives, so the document type itself never leaves the API.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Dtos;

public class NodeResponse
{
    public string Id { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public double Lat { get; set; }
    public double Lng { get; set; }

    public double CapacityKWh { get; set; }
    public int TotalBatterySlots { get; set; }
    public string Status { get; set; } = string.Empty;

    public List<OperatingScheduleDto> OperatingSchedule { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Projects a stored station outward, unpacking the GeoJSON coordinates.
    // Location is read through a null check only to keep one malformed
    // document from failing a whole list request; a station written by this
    // API always has one.
    public static NodeResponse FromStation(SolarStation station)
    {
        var response = new NodeResponse();
        response.Fill(station);
        return response;
    }

    // Copies the shared fields. Kept separate so NodeDetailResponse can reuse
    // it instead of duplicating the projection.
    protected void Fill(SolarStation station)
    {
        Id = station.Id;
        StationName = station.StationName;
        AddressLine = station.AddressLine;
        City = station.City;
        Lat = station.Location?.Coordinates.Latitude ?? 0;
        Lng = station.Location?.Coordinates.Longitude ?? 0;
        CapacityKWh = station.CapacityKWh;
        TotalBatterySlots = station.TotalBatterySlots;
        Status = station.Status;
        OperatingSchedule = station.OperatingSchedule
            .Select(OperatingScheduleDto.FromEntry)
            .ToList();
        CreatedAt = station.CreatedAt;
        UpdatedAt = station.UpdatedAt;
    }
}
