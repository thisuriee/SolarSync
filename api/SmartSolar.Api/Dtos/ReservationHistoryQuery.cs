/*
 * File:    ReservationHistoryQuery.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The query string of GET /api/reservations/history
 *          (docs/api-contract.md §5), bound as one object so the eight
 *          parameters travel into the service as a single value.
 *
 *          Every property is optional: the smallest useful request carries no
 *          parameters at all and returns the caller's first page.
 *
 *          Binding does not validate. page/pageSize are clamped and an unknown
 *          status is refused by the service, through the same normalisation
 *          the staff search route uses — so both routes accept and reject
 *          exactly the same inputs.
 */
namespace SmartSolar.Api.Dtos;

public class ReservationHistoryQuery
{
    // Prosumers are scoped to the token's NIC regardless of what arrives here.
    public string? Nic { get; set; }

    // Station _id. A non-ObjectId value matches nothing rather than failing.
    public string? NodeId { get; set; }

    // One of the five reservation statuses, any casing. Unknown -> 400.
    public string? Status { get; set; }

    // Inclusive lower and upper bounds on slotStart.
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    // Free text: matches station name, prosumer NIC or a full reservation id.
    public string? Q { get; set; }

    // Clamped rather than rejected: page below 1 becomes 1, pageSize 1..100.
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
