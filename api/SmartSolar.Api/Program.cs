/*
 * File:    Program.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Composition root. Binds configuration, registers the Mongo client
 *          and MongoContext as singletons, configures JWT bearer auth and
 *          CORS, and fixes the middleware order from docs/PLUMBING-GUIDE.md.
 */
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using SmartSolar.Api.Configuration;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Middleware;
using SmartSolar.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ---- §1 Configuration binding — registered before anything that consumes it
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<QrSettings>(builder.Configuration.GetSection("QrSettings"));

// ---- §1 Global Bson conventions, once: camelCase element names to match
// docs/db-schema.md, and tolerate fields the model does not declare.
ConventionRegistry.Register(
    "SmartSolarConventions",
    new ConventionPack
    {
        new CamelCaseElementNameConvention(),
        new IgnoreExtraElementsConvention(true)
    },
    _ => true);

// ---- §1 Mongo client is a singleton: it owns the connection pool and is
// designed to be reused. One per request exhausts the pool under demo load.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoSettings>>().Value.ConnectionString));
builder.Services.AddSingleton<MongoContext>();

// ---- §4 JWT bearer authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JwtSettings section is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names as issued (sub/nic/role/name) instead of the
        // legacy ClaimTypes.* mapping, so RoleClaimType below is deterministic.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero,

            // The RoleClaimType trap: the generator emits a bare "role"
            // claim, so [Authorize(Roles = "...")] must be told to read it.
            RoleClaimType = "role",
            NameClaimType = "name"
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<JwtTokenGenerator>();

// ---- §4 CORS — origins come from configuration so IIS differs from dev
// without a code change. Never AllowAnyOrigin together with credentials.
const string CorsPolicyName = "SmartSolarClients";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

builder.Services.AddControllers();

var app = builder.Build();

// ---- Pipeline order (docs/PLUMBING-GUIDE.md). Do not reorder.
app.UseMiddleware<ExceptionHandlingMiddleware>();   // §2 first — catches everything downstream
app.UseCors(CorsPolicyName);
app.UseAuthentication();
app.UseMiddleware<MethodOverrideMiddleware>();      // §3 after auth (User populated), before routing
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
