/*
 * File:    NodesPage.jsx
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: Microgrid Node Management for staff — the node list with a status filter and
 *          a name/city search, opening the detail dialog and, for Backoffice, the
 *          register and edit forms (GET /nodes).
 *
 *          Filtering and searching are query parameters, not work done in the browser:
 *          the API decides what matches and what each role may see, and this screen
 *          renders the answer.
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
import { getNodes } from '../../services/nodesApi'
import { ROLES } from '../../utils/roles'
import NodeDetailModal from './NodeDetailModal'
import NodeFormModal from './NodeFormModal'
import SlotManagementModal from './SlotManagementModal'
import { nodeStatusVariant } from './nodeDisplay'

const STATUS_OPTIONS = ['Active', 'Inactive']

// Lists microgrid nodes. Both staff roles can view; only Backoffice sees Register and
// Edit. Hiding those buttons is convenience — the API answers a Grid Operator with
// 403 AUTH_FORBIDDEN_ROLE whatever the screen shows.
export default function NodesPage() {
  const { user } = useAuth()
  const isBackoffice = user.role === ROLES.BACKOFFICE

  // `query` is what was last sent to the API; `searchText` is the unsubmitted input.
  const [query, setQuery] = useState({ q: '', status: '' })
  const [searchText, setSearchText] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [nodes, setNodes] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)

  // null = closed. A string id opens the detail dialog; the form dialog opens with
  // either a node (edit) or the sentinel below (register).
  const [viewingId, setViewingId] = useState(null)
  const [editing, setEditing] = useState(null)
  const [managingSlots, setManagingSlots] = useState(null)

  // Fetches the list whenever the query changes or a reload is asked for. `cancelled`
  // drops a stale reply, so a slow unfiltered response cannot overwrite a newer
  // filtered one.
  useEffect(() => {
    let cancelled = false

    getNodes(query)
      .then((data) => {
        if (!cancelled) setNodes(data)
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
  }, [query, reloadKey])

  // Changes the query and shows the spinner while the new list loads.
  function changeQuery(changes) {
    setLoading(true)
    setError(null)
    setQuery((current) => ({ ...current, ...changes }))
  }

  // Re-fetches the current list, e.g. after a status change that may move a row out of
  // the active filter.
  function reload() {
    setLoading(true)
    setReloadKey((k) => k + 1)
  }

  // Search submit. Kept out of the input's onChange so the API is asked once, when the
  // user is done typing, rather than on every keystroke.
  function handleSearch(event) {
    event.preventDefault()
    changeQuery({ q: searchText.trim() })
  }

  // Called by the form dialog once the API accepted a create or an edit.
  function handleSaved(saved, wasEdit) {
    setEditing(null)
    setSuccess(
      wasEdit ? `${saved.stationName} was updated.` : `${saved.stationName} was registered.`,
    )
    reload()
  }

  // Called by the detail dialog after the API accepted a status change. The message is
  // set here as well as in the dialog, so it is still on screen once the dialog closes.
  function handleStatusChanged(updated) {
    setSuccess(`${updated.stationName} is now ${updated.status.toLowerCase()}.`)
    reload()
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h3 mb-0">Microgrid Nodes</h1>
        {isBackoffice && (
          <Button variant="primary" onClick={() => setEditing({})}>
            Register node
          </Button>
        )}
      </div>

      <Form onSubmit={handleSearch} className="mb-3">
        <Row className="g-2">
          <Col md={6}>
            <Form.Control
              type="search"
              placeholder="Search by station name or city"
              aria-label="Search nodes"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
            />
          </Col>
          <Col xs="auto">
            <Button type="submit" variant="outline-primary">
              Search
            </Button>
          </Col>
          <Col md={3}>
            <Form.Select
              aria-label="Filter by status"
              value={query.status}
              onChange={(e) => changeQuery({ status: e.target.value })}
            >
              <option value="">All statuses</option>
              {STATUS_OPTIONS.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </Form.Select>
          </Col>
        </Row>
      </Form>

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
      ) : nodes.length === 0 ? (
        <p className="text-secondary">No microgrid nodes match.</p>
      ) : (
        <Table responsive hover className="align-middle">
          <thead>
            <tr>
              <th>Station</th>
              <th>City</th>
              <th className="text-end">Capacity</th>
              <th className="text-end">Battery slots</th>
              <th>Coordinates</th>
              <th>Status</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {nodes.map((n) => (
              <tr key={n.id}>
                <td>{n.stationName}</td>
                <td>{n.city}</td>
                <td className="text-end text-nowrap">{n.capacityKWh} kWh</td>
                <td className="text-end">{n.totalBatterySlots}</td>
                <td className="font-monospace small text-nowrap">
                  {n.lat}, {n.lng}
                </td>
                <td>
                  <Badge bg={nodeStatusVariant(n.status)}>{n.status}</Badge>
                </td>
                <td className="text-end text-nowrap">
                  <Button size="sm" variant="outline-secondary" onClick={() => setViewingId(n.id)}>
                    View
                  </Button>
                  {/* Opening and adjusting availability is an operator's job as much as
                      the back office's, so both roles reach this. */}
                  <Button
                    size="sm"
                    variant="outline-secondary"
                    className="ms-2"
                    onClick={() => setManagingSlots(n)}
                  >
                    Windows
                  </Button>
                  {isBackoffice && (
                    <Button
                      size="sm"
                      variant="outline-primary"
                      className="ms-2"
                      onClick={() => setEditing(n)}
                    >
                      Edit
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {viewingId && (
        <NodeDetailModal
          key={viewingId}
          nodeId={viewingId}
          onClose={() => setViewingId(null)}
          onChanged={handleStatusChanged}
        />
      )}

      {managingSlots && (
        <SlotManagementModal
          key={managingSlots.id}
          node={managingSlots}
          onClose={() => setManagingSlots(null)}
        />
      )}

      {editing && (
        <NodeFormModal
          key={editing.id ?? 'new'}
          node={editing.id ? editing : null}
          onClose={() => setEditing(null)}
          onSaved={handleSaved}
        />
      )}
    </>
  )
}
