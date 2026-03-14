using DP.Infrastructure.Database;
using DP.Worker.Consumers;
using DP.Worker.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateDefaultBuilder(args);

    builder.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.ConfigureServices((context, services) =>
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(context.Configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddSingleton<IPaymentProcessingService, PaymentProcessingService>();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<PaymentRequestedConsumer, PaymentRequestedConsumerDefinition>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var host = context.Configuration["RabbitMQ:Host"] ?? "localhost";
                var username = context.Configuration["RabbitMQ:Username"] ?? "guest";
                var password = context.Configuration["RabbitMQ:Password"] ?? "guest";

                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });
    });

    var app = builder.Build();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
