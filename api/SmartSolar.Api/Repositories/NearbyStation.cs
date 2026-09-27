/*
 * File:    NearbyStation.cs
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: The shape a proximity search returns: a station document plus the
 *          distance MongoDB measured from the search point to it.
 *
 *          It is a query result, not a stored document. Nothing writes a
 *          distanceKm field to SolarStationInfo — the $geoNear stage adds it
 *          to each document on its way out, because how far away a station is
 *          only has meaning relative to where somebody is standing.
 *
 *          It lives beside the repositories rather than with the models for
 *          that reason: a model describes what is in the collection, and this
 *          describes what came back from one particular question.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public class NearbyStation : SolarStation
{
    // Kilometres from the search point, measured by the database against the
    // 2dsphere index. Never computed by a client, and never stored.
    public double DistanceKm { get; set; }
}
