/*
 * File:    ReservationDetailModal.jsx
 * Author:  Thisuri
 * Created: 2026-09-29
 * Purpose: One reservation in full (GET /reservations/{id}) — slot, node, prosumer,
 *          energy, status and the audit trail of when it was created, approved or
 *          rejected, cancelled and completed — plus the actions the signed-in role may
 *          take: Edit (Backoffice), Cancel (both staff roles), Approve / Reject (Grid
 *          Operator only). The sub-dialogs (edit, cancel, reject) replace this dialog
 *          while open, then hand the updated reservation back so it reopens current.
 */
import { useEffect, useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Badge from 'react-bootstrap/Badge'
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import ListGroup from 'react-bootstrap/ListGroup'
import Modal from 'react-bootstrap/Modal'
import Row from 'react-bootstrap/Row'
import Spinner from 'react-bootstrap/Spinner'
import { useAuth } from '../../context/useAuth'
import { approveReservation, getReservation } from '../../services/reservationsApi'
import { formatDateTime } from '../../utils/format'
import { ROLES } from '../../utils/roles'
import CancelReservationModal from './CancelReservationModal'
import RejectReservationModal from './RejectReservationModal'
import ReservationFormModal from './ReservationFormModal'
import {
  OPEN_STATUSES,
  formatSlotWindow,
  formatTimeUntil,
  reservationStatusVariant,
} from './reservationDisplay'

// `reservationId` is the booking to show; `onChanged(message)` tells the list page an
// action was accepted so it can refresh. Which buttons appear depends on role and
// status, but that is a hint only: the API re-checks role and runs the transition
// guard (RESERVATION_INVALID_STATE) and the 12-hour rule whatever the UI shows.
export default function ReservationDetailModal({
  reservationId,
  nodeNames,
  nodes,
  onClose,
  onChanged,
}) {
  const { user } = useAuth()
  const isBackoffice = user.role === ROLES.BACKOFFICE
  const isOperator = user.role === ROLES.GRID_OPERATOR

  const [reservation, setReservation] = useState(null)
  const [error, setError] = useState(null)
  const [approving, setApproving] = useState(false)
  // 'view' shows this dialog; 'edit' | 'cancel' | 'reject' shows that sub-dialog instead.
  const [mode, setMode] = useState('view')

  // Loads the reservation when the dialog opens. A prosumer's 403 cannot happen here
  // (staff only), but any failure is shown using the API's detail.
  useEffect(() => {
    let cancelled = false
    getReservation(reservationId)
      .then((data) => {
        if (!cancelled) setReservation(data)
      })
      .catch((err) => {
        if (!cancelled) setError(err.detail)
      })
    return () => {
      cancelled = true
    }
  }, [reservationId])

  // Approves a Pending booking (Grid Operator). The API records approvedBy from the
  // token and approvedAt; the QR code is issued later by M4's GET /{id}/qr.
  async function handleApprove() {
    setError(null)
    setApproving(true)
    try {
      const updated = await approveReservation(reservation.id)
      setReservation(updated)
      onChanged(`Reservation for ${updated.prosumerNIC} approved.`)
    } catch (err) {
      setError(err.detail)
    } finally {
      setApproving(false)
    }
  }

  // Called by a sub-dialog after the API accepted its action: show the new state here.
  function handleSubAction(updated, message) {
    setReservation(updated)
    setError(null)
    setMode('view')
    onChanged(message)
  }

  const isOpen = reservation && OPEN_STATUSES.includes(reservation.status)

  return (
    <>
      <Modal show={mode === 'view'} onHide={onClose} centered size="lg">
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            Reservation{' '}
            {reservation && (
              <Badge bg={reservationStatusVariant(reservation.status)} className="ms-2">
                {reservation.status}
              </Badge>
            )}
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger">{error}</Alert>}
          {!reservation ? (
            !error && (
              <div className="text-center py-4">
                <Spinner animation="border" role="status">
                  <span className="visually-hidden">Loading…</span>
                </Spinner>
              </div>
            )
          ) : (
            <>
              <Row as="dl" className="mb-3">
                <Col as="dt" sm={4}>
                  Slot
                </Col>
                <Col as="dd" sm={8}>
                  {formatSlotWindow(reservation.slotStart, reservation.slotEnd)}
                  {isOpen && (
                    <span className="text-secondary small ms-2">
                      (starts {formatTimeUntil(reservation.slotStart)})
                    </span>
                  )}
                </Col>
                <Col as="dt" sm={4}>
                  Node
                </Col>
                <Col as="dd" sm={8}>
                  {nodeNames[reservation.stationId] ?? reservation.stationId}
                </Col>
                <Col as="dt" sm={4}>
                  Prosumer NIC
                </Col>
                <Col as="dd" sm={8} className="font-monospace">
                  {reservation.prosumerNIC}
                </Col>
                <Col as="dt" sm={4}>
                  Energy
                </Col>
                <Col as="dd" sm={8}>
                  {reservation.energyKWh} kWh
                </Col>
                <Col as="dt" sm={4}>
                  Reference
                </Col>
                <Col as="dd" sm={8} className="font-monospace small">
                  {reservation.id}
                </Col>
              </Row>

              <h3 className="h6">Audit trail</h3>
              <AuditTrail reservation={reservation} />
            </>
          )}
        </Modal.Body>
        <Modal.Footer>
          {isOpen && isBackoffice && (
            <Button variant="outline-primary" onClick={() => setMode('edit')}>
              Edit
            </Button>
          )}
          {isOpen && (
            <Button variant="outline-danger" onClick={() => setMode('cancel')}>
              Cancel booking
            </Button>
          )}
          {reservation?.status === 'Pending' && isOperator && (
            <>
              <Button
                variant="outline-danger"
                onClick={() => setMode('reject')}
                disabled={approving}
              >
                Reject
              </Button>
              <Button variant="success" onClick={handleApprove} disabled={approving}>
                {approving ? 'Approving…' : 'Approve'}
              </Button>
            </>
          )}
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
        </Modal.Footer>
      </Modal>

      {mode === 'edit' && (
        <ReservationFormModal
          reservation={reservation}
          nodes={nodes}
          onClose={() => setMode('view')}
          onSaved={(updated) => handleSubAction(updated, 'Reservation updated.')}
        />
      )}
      {mode === 'cancel' && (
        <CancelReservationModal
          reservation={reservation}
          onClose={() => setMode('view')}
          onCancelled={(updated) => handleSubAction(updated, 'Reservation cancelled.')}
        />
      )}
      {mode === 'reject' && (
        <RejectReservationModal
          reservation={reservation}
          onClose={() => setMode('view')}
          onRejected={(updated) => handleSubAction(updated, 'Reservation rejected.')}
        />
      )}
    </>
  )
}

// Lists the lifecycle events the reservation has actually been through, oldest first.
// Only timestamps the API recorded are shown; nothing is inferred on the client.
function AuditTrail({ reservation: r }) {
  const events = [
    { label: 'Created', at: r.createdAt },
    r.approvedAt && {
      label: 'Approved',
      at: r.approvedAt,
      note: r.approvedBy ? `by ${r.approvedBy}` : null,
    },
    r.qrIssuedAt && {
      label: 'QR code issued',
      at: r.qrIssuedAt,
      note: r.qrExpiresAt ? `expires ${formatDateTime(r.qrExpiresAt)}` : null,
    },
    r.cancelledAt && { label: 'Cancelled', at: r.cancelledAt },
    r.completedAt && {
      label: 'Completed',
      at: r.completedAt,
      note: r.verifiedByOperatorId ? `verified by ${r.verifiedByOperatorId}` : null,
    },
  ]
    .filter(Boolean)
    .sort((a, b) => new Date(a.at) - new Date(b.at))

  return (
    <ListGroup variant="flush" className="small">
      {events.map((e) => (
        <ListGroup.Item key={e.label} className="d-flex justify-content-between px-0">
          <span>
            <strong>{e.label}</strong>
            {e.note && <span className="text-secondary ms-2">{e.note}</span>}
          </span>
          <span>{formatDateTime(e.at)}</span>
        </ListGroup.Item>
      ))}
      {r.status === 'Rejected' && (
        <ListGroup.Item className="px-0">
          <strong>Rejected</strong>
          <span className="text-secondary ms-2">reason: {r.rejectionReason ?? '—'}</span>
        </ListGroup.Item>
      )}
      <ListGroup.Item className="d-flex justify-content-between px-0 text-secondary">
        <span>Last updated</span>
        <span>{formatDateTime(r.updatedAt)}</span>
      </ListGroup.Item>
    </ListGroup>
  )
}
