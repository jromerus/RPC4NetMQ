using RPC4NetMq.MessengingTypes;

namespace RPC4NetMq.Server
{
    /// <summary>
    /// A RPC server should implement this interface to handle RPC request
    /// I don't really think you need to do that if you use this library as the default implementation is pretty enough
    /// </summary>
    public interface IRpcServerCoordinator
    {
        void Start();
        void HandleMessage(RpcRequest request);
        void Stop();
    }
}