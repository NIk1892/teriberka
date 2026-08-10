using Grpc;
using Grpc.Net.ClientFactory;

namespace Rpc.Clients;

public class UserRpcClient(GrpcClientFactory grpcClientFactory)
{
    private readonly UserRpcService.UserRpcServiceClient  _client = grpcClientFactory.CreateClient<UserRpcService.UserRpcServiceClient>(RpcClientsDefinition.Users);

    public async Task<UserRpcResponse> GetItems(UserRpcQuery query)
       => await _client.GetItemsAsync(query);

    public async Task<UserGetOrCreateResponse> GetOrCreate(UserGetOrCreateRequest request)
       => await _client.GetOrCreateAsync(request);

    public async Task<AdminLoginResponse> AdminLogin(AdminLoginRequest request)
       => await _client.AdminLoginAsync(request);

    public async Task<SetTotpSecretResponse> SetTotpSecret(SetTotpSecretRequest request)
       => await _client.SetTotpSecretAsync(request);

    public async Task<SetPasswordResponse> SetPassword(SetPasswordRequest request)
       => await _client.SetPasswordAsync(request);
}
