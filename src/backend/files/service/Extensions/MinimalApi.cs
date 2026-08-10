using System.Text.Json;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Contracts;
using Domain;
using Microsoft.AspNetCore.Components.Forms;

namespace Files;

public static class MinimalApi
{
    public static WebApplication MediatePutFileFromForm<TRequest>(this WebApplication app, string group,
        string template, string role)
        where TRequest : FileSaveRequest
    {
        app.MapPut($"/api/{role}/{group}/{template}",
                async (IMediator mediator, [FromForm] TRequest request, CancellationToken cancellationToken) =>
                {
                    var file = request.File;
                    await using var ms = new MemoryStream();
                    await file.OpenReadStream().CopyToAsync(ms, cancellationToken);
                    return Results.Ok(await mediator.Send(new FileSaveCommand()
                    {
                        FileName = file.FileName,
                        File = ms.ToArray(),
                        ContentType = file.ContentType,
                    }, cancellationToken));
                }).DisableAntiforgery()
            .Produces<ExecuteRequestResult>().WithTags(group);

        return app;
    }
}