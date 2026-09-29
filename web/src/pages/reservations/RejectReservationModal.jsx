/*
 * File:    RejectReservationModal.jsx
 * Author:  Thisuri
 * Created: 2026-09-29
 * Purpose: Dialog for a Grid Operator rejecting a Pending booking with a reason
 *          (PATCH /reservations/{id}/reject). The reason is required by the API
 *          (RejectReservationRequest, [Required], max 500) so the prosumer always
 *          sees why; this dialog shows the API's field message if it is missing.
 */
import { useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Button from 'react-bootstrap/Button'
import Form from 'react-bootstrap/Form'
import Modal from 'react-bootstrap/Modal'
import { rejectReservation } from '../../services/reservationsApi'
import { fieldError } from '../../utils/fieldErrors'
import { formatSlotWindow } from './reservationDisplay'

// Collects the reason and rejects. On success hands the updated reservation back;
// the API has already released the slot place. Rendered only while open.
export default function RejectReservationModal({ reservation, onClose, onRejected }) {
  const [reason, setReason] = useState('')
  const [error, setError] = useState(null)
  const [fieldErrors, setFieldErrors] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Sends the rejection. Only-from-Pending and GridOperator-only are enforced by the
  // API (RESERVATION_INVALID_STATE, AUTH_FORBIDDEN_ROLE); their detail is shown here.
  async function handleSubmit(event) {
    event.preventDefault()
    setError(null)
    setFieldErrors(null)
    setSubmitting(true)
    try {
      const updated = await rejectReservation(reservation.id, reason.trim())
      onRejected(updated)
    } catch (err) {
      setError(err.detail)
      setFieldErrors(err.errors)
      setSubmitting(false)
    }
  }

  const reasonError = fieldError(fieldErrors, 'Reason')

  return (
    <Modal show onHide={onClose} centered>
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            Reject reservation
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger">{error}</Alert>}
          <p>
            Reject the booking for <span className="font-monospace">{reservation.prosumerNIC}</span>,
            slot {formatSlotWindow(reservation.slotStart, reservation.slotEnd)}? The prosumer will
            see the reason below.
          </p>
          <Form.Group controlId="reject-reason">
            <Form.Label>Reason</Form.Label>
            <Form.Control
              as="textarea"
              rows={3}
              maxLength={500}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              isInvalid={Boolean(reasonError)}
            />
            <Form.Control.Feedback type="invalid">{reasonError}</Form.Control.Feedback>
          </Form.Group>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onClose} disabled={submitting}>
            Close
          </Button>
          <Button type="submit" variant="danger" disabled={submitting}>
            {submitting ? 'Rejecting…' : 'Reject'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
