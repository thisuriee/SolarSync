/*
 * File:    DashboardPage.jsx
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: The staff dashboard (docs/api-contract.md §5) — the approval queue and the
 *          counts that go with it, from GET /dashboard/operator.
 *
 *          Both staff roles land here, so the two system-wide cards appear only for
 *          Backoffice. Every figure is shown exactly as the API returned it: this page
 *          never counts a list to produce one of the reservation numbers, and never
 *          works out for itself which bookings are "upcoming". The node filter is sent to
 *          the API as ?nodeId=, so a narrowed view is the server's answer, not the
 *          browser's selection.
 *
 *          Columns, badges and the queue table itself come from the shared
 *          ReservationTable, so a reservation looks the same here as in management.
 */
import { useEffect, useMemo, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Col from "react-bootstrap/Col";
import Form from "react-bootstrap/Form";
import Row from "react-bootstrap/Row";
import ReservationTable from "../../components/ReservationTable";
import StatCard from "../../components/StatCard";
import { useAuth } from "../../context/useAuth";
import { getOperatorDashboard } from "../../services/dashboardApi";
import { getProsumers } from "../../services/identityApi";
import { getNodes } from "../../services/nodesApi";
import { ROLES } from "../../utils/roles";
import ReservationDetailModal from "../reservations/ReservationDetailModal";

export default function DashboardPage() {
  const { user } = useAuth();
  const isBackoffice = user.role === ROLES.BACKOFFICE;

  const [nodeId, setNodeId] = useState("");
  const [reloadKey, setReloadKey] = useState(0);
  const [dashboard, setDashboard] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [nodes, setNodes] = useState([]);
  const [prosumerTotal, setProsumerTotal] = useState(undefined);
  const [viewingId, setViewingId] = useState(null);

  // The node list powers both the filter and the station names in the queue. A failure
  // only degrades the display, so it is not raised as an error.
  useEffect(() => {
    let cancelled = false;
    getNodes()
      .then((data) => {
        if (!cancelled) setNodes(data);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  // System-wide prosumer total for Backoffice only. pageSize 1 asks for the count
  // without fetching rows.
  useEffect(() => {
    if (!isBackoffice) return undefined;

    let cancelled = false;
    getProsumers({ pageSize: 1 })
      .then((data) => {
        if (!cancelled) setProsumerTotal(data.totalCount);
      })
      .catch(() => {
        if (!cancelled) setProsumerTotal(null);
      });
    return () => {
      cancelled = true;
    };
  }, [isBackoffice]);

  // Re-reads whenever the node filter changes or an action asks for a refresh.
  // `cancelled` drops a stale reply so a slow earlier request cannot overwrite a newer one.
  // The spinner is switched on by whoever changes the query, not from in here: setting
  // state synchronously inside an effect costs an extra render for no benefit.
  useEffect(() => {
    let cancelled = false;

    getOperatorDashboard({ nodeId })
      .then((data) => {
        if (!cancelled) {
          setDashboard(data);
          setError(null);
        }
      })
      .catch((err) => {
        if (!cancelled) setError(err.detail);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [nodeId, reloadKey]);

  // Node id → display name, so the queue reads "Colombo North" rather than an ObjectId.
  const nodeNames = useMemo(
    () => Object.fromEntries(nodes.map((n) => [n.id, n.stationName])),
    [nodes],
  );

  // A different node is a different question, so the spinner goes on with the change.
  function changeNode(value) {
    setLoading(true);
    setError(null);
    setNodeId(value);
  }

  // Re-reads after an action that may move a booking out of the queue, and reports what
  // the detail dialog did. The dialog sends the message; this page does not write one.
  function reload(message) {
    setLoading(true);
    setSuccess(message);
    setReloadKey((k) => k + 1);
  }

  // StatCard reads undefined as "still loading" and null as "could not load". A failure
  // therefore has to reach it as null, or the cards would spin forever behind the alert.
  const counts = error
    ? { pending: null, approvedFuture: null, completedToday: null }
    : {
        pending: dashboard?.pendingCount,
        approvedFuture: dashboard?.approvedFutureCount,
        completedToday: dashboard?.completedToday,
      };

  const pending = dashboard?.pendingReservations ?? [];

  return (
    <>
      <h1 className="h3 mb-3">Dashboard</h1>

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

      <Row xs={1} md={2} lg={isBackoffice ? 5 : 3} className="g-3 mb-4">
        <Col>
          <StatCard
            label="Pending approval"
            value={counts.pending}
            to="/reservations"
            linkText="Manage reservations"
          />
        </Col>
        <Col>
          <StatCard label="Approved, upcoming" value={counts.approvedFuture} />
        </Col>
        <Col>
          <StatCard label="Completed today" value={counts.completedToday} />
        </Col>
        {isBackoffice && (
          <>
            <Col>
              <StatCard
                label="Nodes"
                value={nodes.length}
                to="/nodes"
                linkText="View nodes"
              />
            </Col>
            <Col>
              <StatCard
                label="Prosumers"
                value={prosumerTotal}
                to="/prosumers"
                linkText="View prosumers"
              />
            </Col>
          </>
        )}
      </Row>

      {/* Changing the node re-asks the API, the same way the other filter screens work:
          it selects a different question rather than narrowing a list already fetched. */}
      <Form className="mb-3">
        <Row className="g-2 align-items-end">
          <Col md={4}>
            <Form.Label htmlFor="dashboard-node" className="small mb-1">
              Node
            </Form.Label>
            <Form.Select
              id="dashboard-node"
              value={nodeId}
              onChange={(e) => changeNode(e.target.value)}
            >
              <option value="">All nodes</option>
              {nodes.map((n) => (
                <option key={n.id} value={n.id}>
                  {n.stationName}
                </option>
              ))}
            </Form.Select>
          </Col>
        </Row>
      </Form>

      <h2 className="h5">Waiting for approval</h2>
      <ReservationTable
        reservations={pending}
        nodeNames={nodeNames}
        loading={loading}
        emptyText="Nothing is waiting for approval."
        onSelect={setViewingId}
        actionLabel="Review"
      />

      {viewingId && (
        <ReservationDetailModal
          reservationId={viewingId}
          nodeNames={nodeNames}
          nodes={nodes}
          onClose={() => setViewingId(null)}
          onChanged={reload}
        />
      )}
    </>
  );
}
