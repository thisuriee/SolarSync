/*
 * File:    MongoSettings.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Strongly-typed binding for the "MongoSettings" configuration
 *          section (Atlas connection string + database name).
 */
namespace SmartSolar.Api.Configuration;

public class MongoSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
