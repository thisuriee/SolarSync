/*
 * File:    nodesApi.js
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: Every Microgrid Node and Booking Slot API call the web screens make, in one
 *          place (docs/api-contract.md §3). Thin wrappers only — no rule is decided here.
 *          Failures reach the caller already shaped as {status, code, detail, errors}.
 */
import apiClient from './apiClient'

// ---- Microgrid nodes ----

// GET /nodes?status=&q= — lists nodes, narrowed by status and a name/city search.
// Empty filters are dropped so the API applies no filter at all. The API decides what
// each role may see: a Prosumer is served active nodes only, whatever it asks for.
export async function getNodes({ status, q } = {}) {
  const { data } = await apiClient.get('/nodes', {
    params: { status: status || undefined, q: q || undefined },
  })
  return data
}

// GET /nodes/{id} — one node plus its booking-window summary and the count of active
// reservations. That count is what a deactivation will be refused over.
export async function getNode(id) {
  const { data } = await apiClient.get(`/nodes/${id}`)
  return data
}

// POST /nodes — registers a node (Backoffice). Coordinates are sent as plain lat/lng;
// the API maps them into its stored geospatial form. Status, owner and timestamps are
// set server-side and are not part of the body.
export async function createNode(body) {
  const { data } = await apiClient.post('/nodes', body)
  return data
}

// PUT /nodes/{id} — full update of a node's editable fields (Backoffice). The API
// refuses to cut the battery slot count below a booking window that already uses it
// (NODE_CAPACITY_CONFLICT). Status is not part of the body — it has its own routes.
export async function updateNode(id, body) {
  const { data } = await apiClient.put(`/nodes/${id}`, body)
  return data
}

// PATCH /nodes/{id}/deactivate — takes a node out of service (Backoffice).
// Refused with NODE_HAS_ACTIVE_RESERVATIONS while bookings still point at it.
export async function deactivateNode(id) {
  const { data } = await apiClient.patch(`/nodes/${id}/deactivate`)
  return data
}

// PATCH /nodes/{id}/activate — returns a node to service (Backoffice).
export async function activateNode(id) {
  const { data } = await apiClient.patch(`/nodes/${id}/activate`)
  return data
}

// GET /nodes/nearby?lat=&lng=&radiusKm= — nodes in service near a point, nearest first,
// each with the distance the server measured. Used by the mobile map; here for
// completeness so every node call lives in one file.
export async function getNearbyNodes({ lat, lng, radiusKm } = {}) {
  const { data } = await apiClient.get('/nodes/nearby', {
    params: { lat, lng, radiusKm: radiusKm || undefined },
  })
  return data
}

// ---- Booking slots ----

// GET /nodes/{nodeId}/slots?from=&to= — every window on a node, whatever its status.
export async function getNodeSlots(nodeId, { from, to } = {}) {
  const { data } = await apiClient.get(`/nodes/${nodeId}/slots`, {
    params: { from: from || undefined, to: to || undefined },
  })
  return data
}

// POST /nodes/{nodeId}/slots — opens a window (Backoffice or Grid Operator). The API
// refuses overlaps (SLOT_OVERLAP) and capacity above the node's battery slots.
export async function createSlot(nodeId, body) {
  const { data } = await apiClient.post(`/nodes/${nodeId}/slots`, body)
  return data
}

// PUT /slots/{id} — updates a window's times, capacity and energy (Backoffice or Grid
// Operator). Reserved count and status are not part of the body: only the reservation
// side writes those, so sending them would have no effect.
export async function updateSlot(id, body) {
  const { data } = await apiClient.put(`/slots/${id}`, body)
  return data
}

// DELETE /slots/{id} — removes a window (Backoffice). Refused while any reservation
// still references it (SLOT_HAS_RESERVATIONS).
export async function deleteSlot(id) {
  await apiClient.delete(`/slots/${id}`)
}

// GET /slots/available?nodeId=&date= — windows that can actually be booked: open, with
// a free place, starting inside the booking horizon, on a node that is in service.
export async function getAvailableSlots({ nodeId, date } = {}) {
  const { data } = await apiClient.get('/slots/available', {
    params: { nodeId: nodeId || undefined, date: date || undefined },
  })
  return data
}
