using Applications.Contracts;
using Mediator;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace UI.Public.Web.Features.Manager;

/// <summary>
/// Вход, выход и отметки на странице заявок. Эндпоинты, а не формы компонентов: cookie
/// ставится только из HTTP-обработчика (как у /set-culture), а отметки шлёт manager.js
/// без перезагрузки — тем же телом, что и форма без скрипта.
/// </summary>
public static class ManagerEndpoints
{
    public static IServiceCollection AddManager(this IServiceCollection services)
    {
        services.AddSingleton<ManagerAuth>();

        services.AddAuthentication(ManagerAuth.Scheme)
            .AddCookie(ManagerAuth.Scheme, options =>
            {
                options.Cookie.Name = ManagerAuth.CookieName;
                options.Cookie.Path = ManagerAuth.BasePath;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                // за nginx https приезжает X-Forwarded-Proto — cookie будет Secure
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = ManagerAuth.LoginPath;
                options.AccessDeniedPath = ManagerAuth.LoginPath;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;

                options.Events.OnValidatePrincipal = async context =>
                {
                    var auth = context.HttpContext.RequestServices.GetRequiredService<ManagerAuth>();
                    if (context.Principal is not null && auth.IsCurrent(context.Principal))
                        return;

                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(ManagerAuth.Scheme);
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static void MapManager(this WebApplication app)
    {
        app.MapPost(ManagerAuth.SignInPath, async (HttpContext context, ManagerAuth auth, IAntiforgery antiforgery,
            ILogger<ManagerAuth> logger) =>
        {
            if (!auth.Enabled)
                return Results.NotFound();

            if (!await IsValidAsync(context, antiforgery))
                return Results.BadRequest();

            var form = await context.Request.ReadFormAsync();
            if (!auth.Check(form["password"].ToString()))
            {
                logger.LogWarning("Неверный пароль на странице заявок");
                // именно "true": биндинг bool из query не понимает "1"
                return Results.LocalRedirect(ManagerAuth.LoginPath + "?error=true");
            }

            await context.SignInAsync(ManagerAuth.Scheme, auth.CreatePrincipal(),
                new AuthenticationProperties { IsPersistent = true });

            return Results.LocalRedirect(ManagerAuth.BasePath);
        });

        app.MapPost("/manager/logout", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await IsValidAsync(context, antiforgery))
                return Results.BadRequest();

            await context.SignOutAsync(ManagerAuth.Scheme);
            return Results.LocalRedirect(ManagerAuth.LoginPath);
        });

        // Отметка «обработана» и комментарий. JSON для manager.js, редирект к карточке —
        // для формы без скрипта.
        app.MapPost("/manager/process", async (HttpContext context, IAntiforgery antiforgery, IMediator mediator,
            ILogger<ManagerAuth> logger) =>
        {
            if (!await IsValidAsync(context, antiforgery))
                return Results.BadRequest();

            var form = await context.Request.ReadFormAsync();
            var wantsJson = context.Request.Headers.Accept.ToString().Contains("application/json");

            if (!Guid.TryParse(form["id"], out var id))
                return wantsJson ? Results.Json(new { error = "id" }, statusCode: 400) : Results.BadRequest();

            var processed = form["processed"].ToString() == "true";
            var comment = form["comment"].ToString();
            var back = ManagerPaths.List(form["show"].ToString(), int.TryParse(form["page"], out var page) ? page : 1, id);

            if (comment.Length > ApplicationProcessCommand.MaxCommentLength)
                return wantsJson
                    ? Results.Json(new { error = "comment" }, statusCode: 400)
                    : Results.LocalRedirect(back);

            try
            {
                await mediator.Send(new ApplicationProcessCommand
                {
                    Id = id,
                    Processed = processed,
                    ManagerComment = comment
                });
            }
            catch (Exception e)
            {
                // Ни имени, ни телефона здесь нет — только id заявки.
                logger.LogWarning(e, "Не удалось сохранить отметку заявки {ApplicationId}", id);
                return wantsJson
                    ? Results.Json(new { error = "failed" }, statusCode: 502)
                    : Results.LocalRedirect(back);
            }

            return wantsJson
                ? Results.Json(new { processed })
                : Results.LocalRedirect(back);
        }).RequireAuthorization();
    }

    // Форму читаем руками, а UseAntiforgery проверяет только эндпоинты с form-binding —
    // значит и токен проверяем руками (как /chat/send).
    private static async Task<bool> IsValidAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}

public static class ManagerPaths
{
    /// <summary>Вкладка, страница и якорь карточки — вернуться туда же после отметки без скрипта.</summary>
    public static string List(string? show, int page, Guid? id = null)
    {
        var query = new List<string>();
        if (show is "done" or "all")
            query.Add("show=" + show);
        if (page > 1)
            query.Add("page=" + page);

        return ManagerAuth.BasePath
               + (query.Count > 0 ? "?" + string.Join("&", query) : "")
               + (id is { } value ? "#app-" + value : "");
    }
}
