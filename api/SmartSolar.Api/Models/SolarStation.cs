/*
 * File:    SolarStation.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Document model for the SolarStationInfo collection — a microgrid
 *          node with GeoJSON location and embedded operating schedule.
 */
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace SmartSolar.Api.Models;

public class SolarStation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string StationName { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    // GeoJSON Point — coordinates are [longitude, latitude], in that order
    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = null!;

    public double CapacityKWh { get; set; }
    public int TotalBatterySlots { get; set; }
    public string Status { get; set; } = string.Empty;   // Active | Inactive
    public List<OperatingScheduleEntry> OperatingSchedule { get; set; } = new();

    [BsonRepresentation(BsonType.ObjectId)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Embedded: a schedule entry has no life outside its station
public class OperatingScheduleEntry
{
    public int DayOfWeek { get; set; }
    public string OpenTime { get; set; } = string.Empty;   // "HH:mm"
    public string CloseTime { get; set; } = string.Empty;  // "HH:mm"
}
