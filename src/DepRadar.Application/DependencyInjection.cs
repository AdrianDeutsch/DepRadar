using DepRadar.Application.Behaviors;
using DepRadar.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DepRadar.Application;

/// <summary>
/// Composition root for the application layer: wires the mediator, discovers all
/// request handlers in this assembly and registers the pipeline behaviors.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers the application layer services into the container.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped so the mediator resolves handlers (and their scoped DbContext) from
        // the ambient request/work scope rather than the root provider.
        services.AddScoped<ISender, Mediator>();
        services.AddScoped<Risk.GraphAssessmentLoader>();
        services.TryAddSingleton(TimeProvider.System);

        RegisterHandlers(services);

        // Open-generic behavior applied to every request. Order = registration order.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        return services;
    }

    /// <summary>Scans this assembly and registers each closed <c>IRequestHandler</c>.</summary>
    private static void RegisterHandlers(IServiceCollection services)
    {
        var concreteTypes = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var type in concreteTypes)
        {
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddTransient(handlerInterface, type);
            }
        }
    }
}
