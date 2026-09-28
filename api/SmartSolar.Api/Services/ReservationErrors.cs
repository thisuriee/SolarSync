/*
 * File:    ReservationErrors.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: Every reservation BusinessRuleException built in one place, so the
 *          same rule always produces the same code, status and wording no
 *          matter which endpoint tripped it. Codes are registered in
 *          docs/response-format.md. Clients switch on the code and display
 *          the detail — they never write their own message for these rules.
 */
using SmartSolar.Api.Helpers;

namespace SmartSolar.Api.Services;

public static class ReservationErrors
{
    // 404 — no reservation with that id (or the id is not a valid ObjectId).
    // Same code and title M4's QrService already uses.
    public static BusinessRuleException NotFound()
    {
        return new BusinessRuleException(
            "RESERVATION_NOT_FOUND",
            "Reservation not found",
            "The reservation you are trying to access does not exist.",
            StatusCodes.Status404NotFound);
    }

    // 404 — the booking window asked for does not exist.
    public static BusinessRuleException SlotNotFound()
    {
        return new BusinessRuleException(
            "SLOT_NOT_FOUND",
            "Slot not found",
            "The selected time slot does not exist.",
            StatusCodes.Status404NotFound);
    }

    // 422 — R1: the slot starts more than 7 days from now.
    public static BusinessRuleException OutsideWindow(DateTime slotStart, TimeSpan bookingWindow)
    {
        return new BusinessRuleException(
            "RESERVATION_OUTSIDE_WINDOW",
            "Outside booking window",
            $"Reservations can only be made up to {bookingWindow.TotalDays:0} days in advance. " +
            $"This slot starts on {slotStart:yyyy-MM-dd HH:mm} UTC.",
            StatusCodes.Status422UnprocessableEntity);
    }

    // 422 — R1 (lower bound): the slot has already started.
    public static BusinessRuleException SlotAlreadyStarted()
    {
        return new BusinessRuleException(
            "RESERVATION_OUTSIDE_WINDOW",
            "Outside booking window",
            "This slot has already started and can no longer be booked.",
            StatusCodes.Status422UnprocessableEntity);
    }

    // 409 — R2: update or cancel inside the notice period. The detail says how
    // long is left, as the contract requires.
    public static BusinessRuleException NoticePeriod(TimeSpan untilStart, TimeSpan noticePeriod)
    {
        var remaining = untilStart <= TimeSpan.Zero
            ? "This slot has already started."
            : $"This slot starts in {DescribeDuration(untilStart)}.";

        return new BusinessRuleException(
            "RESERVATION_NOTICE_PERIOD",
            "Change window has passed",
            $"Reservations can only be changed or cancelled at least {noticePeriod.TotalHours:0} hours " +
            $"before the slot start time. {remaining}",
            StatusCodes.Status409Conflict);
    }

    // 409 — no space left (or the slot is not open for booking).
    public static BusinessRuleException SlotFull(bool closed)
    {
        return new BusinessRuleException(
            "RESERVATION_SLOT_FULL",
            "Slot unavailable",
            closed
                ? "This slot has been closed for booking."
                : "This slot has no remaining capacity. Please choose another slot.",
            StatusCodes.Status409Conflict);
    }

    // 409 — the prosumer already holds an active booking on this slot.
    public static BusinessRuleException Duplicate()
    {
        return new BusinessRuleException(
            "RESERVATION_DUPLICATE",
            "Duplicate reservation",
            "You already have an active reservation for this slot.",
            StatusCodes.Status409Conflict);
    }

    // 409 — the status transition guard refused the change.
    public static BusinessRuleException InvalidState(string from, string to)
    {
        return new BusinessRuleException(
            "RESERVATION_INVALID_STATE",
            "Invalid reservation state",
            $"A {from} reservation cannot be {DescribeTarget(to)}.",
            StatusCodes.Status409Conflict);
    }

    // 409 — the compare-and-set lost: someone changed the reservation between
    // our read and our write (e.g. a double-clicked cancel).
    public static BusinessRuleException ChangedConcurrently()
    {
        return new BusinessRuleException(
            "RESERVATION_INVALID_STATE",
            "Invalid reservation state",
            "This reservation was changed by someone else. Please refresh and try again.",
            StatusCodes.Status409Conflict);
    }

    // 409 — only an Active prosumer may book.
    public static BusinessRuleException ProsumerInactive(string status)
    {
        return new BusinessRuleException(
            "RESERVATION_PROSUMER_INACTIVE",
            "Prosumer account not active",
            $"This prosumer account is {status}. Only active accounts can make reservations.",
            StatusCodes.Status409Conflict);
    }

    // 409 — only an Active node may be booked.
    public static BusinessRuleException NodeInactive()
    {
        return new BusinessRuleException(
            "RESERVATION_NODE_INACTIVE",
            "Node not active",
            "This microgrid node is not currently accepting reservations.",
            StatusCodes.Status409Conflict);
    }

    // 400 — energy amount outside (0, slot.energyPerSlotKWh].
    public static BusinessRuleException InvalidEnergy(double maxKWh)
    {
        return new BusinessRuleException(
            "RESERVATION_INVALID_ENERGY",
            "Invalid energy amount",
            $"Energy must be greater than 0 and at most {maxKWh:0.##} kWh for this slot.",
            StatusCodes.Status400BadRequest);
    }

    // 400 — ?status= is not one of the five reservation statuses.
    public static BusinessRuleException InvalidStatusFilter(string status)
    {
        return new BusinessRuleException(
            "RESERVATION_INVALID_STATUS_FILTER",
            "Invalid status filter",
            $"'{status}' is not a reservation status. Use Pending, Approved, Rejected, Cancelled or Completed.",
            StatusCodes.Status400BadRequest);
    }

    // 409 — update attempted on a booking that is no longer Pending/Approved.
    public static BusinessRuleException NotEditable(string status)
    {
        return new BusinessRuleException(
            "RESERVATION_INVALID_STATE",
            "Invalid reservation state",
            $"A {status} reservation can no longer be changed.",
            StatusCodes.Status409Conflict);
    }

    // 400 — Backoffice booking on behalf without a usable prosumerNIC. Reuses
    // M1's USER_NIC_INVALID code and wording so both verticals agree.
    public static BusinessRuleException ProsumerNicInvalid()
    {
        return new BusinessRuleException(
            "USER_NIC_INVALID",
            "Invalid NIC",
            "prosumerNIC is required when booking on a prosumer's behalf. " +
            "NIC must be 9 digits followed by V or X (old format), or 12 digits (new format).",
            StatusCodes.Status400BadRequest);
    }

    // "3 hours 20 minutes" / "45 minutes" — for the notice-period detail.
    private static string DescribeDuration(TimeSpan span)
    {
        var hours = (int)span.TotalHours;
        var minutes = span.Minutes;

        if (hours == 0) return $"{minutes} minute(s)";
        return minutes == 0 ? $"{hours} hour(s)" : $"{hours} hour(s) {minutes} minute(s)";
    }

    // Turns a target status into the verb used in the invalid-state message.
    private static string DescribeTarget(string to)
    {
        return to switch
        {
            "Approved" => "approved",
            "Rejected" => "rejected",
            "Cancelled" => "cancelled",
            "Completed" => "completed",
            _ => $"moved to {to}"
        };
    }
}
