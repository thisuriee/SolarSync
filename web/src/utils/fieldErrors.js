/*
 * File:    fieldErrors.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Reads one field's messages out of the API's 400 validation `errors` map
 *          (docs/response-format.md), so forms can show them under the right input.
 */

// Returns the API's message(s) for `field`, or null. ASP.NET keys the map by the C#
// property name ("FullName"), so the lookup ignores case. The text is the API's own
// DataAnnotations message — the client still writes no validation messages itself.
export function fieldError(errors, field) {
  if (!errors) return null
  const key = Object.keys(errors).find((k) => k.toLowerCase() === field.toLowerCase())
  return key ? errors[key].join(' ') : null
}
