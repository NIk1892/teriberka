using Contracts;
using Mediator;

namespace Application;

public class SanitizationBehavior<TRequest, TResponse>(HtmlSanitizerService sanitizerService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IHtmlContent { Content: not null } htmlContent)
        {
            htmlContent.Content = sanitizerService.Sanitize(htmlContent.Content);
        }

        return await next(request, cancellationToken);
    }
}
