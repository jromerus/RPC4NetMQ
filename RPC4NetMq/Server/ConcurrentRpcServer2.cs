using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using RPC4NetMq.Server;
using RPC4NetMq.MessengingTypes;
using RPC4NetMq;
using System.Collections.Generic;
using RPC4NetMq.Serialization;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;

namespace RPC4NetMQ.Server
{
    public class ConcurrentRpcServer2<T> : IDisposable, IRpcServerCoordinator where T : class
    {
        private readonly T _realInstance;
        private readonly RouterSocket _router;
        private readonly BlockingCollection<NetMQMessage> _messageQueue = new BlockingCollection<NetMQMessage>();
        private bool _running;
        private Task _receiverTask;
        private Task[] _workerTasks;
        int workerCount = 4;
        ILogger log;
        private CancellationTokenSource cts;
        string address;

        public ConcurrentRpcServer2(T realInstance, string address /*= "tcp://*:9002"*/, ILogger log)
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
            // Tarea que recibe mensajes y los coloca en la cola
            _receiverTask = Task.Run(() =>
            {
                while (_running)
                {
                    if (token.IsCancellationRequested)
                    {
                        Console.WriteLine($"Task {Task.CurrentId} was cancelled before it got started.");
                        token.ThrowIfCancellationRequested();
                    }

                    try
                    {                        
                        var message = _router.ReceiveMultipartMessage();
                        _messageQueue.Add(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Receiver] Error: {ex.Message}");
                    }
                }
            }, token);

            // Tareas que procesan los mensajes
            _workerTasks = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                if (token.IsCancellationRequested) break;

                _workerTasks[i] = Task.Run(() =>
                {                    
                    foreach (var msg in _messageQueue.GetConsumingEnumerable())
                    {
                        if (token.IsCancellationRequested)
                        {
                            Console.WriteLine($"Task {Task.CurrentId} was cancelled before it got started.");
                            token.ThrowIfCancellationRequested();
                        }
                        try
                        {
                            var reply = HandleRpc(msg);
                            lock (_router) // acceso exclusivo al socket
                            {
                                _router.SendMultipartMessage(reply);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Worker] Error: {ex.Message}");
                        }
                    }
                });
            }
        }

        private NetMQMessage HandleRpc(NetMQMessage request)
        {
            var clientId = request.Pop();       // Identity
            request.Pop();                      // Empty frame
            var payload = request.Pop().ConvertToString();

            Console.WriteLine($"Received request: {payload}");

            RpcRequest rpcRequest = JSON.DeSerializeRequest(payload);
            List<string> paramList = new List<string>();
            foreach (var param in rpcRequest.Params)
            {
                if (param.Value != null && param.Value.GetType() == typeof(byte[]))
                    paramList.Add(param.Key);
            }

            string response = $"Echo: {payload}";

            var reply = new NetMQMessage();
            reply.Append(clientId);
            reply.AppendEmptyFrame();

            var rpcResponse = BuildResponse(rpcRequest);
            string jsonResponse = JsonConvert.SerializeObject(rpcResponse);
            List<string> respParamList = new List<string>();
            foreach (var param in rpcResponse.ChangedParams)
            {
                if (param.Value != null && param.Value.GetType() == typeof(byte[]))
                    respParamList.Add(param.Key);
            }
            //DebugMessage(paramList, jsonResponse, "Server Send", "ChangedParams");

            reply.Append(jsonResponse);
            return reply;
        }

        public RpcResponse BuildResponse(RpcRequest msg)
        {
            if (msg.UtcExpiryTime != null && msg.UtcExpiryTime < DateTime.UtcNow)
            {
                // Global.DefaultWatcher.WarnFormat("Msg {0}.{1} from {2} has been expired", msg.DeclaringType, msg.MethodName, msg.ResponseAddress);
                return null;
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

            return response;
            //SendResponse(response);
        }

        ~ConcurrentRpcServer2()
        {
            Dispose();
        }

        public void Dispose()
        {
            _running = false;            
            cts.Dispose();
            _messageQueue.CompleteAdding();
            _router.Dispose();            
        }

        /*
        public void HandleMessage(RpcRequest request)
        {
            throw new NotImplementedException();
        }
        */

        public void Stop()
        {
            try
            {
                _running = false;
                _router.Close();                
                cts.Cancel();
                //_receiverTask.Wait();
                //Task.WaitAny(_receiverTask);
                //Task.WaitAny(_workerTasks);                
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message);
            }
        }
    }
}
