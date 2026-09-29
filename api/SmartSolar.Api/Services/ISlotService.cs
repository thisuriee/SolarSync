/*
 * File:    ISlotService.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Contract for booking-window management. Every rule about a window
 *          — no two overlapping on the same node, capacity bounded by the
 *          node's battery bays and by what is already booked, and deletion
 *          refused while reservations point at it — lives behind this
 *          interface, so SlotsController stays a routing and authorisation
 *          layer with no logic of its own.
 */
using SmartSolar.Api.Dtos;

namespace SmartSolar.Api.Services;

public interface ISlotService
{
    // Lists a node's windows, optionally narrowed to those starting inside a
    // range. Ordered by start time.
    Task<List<SlotResponse>> FindByNode(string nodeId, DateTime? from, DateTime? to);

    // Opens a new bookable window on a node.
    Task<SlotResponse> Create(string nodeId, SlotCreateRequest request);

    // Updates a window's times, capacity and energy. Its reserved count and
    // status are carried over from the stored document.
    Task<SlotResponse> Update(string id, SlotUpdateRequest request);

    // Removes a window. Refused while any reservation still references it.
    Task Delete(string id);

    // Windows a prosumer could book right now: on an active node, still open,
    // with a free place, and starting within the booking window.
    Task<List<SlotResponse>> FindBookable(string? nodeId, DateTime? date);
}
