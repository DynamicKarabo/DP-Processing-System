using DP.Api.Middleware;
using DP.Api.Services;
using DP.Infrastructure.Database;
using DP.Infrastructure.Extensions;
using DP.Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddControllers();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
    builder.Services.AddScoped<IAuditLogService, AuditLogService>();

    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddRabbitMqWithConfig(builder.Configuration);
    });

    var app = builder.Build();

    if (args.Contains("--migrate"))
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }
    }

    app.UseSerilogRequestLogging();
    app.UseHttpMetrics(); // Prometheus

    app.UseRouting();

    app.UseMiddleware<IdempotencyMiddleware>();

    app.MapMetrics(); // Expose /metrics
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
