/*
 * File:    IReservationService.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: The reservation workflow operations behind
 *          docs/api-contract.md §4. Every method takes the CallerIdentity
 *          built from the JWT, so ownership and role are re-checked inside
 *          the service, not only by the controller's [Authorize] attribute.
 */
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;

namespace SmartSolar.Api.Services;

public interface IReservationService
{
    // POST /reservations — R1, active prosumer + node, duplicate, atomic claim.
    Task<ReservationResponse> Create(CreateReservationRequest request, CallerIdentity caller);

    // GET /reservations/{id} — a prosumer may only read their own.
    Task<ReservationResponse> FindById(string id, CallerIdentity caller);

    // GET /reservations/mine — NIC from the token only.
    Task<List<ReservationResponse>> FindMine(string? status, CallerIdentity caller);

    // GET /reservations — staff search, filtered and paged in the query.
    Task<PagedResult<ReservationResponse>> Search(
        string? status, string? nodeId, string? nic, DateTime? from, DateTime? to,
        int? page, int? pageSize, CallerIdentity caller);

    // PUT /reservations/{id} — R2 on the current booking, R1 + capacity on the new slot.
    Task<ReservationResponse> Update(string id, UpdateReservationRequest request, CallerIdentity caller);

    // PATCH /reservations/{id}/cancel — R2, then release capacity.
    Task<ReservationResponse> Cancel(string id, CallerIdentity caller);

    // PATCH /reservations/{id}/approve — GridOperator only, from Pending.
    Task<ReservationResponse> Approve(string id, CallerIdentity caller);

    // PATCH /reservations/{id}/reject — GridOperator only, from Pending; releases capacity.
    Task<ReservationResponse> Reject(string id, string reason, CallerIdentity caller);
}
