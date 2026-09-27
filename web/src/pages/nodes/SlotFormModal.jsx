/*
 * File:    SlotFormModal.jsx
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: Opens a booking window on a node, or edits one (Backoffice and Grid Operator —
 *          POST /nodes/{id}/slots, PUT /slots/{id}).
 *
 *          There is no input for the reserved count or the window's status. Those two
 *          move together, in one write, owned by the reservation side; the request type
 *          the API accepts does not carry them, so there is nothing here to send. Their
 *          absence from this form is that rule made visible.
 *
 *          Times are entered in the browser's local zone and converted to UTC before
 *          they are sent. Every rule in the system is evaluated in UTC, and Sri Lanka
 *          is five and a half hours ahead of it.
 */
import { useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import Form from 'react-bootstrap/Form'
import Modal from 'react-bootstrap/Modal'
import Row from 'react-bootstrap/Row'
import Spinner from 'react-bootstrap/Spinner'
import { createSlot, updateSlot } from '../../services/nodesApi'
import { fieldError } from '../../utils/fieldErrors'
import { toLocalInput, toUtcIso } from './nodeDisplay'

// A blank window. Built fresh each time so no two dialogs share a value.
function blankSlot() {
  return { slotStart: '', slotEnd: '', totalCapacity: '', energyPerSlotKWh: '' }
}

// Loads an existing window into the form, converting its UTC instants into the local
// wall-clock strings a datetime-local input expects.
function initialForm(slot) {
  if (!slot) return blankSlot()

  return {
    slotStart: toLocalInput(slot.slotStart),
    slotEnd: toLocalInput(slot.slotEnd),
    totalCapacity: String(slot.totalCapacity),
    energyPerSlotKWh: String(slot.energyPerSlotKWh),
  }
}

// `slot` null means create. `node` is the parent, used for the capacity hint and to
// name the node the window is being opened on.
export default function SlotFormModal({ node, slot, onClose, onSaved }) {
  const isEdit = Boolean(slot)

  const [form, setForm] = useState(() => initialForm(slot))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(null)

  // Updates one field of the form.
  function setField(name, value) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  // Sends the window to the API. The two instants go up as UTC; capacity and energy are
  // converted from the inputs' strings. Overlap, capacity and window-length rules are
  // all the API's, and its answer is what gets shown.
  async function handleSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError(null)

    const body = {
      slotStart: toUtcIso(form.slotStart),
      slotEnd: toUtcIso(form.slotEnd),
      totalCapacity: Number(form.totalCapacity),
      energyPerSlotKWh: Number(form.energyPerSlotKWh),
    }

    try {
      const saved = isEdit ? await updateSlot(slot.id, body) : await createSlot(node.id, body)
      onSaved(saved, isEdit)
    } catch (err) {
      setError(err)
    } finally {
      setSaving(false)
    }
  }

  // A rule refusal reads as a warning: the request was understood, and the system is
  // saying why it will not do it.
  const isConflict = error?.status === 409

  return (
    <Modal show onHide={onClose} backdrop="static">
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton>
          <Modal.Title className="h5">
            {isEdit ? 'Edit booking window' : `New booking window — ${node.stationName}`}
          </Modal.Title>
        </Modal.Header>

        <Modal.Body>
          {error && <Alert variant={isConflict ? 'warning' : 'danger'}>{error.detail}</Alert>}

          <Row className="g-3">
            <Col md={6}>
              <Form.Group controlId="slot-start">
                <Form.Label>Starts</Form.Label>
                <Form.Control
                  type="datetime-local"
                  value={form.slotStart}
                  onChange={(e) => setField('slotStart', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'SlotStart'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'SlotStart')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>

            <Col md={6}>
              <Form.Group controlId="slot-end">
                <Form.Label>Ends</Form.Label>
                <Form.Control
                  type="datetime-local"
                  value={form.slotEnd}
                  onChange={(e) => setField('slotEnd', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'SlotEnd'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'SlotEnd')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>

            <Col md={6}>
              <Form.Group controlId="slot-capacity">
                <Form.Label>Places</Form.Label>
                <Form.Control
                  type="number"
                  min="1"
                  step="1"
                  value={form.totalCapacity}
                  onChange={(e) => setField('totalCapacity', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'TotalCapacity'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'TotalCapacity')}
                </Form.Control.Feedback>
                <Form.Text className="text-secondary">
                  This node has {node.totalBatterySlots} battery slots.
                </Form.Text>
              </Form.Group>
            </Col>

            <Col md={6}>
              <Form.Group controlId="slot-energy">
                <Form.Label>Energy (kWh)</Form.Label>
                <Form.Control
                  type="number"
                  min="0"
                  step="any"
                  value={form.energyPerSlotKWh}
                  onChange={(e) => setField('energyPerSlotKWh', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'EnergyPerSlotKWh'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'EnergyPerSlotKWh')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>
          </Row>

          <Form.Text className="text-secondary d-block mt-3">
            Times are entered in your local time and stored in UTC.
            {isEdit && ' Bookings already placed on this window are not affected by an edit.'}
          </Form.Text>
        </Modal.Body>

        <Modal.Footer>
          <Button variant="secondary" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" disabled={saving}>
            {saving && <Spinner as="span" animation="border" size="sm" className="me-2" />}
            {isEdit ? 'Save changes' : 'Open window'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
