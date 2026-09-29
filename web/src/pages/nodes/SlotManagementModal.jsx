/*
 * File:    SlotManagementModal.jsx
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: The booking windows on one microgrid node — opening them, editing them and
 *          removing them (GET /nodes/{id}/slots, DELETE /slots/{id}).
 *
 *          The reserved count and the window status are shown but have no controls.
 *          Only the reservation side writes those, and the request types the API
 *          accepts do not carry them, so there is nothing for this screen to send.
 *
 *          Deletion is not pre-checked. A window with bookings against it is refused by
 *          the API, and that refusal is rendered here rather than guessed at in
 *          advance — the same reason the node's take-out-of-service button stays live.
 */
import { useEffect, useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Badge from 'react-bootstrap/Badge'
import Button from 'react-bootstrap/Button'
import Modal from 'react-bootstrap/Modal'
import Spinner from 'react-bootstrap/Spinner'
import Table from 'react-bootstrap/Table'
import { useAuth } from '../../context/useAuth'
import { deleteSlot, getNodeSlots } from '../../services/nodesApi'
import { formatDateTime } from '../../utils/format'
import { ROLES } from '../../utils/roles'
import SlotFormModal from './SlotFormModal'
import { slotStatusVariant } from './nodeDisplay'

// `node` is the node whose windows these are. The caller gives this component a key,
// so opening a different node mounts a fresh one.
export default function SlotManagementModal({ node, onClose }) {
  const { user } = useAuth()
  const isBackoffice = user.role === ROLES.BACKOFFICE

  const [slots, setSlots] = useState([])
  const [loading, setLoading] = useState(true)
  const [reloadKey, setReloadKey] = useState(0)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)
  const [busyId, setBusyId] = useState(null)

  // null = form closed. {} opens it for a new window; a slot opens it for an edit.
  const [editing, setEditing] = useState(null)

  // Loads this node's windows. `cancelled` drops a reply that arrives after the dialog
  // has closed, so it cannot set state on a component that is gone.
  useEffect(() => {
    let cancelled = false

    getNodeSlots(node.id)
      .then((data) => {
        if (!cancelled) setSlots(data)
      })
      .catch((err) => {
        if (!cancelled) setError(err.detail)
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [node.id, reloadKey])

  // Re-fetches the list after a change.
  function reload() {
    setLoading(true)
    setReloadKey((k) => k + 1)
  }

  // Called once the API accepted a create or an edit.
  function handleSaved(saved, wasEdit) {
    setEditing(null)
    setError(null)
    setSuccess(wasEdit ? 'Booking window updated.' : 'Booking window opened.')
    reload()
  }

  // Removes one window. No confirmation step and no pre-check: the API refuses a
  // window that reservations still point at, and that refusal is the useful answer.
  async function handleDelete(slot) {
    setError(null)
    setSuccess(null)
    setBusyId(slot.id)

    try {
      await deleteSlot(slot.id)
      setSuccess('Booking window removed.')
      reload()
    } catch (err) {
      setError(err)
    } finally {
      setBusyId(null)
    }
  }

  // A refusal is a warning rather than a failure.
  const isConflict = typeof error === 'object' && error?.status === 409
  const errorText = typeof error === 'object' ? error?.detail : error

  return (
    <>
      <Modal show onHide={onClose} size="xl" scrollable>
        <Modal.Header closeButton>
          <Modal.Title className="h5">
            Booking windows — {node.stationName}
            <span className="text-secondary fs-6 ms-2">
              {node.totalBatterySlots} battery slots
            </span>
          </Modal.Title>
        </Modal.Header>

        <Modal.Body>
          <div className="d-flex justify-content-end mb-3">
            <Button variant="primary" size="sm" onClick={() => setEditing({})}>
              Open a window
            </Button>
          </div>

          {errorText && (
            <Alert variant={isConflict ? 'warning' : 'danger'} dismissible onClose={() => setError(null)}>
              {errorText}
            </Alert>
          )}
          {success && (
            <Alert variant="success" dismissible onClose={() => setSuccess(null)}>
              {success}
            </Alert>
          )}

          {loading ? (
            <div className="text-center py-5">
              <Spinner animation="border" role="status">
                <span className="visually-hidden">Loading…</span>
              </Spinner>
            </div>
          ) : slots.length === 0 ? (
            <p className="text-secondary mb-0">
              This node has no booking windows yet. Open one to make it bookable.
            </p>
          ) : (
            <Table responsive hover className="align-middle">
              <thead>
                <tr>
                  <th>Starts</th>
                  <th>Ends</th>
                  <th className="text-end">Booked</th>
                  <th className="text-end">Energy</th>
                  <th>Status</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {slots.map((s) => (
                  <tr key={s.id}>
                    <td className="text-nowrap">{formatDateTime(s.slotStart)}</td>
                    <td className="text-nowrap">{formatDateTime(s.slotEnd)}</td>
                    <td className="text-end text-nowrap">
                      {s.reservedCount} / {s.totalCapacity}
                      {s.availableCapacity > 0 && (
                        <span className="text-secondary small ms-2">
                          {s.availableCapacity} free
                        </span>
                      )}
                    </td>
                    <td className="text-end text-nowrap">{s.energyPerSlotKWh} kWh</td>
                    <td>
                      <Badge bg={slotStatusVariant(s.status)}>{s.status}</Badge>
                    </td>
                    <td className="text-end text-nowrap">
                      <Button
                        size="sm"
                        variant="outline-primary"
                        onClick={() => setEditing(s)}
                        disabled={busyId !== null}
                      >
                        Edit
                      </Button>
                      {/* Removing a window is administrative — operators adjust
                          availability, the back office destroys it. The API answers a
                          Grid Operator with 403 whatever this shows. */}
                      {isBackoffice && (
                        <Button
                          size="sm"
                          variant="outline-danger"
                          className="ms-2"
                          onClick={() => handleDelete(s)}
                          disabled={busyId !== null}
                        >
                          {busyId === s.id && (
                            <Spinner as="span" animation="border" size="sm" className="me-1" />
                          )}
                          Remove
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}

          <p className="text-secondary small mb-0">
            Booked counts and window status are set by the reservation process and cannot
            be edited here.
          </p>
        </Modal.Body>

        <Modal.Footer>
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
        </Modal.Footer>
      </Modal>

      {editing && (
        <SlotFormModal
          key={editing.id ?? 'new'}
          node={node}
          slot={editing.id ? editing : null}
          onClose={() => setEditing(null)}
          onSaved={handleSaved}
        />
      )}
    </>
  )
}
