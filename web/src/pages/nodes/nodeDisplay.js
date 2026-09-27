/*
 * File:    nodeDisplay.js
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: Display helpers for the node screens. Presentation only — nothing here
 *          decides whether an action is allowed, and no message a user reads about a
 *          business rule is written in this file. Those come from the API's `detail`.
 */

// Days as the API numbers them: 0 = Sunday through 6 = Saturday, matching
// System.DayOfWeek so neither side needs a lookup table.
export const DAY_NAMES = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
]

// Bootstrap badge colour for a node's service status. Kept separate from the account
// status helper because they are different sets of values that happen to share a word.
export function nodeStatusVariant(status) {
  switch (status) {
    case 'Active':
      return 'success'
    case 'Inactive':
      return 'secondary'
    default:
      return 'light'
  }
}

// Names a day number for display, falling back to the raw value so an unexpected
// number is visible rather than silently blank.
export function dayName(dayOfWeek) {
  return DAY_NAMES[dayOfWeek] ?? `Day ${dayOfWeek}`
}

// Formats a coordinate pair the way map tools accept it, latitude first. The API sends
// the two as separate numbers precisely so neither client has to know the order they
// are stored in.
export function formatCoordinates(lat, lng) {
  return `${lat}, ${lng}`
}

// Bootstrap badge colour for a booking window's status.
export function slotStatusVariant(status) {
  switch (status) {
    case 'Open':
      return 'success'
    case 'Full':
      return 'warning'
    case 'Closed':
      return 'secondary'
    default:
      return 'light'
  }
}

// Turns a UTC instant from the API into the value a <input type="datetime-local">
// expects: local wall-clock time, no zone, seconds dropped. Built from the local
// getters rather than by slicing toISOString(), which would hand back UTC and shift
// every time shown by the local offset.
export function toLocalInput(isoUtc) {
  if (!isoUtc) return ''

  const d = new Date(isoUtc)
  const pad = (n) => String(n).padStart(2, '0')

  return (
    `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}` +
    `T${pad(d.getHours())}:${pad(d.getMinutes())}`
  )
}

// Turns a datetime-local value back into the UTC instant the API stores. The Date
// constructor reads the zone-less string as local time, which is what the user typed,
// and toISOString renders it in UTC.
export function toUtcIso(localValue) {
  if (!localValue) return null
  return new Date(localValue).toISOString()
}

// Renders a schedule as one short line per day, in week order starting Sunday.
export function summariseSchedule(schedule) {
  if (!schedule || schedule.length === 0) return []
  return [...schedule]
    .sort((a, b) => a.dayOfWeek - b.dayOfWeek)
    .map((entry) => `${dayName(entry.dayOfWeek)} ${entry.openTime}–${entry.closeTime}`)
}
