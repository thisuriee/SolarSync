/*
 * File:    ReservationTable.jsx
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: One reservation table for every screen that lists reservations — reservation
 *          management, the dashboard's approval queue, and booking history. It also owns
 *          the loading and empty states that go with a list, which were previously
 *          written out again on each of those screens.
 *
 *          The columns are fixed rather than configurable: all three callers want the
 *          same five, so a column config would be configuration with only one possible
 *          value. The genuinely variable part is the per-row action, which is a callback.
 *
 *          Presentation only, and deliberately not error handling: a page-level failure
 *          is shown as a dismissible alert above the table by the page itself, because
 *          that alert belongs with the page's other notices. With a failed load the list
 *          is simply empty here, which is what the screens showed before this existed.
 *
 *          Nothing is judged here. `reservationStatusVariant` picks a badge colour and
 *          nothing else; whether an action is allowed is the API's decision.
 */
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Spinner from "react-bootstrap/Spinner";
import Table from "react-bootstrap/Table";
import {
  formatSlotWindow,
  reservationStatusVariant,
} from "../pages/reservations/reservationDisplay";

export default function ReservationTable({
  reservations,
  nodeNames,
  loading,
  emptyText = "No reservations match.",
  onSelect,
  actionLabel = "View",
}) {
  if (loading) {
    return (
      <div className="text-center py-5">
        <Spinner animation="border" role="status">
          <span className="visually-hidden">Loading…</span>
        </Spinner>
      </div>
    );
  }

  if (reservations.length === 0) {
    return <p className="text-secondary">{emptyText}</p>;
  }

  return (
    <Table responsive hover className="align-middle">
      <thead>
        <tr>
          <th>Slot</th>
          <th>Node</th>
          <th>Prosumer NIC</th>
          <th className="text-end">Energy (kWh)</th>
          <th>Status</th>
          <th aria-label="Actions" />
        </tr>
      </thead>
      <tbody>
        {reservations.map((reservation) => (
          <tr key={reservation.id}>
            <td>
              {formatSlotWindow(reservation.slotStart, reservation.slotEnd)}
            </td>
            <td>
              {nodeNames?.[reservation.stationId] ?? reservation.stationId}
            </td>
            <td className="font-monospace">{reservation.prosumerNIC}</td>
            <td className="text-end">{reservation.energyKWh}</td>
            <td>
              <Badge bg={reservationStatusVariant(reservation.status)}>
                {reservation.status}
              </Badge>
            </td>
            <td className="text-end">
              {onSelect && (
                <Button
                  size="sm"
                  variant="outline-secondary"
                  onClick={() => onSelect(reservation.id)}
                >
                  {actionLabel}
                </Button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
