/*
 * File:    reservationsApi.js
 * Author:  Thisuri
 * Created: 2026-09-29
 * Purpose: Every Reservation (M3) API call the web screens make, in one place
 *          (docs/api-contract.md §4). Thin wrappers only — no rule is decided here:
 *          the 7-day window, the 12-hour notice period, capacity and the status
 *          transitions all live in ReservationService on the API. Failures reach the
 *          caller already shaped as {status, code, detail, errors} by apiClient.
 */
import apiClient from './apiClient'

// GET /reservations?status=&nodeId=&nic=&from=&to=&page=&pageSize= — staff search
// (Backoffice, Grid Operator). Every filter is applied in the Mongo query on the API;
// empty ones are dropped so no filter is applied. Returns the paging envelope
// {items, page, pageSize, totalCount, totalPages}.
export async function getReservations({ status, nodeId, nic, from, to, page, pageSize } = {}) {
  const { data } = await apiClient.get('/reservations', {
    params: {
      status: status || undefined,
      nodeId: nodeId || undefined,
      nic: nic || undefined,
      from: from || undefined,
      to: to || undefined,
      page,
      pageSize,
    },
  })
  return data
}

// GET /reservations/{id} — one reservation with every audit timestamp.
export async function getReservation(id) {
  const { data } = await apiClient.get(`/reservations/${encodeURIComponent(id)}`)
  return data
}

// POST /reservations — Backoffice books on a prosumer's behalf, so prosumerNIC is
// sent. The API honours that field only for a Backoffice token (R1, capacity,
// duplicate and active-prosumer rules are all checked there).
export async function createReservation({ slotId, energyKWh, prosumerNIC }) {
  const { data } = await apiClient.post('/reservations', { slotId, energyKWh, prosumerNIC })
  return data
}

// PUT /reservations/{id} — change slot and/or energy. The owner cannot change, so no
// NIC is sent. The API applies the 12-hour rule to the current booking and the 7-day
// rule + capacity to the new slot.
export async function updateReservation(id, { slotId, energyKWh }) {
  const { data } = await apiClient.put(`/reservations/${encodeURIComponent(id)}`, {
    slotId,
    energyKWh,
  })
  return data
}

// PATCH /reservations/{id}/cancel — refused by the API inside the 12-hour notice
// period (RESERVATION_NOTICE_PERIOD); on success the slot place is released.
export async function cancelReservation(id) {
  const { data } = await apiClient.patch(`/reservations/${encodeURIComponent(id)}/cancel`)
  return data
}

// PATCH /reservations/{id}/approve — Grid Operator only, from Pending only.
export async function approveReservation(id) {
  const { data } = await apiClient.patch(`/reservations/${encodeURIComponent(id)}/approve`)
  return data
}

// PATCH /reservations/{id}/reject {reason} — Grid Operator only, from Pending only.
// The reason is mandatory on the API so the prosumer always sees why.
export async function rejectReservation(id, reason) {
  const { data } = await apiClient.patch(`/reservations/${encodeURIComponent(id)}/reject`, {
    reason,
  })
  return data
}
