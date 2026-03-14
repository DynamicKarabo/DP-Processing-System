using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DP.Infrastructure.Extensions;

public static class MassTransitExtensions
{
    public static void AddRabbitMqWithConfig(this IBusRegistrationConfigurator x, IConfiguration configuration, Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configure = null)
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            var host = configuration["RabbitMQ:Host"] ?? "localhost";
            var username = configuration["RabbitMQ:Username"] ?? "guest";
            var password = configuration["RabbitMQ:Password"] ?? "guest";

            cfg.Host(host, "/", h =>
            {
                h.Username(username);
                h.Password(password);
            });

            configure?.Invoke(context, cfg);

            cfg.ConfigureEndpoints(context);
        });
    }
}
