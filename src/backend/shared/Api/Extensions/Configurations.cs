using System.Text.Json.Serialization;
using Application;
using Domain;
using FluentValidation;
using Infrastructure;
using Infrastructure.DataAccess;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Riok.Mapperly.Abstractions;


namespace Api;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddExceptionHandling()
        {
            services.AddProblemDetails();

            //to do validation exception handling
        
            services.AddExceptionHandler<GlobalExceptionHandler>();
        
            return services;
        }

        public IServiceCollection AddPersistence<TR, TW>(IConfiguration configuration, Action<NpgsqlDbContextOptionsBuilder>? optionsBuilder = null)
            where TR : ReadDbContext
            where TW : WriteDbContext
        {
            // Schema routing is done by Postgres' search_path (set via the `Search Path` keyword in
            // the connection string). EF Core stays schema-agnostic — migrations emit unqualified
            // SQL, and __EFMigrationsHistory lands in search_path's first schema. To swap topologies
            // (per-service DB vs. shared DB) you only edit the connection string; no code changes.

            // Register the concrete TR/TW once via AddDbContext, then expose the base types
            // (ReadDbContext / WriteDbContext / IWriteDbContext) as **factory aliases** that
            // resolve to the same scoped instance. Using AddScoped<TBase, TImpl>() instead
            // would create a *second* scoped instance per request, so a UnitOfWork pulling
            // WriteDbContext and a repository pulling TW would diverge — SaveChanges on one
            // wouldn't see entities tracked by the other.
            services.AddDbContext<TR>(options =>
                options.UseNpgsql(configuration[DbConstants.ConnectionStringRead], optionsBuilder));
            services.AddScoped<ReadDbContext>(sp => sp.GetRequiredService<TR>());

            services.AddDbContext<TW>(options =>
                options.UseNpgsql(configuration[DbConstants.ConnectionStringWrite], optionsBuilder)
                       .ReplaceService<IMigrationsSqlGenerator, SafeNpgsqlMigrationsSqlGenerator>());
            services.AddScoped<WriteDbContext>(sp => sp.GetRequiredService<TW>());
            services.AddScoped<IWriteDbContext>(sp => sp.GetRequiredService<TW>());

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }

        public IServiceCollection AddApiServices()
        {
            services.AddEndpointsApiExplorer();
        
            services.AddControllers();
        
            services.AddSwaggerGen(option =>
            {
                option.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });
                option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please enter a valid token",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });
                option.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        []
                    }
                });
            });
        
            services.AddHttpContextAccessor();
        
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Where(a =>
                {
                    try { _ = a.GetExportedTypes(); return true; }
                    catch { return false; }
                })
                .ToArray();


            services.AddScoped(typeof(IQueryRepository<,,>), typeof(SingleQueryRepository<,,>));
            services.AddScoped(typeof(IListQueryRepository<,,>), typeof(ListQueryRepository<,,>));
            services.AddScoped(typeof(ICommandRepository<,>), typeof(CommandRepository<,>));

            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(t => t.AssignableTo(typeof(CommandRepository<,>)).Where(c => c != typeof(CommandRepository<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(t => t.AssignableTo(typeof(SingleQueryRepository<,,>)).Where(c => c != typeof(SingleQueryRepository<,,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(t => t.AssignableTo(typeof(ListQueryRepository<,,>)).Where(c => c != typeof(ListQueryRepository<,,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

            services.AddValidatorsFromAssemblies(assemblies, ServiceLifetime.Singleton);

            services.ConfigureMappers();

            services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull;
            });

        
            return services;
        }
    }


    extension(WebApplication app)
    {
        public async Task EnsureDataBaseAsync()
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<IWriteDbContext>();
            if (dbContext != null)
            {
                if (!Domain.Environments.IsTesting)
                    await dbContext.MigrateAsync();
                else
                    await dbContext.EnsureCreatedAsync();
            }
        }

        public WebApplication ConfigureHealthCheck()
        {
            app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });

            app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => false
            });

            return app;
        }
    }


    public static IServiceCollection ConfigureMappers(this IServiceCollection services)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x=>x.Modules.Any(c=>c.Name.EndsWith("Infrastructure.dll"))).ToArray();
        
        var types = assemblies
            .SelectMany(s => s.GetTypes())
            .Where(p => (p.Name.EndsWith("DtoMapper") || p.Name.EndsWith("EntityMapper") || p.Name.EndsWith("RpcMapper")) && p is { IsInterface: false, IsPublic: true })
            .Select(s => new
            {
                Service = s.GetInterfaces().FirstOrDefault(),
                Implementation = s
            })
            .Where(x => x.Service != null);

        foreach (var type in types)
        {
            services.AddSingleton(type.Service, type.Implementation);
            services.AddSingleton(type.Implementation);
        }

        return services;
    }
}