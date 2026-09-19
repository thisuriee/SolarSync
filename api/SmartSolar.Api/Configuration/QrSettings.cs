/*
 * File:    QrSettings.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Strongly-typed binding for the "QrSettings" section — how many
 *          hours after slotEnd a QR token remains valid.
 */
namespace SmartSolar.Api.Configuration;

public class QrSettings
{
    public int TokenExpiryHours { get; set; }
}
