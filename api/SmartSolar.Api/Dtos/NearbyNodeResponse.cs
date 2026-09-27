/*
 * File:    NearbyNodeResponse.cs
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: One result of GET /api/nodes/nearby — a node as the clients
 *          already know it, plus how far it is from the point that was
 *          searched.
 *
 *          distanceKm is on this response and not on the plain node response
 *          because distance is not a property of a station. It only exists
 *          relative to somewhere, so it belongs to the answer to a proximity
 *          question rather than to the station itself.
 *
 *          The value is measured by the database and passed straight through.
 *          Neither client recomputes it: the map draws a marker at the lat and
 *          lng this response carries and prints the distance beside it.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Dtos;

public class NearbyNodeResponse : NodeResponse
{
    // Kilometres from the searched point, nearest first across the array.
    public double DistanceKm { get; set; }

    // Projects a station and its measured distance outward. Rounding happens
    // here, once, so both clients display the same figure rather than each
    // choosing its own precision. Two decimal places is ten-metre resolution,
    // far finer than a phone's own position fix.
    public static NearbyNodeResponse FromStation(SolarStation station, double distanceKm)
    {
        var response = new NearbyNodeResponse
        {
            DistanceKm = Math.Round(distanceKm, 2, MidpointRounding.AwayFromZero)
        };

        response.Fill(station);
        return response;
    }
}
