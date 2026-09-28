/*
 * File:    NodeDetailModal.jsx
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: One node in full — location, capacity, opening hours, its booking windows
 *          and how many reservations are currently live against it (GET /nodes/{id}) —
 *          plus the Backoffice-only actions that take it in and out of service.
 *
 *          Deactivation is refused while active reservations reference the node. This
 *          dialog renders that refusal exactly as the API worded it, including the
 *          count, because the count is the part no client could have worked out for
 *          itself. The button is never disabled to pre-empt the rule: the refusal is
 *          the server's to give, and hiding the attempt would hide the rule.
 */
import { useEffect, useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Badge from 'react-bootstrap/Badge'
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import Modal from 'react-bootstrap/Modal'
import Row from 'react-bootstrap/Row'
import Spinner from 'react-bootstrap/Spinner'
import Table from 'react-bootstrap/Table'
import { useAuth } from '../../context/useAuth'
import { activateNode, deactivateNode, getNode } from '../../services/nodesApi'
import { formatDateTime } from '../../utils/format'
import { ROLES } from '../../utils/roles'
import { dayName, formatCoordinates, nodeStatusVariant } from './nodeDisplay'

// `nodeId` is the node to show. `onChanged` receives the updated node after a status
// change so the list behind the dialog can stay in step.
//
// The caller gives this component a key, so opening a different node mounts a fresh
// one. That is why the fetch effect only fetches: it has no state to reset first.
export default function NodeDetailModal({ nodeId, onClose, onChanged }) {
  const { user } = useAuth()
  const isBackoffice = user.role === ROLES.BACKOFFICE

  const [node, setNode] = useState(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)

  // Loads the node on mount. `cancelled` drops a stale reply so a response that
  // arrives after the dialog closed cannot set state on a gone component.
  useEffect(() => {
    let cancelled = false

    getNode(nodeId)
      .then((data) => {
        if (!cancelled) setNode(data)
      })
      .catch((err) => {
        if (!cancelled) setError(err)
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [nodeId])

  // Runs a status change and re-reads the node, so the counts shown afterwards are the
  // server's rather than this component's guess at what they became.
  async function changeStatus(action) {
    setBusy(true)
    setError(null)
    setSuccess(null)

    try {
      const updated = await action(nodeId)
      const refreshed = await getNode(nodeId)

      setNode(refreshed)
      setSuccess(`${updated.stationName} is now ${updated.status.toLowerCase()}.`)
      onChanged(updated)
    } catch (err) {
      setError(err)
      // A refusal leaves the node as it was, but the counts behind it may have moved
      // since the dialog opened — re-read so the number on screen matches the reason.
      try {
        setNode(await getNode(nodeId))
      } catch {
        // Leave the previously loaded node on screen; the refusal is the useful part.
      }
    } finally {
      setBusy(false)
    }
  }

  // A rule refusal is a warning, not a failure: the request was understood and the
  // system is explaining why it will not do it.
  const isConflict = error?.status === 409
  const isActive = node?.status === 'Active'

  return (
    <Modal show onHide={onClose} size="lg" scrollable>
      <Modal.Header closeButton>
        <Modal.Title>{node ? node.stationName : 'Microgrid node'}</Modal.Title>
      </Modal.Header>

      <Modal.Body>
        {error && (
          <Alert variant={isConflict ? 'warning' : 'danger'}>
            {isConflict && <strong className="d-block mb-1">Cannot deactivate this node</strong>}
            {error.detail}
          </Alert>
        )}
        {success && <Alert variant="success">{success}</Alert>}

        {loading ? (
          <div className="text-center py-5">
            <Spinner animation="border" role="status">
              <span className="visually-hidden">Loading…</span>
            </Spinner>
          </div>
        ) : !node ? (
          <p className="text-secondary mb-0">This node could not be loaded.</p>
        ) : (
          <>
            <Row className="g-3 mb-3">
              <Col md={6}>
                <div className="text-secondary small">Status</div>
                <Badge bg={nodeStatusVariant(node.status)}>{node.status}</Badge>
              </Col>
              <Col md={6}>
                <div className="text-secondary small">Location</div>
                <div className="font-monospace">{formatCoordinates(node.lat, node.lng)}</div>
              </Col>
              <Col md={12}>
                <div className="text-secondary small">Address</div>
                <div>
                  {node.addressLine}, {node.city}
                </div>
              </Col>
            </Row>

            <Row className="g-3 mb-3">
              <Col sm={6} md={3}>
                <div className="text-secondary small">Capacity</div>
                <div className="fs-5">{node.capacityKWh} kWh</div>
              </Col>
              <Col sm={6} md={3}>
                <div className="text-secondary small">Battery slots</div>
                <div className="fs-5">{node.totalBatterySlots}</div>
              </Col>
              <Col sm={6} md={3}>
                <div className="text-secondary small">Booking windows</div>
                <div className="fs-5">
                  {node.openSlotCount} open <span className="text-secondary fs-6">of {node.slotCount}</span>
                </div>
              </Col>
              <Col sm={6} md={3}>
                <div className="text-secondary small">Active reservations</div>
                <div className="fs-5">{node.activeReservationCount}</div>
              </Col>
            </Row>

            {node.activeReservationCount > 0 && isActive && (
              <Alert variant="light" className="border py-2 small">
                This node currently has {node.activeReservationCount} active reservation
                {node.activeReservationCount === 1 ? '' : 's'}. Deactivation will be refused
                until they are cancelled or completed.
              </Alert>
            )}

            <div className="text-secondary small mb-1">Operating schedule</div>
            {node.operatingSchedule.length === 0 ? (
              <p className="text-secondary">No opening days recorded.</p>
            ) : (
              <Table size="sm" className="align-middle">
                <thead>
                  <tr>
                    <th>Day</th>
                    <th>Opens</th>
                    <th>Closes</th>
                  </tr>
                </thead>
                <tbody>
                  {[...node.operatingSchedule]
                    .sort((a, b) => a.dayOfWeek - b.dayOfWeek)
                    .map((entry) => (
                      <tr key={entry.dayOfWeek}>
                        <td>{dayName(entry.dayOfWeek)}</td>
                        <td>{entry.openTime}</td>
                        <td>{entry.closeTime}</td>
                      </tr>
                    ))}
                </tbody>
              </Table>
            )}

            <div className="text-secondary small mt-3">
              Registered {formatDateTime(node.createdAt)} · last updated{' '}
              {formatDateTime(node.updatedAt)}
            </div>
          </>
        )}
      </Modal.Body>

      <Modal.Footer>
        {/* Backoffice alone may change a node's service status. Hiding these from a
            Grid Operator is convenience — the API answers them with 403 regardless. */}
        {isBackoffice && node && (
          <div className="me-auto">
            {isActive ? (
              <Button
                variant="outline-danger"
                disabled={busy}
                onClick={() => changeStatus(deactivateNode)}
              >
                {busy && <Spinner as="span" animation="border" size="sm" className="me-2" />}
                Take out of service
              </Button>
            ) : (
              <Button
                variant="success"
                disabled={busy}
                onClick={() => changeStatus(activateNode)}
              >
                {busy && <Spinner as="span" animation="border" size="sm" className="me-2" />}
                Return to service
              </Button>
            )}
          </div>
        )}
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      </Modal.Footer>
    </Modal>
  )
}
