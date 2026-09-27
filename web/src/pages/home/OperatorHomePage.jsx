/*
 * File:    OperatorHomePage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Grid Operator landing page: prosumer counts and shortcuts to the screens
 *          an operator may use. No activation actions — those are Backoffice-only.
 */
import { useEffect, useState } from 'react'
import Col from 'react-bootstrap/Col'
import Row from 'react-bootstrap/Row'
import QuickLinks from '../../components/QuickLinks'
import StatCard from '../../components/StatCard'
import { useAuth } from '../../context/useAuth'
import { getProsumers } from '../../services/identityApi'

// Greets the operator and loads prosumer counts. It does not call /prosumers/pending:
// that route is Backoffice-only and would answer 403 AUTH_FORBIDDEN_ROLE.
export default function OperatorHomePage() {
  const { user } = useAuth()
  // undefined = loading, null = failed, number = loaded.
  const [stats, setStats] = useState({})

  useEffect(() => {
    let cancelled = false
    // pageSize 1: only totalCount is needed, not the rows.
    Promise.allSettled([
      getProsumers({ status: 'Active', pageSize: 1 }),
      getProsumers({ pageSize: 1 }),
    ]).then(([active, all]) => {
      if (cancelled) return
      setStats({
        active: active.status === 'fulfilled' ? active.value.totalCount : null,
        total: all.status === 'fulfilled' ? all.value.totalCount : null,
      })
    })
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <>
      <h1 className="h3">Grid Operator</h1>
      <p className="text-secondary">Welcome, {user.fullName}.</p>

      <Row xs={1} md={3} className="g-3 mb-4">
        <Col>
          <StatCard
            label="Active prosumers"
            value={stats.active}
            to="/prosumers"
            linkText="View prosumers"
          />
        </Col>
        <Col>
          <StatCard label="All registered prosumers" value={stats.total} />
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
