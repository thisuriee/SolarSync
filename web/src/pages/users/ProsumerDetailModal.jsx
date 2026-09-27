/*
 * File:    ProsumerDetailModal.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Shows one prosumer's full profile to staff and lets them edit the contact
 *          fields (PUT /prosumers/{nic}). NIC, role and status are read-only here.
 */
import { useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Badge from 'react-bootstrap/Badge'
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import Form from 'react-bootstrap/Form'
import Modal from 'react-bootstrap/Modal'
import Row from 'react-bootstrap/Row'
import { updateProsumer } from '../../services/identityApi'
import { fieldError } from '../../utils/fieldErrors'
import { formatDateTime, statusVariant } from '../../utils/format'

const EDITABLE_FIELDS = [
  { name: 'fullName', label: 'Full name', type: 'text' },
  { name: 'email', label: 'Email', type: 'email' },
  { name: 'phone', label: 'Phone', type: 'tel' },
  { name: 'address', label: 'Address', type: 'text' },
]

// Displays the profile and an edit form. Rule: only fullName, email, phone and address
// are editable — nic, role and status are shown as text, never as inputs, and the API
// would ignore them anyway. Email uniqueness (USER_EMAIL_EXISTS) is checked by the API.
// Rendered only while `prosumer` is set, so the form starts from that prosumer's data.
export default function ProsumerDetailModal({ prosumer, onClose, onSaved }) {
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState(() => ({
    fullName: prosumer.fullName,
    email: prosumer.email,
    phone: prosumer.phone,
    address: prosumer.address ?? '',
  }))
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Updates one form field as the user types.
  function handleChange(event) {
    const { name, value } = event.target
    setForm((f) => ({ ...f, [name]: value }))
  }

  // Saves the contact fields; on success hands the updated profile back to the page.
  // A 400 keeps `errors` so each message appears under its input.
  async function handleSave(event) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const updated = await updateProsumer(prosumer.nic, {
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        phone: form.phone.trim(),
        address: form.address.trim(),
      })
      onSaved(updated)
    } catch (err) {
      setError(err)
      setSubmitting(false)
    }
  }

  return (
    <Modal show onHide={onClose} centered size="lg">
      <Form onSubmit={handleSave} noValidate>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            {prosumer.fullName}{' '}
            <Badge bg={statusVariant(prosumer.status)}>{prosumer.status}</Badge>
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {/* Field errors appear under inputs; only a non-field failure uses the banner. */}
          {error && !error.errors && <Alert variant="danger">{error.detail}</Alert>}

          <dl className="row mb-4">
            <dt className="col-sm-4">NIC</dt>
            <dd className="col-sm-8 font-monospace">{prosumer.nic}</dd>
            <dt className="col-sm-4">Username</dt>
            <dd className="col-sm-8">{prosumer.username}</dd>
            <dt className="col-sm-4">Registered</dt>
            <dd className="col-sm-8">{formatDateTime(prosumer.createdAt)}</dd>
            <dt className="col-sm-4">Activated</dt>
            <dd className="col-sm-8">{formatDateTime(prosumer.activatedAt)}</dd>
            {prosumer.deactivationRequestedAt && (
              <>
                <dt className="col-sm-4">Deactivation requested</dt>
                <dd className="col-sm-8">{formatDateTime(prosumer.deactivationRequestedAt)}</dd>
              </>
            )}
            {prosumer.deactivationReason && (
              <>
                <dt className="col-sm-4">Deactivation reason</dt>
                <dd className="col-sm-8">{prosumer.deactivationReason}</dd>
              </>
            )}
          </dl>

          <Row className="g-3">
            {EDITABLE_FIELDS.map(({ name, label, type }) => {
              const message = fieldError(error?.errors, name)
              return (
                <Col md={6} key={name}>
                  <Form.Group controlId={`prosumer-${name}`}>
                    <Form.Label>{label}</Form.Label>
                    <Form.Control
                      name={name}
                      type={type}
                      value={form[name]}
                      onChange={handleChange}
                      readOnly={!editing}
                      plaintext={!editing}
                      isInvalid={Boolean(message)}
                    />
                    <Form.Control.Feedback type="invalid">{message}</Form.Control.Feedback>
                  </Form.Group>
                </Col>
              )
            })}
          </Row>
        </Modal.Body>
        <Modal.Footer>
          {editing ? (
            <>
              <Button variant="secondary" onClick={onClose} disabled={submitting}>
                Cancel
              </Button>
              <Button type="submit" disabled={submitting}>
                {submitting ? 'Saving…' : 'Save changes'}
              </Button>
            </>
          ) : (
            <>
              <Button variant="secondary" onClick={onClose}>
                Close
              </Button>
              <Button onClick={() => setEditing(true)}>Edit contact details</Button>
            </>
          )}
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
