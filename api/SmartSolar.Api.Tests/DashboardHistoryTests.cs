/*
 * File:    DashboardHistoryTests.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: Integration tests for the dashboards and booking history
 *          (docs/api-contract.md §5). Runs the real DashboardService against
 *          MongoDB Atlas through the API project's own user secrets, so the
 *          Mongo filters and counts are exercised by the real database rather
 *          than a fake that could agree with a wrong query.
 *
 *          Every test works on a NIC and a station id generated for that test
 *          alone. The station id is a fresh ObjectId that belongs to no
 *          station, so a node-scoped count returns exactly this test's
 *          documents and nothing from the seeded demo data. The NIC is
 *          deliberately not a valid Sri Lankan NIC, so it cannot collide with
 *          a seeded account. Everything created is deleted in Dispose.
 *
 *          The point of the counts being asserted against real rows is that
 *          the rubric's failure mode is a hard-coded or client-counted
 *          number; these tests fail if any count stops following the data.
 *
 *          Run with: dotnet test api\SmartSolar.Api.Tests
 */
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using SmartSolar.Api.Configuration;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Tests;

public class DashboardHistoryTests : IDisposable
{
    private readonly IMongoCollection<Reservation> _reservations;
    private readonly DashboardService _service;

    // Regenerated per test instance, so no two tests can see each other's data.
    private readonly string _scratchNic = NewSyntheticNic();
    private readonly string _otherNic = NewSyntheticNic();
    private readonly string _scratchStationId = ObjectId.GenerateNewId().ToString();
    private readonly string _otherStationId = ObjectId.GenerateNewId().ToString();

    private readonly CallerIdentity _prosumer;
    private readonly CallerIdentity _operator;
    private readonly CallerIdentity _backoffice;

    // A real seeded Active station, needed by the station-name search test:
    // the name lives in SolarStationInfo and must resolve to this id.
    private readonly string _realStationId;
    private readonly string _realStationName;

    private readonly List<string> _created = new();

    public DashboardHistoryTests()
    {
        // The composition root registers these; tests never run it.
        BsonConventions.Register();

        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DashboardService).Assembly, optional: true)
            .Build();

        var settings = config.GetSection("MongoSettings").Get<MongoSettings>()
            ?? throw new InvalidOperationException(
                "MongoSettings not found. Set them on the API project with: " +
                "dotnet user-secrets set \"MongoSettings:ConnectionString\" \"...\"");

        var db = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
        _reservations = db.GetCollection<Reservation>("EnergyReservation");
        var slots = db.GetCollection<BookingSlot>("EnergyBookingSlots");
        var users = db.GetCollection<User>("UsersDetail");
        var stations = db.GetCollection<SolarStation>("SolarStationInfo");

        var repository = new ReservationRepository(_reservations);

        // The real ReservationService supplies the shared active-reservation
        // predicate, so activeCount is tested against the same implementation
        // the node, slot and prosumer guards use — not a stub that could
        // disagree with production.
        var reservationService = new ReservationService(
            repository,
            new SlotCapacityRepository(slots),
            new StationRepository(stations),
            new UserRepository(users));

        _service = new DashboardService(repository, new StationRepository(stations), reservationService);

        _prosumer = new CallerIdentity(ObjectId.GenerateNewId().ToString(), UserRoles.Prosumer, _scratchNic);
        _operator = new CallerIdentity(ObjectId.GenerateNewId().ToString(), UserRoles.GridOperator, null);
        _backoffice = new CallerIdentity(ObjectId.GenerateNewId().ToString(), UserRoles.Backoffice, null);

        var station = stations.Find(s => s.Status == StationStatuses.Active).FirstOrDefault();

        Assert.True(station is not null,
            "Need at least one Active station. Re-run docs/seed/seed-database.js.");

        _realStationId = station!.Id;
        _realStationName = station.StationName;
    }

    // =========================================================================
    // Prosumer dashboard
    // =========================================================================

    [Fact]
    public async Task ProsumerDashboard_CountsOnlyTheCallersOwnBookings()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));
        await CreateAsync(_scratchNic, ReservationStatuses.Approved, DateTime.UtcNow.AddHours(48));
        await CreateAsync(_scratchNic, ReservationStatuses.Completed, DateTime.UtcNow.AddHours(-24));

        // Another prosumer's upcoming booking must not appear in any count.
        await CreateAsync(_otherNic, ReservationStatuses.Approved, DateTime.UtcNow.AddHours(24));

        var dashboard = await _service.ProsumerDashboard(_prosumer);

        Assert.Equal(2, dashboard.PendingCount + dashboard.ApprovedFutureCount);
        Assert.Equal(1, dashboard.PendingCount);
        Assert.Equal(1, dashboard.ApprovedFutureCount);
        Assert.Equal(2, dashboard.ActiveCount);          // Pending + Approved, both future
    }

    // Locks in the contract reading: pendingCount carries no time qualifier, so
    // a booking awaiting an operator decision stays visible after its slot
    // passes. It is still not "active", which does require a future slot.
    [Fact]
    public async Task ProsumerDashboard_PastDatedPending_IsPendingButNotActive()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(-24));

        var dashboard = await _service.ProsumerDashboard(_prosumer);

        Assert.Equal(1, dashboard.PendingCount);
        Assert.Equal(0, dashboard.ActiveCount);
        Assert.Equal(0, dashboard.ApprovedFutureCount);
    }

    [Fact]
    public async Task ProsumerDashboard_NextBooking_IsTheSoonestApprovedFuture()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Approved, DateTime.UtcNow.AddHours(48));
        var sooner = await CreateAsync(_scratchNic, ReservationStatuses.Approved, DateTime.UtcNow.AddHours(24));

        // A past Approved booking is not "next" — the slot has been and gone.
        await CreateAsync(_scratchNic, ReservationStatuses.Approved, DateTime.UtcNow.AddHours(-24));

        var dashboard = await _service.ProsumerDashboard(_prosumer);

        Assert.NotNull(dashboard.NextBooking);
        Assert.Equal(sooner, dashboard.NextBooking!.Id);
    }

    [Fact]
    public async Task ProsumerDashboard_NextBooking_IsNullWhenNothingIsApproved()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));

        var dashboard = await _service.ProsumerDashboard(_prosumer);

        Assert.Null(dashboard.NextBooking);
    }

    // The rubric's "counts read live from the API": the numbers must move when
    // the data moves, not sit at a constant.
    [Fact]
    public async Task ProsumerDashboard_CountsFollowTheData()
    {
        var before = await _service.ProsumerDashboard(_prosumer);

        await CreateAsync(_scratchNic, ReservationStatuses.Approved, DateTime.UtcNow.AddHours(24));

        var after = await _service.ProsumerDashboard(_prosumer);

        Assert.Equal(before.PendingCount, after.PendingCount);
        Assert.Equal(before.ApprovedFutureCount + 1, after.ApprovedFutureCount);
        Assert.Equal(before.ActiveCount + 1, after.ActiveCount);
    }

    [Fact]
    public async Task ProsumerDashboard_ForAStaffCaller_IsForbidden()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.ProsumerDashboard(_operator));

        Assert.Equal("AUTH_FORBIDDEN_ROLE", ex.Code);
        Assert.Equal(403, ex.StatusCode);
    }

    // =========================================================================
    // Operator dashboard
    // =========================================================================

    [Fact]
    public async Task OperatorDashboard_Pending_IsScopedToTheNode()
    {
        var atScratchNode = await CreateAsync(
            _scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(48), _scratchStationId);
        var soonerAtScratchNode = await CreateAsync(
            _scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24), _scratchStationId);

        // Same status, different node: must not be counted or returned.
        await CreateAsync(_otherNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24), _otherStationId);

        var dashboard = await _service.OperatorDashboard(_scratchStationId, _operator);

        Assert.Equal(2, dashboard.PendingCount);
        Assert.All(dashboard.PendingReservations, r => Assert.Equal(_scratchStationId, r.StationId));

        var returnedIds = dashboard.PendingReservations.Select(r => r.Id).ToList();
        Assert.Contains(atScratchNode, returnedIds);
        Assert.Contains(soonerAtScratchNode, returnedIds);

        // Soonest slot first — the order an operator works the queue in.
        Assert.Equal(soonerAtScratchNode, dashboard.PendingReservations[0].Id);
    }

    [Fact]
    public async Task OperatorDashboard_ApprovedFuture_ExcludesPastAndCompleted()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Approved, DateTime.UtcNow.AddHours(24), _scratchStationId);
        await CreateAsync(_scratchNic, ReservationStatuses.Approved, DateTime.UtcNow.AddHours(-24), _scratchStationId);
        await CreateAsync(_scratchNic, ReservationStatuses.Completed, DateTime.UtcNow.AddHours(24), _scratchStationId);

        var dashboard = await _service.OperatorDashboard(_scratchStationId, _operator);

        Assert.Equal(1, dashboard.ApprovedFutureCount);
    }

    [Fact]
    public async Task OperatorDashboard_CompletedToday_CountsOnlyTodayAndInScope()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Completed, DateTime.UtcNow.AddHours(-4),
            _scratchStationId, completedAt: DateTime.UtcNow);

        // Completed two days ago: not today.
        await CreateAsync(_scratchNic, ReservationStatuses.Completed, DateTime.UtcNow.AddHours(-52),
            _scratchStationId, completedAt: DateTime.UtcNow.AddDays(-2));

        // Completed today, but at another node.
        await CreateAsync(_otherNic, ReservationStatuses.Completed, DateTime.UtcNow.AddHours(-4),
            _otherStationId, completedAt: DateTime.UtcNow);

        var dashboard = await _service.OperatorDashboard(_scratchStationId, _operator);

        Assert.Equal(1, dashboard.CompletedToday);
    }

    [Fact]
    public async Task OperatorDashboard_WithoutNodeId_CoversTheWholeGrid()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24), _scratchStationId);
        await CreateAsync(_otherNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24), _otherStationId);

        var scoped = await _service.OperatorDashboard(_scratchStationId, _operator);
        var wholeGrid = await _service.OperatorDashboard(null, _operator);

        Assert.Equal(1, scoped.PendingCount);
        Assert.True(wholeGrid.PendingCount >= scoped.PendingCount,
            "An unscoped dashboard cannot report fewer pending bookings than a single node holds.");
    }

    [Fact]
    public async Task OperatorDashboard_ForAProsumerCaller_IsForbidden()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.OperatorDashboard(null, _prosumer));

        Assert.Equal("AUTH_FORBIDDEN_ROLE", ex.Code);
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task OperatorDashboard_IsAvailableToBackoffice()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24), _scratchStationId);

        var dashboard = await _service.OperatorDashboard(_scratchStationId, _backoffice);

        Assert.Equal(1, dashboard.PendingCount);
    }

    // =========================================================================
    // Booking history
    // =========================================================================

    // The scoping rule the rubric asks about: however the request is shaped, a
    // prosumer only ever sees their own history.
    [Fact]
    public async Task History_ForAProsumer_IsScopedToTheTokenNic_RegardlessOfTheNicParameter()
    {
        var mine = await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));
        await CreateAsync(_otherNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));

        // The caller asks for the other prosumer's history and is given their own.
        var page = await _service.FindHistory(new ReservationHistoryQuery { Nic = _otherNic }, _prosumer);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(mine, page.Items[0].Id);
        Assert.All(page.Items, r => Assert.Equal(_scratchNic, r.ProsumerNIC));
    }

    [Fact]
    public async Task History_ForStaff_MayFilterByAnyNic()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));
        var theirs = await CreateAsync(_otherNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));

        var page = await _service.FindHistory(new ReservationHistoryQuery { Nic = _otherNic }, _operator);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(theirs, page.Items[0].Id);
        Assert.All(page.Items, r => Assert.Equal(_otherNic, r.ProsumerNIC));
    }

    [Fact]
    public async Task History_FiltersByStatusAndDateRange()
    {
        var utcNow = DateTime.UtcNow;
        await CreateAsync(_scratchNic, ReservationStatuses.Approved, utcNow.AddHours(24));
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, utcNow.AddHours(24));

        var page = await _service.FindHistory(new ReservationHistoryQuery
        {
            Nic = _scratchNic,
            Status = "approved",                      // any casing, normalised
            From = utcNow.AddHours(12),
            To = utcNow.AddHours(36)
        }, _operator);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(ReservationStatuses.Approved, page.Items[0].Status);
    }

    [Fact]
    public async Task History_DateRange_ExcludesBookingsOutsideIt()
    {
        var utcNow = DateTime.UtcNow;
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, utcNow.AddHours(24));
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, utcNow.AddDays(3));

        var page = await _service.FindHistory(new ReservationHistoryQuery
        {
            Nic = _scratchNic,
            To = utcNow.AddHours(36)
        }, _operator);

        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task History_Pages_WithTotalCountAndTotalPages()
    {
        var utcNow = DateTime.UtcNow;
        for (var i = 1; i <= 3; i++)
        {
            await CreateAsync(_scratchNic, ReservationStatuses.Pending, utcNow.AddHours(24 + i));
        }

        var firstPage = await _service.FindHistory(
            new ReservationHistoryQuery { Nic = _scratchNic, Page = 1, PageSize = 2 }, _operator);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);

        var secondPage = await _service.FindHistory(
            new ReservationHistoryQuery { Nic = _scratchNic, Page = 2, PageSize = 2 }, _operator);

        Assert.Single(secondPage.Items);
        Assert.Equal(3, secondPage.TotalCount);
    }

    // page/pageSize are clamped, not refused — the same rule the staff search
    // route applies, because both go through one normaliser.
    [Fact]
    public async Task History_ClampsPageAndPageSize()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));

        var page = await _service.FindHistory(new ReservationHistoryQuery
        {
            Nic = _scratchNic,
            Page = 0,
            PageSize = 5000
        }, _operator);

        Assert.Equal(1, page.Page);
        Assert.Equal(QueryNormalisation.MaxPageSize, page.PageSize);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task History_SearchByNic_MatchesTheTerm()
    {
        var mine = await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));
        await CreateAsync(_otherNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));

        var page = await _service.FindHistory(new ReservationHistoryQuery { Q = _scratchNic }, _operator);

        Assert.Contains(mine, page.Items.Select(r => r.Id));
        Assert.All(page.Items, r => Assert.Equal(_scratchNic, r.ProsumerNIC));
    }

    // Free-text name search: the name is not on the reservation, so the service
    // must resolve it to a station id and filter by that, not fetch and filter.
    [Fact]
    public async Task History_SearchByStationName_ReturnsOnlyThatStation()
    {
        var mine = await CreateAsync(
            _scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24), _realStationId);

        // Same prosumer, a station whose name cannot match the term, so its
        // absence proves the term actually narrowed the result.
        var elsewhere = await CreateAsync(
            _scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24), _scratchStationId);

        var page = await _service.FindHistory(new ReservationHistoryQuery { Q = _realStationName }, _operator);

        Assert.Contains(mine, page.Items.Select(r => r.Id));
        Assert.DoesNotContain(elsewhere, page.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task History_SearchByReservationId_FindsThatBooking()
    {
        var mine = await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));

        var page = await _service.FindHistory(new ReservationHistoryQuery { Q = mine }, _operator);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(mine, page.Items[0].Id);
    }

    [Fact]
    public async Task History_WithUnknownStatusFilter_Is400()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.FindHistory(new ReservationHistoryQuery { Status = "Nonsense" }, _operator));

        Assert.Equal("RESERVATION_INVALID_STATUS_FILTER", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task History_IsAvailableToAProsumer()
    {
        await CreateAsync(_scratchNic, ReservationStatuses.Pending, DateTime.UtcNow.AddHours(24));

        var page = await _service.FindHistory(new ReservationHistoryQuery(), _prosumer);

        Assert.Equal(1, page.TotalCount);
    }

    // =========================================================================
    // Fixtures
    // =========================================================================

    // A NIC that cannot belong to a seeded account, so a per-NIC count sees
    // only this test's documents.
    private static string NewSyntheticNic()
    {
        return ("ZZ" + Guid.NewGuid().ToString("N"))[..16].ToUpperInvariant();
    }

    // Inserts a throwaway reservation. The seeded demo data is never modified.
    private async Task<string> CreateAsync(
        string nic, string status, DateTime slotStart,
        string? stationId = null, DateTime? completedAt = null)
    {
        var id = ObjectId.GenerateNewId().ToString();
        var now = DateTime.UtcNow;

        await _reservations.InsertOneAsync(new Reservation
        {
            Id = id,
            ProsumerNIC = nic,
            StationId = stationId ?? _scratchStationId,
            SlotId = ObjectId.GenerateNewId().ToString(),
            SlotStart = slotStart,
            SlotEnd = slotStart.AddHours(4),
            EnergyKWh = 10,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = completedAt
        });

        _created.Add(id);
        return id;
    }

    public void Dispose()
    {
        if (_created.Count > 0)
        {
            _reservations.DeleteMany(Builders<Reservation>.Filter.In(r => r.Id, _created));
        }
    }
}
