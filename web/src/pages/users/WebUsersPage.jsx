/*
 * File:    WebUsersPage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Web User Management (Backoffice only) — list, filter, create, edit and
 *          activate/deactivate Backoffice and Grid Operator accounts (GET /webusers).
 */
import { useEffect, useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Badge from 'react-bootstrap/Badge'
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import Form from 'react-bootstrap/Form'
import Row from 'react-bootstrap/Row'
import Spinner from 'react-bootstrap/Spinner'
import Table from 'react-bootstrap/Table'
import { useAuth } from '../../context/useAuth'
import { getWebUsers, setWebUserStatus } from '../../services/identityApi'
import { formatDateTime, statusVariant } from '../../utils/format'
import { ROLES } from '../../utils/roles'
import WebUserFormModal from './WebUserFormModal'

const ROLE_LABELS = {
  [ROLES.BACKOFFICE]: 'Backoffice',
  [ROLES.GRID_OPERATOR]: 'Grid Operator',
}

// Lists web users with role/status filters (applied by the API). Rule: the last active
// Backoffice cannot be demoted or deactivated — deliberately NOT pre-checked here, so
// the API's USER_LAST_BACKOFFICE detail is what the officer sees (client never decides).
export default function WebUsersPage() {
  const { user } = useAuth()
  const [filters, setFilters] = useState({ role: '', status: '' })
  const [reloadKey, setReloadKey] = useState(0)
  const [webUsers, setWebUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)
  const [busyId, setBusyId] = useState(null)
  // null = closed, 'new' = create mode, a user object = edit mode.
  const [editing, setEditing] = useState(null)

  // Fetches the list whenever the filters change or a reload is requested. State is
  // only set in the async callbacks; `cancelled` drops stale responses.
  useEffect(() => {
    let cancelled = false
    getWebUsers(filters)
      .then((rows) => {
        if (!cancelled) setWebUsers(rows)
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
  }, [filters, reloadKey])

  // Changes one filter and shows the spinner while the list reloads.
  function changeFilter(name, value) {
    setLoading(true)
    setError(null)
    setFilters((f) => ({ ...f, [name]: value }))
  }

  // Re-fetches with the current filters (a saved row may no longer match them).
  function reload() {
    setLoading(true)
    setReloadKey((k) => k + 1)
  }

  // Flips a user between Active and Deactivated via PATCH /webusers/{id}/status.
  // Deactivation asks for confirmation first; the API may still refuse it with
  // USER_LAST_BACKOFFICE, whose detail is shown as-is.
  async function handleToggleStatus(target) {
    const newStatus = target.status === 'Active' ? 'Deactivated' : 'Active'
    if (
      newStatus === 'Deactivated' &&
      !window.confirm(`Deactivate ${target.username}? They will not be able to sign in.`)
    ) {
      return
    }
    setError(null)
    setSuccess(null)
    setBusyId(target.id)
    try {
      const updated = await setWebUserStatus(target.id, newStatus)
      setSuccess(`${updated.username} is now ${updated.status}.`)
      reload()
    } catch (err) {
      setError(err.detail)
    } finally {
      setBusyId(null)
    }
  }

  // Called by the form after a successful create or edit.
  function handleSaved(saved, wasEdit) {
    setEditing(null)
    setSuccess(wasEdit ? `${saved.username} was updated.` : `${saved.username} was created.`)
    reload()
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h3 mb-0">Web Users</h1>
        <Button onClick={() => setEditing('new')}>New web user</Button>
      </div>

      <Row className="g-2 mb-3">
        <Col md={3}>
          <Form.Select
            aria-label="Filter by role"
            value={filters.role}
            onChange={(e) => changeFilter('role', e.target.value)}
          >
            <option value="">All roles</option>
            {Object.entries(ROLE_LABELS).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </Form.Select>
        </Col>
        <Col md={3}>
          <Form.Select
            aria-label="Filter by status"
            value={filters.status}
            onChange={(e) => changeFilter('status', e.target.value)}
          >
            <option value="">All statuses</option>
            <option value="Active">Active</option>
            <option value="Deactivated">Deactivated</option>
          </Form.Select>
        </Col>
      </Row>

      {error && (
        <Alert variant="danger" dismissible onClose={() => setError(null)}>
          {error}
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
      ) : webUsers.length === 0 ? (
        <p className="text-secondary">No web users match.</p>
      ) : (
        <Table responsive hover className="align-middle">
          <thead>
            <tr>
              <th>Username</th>
              <th>Full name</th>
              <th>Email</th>
              <th>Phone</th>
              <th>Role</th>
              <th>Status</th>
              <th>Created</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {webUsers.map((u) => (
              <tr key={u.id}>
                <td>
                  {u.username}
                  {u.id === user.id && <span className="text-secondary small"> (you)</span>}
                </td>
                <td>{u.fullName}</td>
                <td>{u.email}</td>
                <td>{u.phone}</td>
                <td>{ROLE_LABELS[u.role] ?? u.role}</td>
                <td>
                  <Badge bg={statusVariant(u.status)}>{u.status}</Badge>
                </td>
                <td>{formatDateTime(u.createdAt)}</td>
                <td className="text-end text-nowrap">
                  <Button size="sm" variant="outline-secondary" onClick={() => setEditing(u)}>
                    Edit
                  </Button>
                  <Button
                    size="sm"
                    className="ms-2"
                    variant={u.status === 'Active' ? 'outline-danger' : 'success'}
                    onClick={() => handleToggleStatus(u)}
                    disabled={busyId !== null}
                  >
                    {busyId === u.id ? 'Saving…' : u.status === 'Active' ? 'Deactivate' : 'Activate'}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {editing && (
        <WebUserFormModal
          webUser={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={handleSaved}
        />
      )}
    </>
  )
}
