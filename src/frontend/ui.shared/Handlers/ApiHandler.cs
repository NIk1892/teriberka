using Mediator;
using UI.Shared.Helpers;

namespace UI.Shared.Handlers;

public abstract class ApiHandler<TRequest, TResponse>
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    protected abstract string ApiPath { get; }

    public abstract ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);

    protected virtual string BuildQueryString(IBaseRequest request)
        => request.ToQueryString(ApiPath);
}
