/*
 * File:    PingController.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Anonymous health check — GET /api/ping. Reports API status, the
 *          UTC timestamp, and whether Mongo answered a {ping:1} command.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SmartSolar.Api.Repositories;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api/ping")]
public class PingController : ControllerBase
{
    private readonly MongoContext _mongo;

    public PingController(MongoContext mongo)
    {
        _mongo = mongo;
    }

    // Returns 200 with a status object. Mongo reachability is reported as a
    // boolean rather than failing the endpoint, so the API can still prove
    // it is up when Atlas is not.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get()
    {
        bool mongoOk;
        try
        {
            await _mongo.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            mongoOk = true;
        }
        catch
        {
            mongoOk = false;
        }

        return Ok(new
        {
            status = "ok",
            timestampUtc = DateTime.UtcNow,
            mongo = mongoOk
        });
    }
}
