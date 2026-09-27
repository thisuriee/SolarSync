/*
 * File:    PendingActivationsPage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Backoffice queue of prosumers who registered on Android and are waiting to be
 *          activated. Activating here is what lets them log in (assignment §4, rule 1).
 */
import { useCallback, useEffect, useState } from 'react'
import Alert from 'react-bootstrap/Alert'
import Button from 'react-bootstrap/Button'
import Spinner from 'react-bootstrap/Spinner'
import Table from 'react-bootstrap/Table'
import { activateProsumer, getPendingProsumers } from '../../services/identityApi'
import { formatDateTime } from '../../utils/format'

// Lists every Pending prosumer (oldest first, as the API orders them) with an Activate
// button. Rule: only Backoffice may activate — enforced by the API attribute and the
// re-check in UserService.Activate; this page is Backoffice-only in App.jsx for UX.
export default function PendingActivationsPage() {
  const [pending, setPending] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)
  // NIC of the row whose Activate call is in flight, so only that button spins.
  const [activatingNic, setActivatingNic] = useState(null)

  // Bumped by the Refresh button; the effect below re-fetches whenever it changes.
  const [reloadKey, setReloadKey] = useState(0)

  // Loads the queue from GET /prosumers/pending. State is only set in the async
  // callbacks, and `cancelled` drops a response that arrives after unmount/re-fetch.
  useEffect(() => {
    let cancelled = false
    getPendingProsumers()
      .then((rows) => {
        if (!cancelled) setPending(rows)
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
  }, [reloadKey])

  // Refresh button: show the spinner and trigger the effect again.
  const loadPending = useCallback(() => {
    setLoading(true)
    setError(null)
    setReloadKey((k) => k + 1)
  }, [])

  // Activates one prosumer via PATCH /prosumers/{nic}/activate. On success the row leaves
  // the queue; on failure the API's detail is shown unchanged (the client never writes
  // its own rule message).
  async function handleActivate(prosumer) {
    setError(null)
    setSuccess(null)
    setActivatingNic(prosumer.nic)
    try {
      const activated = await activateProsumer(prosumer.nic)
      setPending((rows) => rows.filter((p) => p.nic !== activated.nic))
      setSuccess(`${activated.fullName} (${activated.nic}) is now active and can sign in.`)
    } catch (err) {
      setError(err.detail)
    } finally {
      setActivatingNic(null)
    }
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h3 mb-0">Pending Activations</h1>
        <Button variant="outline-secondary" size="sm" onClick={loadPending} disabled={loading}>
          Refresh
        </Button>
      </div>

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
      ) : pending.length === 0 ? (
        <p className="text-secondary">No prosumers are waiting for activation.</p>
      ) : (
        <Table responsive hover className="align-middle">
          <thead>
            <tr>
              <th>NIC</th>
              <th>Full name</th>
              <th>Email</th>
              <th>Phone</th>
              <th>Registered</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {pending.map((p) => (
              <tr key={p.nic}>
                <td className="font-monospace">{p.nic}</td>
                <td>{p.fullName}</td>
                <td>{p.email}</td>
                <td>{p.phone}</td>
                <td>{formatDateTime(p.createdAt)}</td>
                <td className="text-end">
                  <Button
                    size="sm"
                    variant="success"
                    onClick={() => handleActivate(p)}
                    disabled={activatingNic !== null}
                  >
                    {activatingNic === p.nic ? 'Activating…' : 'Activate'}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </>
  )
}
