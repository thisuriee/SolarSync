/*
 * File:    WebUserFormModal.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Create (POST /webusers) or edit (PUT /webusers/{id}) a Backoffice or
 *          Grid Operator account. One form, two modes: `webUser` null means create.
 */
import { useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import Form from 'react-bootstrap/Form'
import Modal from 'react-bootstrap/Modal'
import Row from 'react-bootstrap/Row'
import { createWebUser, updateWebUser } from '../../services/identityApi'
import { fieldError } from '../../utils/fieldErrors'
import { ROLES } from '../../utils/roles'

// Only the two web roles are offered. Prosumers self-register on Android; the API also
// rejects role Prosumer on /webusers (USER_ROLE_INVALID) — this list is not the rule.
const WEB_ROLES = [
  { value: ROLES.BACKOFFICE, label: 'Backoffice' },
  { value: ROLES.GRID_OPERATOR, label: 'Grid Operator' },
]
const WEB_STATUSES = ['Active', 'Deactivated']

// Builds the starting form values: blank for create, the user's values for edit.
function initialForm(webUser) {
  if (!webUser) {
    return {
      username: '',
      fullName: '',
      email: '',
      phone: '',
      password: '',
      role: ROLES.GRID_OPERATOR,
    }
  }
  return {
    fullName: webUser.fullName,
    email: webUser.email,
    phone: webUser.phone,
    role: webUser.role,
    status: webUser.status,
  }
}

// Renders the form and submits it. Create sends no status (the API forces Active);
// edit sends no username or password (neither is changeable on PUT). Uniqueness
// (USER_USERNAME_EXISTS / USER_EMAIL_EXISTS) and the last-active-Backoffice guard
// (USER_LAST_BACKOFFICE) are decided by the API; their detail is shown unchanged.
// Rendered only while open, so state starts fresh each time.
export default function WebUserFormModal({ webUser, onClose, onSaved }) {
  const isEdit = Boolean(webUser)
  const [form, setForm] = useState(() => initialForm(webUser))
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Updates one form field as the user types.
  function handleChange(event) {
    const { name, value } = event.target
    setForm((f) => ({ ...f, [name]: value }))
  }

  // Sends POST or PUT; on success hands the saved user and the mode back to the page.
  async function handleSubmit(event) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    const common = {
      fullName: form.fullName.trim(),
      email: form.email.trim(),
      phone: form.phone.trim(),
      role: form.role,
    }
    try {
      const saved = isEdit
        ? await updateWebUser(webUser.id, { ...common, status: form.status })
        : await createWebUser({
            ...common,
            username: form.username.trim(),
            password: form.password,
          })
      onSaved(saved, isEdit)
    } catch (err) {
      setError(err)
      setSubmitting(false)
    }
  }

  // Renders one text input with the API's validation message (if any) under it.
  function textField(name, label, props = {}) {
    const message = fieldError(error?.errors, name)
    return (
      <Form.Group controlId={`webuser-${name}`}>
        <Form.Label>{label}</Form.Label>
        <Form.Control
          name={name}
          value={form[name]}
          onChange={handleChange}
          isInvalid={Boolean(message)}
          {...props}
        />
        <Form.Control.Feedback type="invalid">{message}</Form.Control.Feedback>
      </Form.Group>
    )
  }

  return (
    <Modal show onHide={onClose} centered size="lg">
      <Form onSubmit={handleSubmit} noValidate>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            {isEdit ? `Edit ${webUser.username}` : 'New web user'}
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && !error.errors && <Alert variant="danger">{error.detail}</Alert>}

          <Row className="g-3">
            <Col md={6}>
              {isEdit ? (
                <Form.Group controlId="webuser-username">
                  <Form.Label>Username</Form.Label>
                  <Form.Control plaintext readOnly value={webUser.username} />
                </Form.Group>
              ) : (
                textField('username', 'Username', { autoComplete: 'off' })
              )}
            </Col>
            <Col md={6}>{textField('fullName', 'Full name')}</Col>
            <Col md={6}>{textField('email', 'Email', { type: 'email' })}</Col>
            <Col md={6}>{textField('phone', 'Phone', { type: 'tel' })}</Col>
            {!isEdit && (
              <Col md={6}>
                {textField('password', 'Initial password', {
                  type: 'password',
                  autoComplete: 'new-password',
                })}
              </Col>
            )}
            <Col md={6}>
              <Form.Group controlId="webuser-role">
                <Form.Label>Role</Form.Label>
                <Form.Select
                  name="role"
                  value={form.role}
                  onChange={handleChange}
                  isInvalid={Boolean(fieldError(error?.errors, 'role'))}
                >
                  {WEB_ROLES.map((r) => (
                    <option key={r.value} value={r.value}>
                      {r.label}
                    </option>
                  ))}
                </Form.Select>
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'role')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>
            {isEdit && (
              <Col md={6}>
                <Form.Group controlId="webuser-status">
                  <Form.Label>Status</Form.Label>
                  <Form.Select name="status" value={form.status} onChange={handleChange}>
                    {WEB_STATUSES.map((s) => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </Form.Select>
                </Form.Group>
              </Col>
            )}
          </Row>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create user'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
