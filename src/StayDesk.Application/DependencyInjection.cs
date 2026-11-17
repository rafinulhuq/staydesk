using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Bookings;
using StayDesk.Application.Pricing;
using StayDesk.Application.Properties;
using StayDesk.Application.Rooms;

namespace StayDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<PropertyService>();
        services.AddScoped<RoomService>();
        services.AddScoped<RoomSearchService>();
        services.AddScoped<PricingService>();
        services.AddScoped<PriceRuleService>();
        services.AddScoped<BookingService>();
        return services;
    }
}
