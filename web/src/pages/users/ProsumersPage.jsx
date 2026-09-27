/*
 * File:    ProsumersPage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Prosumer Management for staff — paged list with search and status filter,
 *          detail/edit, and (Backoffice only) activate / deactivate (GET /prosumers).
 */
import { useEffect, useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Badge from 'react-bootstrap/Badge'
import Button from 'react-bootstrap/Button'
import Col from 'react-bootstrap/Col'
import Form from 'react-bootstrap/Form'
import Pagination from 'react-bootstrap/Pagination'
import Row from 'react-bootstrap/Row'
import Spinner from 'react-bootstrap/Spinner'
import Table from 'react-bootstrap/Table'
import { useAuth } from '../../context/useAuth'
import { activateProsumer, getProsumers } from '../../services/identityApi'
import { formatDateTime, statusVariant } from '../../utils/format'
import { ROLES } from '../../utils/roles'
import DeactivateProsumerModal from './DeactivateProsumerModal'
import ProsumerDetailModal from './ProsumerDetailModal'

const PAGE_SIZE = 20
const STATUS_OPTIONS = ['Pending', 'Active', 'Deactivated']

// Lists prosumers page by page. Both staff roles can view and edit contact details;
// only Backoffice sees Activate / Deactivate. Hiding the buttons is UX — the API
// refuses a Grid Operator with 403 AUTH_FORBIDDEN_ROLE regardless (rule 1).
export default function ProsumersPage() {
  const { user } = useAuth()
  const isBackoffice = user.role === ROLES.BACKOFFICE

  // `query` is what was last sent to the API; `searchText` is the unsubmitted input.
  const [query, setQuery] = useState({ q: '', status: '', page: 1 })
  const [searchText, setSearchText] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [result, setResult] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)
  const [busyNic, setBusyNic] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [deactivating, setDeactivating] = useState(null)

  // Fetches the current page whenever the query changes or a reload is requested.
  // State is only set in the async callbacks; `cancelled` drops stale responses, so a
  // slow page-1 reply cannot overwrite a newer page-2 reply.
  useEffect(() => {
    let cancelled = false
    getProsumers({ ...query, pageSize: PAGE_SIZE })
      .then((data) => {
        if (!cancelled) setResult(data)
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

  // Changes the query (search, filter or page) and shows the spinner while it loads.
  function changeQuery(changes) {
    setLoading(true)
    setError(null)
    setQuery((current) => ({ ...current, ...changes }))
  }

  // Re-fetches the current page, e.g. after a status change that may move a row out
  // of the active filter.
  function reload() {
    setLoading(true)
    setReloadKey((k) => k + 1)
  }

  // Search submit: new search text always starts again from page 1.
  function handleSearch(event) {
    event.preventDefault()
    changeQuery({ q: searchText.trim(), page: 1 })
  }

  // Activates one prosumer (Backoffice only). The API's detail is shown on failure.
  async function handleActivate(prosumer) {
    setError(null)
    setSuccess(null)
    setBusyNic(prosumer.nic)
    try {
      const updated = await activateProsumer(prosumer.nic)
      setSuccess(`${updated.fullName} (${updated.nic}) is now active.`)
      reload()
    } catch (err) {
      setError(err.detail)
    } finally {
      setBusyNic(null)
    }
  }

  // Called by the deactivate dialog after the API accepted the deactivation.
  function handleDeactivated(updated) {
    setDeactivating(null)
    setSuccess(`${updated.fullName} (${updated.nic}) has been deactivated.`)
    reload()
  }

  // Called by the detail dialog after a successful edit: swap the row in place.
  function handleSaved(updated) {
    setViewing(null)
    setSuccess(`${updated.fullName}'s contact details were saved.`)
    setResult((r) => ({
      ...r,
      items: r.items.map((p) => (p.nic === updated.nic ? updated : p)),
    }))
  }

  const items = result?.items ?? []
  const totalPages = result?.totalPages ?? 0

  return (
    <>
      <h1 className="h3 mb-3">Prosumers</h1>

      <Form onSubmit={handleSearch} className="mb-3">
        <Row className="g-2">
          <Col md={6}>
            <Form.Control
              type="search"
              placeholder="Search by name, NIC, email or username"
              aria-label="Search prosumers"
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
              onChange={(e) => changeQuery({ status: e.target.value, page: 1 })}
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
      ) : items.length === 0 ? (
        <p className="text-secondary">No prosumers match.</p>
      ) : (
        <>
          <Table responsive hover className="align-middle">
            <thead>
              <tr>
                <th>NIC</th>
                <th>Full name</th>
                <th>Email</th>
                <th>Phone</th>
                <th>Status</th>
                <th>Registered</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {items.map((p) => (
                <tr key={p.nic}>
                  <td className="font-monospace">{p.nic}</td>
                  <td>{p.fullName}</td>
                  <td>{p.email}</td>
                  <td>{p.phone}</td>
                  <td>
                    <Badge bg={statusVariant(p.status)}>{p.status}</Badge>
                  </td>
                  <td>{formatDateTime(p.createdAt)}</td>
                  <td className="text-end text-nowrap">
                    <Button size="sm" variant="outline-secondary" onClick={() => setViewing(p)}>
                      View
                    </Button>
                    {isBackoffice && p.status !== 'Active' && (
                      <Button
                        size="sm"
                        variant="success"
                        className="ms-2"
                        onClick={() => handleActivate(p)}
                        disabled={busyNic !== null}
                      >
                        {busyNic === p.nic
                          ? 'Activating…'
                          : p.status === 'Pending'
                            ? 'Activate'
                            : 'Reactivate'}
                      </Button>
                    )}
                    {isBackoffice && p.status !== 'Deactivated' && (
                      <Button
                        size="sm"
                        variant="outline-danger"
                        className="ms-2"
                        onClick={() => setDeactivating(p)}
                        disabled={busyNic !== null}
                      >
                        Deactivate
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>

          <div className="d-flex justify-content-between align-items-center">
            <span className="text-secondary small">
              {result.totalCount} prosumer{result.totalCount === 1 ? '' : 's'} · page{' '}
              {result.page} of {totalPages}
            </span>
            <Pagination className="mb-0">
              <Pagination.Prev
                disabled={query.page <= 1}
                onClick={() => changeQuery({ page: query.page - 1 })}
              />
              <Pagination.Next
                disabled={query.page >= totalPages}
                onClick={() => changeQuery({ page: query.page + 1 })}
              />
            </Pagination>
          </div>
        </>
      )}

      {viewing && (
        <ProsumerDetailModal
          prosumer={viewing}
          onClose={() => setViewing(null)}
          onSaved={handleSaved}
        />
      )}
      {deactivating && (
        <DeactivateProsumerModal
          prosumer={deactivating}
          onClose={() => setDeactivating(null)}
          onDeactivated={handleDeactivated}
        />
      )}
    </>
  )
}
