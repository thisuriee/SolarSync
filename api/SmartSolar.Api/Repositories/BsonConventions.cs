/*
 * File:    BsonConventions.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: The global BSON conventions the API depends on: camelCase element
 *          names to match docs/db-schema.md, and tolerance of fields the models
 *          do not declare. Extracted from Program.cs so anything that talks to
 *          Mongo without running the composition root — the integration tests,
 *          future tooling — gets identical mapping.
 */
using MongoDB.Bson.Serialization.Conventions;

namespace SmartSolar.Api.Repositories;

public static class BsonConventions
{
    private static readonly object Gate = new();
    private static bool _registered;

    // Registers the convention pack once per process. Idempotent, so it is safe
    // to call from the composition root and from every test fixture.
    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            ConventionRegistry.Register(
                "SmartSolarConventions",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true)
                },
                _ => true);

            _registered = true;
        }
    }
}
