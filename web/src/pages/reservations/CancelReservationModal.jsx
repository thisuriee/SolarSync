/*
 * File:    CancelReservationModal.jsx
 * Author:  Thisuri
 * Created: 2026-09-29
 * Purpose: Confirmation dialog for cancelling a booking (PATCH /reservations/{id}/cancel).
 *          Shows how far away the slot is so the officer can see it, but does not check
 *          the 12-hour notice period itself: the Confirm button always sends, and inside
 *          the notice period the API answers 409 RESERVATION_NOTICE_PERIOD, whose
 *          `detail` (with the exact time remaining) is shown here as-is.
 */
import { useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Button from 'react-bootstrap/Button'
import Modal from 'react-bootstrap/Modal'
import { cancelReservation } from '../../services/reservationsApi'
import { formatSlotWindow, formatTimeUntil } from './reservationDisplay'

// Asks for confirmation, then cancels. On success hands the updated reservation back;
// the API has already released the slot place. Rendered only while open.
export default function CancelReservationModal({ reservation, onClose, onCancelled }) {
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Sends the cancel; a refusal (notice period, wrong status) keeps the dialog open.
  async function handleConfirm() {
    setError(null)
    setSubmitting(true)
    try {
      const updated = await cancelReservation(reservation.id)
      onCancelled(updated)
    } catch (err) {
      setError(err.detail)
      setSubmitting(false)
    }
  }

  return (
    <Modal show onHide={onClose} centered>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          Cancel reservation
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {error && <Alert variant="danger">{error}</Alert>}
        <p className="mb-1">
          Cancel the booking for <span className="font-monospace">{reservation.prosumerNIC}</span>{' '}
          ({reservation.energyKWh} kWh)?
        </p>
        <p className="mb-0">
          Slot {formatSlotWindow(reservation.slotStart, reservation.slotEnd)}{' '}
          <span className="text-secondary">— starts {formatTimeUntil(reservation.slotStart)}</span>
        </p>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onClose} disabled={submitting}>
          Keep booking
        </Button>
        <Button variant="danger" onClick={handleConfirm} disabled={submitting}>
          {submitting ? 'Cancelling…' : 'Cancel booking'}
        </Button>
      </Modal.Footer>
    </Modal>
  )
}
