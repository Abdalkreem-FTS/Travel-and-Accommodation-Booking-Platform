using System.Text;
using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Authentication;
using HotelBooking.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HotelBooking.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException(
                      $"Configuration section '{JwtOptions.SectionName}' is missing.");

        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < JwtOptions.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"'{JwtOptions.SectionName}:SigningKey' must be at least " +
                $"{JwtOptions.MinimumSigningKeyBytes} bytes for HS256. Set it with: " +
                $"dotnet user-secrets set \"{JwtOptions.SectionName}:SigningKey\" \"<random 32+ byte value>\" " +
                "-p src/HotelBooking.Api");
        }

        var authenticationOptions = configuration.GetSection(AuthenticationOptions.SectionName)
                                 .Get<AuthenticationOptions>()
                             ?? new AuthenticationOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = authenticationOptions.ClockSkew,
                    RoleClaimType = ClaimNames.Role,
                    NameClaimType = JwtRegisteredClaimNames.Sub
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<ITokenDenylist, ILoggerFactory>((options, denylist, loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger(typeof(SessionRevocationCheck));

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context => SessionRevocationCheck.RejectRevokedSessionsAsync(context, denylist, logger)
                };
            });

        return services;
    }
}
