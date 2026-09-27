/*
 * File:    DeactivateProsumerModal.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Confirmation dialog for a Backoffice officer deactivating a prosumer, with
 *          an optional reason (PATCH /prosumers/{nic}/deactivate).
 */
import { useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Button from 'react-bootstrap/Button'
import Form from 'react-bootstrap/Form'
import Modal from 'react-bootstrap/Modal'
import { deactivateProsumer } from '../../services/identityApi'

// Asks for confirmation and an optional reason, then calls the API. Rule: deactivation
// is blocked while the prosumer holds active reservations — the API decides that and
// answers USER_HAS_ACTIVE_RESERVATIONS; this dialog only shows its detail.
// Rendered only while `prosumer` is set, so state starts fresh for each prosumer.
export default function DeactivateProsumerModal({ prosumer, onClose, onDeactivated }) {
  const [reason, setReason] = useState('')
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Sends the deactivation; on success hands the updated prosumer back to the page.
  async function handleConfirm(event) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const updated = await deactivateProsumer(prosumer.nic, reason)
      onDeactivated(updated)
    } catch (err) {
      setError(err.detail)
      setSubmitting(false)
    }
  }

  return (
    <Modal show onHide={onClose} centered>
      <Form onSubmit={handleConfirm}>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            Deactivate prosumer
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger">{error}</Alert>}
          <p>
            Deactivate <strong>{prosumer.fullName}</strong> ({prosumer.nic})? They will not be
            able to sign in until a Backoffice officer reactivates the account.
          </p>
          <Form.Group controlId="deactivate-reason">
            <Form.Label>Reason (optional)</Form.Label>
            <Form.Control
              as="textarea"
              rows={3}
              maxLength={300}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </Form.Group>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" variant="danger" disabled={submitting}>
            {submitting ? 'Deactivating…' : 'Deactivate'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
