/*
 * File:    ScheduleEditor.jsx
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: Edits a node's operating schedule — the days it opens and the hours it keeps
 *          (docs/api-contract.md §3, POST/PUT /nodes). A controlled component: it owns
 *          no state, it reports every change upward, and it decides nothing. The API
 *          refuses an empty schedule, a repeated day, or a close time that is not after
 *          its open time, and those refusals are rendered by the form that owns it.
 */
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import Form from 'react-bootstrap/Form'
import Row from 'react-bootstrap/Row'
import Table from 'react-bootstrap/Table'
import { DAY_NAMES } from './nodeDisplay'

// A sensible starting row when a schedule is empty: a weekday daytime shift.
const DEFAULT_ENTRY = { dayOfWeek: 1, openTime: '06:00', closeTime: '18:00' }

// Renders one row per opening day. `value` is the array the parent form holds and
// `onChange` receives the whole replacement array, so the parent stays the single
// owner of the form's data.
export default function ScheduleEditor({ value, onChange, disabled }) {
  const entries = value ?? []

  // Adds a row, defaulting to the first day not already listed so the common case
  // needs no further clicking. The API rejects a repeated day, and this keeps the
  // user from walking into that refusal for no reason.
  function addEntry() {
    const used = new Set(entries.map((e) => e.dayOfWeek))
    const nextDay = DAY_NAMES.findIndex((_, day) => !used.has(day))

    onChange([
      ...entries,
      { ...DEFAULT_ENTRY, dayOfWeek: nextDay === -1 ? DEFAULT_ENTRY.dayOfWeek : nextDay },
    ])
  }

  // Replaces one field of one row, leaving the rest of the array untouched.
  function updateEntry(index, changes) {
    onChange(entries.map((entry, i) => (i === index ? { ...entry, ...changes } : entry)))
  }

  // Drops one row. The form may end up with none, which the API refuses — the user
  // sees that refusal rather than being prevented from getting there, because the
  // rule belongs to the server.
  function removeEntry(index) {
    onChange(entries.filter((_, i) => i !== index))
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-2">
        <Form.Label className="mb-0">Operating schedule</Form.Label>
        <Button size="sm" variant="outline-primary" onClick={addEntry} disabled={disabled}>
          Add day
        </Button>
      </div>

      {entries.length === 0 ? (
        <p className="text-secondary small mb-0">
          No opening days yet. Add at least one before saving.
        </p>
      ) : (
        <Table size="sm" className="align-middle mb-0">
          <thead>
            <tr>
              <th style={{ width: '45%' }}>Day</th>
              <th>Opens</th>
              <th>Closes</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {entries.map((entry, index) => (
              <tr key={index}>
                <td>
                  <Form.Select
                    aria-label={`Day for schedule row ${index + 1}`}
                    value={entry.dayOfWeek}
                    disabled={disabled}
                    onChange={(e) => updateEntry(index, { dayOfWeek: Number(e.target.value) })}
                  >
                    {DAY_NAMES.map((name, day) => (
                      <option key={day} value={day}>
                        {name}
                      </option>
                    ))}
                  </Form.Select>
                </td>
                <td>
                  <Form.Control
                    type="time"
                    aria-label={`Opening time for schedule row ${index + 1}`}
                    value={entry.openTime}
                    disabled={disabled}
                    onChange={(e) => updateEntry(index, { openTime: e.target.value })}
                  />
                </td>
                <td>
                  <Form.Control
                    type="time"
                    aria-label={`Closing time for schedule row ${index + 1}`}
                    value={entry.closeTime}
                    disabled={disabled}
                    onChange={(e) => updateEntry(index, { closeTime: e.target.value })}
                  />
                </td>
                <td className="text-end">
                  <Button
                    size="sm"
                    variant="outline-danger"
                    aria-label={`Remove schedule row ${index + 1}`}
                    disabled={disabled}
                    onClick={() => removeEntry(index)}
                  >
                    Remove
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      <Row>
        <Col>
          <Form.Text className="text-secondary">
            Times are the node&apos;s local opening hours, entered as 24-hour values.
          </Form.Text>
        </Col>
      </Row>
    </>
  )
}
