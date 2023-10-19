using RPC4NetMq.MessengingTypes;

namespace RPC4NetMq.Client
{
    /// <summary>
    /// Implement this interface to send Request to server
    /// </summary>
    public interface IRpcClientCoordinator
    {
        void SendAsync(RpcRequest request);
        RpcResponse Send(RpcRequest request);
    }
}