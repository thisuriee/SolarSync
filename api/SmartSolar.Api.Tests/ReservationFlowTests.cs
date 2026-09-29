/*
 * File:    ReservationFlowTests.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: Integration tests for the reservation workflow. Runs the real
 *          ReservationService against MongoDB Atlas (the API project's user
 *          secrets), so the atomic capacity pipeline and the compare-and-set
 *          writes are exercised by the real database, not a fake. Each test
 *          creates its own slots on a seeded Active station and deletes every
 *          slot and reservation it created afterwards.
 *
 *          Covers the viva edge cases: exactly 7 days (allowed), 8 days out
 *          (422), 11 h before (409), two concurrent bookings of the last space
 *          (one success, one 409), a double cancel (capacity released once),
 *          and an update to a full slot (booking left untouched).
 *
 *          Run with: dotnet test api\SmartSolar.Api.Tests
 */
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using SmartSolar.Api.Configuration;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Tests;

public class ReservationFlowTests : IDisposable
{
    // Seeded "operator.colombo" — any valid ObjectId works for approvedBy.
    private const string OperatorId = "650000000000000000000011";

    private readonly IMongoCollection<Reservation> _reservations;
    private readonly IMongoCollection<BookingSlot> _slots;
    private readonly ReservationService _service;
    private readonly string _stationId;
    private readonly CallerIdentity _prosumer;
    private readonly CallerIdentity _otherProsumer;
    private readonly CallerIdentity _operator = new(OperatorId, UserRoles.GridOperator, null);
    private readonly List<string> _createdSlots = new();

    public ReservationFlowTests()
    {
        BsonConventions.Register();

        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ReservationService).Assembly, optional: true)
            .Build();

        var settings = config.GetSection("MongoSettings").Get<MongoSettings>()
            ?? throw new InvalidOperationException("MongoSettings missing from user secrets.");

        var db = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
        _reservations = db.GetCollection<Reservation>("EnergyReservation");
        _slots = db.GetCollection<BookingSlot>("EnergyBookingSlots");
        var users = db.GetCollection<User>("UsersDetail");
        var stations = db.GetCollection<SolarStation>("SolarStationInfo");

        _service = new ReservationService(
            new ReservationRepository(_reservations),
            new SlotCapacityRepository(_slots),
            new StationRepository(stations),
            new UserRepository(users));

        var prosumers = users
            .Find(u => u.Role == UserRoles.Prosumer && u.Status == UserStatuses.Active && u.Nic != null)
            .Limit(2)
            .ToList();
        Assert.True(prosumers.Count >= 2, "Need two Active prosumers. Re-run docs/seed/seed-database.js.");

        _prosumer = new CallerIdentity(prosumers[0].Id, UserRoles.Prosumer, prosumers[0].Nic);
        _otherProsumer = new CallerIdentity(prosumers[1].Id, UserRoles.Prosumer, prosumers[1].Nic);

        var station = stations.Find(s => s.Status == StationStatuses.Active).FirstOrDefault();
        Assert.NotNull(station);
        _stationId = station!.Id;
    }

    // ---- Shared piece 3: transition guard (pure) ----

    // The table from docs/api-contract.md §4, legal edges.
    [Theory]
    [InlineData("Pending", "Approved")]
    [InlineData("Pending", "Rejected")]
    [InlineData("Pending", "Cancelled")]
    [InlineData("Approved", "Cancelled")]
    [InlineData("Approved", "Completed")]
    public void Guard_AllowsLegalTransitions(string from, string to)
    {
        ReservationService.EnsureTransition(from, to);
    }

    // Everything else, including leaving a terminal state, is 409.
    [Theory]
    [InlineData("Approved", "Pending")]
    [InlineData("Approved", "Rejected")]
    [InlineData("Pending", "Completed")]
    [InlineData("Cancelled", "Approved")]
    [InlineData("Rejected", "Cancelled")]
    [InlineData("Completed", "Cancelled")]
    public void Guard_RejectsIllegalTransitions(string from, string to)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => ReservationService.EnsureTransition(from, to));
        Assert.Equal("RESERVATION_INVALID_STATE", ex.Code);
    }

    // ---- R1 ----

    // Exactly 7 days is inside the window (inclusive): the service's own
    // UtcNow is later than ours, so this slot is at or just under 7 days.
    [Fact]
    public async Task Create_AtExactlySevenDays_Succeeds()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(7));

        var created = await _service.Create(Request(slot), _prosumer);

        Assert.Equal(ReservationStatuses.Pending, created.Status);
        Assert.Equal(_prosumer.Nic, created.ProsumerNIC);
        Assert.Equal(slot.SlotStart, created.SlotStart, TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, (await Reload(slot)).ReservedCount);
    }

    // 8 days out -> 422, and no capacity is taken.
    [Fact]
    public async Task Create_EightDaysOut_IsOutsideWindow()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(8));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.Create(Request(slot), _prosumer));

        Assert.Equal("RESERVATION_OUTSIDE_WINDOW", ex.Code);
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal(0, (await Reload(slot)).ReservedCount);
    }

    // A body NIC from a Prosumer is ignored: the booking is in the token's NIC.
    [Fact]
    public async Task Create_ProsumerBodyNicIsIgnored()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2));
        var request = Request(slot);
        request.ProsumerNIC = _otherProsumer.Nic;

        var created = await _service.Create(request, _prosumer);

        Assert.Equal(_prosumer.Nic, created.ProsumerNIC);
    }

    // Same NIC, same slot, twice -> 409 RESERVATION_DUPLICATE.
    [Fact]
    public async Task Create_SameSlotTwice_IsDuplicate()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2), capacity: 3);
        await _service.Create(Request(slot), _prosumer);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.Create(Request(slot), _prosumer));

        Assert.Equal("RESERVATION_DUPLICATE", ex.Code);
        Assert.Equal(1, (await Reload(slot)).ReservedCount);
    }

    // ---- Shared piece 2: atomic capacity ----

    // Two prosumers race for the last space: exactly one wins, the other gets
    // 409 RESERVATION_SLOT_FULL, and the slot ends Full with count == capacity.
    [Fact]
    public async Task Create_ConcurrentLastSpace_OneWinsOneFull()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2), capacity: 1);

        var results = await Task.WhenAll(
            Attempt(() => _service.Create(Request(slot), _prosumer)),
            Attempt(() => _service.Create(Request(slot), _otherProsumer)));

        Assert.Single(results, r => r is null);
        Assert.Single(results, r => r == "RESERVATION_SLOT_FULL");

        var after = await Reload(slot);
        Assert.Equal(1, after.ReservedCount);
        Assert.Equal(SlotStatuses.Full, after.Status);
    }

    // ---- R2 ----

    // 11 hours before -> 409 with the time left in the detail; nothing released.
    [Fact]
    public async Task Cancel_ElevenHoursBefore_IsNoticePeriod()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddHours(11));
        var created = await _service.Create(Request(slot), _prosumer);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.Cancel(created.Id, _prosumer));

        Assert.Equal("RESERVATION_NOTICE_PERIOD", ex.Code);
        Assert.Contains("starts in 10 hour", ex.Detail);
        Assert.Equal(1, (await Reload(slot)).ReservedCount);
    }

    // Cancel frees the space and reopens a Full slot; a second cancel is a 409
    // and does NOT release capacity again (compare-and-set).
    [Fact]
    public async Task Cancel_Twice_ReleasesCapacityOnce()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2), capacity: 1);
        var created = await _service.Create(Request(slot), _prosumer);
        Assert.Equal(SlotStatuses.Full, (await Reload(slot)).Status);

        var cancelled = await _service.Cancel(created.Id, _prosumer);
        Assert.Equal(ReservationStatuses.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CancelledAt);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.Cancel(created.Id, _prosumer));
        Assert.Equal("RESERVATION_INVALID_STATE", ex.Code);

        var after = await Reload(slot);
        Assert.Equal(0, after.ReservedCount);
        Assert.Equal(SlotStatuses.Open, after.Status);
    }

    // ---- Ownership ----

    // Another prosumer's booking -> 403 AUTH_FORBIDDEN_ROLE, never 404.
    [Fact]
    public async Task OtherProsumer_GetsForbidden()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2));
        var created = await _service.Create(Request(slot), _prosumer);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.FindById(created.Id, _otherProsumer));

        Assert.Equal("AUTH_FORBIDDEN_ROLE", ex.Code);
        Assert.Equal(403, ex.StatusCode);
    }

    // ---- Update ----

    // Moving to a full slot fails with 409 and leaves the booking and BOTH
    // slot counts exactly as they were (claim-new-before-release-old).
    [Fact]
    public async Task Update_ToFullSlot_LeavesBookingUntouched()
    {
        var original = await NewSlot(DateTime.UtcNow.AddDays(2));
        var full = await NewSlot(DateTime.UtcNow.AddDays(3), capacity: 1);
        var created = await _service.Create(Request(original), _prosumer);
        await _service.Create(Request(full), _otherProsumer);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.Update(created.Id, new UpdateReservationRequest { SlotId = full.Id, EnergyKWh = 1 }, _prosumer));

        Assert.Equal("RESERVATION_SLOT_FULL", ex.Code);
        Assert.Equal(original.Id, (await _service.FindById(created.Id, _prosumer)).SlotId);
        Assert.Equal(1, (await Reload(original)).ReservedCount);
        Assert.Equal(1, (await Reload(full)).ReservedCount);
    }

    // A successful move claims the new space, releases the old one, and
    // re-copies slotStart from the new slot.
    [Fact]
    public async Task Update_ToOpenSlot_MovesCapacity()
    {
        var original = await NewSlot(DateTime.UtcNow.AddDays(2));
        var target = await NewSlot(DateTime.UtcNow.AddDays(3));
        var created = await _service.Create(Request(original), _prosumer);

        var updated = await _service.Update(created.Id,
            new UpdateReservationRequest { SlotId = target.Id, EnergyKWh = 2 }, _prosumer);

        Assert.Equal(target.Id, updated.SlotId);
        Assert.Equal(target.SlotStart, updated.SlotStart, TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, (await Reload(original)).ReservedCount);
        Assert.Equal(1, (await Reload(target)).ReservedCount);
    }

    // ---- Approve / reject ----

    // Approve sets approvedBy/approvedAt; approving again is an illegal
    // transition. Reject from Approved is also refused.
    [Fact]
    public async Task Approve_ThenApproveOrRejectAgain_IsInvalidState()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2));
        var created = await _service.Create(Request(slot), _prosumer);

        var approved = await _service.Approve(created.Id, _operator);
        Assert.Equal(ReservationStatuses.Approved, approved.Status);
        Assert.Equal(OperatorId, approved.ApprovedBy);

        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.Approve(created.Id, _operator));
        Assert.Equal("RESERVATION_INVALID_STATE", again.Code);

        var reject = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.Reject(created.Id, "late", _operator));
        Assert.Equal("RESERVATION_INVALID_STATE", reject.Code);
    }

    // Only a GridOperator may approve — the service re-checks the role.
    [Fact]
    public async Task Approve_ByProsumer_IsForbidden()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2));
        var created = await _service.Create(Request(slot), _prosumer);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.Approve(created.Id, _prosumer));

        Assert.Equal("AUTH_FORBIDDEN_ROLE", ex.Code);
    }

    // Reject records the reason and gives the space back.
    [Fact]
    public async Task Reject_ReleasesCapacity()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2));
        var created = await _service.Create(Request(slot), _prosumer);

        var rejected = await _service.Reject(created.Id, "  Grid maintenance  ", _operator);

        Assert.Equal(ReservationStatuses.Rejected, rejected.Status);
        Assert.Equal("Grid maintenance", rejected.RejectionReason);
        Assert.Equal(0, (await Reload(slot)).ReservedCount);
    }

    // ---- Shared piece 1: the predicate ----

    // A Pending future booking counts as active for slot, station and prosumer;
    // once cancelled it no longer does.
    [Fact]
    public async Task ActivePredicate_CountsPendingFutureOnly()
    {
        var slot = await NewSlot(DateTime.UtcNow.AddDays(2));
        var created = await _service.Create(Request(slot), _prosumer);

        Assert.Equal(1, await _service.CountActiveForSlot(slot.Id));
        var stationBefore = await _service.CountActiveForStation(_stationId);
        var prosumerBefore = await _service.CountActiveForProsumer(_prosumer.Nic!.ToLowerInvariant());
        Assert.True(prosumerBefore >= 1);

        await _service.Cancel(created.Id, _prosumer);

        Assert.Equal(0, await _service.CountActiveForSlot(slot.Id));
        Assert.Equal(stationBefore - 1, await _service.CountActiveForStation(_stationId));
        Assert.Equal(prosumerBefore - 1, await _service.CountActiveForProsumer(_prosumer.Nic!));
    }

    // ---- helpers ----

    // Inserts an Open one-hour slot on the seeded station and tracks it for cleanup.
    private async Task<BookingSlot> NewSlot(DateTime start, int capacity = 2)
    {
        var now = DateTime.UtcNow;
        var slot = new BookingSlot
        {
            StationId = _stationId,
            SlotStart = start,
            SlotEnd = start.AddHours(1),
            TotalCapacity = capacity,
            ReservedCount = 0,
            EnergyPerSlotKWh = 5,
            Status = SlotStatuses.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _slots.InsertOneAsync(slot);
        _createdSlots.Add(slot.Id);
        return slot;
    }

    // A valid create request for the slot (1 kWh, well under the 5 kWh cap).
    private static CreateReservationRequest Request(BookingSlot slot)
    {
        return new CreateReservationRequest { SlotId = slot.Id, EnergyKWh = 1 };
    }

    // Re-reads a slot so assertions see the database, not our copy.
    private async Task<BookingSlot> Reload(BookingSlot slot)
    {
        return await _slots.Find(s => s.Id == slot.Id).FirstAsync();
    }

    // Runs a create and returns null on success, or the error code.
    private static async Task<string?> Attempt(Func<Task<ReservationResponse>> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (BusinessRuleException ex)
        {
            return ex.Code;
        }
    }

    // Removes every slot this test created and every reservation on them.
    public void Dispose()
    {
        if (_createdSlots.Count == 0) return;

        _reservations.DeleteMany(Builders<Reservation>.Filter.In(r => r.SlotId, _createdSlots));
        _slots.DeleteMany(Builders<BookingSlot>.Filter.In(s => s.Id, _createdSlots));
    }
}
