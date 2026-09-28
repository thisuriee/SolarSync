/*
 * File:    NodeFormModal.jsx
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: Registers a new microgrid node or edits an existing one (Backoffice only —
 *          POST /nodes, PUT /nodes/{id}). One component for both, because the fields
 *          are the same and keeping them together keeps them from drifting apart.
 *
 *          Every validation message shown here comes from the API: field-level ones out
 *          of the 400 `errors` map, rule-level ones out of `detail`. The form writes no
 *          validation text of its own, so what a user reads is what the server decided.
 *
 *          Status is not on this form. Taking a node in or out of service is a separate
 *          action with its own rule, and it lives on the detail dialog.
 */
import { useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import Form from 'react-bootstrap/Form'
import Modal from 'react-bootstrap/Modal'
import Row from 'react-bootstrap/Row'
import Spinner from 'react-bootstrap/Spinner'
import { createNode, updateNode } from '../../services/nodesApi'
import { fieldError } from '../../utils/fieldErrors'
import ScheduleEditor from './ScheduleEditor'

// A blank node for create mode. Built fresh each time rather than shared from a
// module-level constant: the schedule is an array, and handing every dialog the same
// instance would mean one of them could reach the others.
function blankNode() {
  return {
    stationName: '',
    addressLine: '',
    city: '',
    lat: '',
    lng: '',
    capacityKWh: '',
    totalBatterySlots: '',
    operatingSchedule: [{ dayOfWeek: 1, openTime: '06:00', closeTime: '18:00' }],
  }
}

// Builds the form's starting values. Numbers become strings so the inputs stay
// controlled and an empty box stays empty rather than showing a 0 nobody typed.
function initialForm(node) {
  if (!node) return blankNode()

  return {
    stationName: node.stationName,
    addressLine: node.addressLine,
    city: node.city,
    lat: String(node.lat),
    lng: String(node.lng),
    capacityKWh: String(node.capacityKWh),
    totalBatterySlots: String(node.totalBatterySlots),
    operatingSchedule: node.operatingSchedule.map((e) => ({ ...e })),
  }
}

// Matches a coordinate pair the way mapping tools put it on the clipboard:
// two decimal numbers separated by a comma, with optional spaces.
const COORDINATE_PAIR = /^\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*$/

// True when the two values are a usable point on Earth. Only decides whether to
// offer the preview link — the bounds themselves are the API's rule.
function isPlottable(lat, lng) {
  const a = Number(lat)
  const b = Number(lng)

  return (
    lat !== '' &&
    lng !== '' &&
    Number.isFinite(a) &&
    Number.isFinite(b) &&
    a >= -90 &&
    a <= 90 &&
    b >= -180 &&
    b <= 180
  )
}

// `node` null means create; a node means edit. `onSaved` receives the saved resource so
// the list can update without refetching everything.
//
// The starting values are computed once, when the dialog mounts, rather than copied
// from the prop by an effect. The caller gives this component a key, so opening a
// different node mounts a fresh one and the state starts correct instead of being
// corrected on a second render.
export default function NodeFormModal({ node, onClose, onSaved }) {
  const isEdit = Boolean(node)

  const [form, setForm] = useState(() => initialForm(node))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(null)

  // Updates one field of the form.
  function setField(name, value) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  // Accepts a whole coordinate pair pasted into either box.
  //
  // Mapping tools copy a location as "6.9271, 79.8612", and a number input
  // rejects that outright because of the comma — so the paste lands as nothing
  // and the officer retypes both halves by hand. That retyping is where a
  // longitude becomes a number from the wrong country, which is a mistake no
  // validation can catch, because the result is still a real place.
  //
  // A recognised pair is split across both fields; anything else falls through
  // to the browser's normal paste.
  function handleCoordinatePaste(event) {
    const pair = COORDINATE_PAIR.exec(event.clipboardData.getData('text'))
    if (!pair) return

    event.preventDefault()
    setForm((current) => ({ ...current, lat: pair[1], lng: pair[2] }))
  }

  // Sends the node to the API. The numeric fields are converted here because the inputs
  // hold strings; everything else goes as typed. The API validates all of it again, and
  // its answer — field map or rule message — is what gets displayed.
  async function handleSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError(null)

    const body = {
      stationName: form.stationName.trim(),
      addressLine: form.addressLine.trim(),
      city: form.city.trim(),
      lat: Number(form.lat),
      lng: Number(form.lng),
      capacityKWh: Number(form.capacityKWh),
      totalBatterySlots: Number(form.totalBatterySlots),
      operatingSchedule: form.operatingSchedule,
    }

    try {
      const saved = isEdit ? await updateNode(node.id, body) : await createNode(body)
      onSaved(saved, isEdit)
    } catch (err) {
      setError(err)
    } finally {
      setSaving(false)
    }
  }

  // A rule refusal (409) reads as a warning rather than an error: the request was
  // understood and the system is explaining why it will not do it.
  const isConflict = error?.status === 409

  return (
    <Modal show onHide={onClose} size="lg" backdrop="static" scrollable>
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton>
          <Modal.Title>{isEdit ? `Edit ${node.stationName}` : 'Register microgrid node'}</Modal.Title>
        </Modal.Header>

        <Modal.Body>
          {error && (
            <Alert variant={isConflict ? 'warning' : 'danger'}>{error.detail}</Alert>
          )}

          <Row className="g-3">
            <Col md={12}>
              <Form.Group controlId="node-name">
                <Form.Label>Station name</Form.Label>
                <Form.Control
                  value={form.stationName}
                  onChange={(e) => setField('stationName', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'StationName'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'StationName')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>

            <Col md={8}>
              <Form.Group controlId="node-address">
                <Form.Label>Address</Form.Label>
                <Form.Control
                  value={form.addressLine}
                  onChange={(e) => setField('addressLine', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'AddressLine'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'AddressLine')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>

            <Col md={4}>
              <Form.Group controlId="node-city">
                <Form.Label>City</Form.Label>
                <Form.Control
                  value={form.city}
                  onChange={(e) => setField('city', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'City'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'City')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>

            <Col md={6}>
              <Form.Group controlId="node-lat">
                <Form.Label>Latitude</Form.Label>
                <Form.Control
                  type="number"
                  step="any"
                  placeholder="6.9271"
                  value={form.lat}
                  onChange={(e) => setField('lat', e.target.value)}
                  onPaste={handleCoordinatePaste}
                  isInvalid={Boolean(fieldError(error?.errors, 'Lat'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'Lat')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>

            <Col md={6}>
              <Form.Group controlId="node-lng">
                <Form.Label>Longitude</Form.Label>
                <Form.Control
                  type="number"
                  step="any"
                  placeholder="79.8612"
                  value={form.lng}
                  onChange={(e) => setField('lng', e.target.value)}
                  onPaste={handleCoordinatePaste}
                  isInvalid={Boolean(fieldError(error?.errors, 'Lng'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'Lng')}
                </Form.Control.Feedback>
                <Form.Text className="text-secondary">
                  The map plots this node from these two values.
                </Form.Text>
              </Form.Group>
            </Col>

            {/* Checking the point before saving. A wrong longitude is still a
                real place, so no amount of validation will catch it — only
                looking does. This is an ordinary link, not an embedded map: no
                key, no library, and nothing added to the bundle. */}
            <Col md={12} className="mt-0">
              <div className="d-flex flex-wrap gap-3 align-items-center">
                {isPlottable(form.lat, form.lng) ? (
                  <a
                    href={`https://www.google.com/maps?q=${form.lat},${form.lng}`}
                    target="_blank"
                    rel="noreferrer"
                  >
                    Preview this location on a map &#8599;
                  </a>
                ) : (
                  <span className="text-secondary small">
                    Enter both coordinates to preview the location.
                  </span>
                )}
                <span className="text-secondary small">
                  You can paste a whole pair like <code>6.9271, 79.8612</code> into
                  either box.
                </span>
              </div>
            </Col>

            <Col md={6}>
              <Form.Group controlId="node-capacity">
                <Form.Label>Capacity (kWh)</Form.Label>
                <Form.Control
                  type="number"
                  step="any"
                  min="0"
                  value={form.capacityKWh}
                  onChange={(e) => setField('capacityKWh', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'CapacityKWh'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'CapacityKWh')}
                </Form.Control.Feedback>
              </Form.Group>
            </Col>

            <Col md={6}>
              <Form.Group controlId="node-slots">
                <Form.Label>Battery slots</Form.Label>
                <Form.Control
                  type="number"
                  min="1"
                  step="1"
                  value={form.totalBatterySlots}
                  onChange={(e) => setField('totalBatterySlots', e.target.value)}
                  isInvalid={Boolean(fieldError(error?.errors, 'TotalBatterySlots'))}
                  disabled={saving}
                  required
                />
                <Form.Control.Feedback type="invalid">
                  {fieldError(error?.errors, 'TotalBatterySlots')}
                </Form.Control.Feedback>
                <Form.Text className="text-secondary">
                  The most prosumers any one booking window here can hold.
                </Form.Text>
              </Form.Group>
            </Col>

            <Col md={12}>
              <hr className="my-2" />
              <ScheduleEditor
                value={form.operatingSchedule}
                onChange={(schedule) => setField('operatingSchedule', schedule)}
                disabled={saving}
              />
              {fieldError(error?.errors, 'OperatingSchedule') && (
                <div className="text-danger small mt-1">
                  {fieldError(error?.errors, 'OperatingSchedule')}
                </div>
              )}
            </Col>
          </Row>
        </Modal.Body>

        <Modal.Footer>
          <Button variant="secondary" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" disabled={saving}>
            {saving && <Spinner as="span" animation="border" size="sm" className="me-2" />}
            {isEdit ? 'Save changes' : 'Register node'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
