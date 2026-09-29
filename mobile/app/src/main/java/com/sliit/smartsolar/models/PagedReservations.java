/*
 * File:    PagedReservations.java
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: The paged envelope from docs/response-format.md
 *          ({items, page, pageSize, totalCount, totalPages}), carrying
 *          reservations. Returned by GET /api/reservations/history.
 *          Holds data only.
 *
 *          The page numbers come back from the server rather than being derived
 *          here, so a screen can say "Page 2 of 7" from what it was told instead
 *          of guessing whether a next page exists.
 */
package com.sliit.smartsolar.models;

import java.util.List;

public class PagedReservations {

    public final List<Reservation> items;
    public final int page;
    public final int pageSize;
    public final long totalCount;
    public final int totalPages;

    public PagedReservations(List<Reservation> items, int page, int pageSize,
                             long totalCount, int totalPages) {
        this.items = items;
        this.page = page;
        this.pageSize = pageSize;
        this.totalCount = totalCount;
        this.totalPages = totalPages;
    }
}
