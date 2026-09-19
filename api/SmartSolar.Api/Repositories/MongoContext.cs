/*
 * File:    MongoContext.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Single entry point to the four Mongo collections. Registered as a
 *          singleton; resolves the database once and exposes typed
 *          IMongoCollection<T> properties with the exact collection names.
 */
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SmartSolar.Api.Configuration;
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public class MongoContext
{
    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users { get; }
    public IMongoCollection<SolarStation> Stations { get; }
    public IMongoCollection<BookingSlot> Slots { get; }
    public IMongoCollection<Reservation> Reservations { get; }

    // Resolves the database once from the shared client and binds each
    // collection to its exact name from docs/db-schema.md.
    public MongoContext(IMongoClient client, IOptions<MongoSettings> options)
    {
        Database = client.GetDatabase(options.Value.DatabaseName);

        Users = Database.GetCollection<User>("UsersDetail");
        Stations = Database.GetCollection<SolarStation>("SolarStationInfo");
        Slots = Database.GetCollection<BookingSlot>("EnergyBookingSlots");
        Reservations = Database.GetCollection<Reservation>("EnergyReservation");
    }
}
