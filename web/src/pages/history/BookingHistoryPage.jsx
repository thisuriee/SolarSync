/*
 * File:    BookingHistoryPage.jsx
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: Booking history for staff (GET /reservations/history, docs/api-contract.md §5)
 *          — a paged list narrowed by status, node, prosumer NIC, slot date and free
 *          text, opening the same detail dialog management uses.
 *
 *          Every filter and every page change is sent to the API as a query parameter and
 *          applied in the Mongo query. Nothing is narrowed in the browser: a filter
 *          applied here would be this page's answer rather than the server's, which is
 *          exactly what the rubric warns against. The free-text term is the one thing this
 *          screen has that reservation management does not — it matches a station name, a
 *          prosumer NIC or a full reservation id, all resolved server-side.
 *
 *          The list and its loading and empty states come from the shared
 *          ReservationTable, so a reservation looks the same as everywhere else.
 */
import { useEffect, useMemo, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Button from "react-bootstrap/Button";
import Col from "react-bootstrap/Col";
import Form from "react-bootstrap/Form";
import Pagination from "react-bootstrap/Pagination";
import Row from "react-bootstrap/Row";
import ReservationTable from "../../components/ReservationTable";
import { getNodes } from "../../services/nodesApi";
import { getReservationHistory } from "../../services/reservationsApi";
import ReservationDetailModal from "../reservations/ReservationDetailModal";
import {
  RESERVATION_STATUSES,
  localDayToUtcIso,
} from "../reservations/reservationDisplay";

const PAGE_SIZE = 20;
const EMPTY_FILTERS = {
  q: "",
  status: "",
  nodeId: "",
  nic: "",
  fromDate: "",
  toDate: "",
};

// Whether anything is actually narrowing the request, which decides the wording of the
// empty state and of the total.
function isFiltered(query) {
  return Boolean(
    query.q ||
    query.status ||
    query.nodeId ||
    query.nic ||
    query.fromDate ||
    query.toDate,
  );
}

export default function BookingHistoryPage() {
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

  // The node list powers the node filter and the station names in the table. A failure
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

  // Fetches the current page whenever the query changes. `cancelled` drops a stale reply
  // so a slow earlier request cannot overwrite a newer one.
  useEffect(() => {
    let cancelled = false;

    getReservationHistory({
      status: query.status,
      nodeId: query.nodeId,
      nic: query.nic,
      q: query.q,
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

  // Moves to another page. The page number lives in `query`, so this re-asks the API
  // rather than slicing anything already fetched. The spinner is switched on here rather
  // than inside the effect, so the fetch effect only reports its outcome.
  function changeQuery(changes) {
    setLoading(true);
    setError(null);
    setQuery((current) => ({ ...current, ...changes }));
  }

  // A changed filter is a new question, so it starts again from page 1.
  function handleApply(event) {
    event.preventDefault();
    changeQuery({
      ...filters,
      q: filters.q.trim(),
      nic: filters.nic.trim(),
      page: 1,
    });
  }

  function handleReset() {
    setFilters(EMPTY_FILTERS);
    changeQuery({ ...EMPTY_FILTERS, page: 1 });
  }

  function setFilter(field, value) {
    setFilters((current) => ({ ...current, [field]: value }));
  }

  // Re-reads the current page after an action that may move a row out of the filter, and
  // reports what the detail dialog did. The dialog sends the message; this page does not
  // write one.
  function reload(message) {
    setLoading(true);
    setSuccess(message);
    setReloadKey((k) => k + 1);
  }

  const items = result?.items ?? [];
  const totalPages = result?.totalPages ?? 0;
  const filtered = isFiltered(query);

  return (
    <>
      <h1 className="h3 mb-3">Booking history</h1>

      <Form onSubmit={handleApply} className="mb-3">
        <Row className="g-2 align-items-end">
          <Col md={3}>
            <Form.Label htmlFor="history-search" className="small mb-1">
              Search
            </Form.Label>
            <Form.Control
              id="history-search"
              placeholder="Station, NIC or booking id"
              value={filters.q}
              onChange={(e) => setFilter("q", e.target.value)}
            />
          </Col>
          <Col md={2}>
            <Form.Label htmlFor="history-status" className="small mb-1">
              Status
            </Form.Label>
            <Form.Select
              id="history-status"
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
          <Col md={2}>
            <Form.Label htmlFor="history-node" className="small mb-1">
              Node
            </Form.Label>
            <Form.Select
              id="history-node"
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
            <Form.Label htmlFor="history-nic" className="small mb-1">
              Prosumer NIC
            </Form.Label>
            <Form.Control
              id="history-nic"
              value={filters.nic}
              onChange={(e) => setFilter("nic", e.target.value)}
            />
          </Col>
          <Col md={1}>
            <Form.Label htmlFor="history-from" className="small mb-1">
              From
            </Form.Label>
            <Form.Control
              id="history-from"
              type="date"
              value={filters.fromDate}
              onChange={(e) => setFilter("fromDate", e.target.value)}
            />
          </Col>
          <Col md={1}>
            <Form.Label htmlFor="history-to" className="small mb-1">
              To
            </Form.Label>
            <Form.Control
              id="history-to"
              type="date"
              value={filters.toDate}
              onChange={(e) => setFilter("toDate", e.target.value)}
            />
          </Col>
          <Col xs="auto" className="d-flex gap-2">
            <Button type="submit" variant="outline-primary">
              Apply
            </Button>
            <Button variant="outline-secondary" onClick={handleReset}>
              Reset
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
        emptyText={
          filtered
            ? "No bookings match these filters."
            : "No bookings have been made yet."
        }
        onSelect={setViewingId}
      />

      {/* The count and the paging controls only mean anything once there is a page to step
          through, so they are hidden on an empty or failed load. */}
      {!loading && items.length > 0 && (
        <div className="d-flex justify-content-between align-items-center">
          <span className="text-secondary small">
            {result.totalCount} booking{result.totalCount === 1 ? "" : "s"}
            {filtered ? " match these filters" : ""} · page {result.page} of{" "}
            {totalPages}
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
          onChanged={reload}
        />
      )}
    </>
  );
}
