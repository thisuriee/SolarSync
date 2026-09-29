/*
 * File:    dashboardApi.js
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: The Dashboard (M4) API call the staff screens make (docs/api-contract.md §5).
 *          A thin wrapper only — every figure is computed by the API in Mongo and shown
 *          as it arrives. Nothing here recalculates a count, and no rule is decided
 *          here. Failures reach the caller already shaped as {status, code, detail,
 *          errors} by apiClient.
 */
import apiClient from "./apiClient";

// GET /dashboard/operator?nodeId= — the approval queue plus the three counts beside it
// (Backoffice, Grid Operator). nodeId is optional; omitted, every figure covers the
// whole grid. Returns {pendingReservations, pendingCount, approvedFutureCount,
// completedToday}, all four computed server-side.
export async function getOperatorDashboard({ nodeId } = {}) {
  const { data } = await apiClient.get("/dashboard/operator", {
    params: { nodeId: nodeId || undefined },
  });
  return data;
}
