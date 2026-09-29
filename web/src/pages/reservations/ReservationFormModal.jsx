/*
 * File:    ReservationFormModal.jsx
 * Author:  Thisuri
 * Created: 2026-09-29
 * Purpose: One dialog for both booking on a prosumer's behalf (POST /reservations,
 *          Backoffice) and editing a booking's slot or energy (PUT /reservations/{id}).
 *          Without `reservation` it creates: pick an active prosumer, a node and one of
 *          that node's bookable slots. With `reservation` it edits: the prosumer is
 *          fixed, and the current slot is offered alongside the bookable ones.
 *
 *          The slot list comes from GET /slots/available and is fetched fresh every
 *          time the dialog opens or the node changes — never cached, because a place
 *          that was free a minute ago may be gone. Even so, the API re-checks the
 *          7-day window, capacity and (on edit) the 12-hour rule; this form only shows
 *          its `detail` when it refuses.
 */
import { useEffect, useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Badge from 'react-bootstrap/Badge'
import Button from 'react-bootstrap/Button'
import Form from 'react-bootstrap/Form'
import InputGroup from 'react-bootstrap/InputGroup'
import ListGroup from 'react-bootstrap/ListGroup'
import Modal from 'react-bootstrap/Modal'
import Spinner from 'react-bootstrap/Spinner'
import { getProsumers } from '../../services/identityApi'
import { getAvailableSlots } from '../../services/nodesApi'
import { createReservation, updateReservation } from '../../services/reservationsApi'
import { fieldError } from '../../utils/fieldErrors'
import { formatSlotWindow, formatTimeUntil } from './reservationDisplay'

const PROSUMER_RESULTS = 10

// Renders the create or edit form. `nodes` feeds the node picker; `onSaved` receives
// the reservation the API returned. Rendered only while open, so state starts fresh.
export default function ReservationFormModal({ reservation, nodes, onClose, onSaved }) {
  const isEdit = Boolean(reservation)

  const [prosumer, setProsumer] = useState(null)
  const [nodeId, setNodeId] = useState(reservation?.stationId ?? '')
  const [slots, setSlots] = useState([])
  const [slotsLoading, setSlotsLoading] = useState(isEdit)
  const [slotId, setSlotId] = useState(reservation?.slotId ?? '')
  const [energy, setEnergy] = useState(reservation ? String(reservation.energyKWh) : '')
  const [error, setError] = useState(null)
  const [fieldErrors, setFieldErrors] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Fetches the node's bookable slots whenever the node changes (and on open for an
  // edit). No `date` is sent: the API would read it as a UTC day, which is not the
  // officer's local day, so the whole 7-day list is shown instead. Never cached.
  useEffect(() => {
    if (!nodeId) return undefined
    let cancelled = false
    getAvailableSlots({ nodeId })
      .then((data) => {
        if (!cancelled) setSlots(data)
      })
      .catch((err) => {
        if (!cancelled) setError(err.detail)
      })
      .finally(() => {
        if (!cancelled) setSlotsLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [nodeId])

  // Switches node: clears the old node's slots and selection, and shows the spinner.
  function handleNodeChange(value) {
    setNodeId(value)
    setSlots([])
    setSlotId(isEdit && value === reservation.stationId ? reservation.slotId : '')
    setSlotsLoading(Boolean(value))
  }

  // Sends the booking or the change. Energy is sent as a number; whether it is in
  // range (0 < kWh <= the slot's energy) is the API's call (RESERVATION_INVALID_ENERGY).
  async function handleSubmit(event) {
    event.preventDefault()
    setError(null)
    setFieldErrors(null)
    setSubmitting(true)
    const energyKWh = Number(energy)
    try {
      const saved = isEdit
        ? await updateReservation(reservation.id, { slotId, energyKWh })
        : await createReservation({ slotId, energyKWh, prosumerNIC: prosumer?.nic })
      onSaved(saved)
    } catch (err) {
      setError(err.detail)
      setFieldErrors(err.errors)
      setSubmitting(false)
    }
  }

  // The current slot is not in /slots/available when this booking holds its last
  // place (the slot is Full), so on edit it is always offered as the first choice.
  const keepCurrent = isEdit && nodeId === reservation.stationId
  const otherSlots = keepCurrent ? slots.filter((s) => s.id !== reservation.slotId) : slots
  const selectedSlot = slots.find((s) => s.id === slotId)
  const canSubmit = (isEdit || prosumer) && slotId && energy !== '' && !submitting

  return (
    <Modal show onHide={onClose} centered size="lg">
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            {isEdit ? 'Edit reservation' : 'New reservation on behalf of a prosumer'}
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger">{error}</Alert>}

          {isEdit ? (
            <p className="mb-3">
              Prosumer <span className="font-monospace">{reservation.prosumerNIC}</span> · current
              slot {formatSlotWindow(reservation.slotStart, reservation.slotEnd)}{' '}
              <span className="text-secondary small">
                (starts {formatTimeUntil(reservation.slotStart)})
              </span>
            </p>
          ) : (
            <ProsumerPicker selected={prosumer} onSelect={setProsumer} />
          )}

          <Form.Group controlId="reservation-node" className="mb-3">
            <Form.Label>Node</Form.Label>
            <Form.Select value={nodeId} onChange={(e) => handleNodeChange(e.target.value)}>
              <option value="">Choose a node…</option>
              {nodes.map((n) => (
                <option key={n.id} value={n.id}>
                  {n.stationName} — {n.city}
                </option>
              ))}
            </Form.Select>
          </Form.Group>

          {nodeId && (
            <fieldset className="mb-3">
              <legend className="form-label fs-6">Slot</legend>
              {slotsLoading ? (
                <Spinner animation="border" size="sm" role="status">
                  <span className="visually-hidden">Loading slots…</span>
                </Spinner>
              ) : (
                <ListGroup style={{ maxHeight: '16rem', overflowY: 'auto' }}>
                  {keepCurrent && (
                    <ListGroup.Item>
                      <Form.Check
                        type="radio"
                        name="slot"
                        id="slot-current"
                        checked={slotId === reservation.slotId}
                        onChange={() => setSlotId(reservation.slotId)}
                        label={`Keep current slot — ${formatSlotWindow(reservation.slotStart, reservation.slotEnd)}`}
                      />
                    </ListGroup.Item>
                  )}
                  {otherSlots.map((s) => (
                    <ListGroup.Item key={s.id}>
                      <Form.Check
                        type="radio"
                        name="slot"
                        id={`slot-${s.id}`}
                        checked={slotId === s.id}
                        onChange={() => setSlotId(s.id)}
                        label={
                          <>
                            {formatSlotWindow(s.slotStart, s.slotEnd)}
                            <Badge bg="light" text="dark" className="ms-2">
                              {s.availableCapacity} of {s.totalCapacity} free
                            </Badge>
                            <span className="text-secondary small ms-2">
                              {s.energyPerSlotKWh} kWh per place
                            </span>
                          </>
                        }
                      />
                    </ListGroup.Item>
                  ))}
                  {!keepCurrent && otherSlots.length === 0 && (
                    <ListGroup.Item className="text-secondary">
                      No bookable slots on this node right now.
                    </ListGroup.Item>
                  )}
                </ListGroup>
              )}
              {fieldError(fieldErrors, 'SlotId') && (
                <div className="text-danger small mt-1">{fieldError(fieldErrors, 'SlotId')}</div>
              )}
            </fieldset>
          )}

          <Form.Group controlId="reservation-energy">
            <Form.Label>Energy</Form.Label>
            <InputGroup hasValidation>
              <Form.Control
                type="number"
                step="0.1"
                inputMode="decimal"
                value={energy}
                onChange={(e) => setEnergy(e.target.value)}
                isInvalid={Boolean(fieldError(fieldErrors, 'EnergyKWh'))}
              />
              <InputGroup.Text>kWh</InputGroup.Text>
              <Form.Control.Feedback type="invalid">
                {fieldError(fieldErrors, 'EnergyKWh')}
              </Form.Control.Feedback>
            </InputGroup>
            {selectedSlot && (
              <Form.Text>This slot offers {selectedSlot.energyPerSlotKWh} kWh per place.</Form.Text>
            )}
          </Form.Group>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onClose} disabled={submitting}>
            Close
          </Button>
          <Button type="submit" disabled={!canSubmit}>
            {submitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create reservation'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}

// Searches active prosumers (M1's GET /prosumers) and lets the officer pick one.
// Listing only Active accounts is a convenience: if the account is deactivated in the
// meantime, the API still refuses the booking (RESERVATION_PROSUMER_INACTIVE).
function ProsumerPicker({ selected, onSelect }) {
  const [searchText, setSearchText] = useState('')
  const [results, setResults] = useState(null)
  const [searching, setSearching] = useState(false)
  const [error, setError] = useState(null)

  // Runs the search on demand (button or Enter), so typing does not fire a request
  // per keystroke. The text matches name, NIC, email or username on the API.
  async function handleSearch() {
    setError(null)
    setSearching(true)
    try {
      const page = await getProsumers({
        status: 'Active',
        q: searchText.trim(),
        page: 1,
        pageSize: PROSUMER_RESULTS,
      })
      setResults(page.items)
    } catch (err) {
      setError(err.detail)
    } finally {
      setSearching(false)
    }
  }

  if (selected) {
    return (
      <div className="mb-3">
        <Form.Label className="d-block">Prosumer</Form.Label>
        <span className="me-2">
          {selected.fullName} <span className="font-monospace">({selected.nic})</span>
        </span>
        <Button size="sm" variant="link" onClick={() => onSelect(null)}>
          Change
        </Button>
      </div>
    )
  }

  return (
    <Form.Group className="mb-3" controlId="reservation-prosumer-search">
      <Form.Label>Prosumer</Form.Label>
      <InputGroup>
        <Form.Control
          type="search"
          placeholder="Search by name, NIC or email"
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          onKeyDown={(e) => {
            // Enter searches instead of submitting the whole booking form.
            if (e.key === 'Enter') {
              e.preventDefault()
              handleSearch()
            }
          }}
        />
        <Button variant="outline-primary" onClick={handleSearch} disabled={searching}>
          {searching ? 'Searching…' : 'Search'}
        </Button>
      </InputGroup>
      {error && <div className="text-danger small mt-1">{error}</div>}
      {results && (
        <ListGroup className="mt-2">
          {results.length === 0 ? (
            <ListGroup.Item className="text-secondary">No active prosumers match.</ListGroup.Item>
          ) : (
            results.map((p) => (
              <ListGroup.Item key={p.nic} action as="button" type="button" onClick={() => onSelect(p)}>
                {p.fullName} <span className="font-monospace small">({p.nic})</span>
                <span className="text-secondary small ms-2">{p.email}</span>
              </ListGroup.Item>
            ))
          )}
        </ListGroup>
      )}
    </Form.Group>
  )
}
