using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using RPC4NetMq.MessengingTypes;
using RPC4NetMq.Serialization;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPC4NetMq.Server
{
    /// <summary>
    /// [ RouterSocket + Poller ]  ← 1 hilo NetMQ
    ///          │
    ///          ▼
    ///   Recepción mensajes
    ///          │
    ///          ▼
    ///   Task.Run por request  ← pool de threads.NET
    ///          │
    ///          ▼
    ///   NetMQQueue(thread-safe)
    ///          │
    ///          ▼
    ///   Envío serializado por RouterSocket
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ConcurrentRpcServer<T> : IRpcServerCoordinator, IDisposable
     where T : class
    {
        private readonly T _realInstance;
        private readonly RouterSocket _router;
        private readonly NetMQPoller _poller;
        private readonly NetMQQueue<NetMQMessage> _outgoing;
        private readonly ILogger _log;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private bool _started;

        public ConcurrentRpcServer(T realInstance, string address, ILogger log)
        {
            _realInstance = realInstance;
            _log = log;

            _router = new RouterSocket();
            _router.Bind(address);

            _outgoing = new NetMQQueue<NetMQMessage>();
            _poller = new NetMQPoller { _router, _outgoing };

            _router.ReceiveReady += OnReceive;
            _outgoing.ReceiveReady += OnOutgoing;
        }

        public void Start()
        {
            if (_started) return;
            _started = true;

            Task.Run(() =>
            {
                try
                {
                    _poller.Run();
                }
                catch (Exception ex)
                {
                    _log.LogCritical(ex, "RPC poller crashed");
                }
            });
        }

        public void Stop()
        {
            // Contrato síncrono, implementación ordenada
            StopInternalAsync().GetAwaiter().GetResult();
        }

        private async Task StopInternalAsync()
        {
            if (!_started) return;

            _cts.Cancel();

            // permite drenar colas
            await Task.Delay(50);

            _poller.Stop();

            _outgoing.Dispose();
            _router.Dispose();
            _poller.Dispose();

            _started = false;
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnReceive(object sender, NetMQSocketEventArgs e)
        {
            try
            {
                var msg = e.Socket.ReceiveMultipartMessage();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var reply = await HandleRpcAsync(msg);
                        if (reply != null)
                            _outgoing.Enqueue(reply);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Worker failed");
                    }
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Receive failed");
            }
        }

        private void OnOutgoing(object sender, NetMQQueueEventArgs<NetMQMessage> e)
        {
            try
            {
                _router.SendMultipartMessage(e.Queue.Dequeue());
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Send failed");
            }
        }

        private async Task<NetMQMessage> HandleRpcAsync(NetMQMessage request)
        {
            var clientId = request.Pop();
            var payload = request.Pop().ConvertToString();

            var rpcRequest = JSON.DeSerializeRequest(payload);
            var rpcResponse = await BuildResponse(rpcRequest);

            var reply = new NetMQMessage();
            reply.Append(clientId);
            reply.Append(JsonConvert.SerializeObject(rpcResponse));

            return reply;
        }

        public Task<RpcResponse> BuildResponse(RpcRequest msg)
        {
            if (msg.UtcExpiryTime != null && msg.UtcExpiryTime < DateTime.UtcNow)
            {
                throw new Exception(string.Format("Msg {0}.{1} from {2} has been expired", msg.DeclaringType, msg.MethodName, msg.ResponseAddress));
            }

            var response = new RpcResponse
            {
                RequestId = msg.Id,
            };

            try
            {
                var methodInfo = InternalDependencies.MethodMatcher.Match<T>(msg);
                if (methodInfo == null)
                {
                    throw new Exception(string.Format("Could not find a match member of type {0} for method {1} of {2}", msg.MemberType.ToString(), msg.MethodName, msg.DeclaringType));
                }

                var parameters = methodInfo.GetParameters();

                //NOTE: Fix param type due to int32/int64 serialization problem
                foreach (var param in parameters)
                {
                    if (param.ParameterType.IsPrimitive)
                    {
                        msg.Params[param.Name] = msg.Params[param.Name].ConvertToCorrectTypeValue(param.ParameterType);
                    }
                }

                object[] parameterValues = msg.Params.Values.ToArray();
                response.ReturnValue = methodInfo.Invoke(_realInstance, parameterValues);
                var keys = msg.Params.Keys.ToArray();

                for (int i = 0; i < msg.Params.Count; i++)
                {
                    msg.Params[keys[i]] = parameterValues[i];
                }
                response.ChangedParams = msg.Params;

            }
            catch (Exception ex)
            {
                response.Exception = ex;
                _log.LogError(ex, ex.Message);
            }

            return Task.FromResult(response);
        }       
    }
}