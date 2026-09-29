/*
 * File:    reservationDisplay.js
 * Author:  Thisuri
 * Created: 2026-09-29
 * Purpose: Display helpers for the reservation screens. Presentation only — nothing
 *          here decides whether an action is allowed, and no rule message is written
 *          here. The 12-hour and 7-day numbers deliberately do not appear in the web
 *          client at all: they live once, as constants, in ReservationService, and a
 *          refusal is shown using the API's own `detail`.
 */

// Status strings exactly as the API stores them (Models/StationStatuses.cs —
// ReservationStatuses). Used for the filter dropdown and to choose which buttons to
// show; the API's transition guard still decides what is legal.
export const RESERVATION_STATUSES = ['Pending', 'Approved', 'Rejected', 'Cancelled', 'Completed']

// Statuses in which a booking still holds a slot place and can be edited or cancelled.
// A UI hint only: the API's transition guard answers RESERVATION_INVALID_STATE anyway.
export const OPEN_STATUSES = ['Pending', 'Approved']

// Bootstrap badge colour for a reservation status. Purely visual.
export function reservationStatusVariant(status) {
  switch (status) {
    case 'Pending':
      return 'warning'
    case 'Approved':
      return 'success'
    case 'Rejected':
      return 'danger'
    case 'Cancelled':
      return 'secondary'
    case 'Completed':
      return 'primary'
    default:
      return 'light'
  }
}

// Formats a slot window (UTC ISO start/end) as one local-time string, e.g.
// "29 Sep 2026, 14:00 – 16:00". The end repeats the date only if it crosses midnight.
export function formatSlotWindow(startUtc, endUtc) {
  if (!startUtc) return '—'
  const start = new Date(startUtc)
  const end = endUtc ? new Date(endUtc) : null
  const startText = start.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
  if (!end) return startText
  const sameDay = start.toDateString() === end.toDateString()
  const endText = sameDay
    ? end.toLocaleTimeString(undefined, { timeStyle: 'short' })
    : end.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
  return `${startText} – ${endText}`
}

// Describes how far away a slot start is, e.g. "in 1 d 4 h" or "started". A fact for
// the officer to read before editing or cancelling — it is not a rule check, and the
// client never compares it with a notice period.
export function formatTimeUntil(startUtc) {
  const ms = new Date(startUtc).getTime() - Date.now()
  if (ms <= 0) return 'already started'
  const totalMinutes = Math.floor(ms / 60000)
  const days = Math.floor(totalMinutes / 1440)
  const hours = Math.floor((totalMinutes % 1440) / 60)
  const minutes = totalMinutes % 60
  if (days > 0) return `in ${days} d ${hours} h`
  if (hours > 0) return `in ${hours} h ${minutes} min`
  return `in ${minutes} min`
}

// Turns a local calendar date from <input type="date"> ("2026-09-29") into the UTC
// instant of local midnight at the start (or, with endOfDay, the last millisecond) of
// that day. The API filters on UTC, so the conversion happens here, for display input.
export function localDayToUtcIso(dateText, endOfDay = false) {
  if (!dateText) return undefined
  const local = new Date(`${dateText}T00:00:00`)
  if (endOfDay) local.setHours(23, 59, 59, 999)
  return local.toISOString()
}
