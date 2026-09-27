/*
 * File:    BackofficeHomePage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Backoffice landing page: the activation queue size, account counts, and
 *          shortcuts to every Backoffice screen.
 */
import { useEffect, useState } from 'react'
import Col from 'react-bootstrap/Col'
import Row from 'react-bootstrap/Row'
import QuickLinks from '../../components/QuickLinks'
import StatCard from '../../components/StatCard'
import { useAuth } from '../../context/useAuth'
import { getPendingProsumers, getProsumers, getWebUsers } from '../../services/identityApi'

// Greets the officer and loads three counts in parallel. allSettled, so one failing
// call shows '—' for that card only. The counts are display only — no rule uses them.
export default function BackofficeHomePage() {
  const { user } = useAuth()
  // undefined = loading, null = failed, number = loaded.
  const [stats, setStats] = useState({})

  useEffect(() => {
    let cancelled = false
    Promise.allSettled([
      getPendingProsumers(),
      // pageSize 1: only totalCount is needed, not the rows.
      getProsumers({ status: 'Active', pageSize: 1 }),
      getWebUsers({ status: 'Active' }),
    ]).then(([pending, prosumers, webUsers]) => {
      if (cancelled) return
      setStats({
        pending: pending.status === 'fulfilled' ? pending.value.length : null,
        activeProsumers: prosumers.status === 'fulfilled' ? prosumers.value.totalCount : null,
        activeWebUsers: webUsers.status === 'fulfilled' ? webUsers.value.length : null,
      })
    })
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <>
      <h1 className="h3">Backoffice</h1>
      <p className="text-secondary">Welcome, {user.fullName}.</p>

      <Row xs={1} md={3} className="g-3 mb-4">
        <Col>
          <StatCard
            label="Awaiting activation"
            value={stats.pending}
            to="/prosumers/pending"
            linkText="Review pending activations"
          />
        </Col>
        <Col>
          <StatCard
            label="Active prosumers"
            value={stats.activeProsumers}
            to="/prosumers"
            linkText="Manage prosumers"
          />
        </Col>
        <Col>
          <StatCard
            label="Active web users"
            value={stats.activeWebUsers}
            to="/webusers"
            linkText="Manage web users"
          />
        </Col>
      </Row>

      <h2 className="h5">Go to</h2>
      <Row>
        <Col md={6} lg={4}>
          <QuickLinks role={user.role} />
        </Col>
      </Row>
    </>
  )
}
