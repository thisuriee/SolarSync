/*
 * File:    IReservationQueries.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: The shared active-reservation predicate from docs/api-contract.md
 *          §6, expressed as an interface.
 *
 *          A reservation is ACTIVE when:
 *              status is Pending or Approved   AND   slotStart is in the future
 *
 *          That definition is written once and called from three places: node
 *          deactivation and slot deletion (nodes vertical), and prosumer
 *          deactivation (identity vertical). A second copy would drift, and
 *          drift here is invisible until a node is deactivated out from under
 *          a live booking.
 *
 *          The interface is declared here, next to its callers, so those
 *          callers depend on the contract rather than on ReservationService.
 *          ReservationService implements it once the reservation vertical
 *          lands, and the only thing that changes is the DI registration.
 *
 *          NOTE — a second, narrower interface for the same predicate
 *          (IProsumerReservationCounter) exists for the identity vertical,
 *          and its placeholder returns a constant zero, so the account
 *          deactivation rule behind it does not currently refuse anything.
 *          CountActiveForProsumer below is the same question with a working
 *          implementation. Both should end up backed by one implementation
 *          before submission, or the system holds two answers to "is this
 *          reservation active" and only one of them is true.
 */
namespace SmartSolar.Api.Services;

public interface IReservationQueries
{
    // Active reservations referencing a station. Non-zero blocks deactivation.
    Task<long> CountActiveForStation(string stationId);

    // Active reservations referencing one booking window. Non-zero blocks
    // deletion of that window.
    Task<long> CountActiveForSlot(string slotId);

    // Active reservations held by one prosumer. Non-zero blocks account
    // deactivation.
    Task<long> CountActiveForProsumer(string nic);

    // EVERY reservation referencing one booking window, whatever its status
    // and whenever it was for. Not part of the active-reservation predicate:
    // this answers "would deleting this window orphan anything", which is a
    // referential question rather than a business-rule one.
    Task<long> CountForSlot(string slotId);
}
