using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PaymentsService.API.Middleware;
using PaymentsService.Core.Interfaces;
using PaymentsService.Infrastructure.Data;
using PaymentsService.Infrastructure.Repositories;
using PaymentsService.Services;
 
/*
 * Application entry point that configures services, authentication, data
 * access, and middleware for the PaymentsService API.
 */
var builder = WebApplication.CreateBuilder(args);

// ─── Database ──────────────────────────────────────────────────────────────
builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Dependency Injection ──────────────────────────────────────────────────
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// ─── JWT Authentication ────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("JWT Key must be supplied via configuration or environment variable 'Jwt__Key'.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidateAudience         = true,
            ValidAudience            = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero  // No tolerance for expired tokens
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                if (ctx.Exception is SecurityTokenExpiredException)
                    ctx.Response.Headers.Append("X-Token-Expired", "true");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// ─── Auto-migrate on startup (dev convenience) ────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    db.Database.EnsureCreated();
}

// ─── Middleware pipeline ───────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Needed for test project to reference the entry point
/*
 * Marker type to allow integration tests to reference the API entry point.
 */
/// <summary>
/// Marker type for the API entry point, used by integration tests to bootstrap the host.
/// </summary>
/// <remarks>
/// As the composition root in Clean Architecture, this class exists to wire infrastructure
/// dependencies such as <see cref="PaymentsDbContext"/>, <see cref="IPaymentRepository"/>,
/// authentication, and middleware into the API. Idempotency and transaction safety are enforced
/// by the application and data layers (e.g., <see cref="IPaymentService"/> and repository
/// implementations) that this entry point registers; this type itself only composes those
/// services for consistent runtime behavior.
/// </remarks>
public partial class Program { }

