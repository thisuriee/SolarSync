/*
 * File:    ReservationsPage.jsx
 * Author:  Thisuri
 * Created: 2026-09-29
 * Purpose: Reservation management for staff (GET /reservations) — a paged list
 *          filtered by status, node, prosumer NIC and slot date, opening a detail
 *          dialog with the audit trail and the actions each role may take. Backoffice
 *          can also book on a prosumer's behalf. Every filter is sent to the API,
 *          which applies it in the Mongo query; nothing is filtered in the browser.
 *
 *          The list itself is drawn by the shared ReservationTable, so the approval
 *          queue on the dashboard and the booking history show a reservation exactly as
 *          this screen does.
 */
import { useEffect, useMemo, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Button from "react-bootstrap/Button";
import Col from "react-bootstrap/Col";
import Form from "react-bootstrap/Form";
import Pagination from "react-bootstrap/Pagination";
import Row from "react-bootstrap/Row";
import ReservationTable from "../../components/ReservationTable";
import { useAuth } from "../../context/useAuth";
import { getNodes } from "../../services/nodesApi";
import { getReservations } from "../../services/reservationsApi";
import { ROLES } from "../../utils/roles";
import ReservationDetailModal from "./ReservationDetailModal";
import ReservationFormModal from "./ReservationFormModal";
import { RESERVATION_STATUSES, localDayToUtcIso } from "./reservationDisplay";

const PAGE_SIZE = 20;
const EMPTY_FILTERS = {
  status: "",
  nodeId: "",
  nic: "",
  fromDate: "",
  toDate: "",
};

// Lists reservations page by page. Both staff roles can view and cancel; only
// Backoffice sees "New reservation" (booking on behalf) and only a Grid Operator
// sees Approve / Reject in the detail dialog. Hiding a button is UX — the API's
// [Authorize(Roles = ...)] plus the service re-check are the real rule.
export default function ReservationsPage() {
  const { user } = useAuth();
  const isBackoffice = user.role === ROLES.BACKOFFICE;

  // `query` is what was last sent to the API; `filters` is the unsubmitted form.
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [query, setQuery] = useState({ ...EMPTY_FILTERS, page: 1 });
  const [reloadKey, setReloadKey] = useState(0);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [nodes, setNodes] = useState([]);
  const [viewingId, setViewingId] = useState(null);
  const [creating, setCreating] = useState(false);

  // Loads the node list once, for the node filter and to show node names instead of
  // raw ids. A failure here only degrades the display, so it is not shown as an error.
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

  // Fetches the current page whenever the query changes or a reload is requested.
  // `cancelled` drops stale responses so a slow reply cannot overwrite a newer one.
  useEffect(() => {
    let cancelled = false;
    getReservations({
      status: query.status,
      nodeId: query.nodeId,
      nic: query.nic,
      from: localDayToUtcIso(query.fromDate),
      to: localDayToUtcIso(query.toDate, true),
      page: query.page,
      pageSize: PAGE_SIZE,
    })
      .then((data) => {
        if (!cancelled) setResult(data);
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
  }, [query, reloadKey]);

  // Node id → display name, so the table reads "Colombo North" rather than an ObjectId.
  const nodeNames = useMemo(
    () => Object.fromEntries(nodes.map((n) => [n.id, n.stationName])),
    [nodes],
  );

  // Changes the query (filters or page) and shows the spinner while it loads.
  function changeQuery(changes) {
    setLoading(true);
    setError(null);
    setQuery((current) => ({ ...current, ...changes }));
  }

  // Re-fetches the current page after an action that may move a row out of the filter.
  function reload() {
    setLoading(true);
    setReloadKey((k) => k + 1);
  }

  // Filter submit: new filters always start again from page 1. The NIC is sent as
  // typed; the API normalises it (IdentityFormat.NormaliseNic) before filtering.
  function handleFilter(event) {
    event.preventDefault();
    changeQuery({ ...filters, nic: filters.nic.trim(), page: 1 });
  }

  // Clears every filter and reloads from page 1.
  function handleClear() {
    setFilters(EMPTY_FILTERS);
    changeQuery({ ...EMPTY_FILTERS, page: 1 });
  }

  // Updates one field of the unsubmitted filter form.
  function setFilter(field, value) {
    setFilters((f) => ({ ...f, [field]: value }));
  }

  // Called by the detail dialog after any accepted action (edit, cancel, approve,
  // reject). The dialog stays open showing the new state; the list is refreshed.
  function handleChanged(message) {
    setSuccess(message);
    reload();
  }

  // Called by the create dialog after the API accepted the booking: opens its detail.
  function handleCreated(created) {
    setCreating(false);
    setSuccess(
      `Reservation created for ${created.prosumerNIC} — status ${created.status}.`,
    );
    setViewingId(created.id);
    reload();
  }

  const items = result?.items ?? [];
  const totalPages = result?.totalPages ?? 0;

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h3 mb-0">Reservations</h1>
        {isBackoffice && (
          <Button onClick={() => setCreating(true)}>New reservation</Button>
        )}
      </div>

      <Form onSubmit={handleFilter} className="mb-3">
        <Row className="g-2 align-items-end">
          <Col md={2}>
            <Form.Label htmlFor="filter-status" className="small mb-1">
              Status
            </Form.Label>
            <Form.Select
              id="filter-status"
              value={filters.status}
              onChange={(e) => setFilter("status", e.target.value)}
            >
              <option value="">All statuses</option>
              {RESERVATION_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </Form.Select>
          </Col>
          <Col md={3}>
            <Form.Label htmlFor="filter-node" className="small mb-1">
              Node
            </Form.Label>
            <Form.Select
              id="filter-node"
              value={filters.nodeId}
              onChange={(e) => setFilter("nodeId", e.target.value)}
            >
              <option value="">All nodes</option>
              {nodes.map((n) => (
                <option key={n.id} value={n.id}>
                  {n.stationName}
                </option>
              ))}
            </Form.Select>
          </Col>
          <Col md={2}>
            <Form.Label htmlFor="filter-nic" className="small mb-1">
              Prosumer NIC
            </Form.Label>
            <Form.Control
              id="filter-nic"
              value={filters.nic}
              onChange={(e) => setFilter("nic", e.target.value)}
            />
          </Col>
          <Col md={2}>
            <Form.Label htmlFor="filter-from" className="small mb-1">
              Slot from
            </Form.Label>
            <Form.Control
              id="filter-from"
              type="date"
              value={filters.fromDate}
              onChange={(e) => setFilter("fromDate", e.target.value)}
            />
          </Col>
          <Col md={2}>
            <Form.Label htmlFor="filter-to" className="small mb-1">
              Slot to
            </Form.Label>
            <Form.Control
              id="filter-to"
              type="date"
              value={filters.toDate}
              onChange={(e) => setFilter("toDate", e.target.value)}
            />
          </Col>
          <Col xs="auto" className="d-flex gap-2">
            <Button type="submit" variant="outline-primary">
              Filter
            </Button>
            <Button variant="outline-secondary" onClick={handleClear}>
              Clear
            </Button>
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

      <ReservationTable
        reservations={items}
        nodeNames={nodeNames}
        loading={loading}
        onSelect={setViewingId}
      />

      {/* The count and the paging controls only mean anything once there is a page to
          step through, so they are hidden on an empty or failed load. */}
      {!loading && items.length > 0 && (
        <div className="d-flex justify-content-between align-items-center">
          <span className="text-secondary small">
            {result.totalCount} reservation{result.totalCount === 1 ? "" : "s"}{" "}
            · page {result.page} of {totalPages}
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
      )}

      {viewingId && (
        <ReservationDetailModal
          reservationId={viewingId}
          nodeNames={nodeNames}
          nodes={nodes}
          onClose={() => setViewingId(null)}
          onChanged={handleChanged}
        />
      )}
      {creating && (
        <ReservationFormModal
          nodes={nodes}
          onClose={() => setCreating(false)}
          onSaved={handleCreated}
        />
      )}
    </>
  );
}
