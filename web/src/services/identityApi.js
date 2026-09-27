/*
 * File:    identityApi.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Every Identity & Access (M1) API call the web screens make, in one place
 *          (docs/api-contract.md §2). Thin wrappers only — no rule is decided here.
 *          Failures reach the caller already shaped as {status, code, detail, errors}.
 */
import apiClient from './apiClient'

// ---- Web users (Backoffice only) ----

// GET /webusers?role=&status= — lists Backoffice and GridOperator accounts.
// Empty filters are dropped so the API applies no filter at all.
export async function getWebUsers({ role, status } = {}) {
  const { data } = await apiClient.get('/webusers', {
    params: { role: role || undefined, status: status || undefined },
  })
  return data
}

// POST /webusers — creates a staff account. A `Prosumer` role is refused by the API
// (USER_ROLE_INVALID); duplicates get USER_USERNAME_EXISTS / USER_EMAIL_EXISTS.
export async function createWebUser(body) {
  const { data } = await apiClient.post('/webusers', body)
  return data
}

// PUT /webusers/{id} — full update of a staff account. The API refuses to demote or
// deactivate the last active Backoffice (USER_LAST_BACKOFFICE).
export async function updateWebUser(id, body) {
  const { data } = await apiClient.put(`/webusers/${id}`, body)
  return data
}

// PATCH /webusers/{id}/status — Active / Deactivated. Same last-Backoffice guard.
export async function setWebUserStatus(id, status) {
  const { data } = await apiClient.patch(`/webusers/${id}/status`, { status })
  return data
}

// ---- Prosumers ----

// GET /prosumers?status=&q=&page=&pageSize= — paged list for Backoffice and Grid
// Operator. Returns {items, page, pageSize, totalCount, totalPages}.
export async function getProsumers({ status, q, page, pageSize } = {}) {
  const { data } = await apiClient.get('/prosumers', {
    params: { status: status || undefined, q: q || undefined, page, pageSize },
  })
  return data
}

// GET /prosumers/pending — the activation queue. Backoffice only (403 for others).
export async function getPendingProsumers() {
  const { data } = await apiClient.get('/prosumers/pending')
  return data
}

// PUT /prosumers/{nic} — edits contact fields only. nic, role and status are never
// sent: the API has nowhere to put them, so a client cannot change them.
export async function updateProsumer(nic, { fullName, email, phone, address }) {
  const { data } = await apiClient.put(`/prosumers/${encodeURIComponent(nic)}`, {
    fullName,
    email,
    phone,
    address,
  })
  return data
}

// PATCH /prosumers/{nic}/activate — Backoffice only; a Grid Operator gets
// 403 AUTH_FORBIDDEN_ROLE from the attribute and again from UserService.Activate.
export async function activateProsumer(nic) {
  const { data } = await apiClient.patch(`/prosumers/${encodeURIComponent(nic)}/activate`)
  return data
}

// PATCH /prosumers/{nic}/deactivate — Backoffice only, optional reason. Blocked by the
// API while the prosumer holds active reservations (USER_HAS_ACTIVE_RESERVATIONS).
export async function deactivateProsumer(nic, reason) {
  const { data } = await apiClient.patch(`/prosumers/${encodeURIComponent(nic)}/deactivate`, {
    reason: reason?.trim() || null,
  })
  return data
}
