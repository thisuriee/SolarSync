/*
 * File:    QrFlowTests.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Integration smoke tests for the QR fulfilment flow. Runs the real
 *          QrService against MongoDB Atlas using the API project's own user
 *          secrets, so the BSON field mappings are exercised for real.
 *          Creates its own reservation documents and deletes them afterwards,
 *          so the seeded demo data is left untouched.
 *
 *          Run with: dotnet test api\SmartSolar.Api.Tests
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SmartSolar.Api.Configuration;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Tests;

public class QrFlowTests : IDisposable
{
    // Must be a real ObjectId string: Reservation.VerifiedByOperatorId carries
    // [BsonRepresentation(BsonType.ObjectId)], so anything else fails to
    // serialise. In the running API this is the JWT sub claim, which holds the
    // operator's Mongo _id. This value is the seeded "operator.colombo".
    private const string OperatorId = "650000000000000000000011";

    private readonly IMongoCollection<Reservation> _reservations;
    private readonly QrService _service;
    private readonly string _prosumerNic;
    private readonly string _otherNic;
    private readonly string _stationId;
    private readonly List<string> _created = new();

    public QrFlowTests()
    {
        // The composition root registers these; tests never run it.
        BsonConventions.Register();

        // Reads the API project's user secrets through its assembly, so the
        // tests need no connection string of their own.
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(QrService).Assembly, optional: true)
            .Build();

        var settings = config.GetSection("MongoSettings").Get<MongoSettings>()
            ?? throw new InvalidOperationException(
                "MongoSettings not found. Set them on the API project with: " +
                "dotnet user-secrets set \"MongoSettings:ConnectionString\" \"...\"");

        var db = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
        _reservations = db.GetCollection<Reservation>("EnergyReservation");
        var users = db.GetCollection<User>("UsersDetail");
        var stations = db.GetCollection<SolarStation>("SolarStationInfo");

        _service = new QrService(
            new ReservationRepository(_reservations),
            new UserRepository(users),
            new StationRepository(stations),
            new VerificationStore(),
            Options.Create(new QrSettings { TokenExpiryHours = 2, VerificationIdExpiryMinutes = 5 }));

        // Two real Active prosumers and one real Active station, so the tests do
        // not depend on hard-coded seed ids.
        var prosumers = users
            .Find(u => u.Role == "Prosumer" && u.Status == "Active" && u.Nic != null)
            .Limit(2)
            .ToList();

        Assert.True(prosumers.Count >= 2,
            "Need at least two Active prosumers holding a NIC. Re-run docs/seed/seed-database.js.");

        _prosumerNic = prosumers[0].Nic!;
        _otherNic = prosumers[1].Nic!;

        var station = stations.Find(s => s.Status == "Active").FirstOrDefault();
        Assert.NotNull(station);
        _stationId = station!.Id;
    }

    // The regression test for the missing slotEnd: a QR must outlive its slot,
    // not be born expired against 0001-01-01.
    [Fact]
    public async Task IssuedQr_ExpiryIsDerivedFromSlotEnd()
    {
        var id = await CreateReservationAsync(_prosumerNic, "Approved");
        var reservation = await FindAsync(id);

        var issued = await _service.IssueQrForReservation(id, _prosumerNic);

        Assert.Equal(reservation.SlotEnd.AddHours(2), issued.ExpiresAt);
        Assert.True(issued.ExpiresAt > DateTime.UtcNow,
            "The QR was born expired — slotEnd is probably 0001-01-01. Re-run the seed script.");
    }

    // The security assertion: the plaintext token is never stored.
    [Fact]
    public async Task IssuedQr_StoresOnlyTheHash()
    {
        var id = await CreateReservationAsync(_prosumerNic, "Approved");

        var issued = await _service.IssueQrForReservation(id, _prosumerNic);
        var stored = await FindAsync(id);

        Assert.Equal(43, issued.Token.Length);          // 32 random bytes as Base64Url
        Assert.NotNull(stored.QrTokenHash);
        Assert.NotEqual(issued.Token, stored.QrTokenHash);
        Assert.NotNull(stored.QrIssuedAt);
        Assert.True(Math.Abs((stored.QrExpiresAt!.Value - issued.ExpiresAt).TotalSeconds) < 1,
            "Stored expiry should match the returned expiry (BSON keeps milliseconds).");
    }

    [Fact]
    public async Task FullFlow_IssueVerifyComplete_AndReplaysAreRejected()
    {
        var id = await CreateReservationAsync(_prosumerNic, "Approved");

        // 1. Issue
        var issued = await _service.IssueQrForReservation(id, _prosumerNic);

        // 2. Verify — resolves the booking, the prosumer and the station
        var verified = await _service.VerifyQrToken(issued.Token, OperatorId);
        Assert.Equal(id, verified.Reservation.Id);
        Assert.Equal(_prosumerNic, verified.Prosumer?.Nic);
        Assert.NotNull(verified.Station);
        Assert.False(string.IsNullOrWhiteSpace(verified.VerificationId));

        // 3. Complete
        await _service.CompleteReservation(id, verified.VerificationId, OperatorId);

        var completed = await FindAsync(id);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(OperatorId, completed.VerifiedByOperatorId);
        Assert.NotNull(completed.CompletedAt);

        // 4. The token cannot be scanned twice
        var replay = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.VerifyQrToken(issued.Token, OperatorId));
        Assert.Equal("QR_ALREADY_USED", replay.Code);
        Assert.Equal(409, replay.StatusCode);

        // 5. The verificationId is single-use
        var reuse = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.CompleteReservation(id, verified.VerificationId, OperatorId));
        Assert.Equal("VERIFICATION_INVALID", reuse.Code);
        Assert.Equal(400, reuse.StatusCode);
    }

    [Fact]
    public async Task Verify_WhenQrHasExpired_Is410()
    {
        var id = await CreateReservationAsync(_prosumerNic, "Approved");
        var issued = await _service.IssueQrForReservation(id, _prosumerNic);

        await _reservations.UpdateOneAsync(
            Builders<Reservation>.Filter.Eq(r => r.Id, id),
            Builders<Reservation>.Update.Set(r => r.QrExpiresAt, DateTime.UtcNow.AddMinutes(-1)));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.VerifyQrToken(issued.Token, OperatorId));

        Assert.Equal("QR_EXPIRED", ex.Code);
        Assert.Equal(410, ex.StatusCode);
    }

    [Fact]
    public async Task Verify_WithUnknownToken_IsInvalid()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.VerifyQrToken("not-a-real-token", OperatorId));

        Assert.Equal("QR_INVALID", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Complete_WithUnknownVerificationId_IsInvalid()
    {
        var id = await CreateReservationAsync(_prosumerNic, "Approved");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.CompleteReservation(id, "bogus-verification-id", OperatorId));

        Assert.Equal("VERIFICATION_INVALID", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    // The race a marker may ask about: approved, scanned, then cancelled before
    // the operator confirms.
    [Fact]
    public async Task Complete_WhenReservationNotApproved_IsConflict()
    {
        var id = await CreateReservationAsync(_prosumerNic, "Approved");
        var issued = await _service.IssueQrForReservation(id, _prosumerNic);
        var verified = await _service.VerifyQrToken(issued.Token, OperatorId);

        await _reservations.UpdateOneAsync(
            Builders<Reservation>.Filter.Eq(r => r.Id, id),
            Builders<Reservation>.Update.Set(r => r.Status, "Cancelled"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.CompleteReservation(id, verified.VerificationId, OperatorId));

        Assert.Equal("RESERVATION_INVALID_STATE", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Issue_ForAnotherProsumer_IsDenied()
    {
        var id = await CreateReservationAsync(_prosumerNic, "Approved");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.IssueQrForReservation(id, _otherNic));

        Assert.Equal("RESERVATION_ACCESS_DENIED", ex.Code);
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Issue_WhenNotApproved_IsRejected()
    {
        var id = await CreateReservationAsync(_prosumerNic, "Pending");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.IssueQrForReservation(id, _prosumerNic));

        Assert.Equal("QR_NOT_APPROVED", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Issue_ForMissingReservation_IsNotFound()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.IssueQrForReservation(ObjectId.GenerateNewId().ToString(), _prosumerNic));

        Assert.Equal("RESERVATION_NOT_FOUND", ex.Code);
        Assert.Equal(404, ex.StatusCode);
    }

    // Guards the seeded demo data. The tests above build their own reservations
    // with an explicit slotEnd, so without this one a missing slotEnd on the
    // seeded fixtures would only surface during the demo itself.
    [Fact]
    public async Task SeededApprovedReservations_AllHaveSlotEnd()
    {
        var seeded = await _reservations
            .Find(Builders<Reservation>.Filter.Eq(r => r.Status, "Approved"))
            .ToListAsync();

        Assert.NotEmpty(seeded);

        var missing = seeded.Where(r => r.SlotEnd == default).Select(r => r.Id).ToList();

        Assert.True(missing.Count == 0,
            "These Approved reservations have no slotEnd, so any QR issued for them is born expired: "
            + string.Join(", ", missing) + ". Re-run docs/seed/seed-database.js.");
    }

    // Inserts a throwaway reservation so the seeded demo data is never modified.
    private async Task<string> CreateReservationAsync(string nic, string status)
    {
        var id = ObjectId.GenerateNewId().ToString();
        var now = DateTime.UtcNow;

        await _reservations.InsertOneAsync(new Reservation
        {
            Id = id,
            ProsumerNIC = nic,
            StationId = _stationId,
            SlotId = ObjectId.GenerateNewId().ToString(),
            SlotStart = now.AddHours(24),
            SlotEnd = now.AddHours(28),
            EnergyKWh = 10,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        });

        _created.Add(id);
        return id;
    }

    private Task<Reservation> FindAsync(string id) =>
        _reservations.Find(Builders<Reservation>.Filter.Eq(r => r.Id, id)).FirstAsync();

    // Removes every document this test class inserted.
    public void Dispose()
    {
        if (_created.Count > 0)
        {
            _reservations.DeleteMany(Builders<Reservation>.Filter.In(r => r.Id, _created));
        }
    }
}
