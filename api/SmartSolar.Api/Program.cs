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
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Driver;
using SmartSolar.Api.Configuration;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Middleware;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;
using SmartSolar.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- §1 Configuration binding — registered before anything that consumes it
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<QrSettings>(builder.Configuration.GetSection("QrSettings"));

// ---- §1 Global Bson conventions: camelCase element names to match
// docs/db-schema.md, and tolerate fields the model does not declare.
// Extracted to BsonConventions so the tests map documents identically.
BsonConventions.Register();

// ---- §1 Mongo client is a singleton: it owns the connection pool and is
// designed to be reused. One per request exhausts the pool under demo load.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoSettings>>().Value.ConnectionString));
builder.Services.AddSingleton<MongoContext>();

// ---- §1 Repositories and services. Each repository takes its collection from
// MongoContext, so the collection names live in exactly one place.
builder.Services.AddScoped<IReservationRepository>(sp =>
    new ReservationRepository(sp.GetRequiredService<MongoContext>().Reservations));
builder.Services.AddScoped<IUserRepository>(sp =>
    new UserRepository(sp.GetRequiredService<MongoContext>().Users));
builder.Services.AddScoped<IStationRepository>(sp =>
    new StationRepository(sp.GetRequiredService<MongoContext>().Stations));
builder.Services.AddScoped<IQrService, QrService>();

// Identity (M1): PBKDF2 hasher from docs/auth.md, stateless so a singleton.
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Singleton: the scan-to-complete handshake spans two requests, so the store
// must outlive a single scoped QrService instance.
builder.Services.AddSingleton<VerificationStore>();

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

// ---- Swagger with a Bearer scheme, so a JWT can be pasted into the UI and
// the [Authorize] attributes on the QR endpoints can be exercised. Dev only.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartSolar API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT only. Swagger adds the 'Bearer ' prefix."
    });

    // Swashbuckle 10 resolves the scheme reference against the document, so the
    // requirement is supplied as a factory rather than a fixed object.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
    });
});

var app = builder.Build();

// ---- Pipeline order (docs/PLUMBING-GUIDE.md). Do not reorder.
app.UseMiddleware<ExceptionHandlingMiddleware>();   // §2 first — catches everything downstream
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(CorsPolicyName);
app.UseAuthentication();
app.UseMiddleware<MethodOverrideMiddleware>();      // §3 after auth (User populated), before routing
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
