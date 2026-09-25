/*
 * File:    DevTokenFactory.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Development aid. Mints real JWTs for a seeded prosumer and Grid
 *          Operator so the Swagger UI can exercise the [Authorize] endpoints.
 *          That is otherwise impossible because no /api/auth/login endpoint
 *          exists yet. Writes them to a temp file and reports the path.
 *          Delete this file once a real login endpoint exists.
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
using Xunit.Abstractions;

namespace SmartSolar.Api.Tests;

public class DevTokenFactory
{
    private readonly ITestOutputHelper _output;

    public DevTokenFactory(ITestOutputHelper output)
    {
        _output = output;
    }

    // Fixed id: each run replaces the previous throwaway reservation, so nothing
    // accumulates in the collection.
    private const string ScratchReservationId = "689999999999999999999999";

    [Fact]
    public void WriteTokensForSwagger()
    {
        BsonConventions.Register();

        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(QrService).Assembly, optional: true)
            .Build();

        var jwt = config.GetSection("JwtSettings").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings missing from user secrets.");
        var mongo = config.GetSection("MongoSettings").Get<MongoSettings>()
            ?? throw new InvalidOperationException("MongoSettings missing from user secrets.");

        var db = new MongoClient(mongo.ConnectionString).GetDatabase(mongo.DatabaseName);
        var users = db.GetCollection<User>("UsersDetail");
        var reservations = db.GetCollection<Reservation>("EnergyReservation");

        var prosumer = users
            .Find(u => u.Role == "Prosumer" && u.Status == "Active" && u.Nic != null)
            .First();
        var operatorUser = users
            .Find(u => u.Role == "GridOperator" && u.Status == "Active")
            .First();
        var station = db.GetCollection<SolarStation>("SolarStationInfo")
            .Find(s => s.Status == "Active")
            .First();

        // Replace the throwaway reservation with a fresh, valid one. It carries a
        // real slotEnd, which the seeded Approved reservations currently lack, so
        // the Swagger flow can be exercised without touching demo data.
        reservations.DeleteOne(Builders<Reservation>.Filter.Eq(r => r.Id, ScratchReservationId));

        var now = DateTime.UtcNow;
        reservations.InsertOne(new Reservation
        {
            Id = ScratchReservationId,
            ProsumerNIC = prosumer.Nic!,
            StationId = station.Id,
            SlotId = ObjectId.GenerateNewId().ToString(),
            SlotStart = now.AddHours(24),
            SlotEnd = now.AddHours(28),
            EnergyKWh = 12,
            Status = "Approved",
            CreatedAt = now,
            UpdatedAt = now
        });

        var generator = new JwtTokenGenerator(Options.Create(jwt));

        var (prosumerToken, prosumerExpiry) =
            generator.Generate(prosumer.Id, prosumer.Nic, prosumer.FullName, "Prosumer");
        var (operatorToken, _) =
            generator.Generate(operatorUser.Id, null, operatorUser.FullName, "GridOperator");

        var path = Path.Combine(Path.GetTempPath(), "smartsolar-dev-tokens.txt");

        File.WriteAllText(path, $"""
            Generated {DateTime.UtcNow:u} UTC. Tokens expire {prosumerExpiry:u} UTC.

            PROSUMER
              nic   = {prosumer.Nic}
              name  = {prosumer.FullName}
            {prosumerToken}

            GRID OPERATOR
              sub   = {operatorUser.Id}
              name  = {operatorUser.FullName}
            {operatorToken}

            DISPOSABLE APPROVED RESERVATION (safe to complete)
              id      = {ScratchReservationId}
              station = {station.StationName}
              slot    = {now.AddHours(24):u} to {now.AddHours(28):u}

            Paste a token into Swagger's Authorize box WITHOUT the word "Bearer".
            """);

        _output.WriteLine($"Dev tokens written to {path}");
        Assert.True(File.Exists(path), $"Expected dev token file at {path}");
    }
}
