using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using RPC4NetMq.MessengingTypes;
using RPC4NetMq.Serialization;
using System;
using System.Linq;
using System.Threading;

namespace RPC4NetMq.Server
{
    public class RpcServerCoordinator<T> : IRpcServerCoordinator where T : class
    {
        private readonly T _realInstance;
        ResponseSocket server;
        ILogger log;
        
        public RpcServerCoordinator(T realInstance, string connectionStringCommands,  ILogger log)
        {
        	this.log = log;
            if (realInstance == null)
            {
                throw new ArgumentNullException("realInstance");
            }
            _realInstance = realInstance;
            server = new ResponseSocket(connectionStringCommands);
            
            // fix timeout behind NAT
			server.Options.TcpKeepalive = true;
			server.Options.TcpKeepaliveInterval = new TimeSpan(0, 0, 55);
			server.Options.TcpKeepaliveIdle = new TimeSpan(0, 0, 25);
        }

        private void Init()
        {
        }

        Thread t;        
        public void Start()
        {
            Init();
            //_tunnel.SubscribeAsync<RpcRequest>(_serverId ?? typeof(T).Name, HandleMesage);
            t = new Thread (Run);
            t.Start();            
        }
        
        public void Stop () {
        	running = false;
        	try {
        		server.Dispose();
                t.Interrupt();        		
        	} catch (Exception ex) {
                log.LogError(ex, ex.Message);
        	}
        }        

        bool running = true;
        void Run () {
        	while (running) {
					// Receive the message from the server socket
					string jsonRequest = server.ReceiveFrameString ();
					log.LogDebug("Server Rcvd: {0}", jsonRequest);

					RpcRequest rpcRequest = JSON.DeSerializeRequest(jsonRequest);					
					HandleMessage(rpcRequest);
				}
        }
        
        void SendResponse (RpcResponse rpcResponse) {        	
        	string jsonResponse = JsonConvert.SerializeObject(rpcResponse);
            log.LogDebug("Server Send: {0}", jsonResponse);
			server.SendFrame(jsonResponse);
        }
                
        public void HandleMessage(RpcRequest msg)
        {
            if (msg.UtcExpiryTime != null && msg.UtcExpiryTime < DateTime.UtcNow)
            {
                // Global.DefaultWatcher.WarnFormat("Msg {0}.{1} from {2} has been expired", msg.DeclaringType, msg.MethodName, msg.ResponseAddress);
                return;
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

          	SendResponse(response);
        }
    }
}