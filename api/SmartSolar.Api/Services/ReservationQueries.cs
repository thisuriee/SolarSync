/*
 * File:    ReservationQueries.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Implements the shared active-reservation predicate directly against
 *          the EnergyReservation collection.
 *
 *          INTERIM IMPLEMENTATION. This predicate belongs to the reservation
 *          vertical, which owns every rule that reads or writes a reservation.
 *          It lives here only so the node deactivation and slot deletion rules
 *          can be built and demonstrated before ReservationService exists.
 *
 *          When ReservationService implements IReservationQueries, delete this
 *          class and repoint the registration in Program.cs. Nothing else
 *          changes: NodeService and SlotService depend on the interface.
 *
 *          It reads the collection directly rather than growing
 *          IReservationRepository, so removing it is a clean delete and leaves
 *          no orphaned methods on a repository another member owns.
 *
 *          The comparison uses DateTime.UtcNow. slotStart is stored in UTC,
 *          and Sri Lanka is UTC+5:30, so a local clock here would count
 *          reservations five and a half hours on either side of the truth.
 */
using MongoDB.Bson;
using MongoDB.Driver;
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Services;

public class ReservationQueries : IReservationQueries
{
    private readonly IMongoCollection<Reservation> _reservations;

    public ReservationQueries(IMongoCollection<Reservation> reservations)
    {
        _reservations = reservations;
    }

    // Counts active reservations pointing at a station. Served by the
    // {stationId: 1, status: 1} index from docs/db-schema.md §4.
    public async Task<long> CountActiveForStation(string stationId)
    {
        if (!ObjectId.TryParse(stationId, out _))
        {
            return 0;
        }

        var filter = ActiveFilter()
            & Builders<Reservation>.Filter.Eq(r => r.StationId, stationId);

        return await _reservations.CountDocumentsAsync(filter);
    }

    // Counts active reservations pointing at one booking window. Served by
    // the {slotId: 1, status: 1} index.
    public async Task<long> CountActiveForSlot(string slotId)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return 0;
        }

        var filter = ActiveFilter()
            & Builders<Reservation>.Filter.Eq(r => r.SlotId, slotId);

        return await _reservations.CountDocumentsAsync(filter);
    }

    // Counts active reservations held by one prosumer, keyed on NIC because
    // that is the business key a reservation carries.
    public async Task<long> CountActiveForProsumer(string nic)
    {
        if (string.IsNullOrWhiteSpace(nic))
        {
            return 0;
        }

        var filter = ActiveFilter()
            & Builders<Reservation>.Filter.Eq(r => r.ProsumerNIC, nic);

        return await _reservations.CountDocumentsAsync(filter);
    }

    // Counts every reservation pointing at a booking window, regardless of
    // status or date. Deliberately does not compose onto the active filter:
    // the question is whether anything at all still refers to the window, so
    // cancelled and completed bookings count too.
    public async Task<long> CountForSlot(string slotId)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return 0;
        }

        var filter = Builders<Reservation>.Filter.Eq(r => r.SlotId, slotId);

        return await _reservations.CountDocumentsAsync(filter);
    }

    // The predicate itself, in one place: a live status and a start time that
    // has not passed. Every public method above composes onto this, so the
    // three counts can never disagree about what "active" means.
    private static FilterDefinition<Reservation> ActiveFilter()
    {
        var builder = Builders<Reservation>.Filter;

        return builder.In(r => r.Status, ReservationStatuses.Active)
             & builder.Gt(r => r.SlotStart, DateTime.UtcNow);
    }
}
