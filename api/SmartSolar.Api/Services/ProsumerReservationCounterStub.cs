/*
 * File:    ProsumerReservationCounterStub.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: TEMPORARY placeholder until M3 (Thisuri) ships
 *          CountActiveForProsumer(nic). Always reports zero, so deactivation
 *          is never blocked while this is registered. Delete this file and
 *          point the IProsumerReservationCounter registration in Program.cs at
 *          M3's implementation before feature freeze.
 */
namespace SmartSolar.Api.Services;

public class ProsumerReservationCounterStub : IProsumerReservationCounter
{
    private readonly ILogger<ProsumerReservationCounterStub> _logger;

    public ProsumerReservationCounterStub(ILogger<ProsumerReservationCounterStub> logger)
    {
        _logger = logger;
    }

    // Returns 0 and logs a warning, so the missing rule is visible in the
    // server log rather than silently passing.
    public Task<long> CountActiveForProsumer(string nic)
    {
        _logger.LogWarning(
            "Active-reservation check for NIC {Nic} skipped: M3's CountActiveForProsumer is not wired in yet.", nic);
        return Task.FromResult(0L);
    }
}
