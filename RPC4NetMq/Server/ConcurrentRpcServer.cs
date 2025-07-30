using NetMQ.Sockets;
using NetMQ;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RPC4NetMq.Server;
using RPC4NetMq;
using RPC4NetMq.MessengingTypes;
using System.Linq;
using RPC4NetMq.Serialization;
using Newtonsoft.Json;
using System.Threading;

namespace RPC4NetMQ.Server
{
    public class ConcurrentRpcServer<T> : IDisposable, IRpcServerCoordinator where T : class
    {
        private readonly T _realInstance;
        private readonly RouterSocket _router;
        private readonly BlockingCollection<NetMQMessage> _requestQueue = new BlockingCollection<NetMQMessage>();
        private readonly BlockingCollection<NetMQMessage> _replyQueue = new BlockingCollection<NetMQMessage>();
        private Task _receiverTask;
        private Task[] _workerTasks;
        private Task _senderTask;
        private bool _running = false;
        int workerCount = 4;
        ILogger log;
        private CancellationTokenSource cts;
        string address;        

        public ConcurrentRpcServer(T realInstance, string address, ILogger log)
        {
            _realInstance = realInstance;
            _router = new RouterSocket(address);
            this.log = log;
            this.address = address;
            cts = new CancellationTokenSource();
        }

        public void Start()
        {
            _running = true;
            var token = cts.Token;

            // Recepción de mensajes
            _receiverTask = Task.Run(() =>
            {
                while (_running)
                {
                    try
                    {
                        if (token.IsCancellationRequested)
                        {
                            log.LogDebug("Cancellation requested");
                            Console.WriteLine($"Task {Task.CurrentId} was cancelled before it got started.");
                            token.ThrowIfCancellationRequested();
                        }
                        var message = _router.ReceiveMultipartMessage();
                        _requestQueue.Add(message);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, ex.Message);
                        Console.WriteLine($"[Receiver] Error: {ex.Message}");
                    }
                }
            }, token);

            // Procesamiento en paralelo
            _workerTasks = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                if (token.IsCancellationRequested) break;

                _workerTasks[i] = Task.Run(async () =>
                {
                    foreach (var message in _requestQueue.GetConsumingEnumerable())
                    {
                        if (token.IsCancellationRequested)
                        {
                            log.LogDebug("Cancellation requested");
                            Console.WriteLine($"Task {Task.CurrentId} was cancelled before it got started.");
                            token.ThrowIfCancellationRequested();
                        }

                        try
                        {
                            var response = await HandleRpcAsync(message);
                            if (response != null)
                            {
                                _replyQueue.Add(response);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, ex.Message);
                            Console.WriteLine($"[Worker] Error: {ex.Message}");
                        }
                    }
                });
            }

            // Envío centralizado de respuestas
            _senderTask = Task.Run(() =>
            {
                foreach (var reply in _replyQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        Console.WriteLine($"[Sender] Enviando mensaje a {reply.First.ConvertToString()}");
                        _router.SendMultipartMessage(reply);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, ex.Message);
                        Console.WriteLine($"[Sender] Error: {ex.Message}");
                    }
                }
            });
        }

        // Ejemplo de lógica RPC simulada, adaptable a tu sistema de tipos anónimos
        private async Task<NetMQMessage> HandleRpcAsync(NetMQMessage request)
        {
            var clientId = request.Pop();      // frame 0: identidad cliente
            //request.Pop();                     // frame 1: separador
            var payload = request.Pop().ConvertToString();  // frame 2: datos

            log.LogDebug($"[RPC] Request payload: {payload}");
            Console.WriteLine($"[RPC] Request payload: {payload}");

            RpcRequest rpcRequest = JSON.DeSerializeRequest(payload);
            List<string> paramList = new List<string>();
            foreach (var param in rpcRequest.Params)
            {
                if (param.Value != null && param.Value.GetType() == typeof(byte[]))
                    paramList.Add(param.Key);
            }

            try
            {
                var rpcResponse = await BuildResponse(rpcRequest);
                string jsonResponse = JsonConvert.SerializeObject(rpcResponse);

                var reply = new NetMQMessage();
                reply.Append(clientId);
                //reply.AppendEmptyFrame();
                reply.Append(jsonResponse);
                string trace = $"[Worker] Responding to client ID {clientId.ConvertToString()} with: {jsonResponse}";
                log.LogDebug(trace);
                Console.WriteLine(trace);
                return reply;
            }
            catch { return null; }
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
                log.LogError(ex, ex.Message);
            }

            return Task.FromResult(response);
        }

        ~ConcurrentRpcServer()
        {
            Dispose();
        }

        public void Dispose()
        {
            _running = false;
            _requestQueue.CompleteAdding();
            _replyQueue.CompleteAdding();
            if (_router != null) _router.Dispose();
        }

        public void Stop()
        {
            try
            {
                _running = false;
                if (_router!= null) _router.Close();
                cts.Cancel();               
            }
            catch (Exception ex)
            {
               log.LogError(ex, ex.Message);
            }
        }
    }
}
