/*
 * File:    format.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Display helpers. The API sends every time in UTC (ISO 8601); the client
 *          converts to the browser's local time for display only, never for a rule.
 */

// Formats a UTC ISO string as local date + time, or '—' when there is no value.
export function formatDateTime(isoUtc) {
  if (!isoUtc) return '—'
  return new Date(isoUtc).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}

// Bootstrap badge colour for an account status. Purely visual.
export function statusVariant(status) {
  switch (status) {
    case 'Active':
      return 'success'
    case 'Pending':
      return 'warning'
    case 'Deactivated':
      return 'secondary'
    default:
      return 'light'
  }
}
