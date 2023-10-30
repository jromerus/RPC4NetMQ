using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPC4NetMq.MessengingTypes;
using RPC4NetMq.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel.Channels;
using System.Threading;
using System.Xml.Linq;

namespace RPC4NetMq.Server
{
    public class RpcServerCoordinator<T> : IRpcServerCoordinator where T : class
    {
        private readonly T _realInstance;
        ResponseSocket server;
        ILogger log;
        private CancellationTokenSource cts;
        private string connectionString;

        public RpcServerCoordinator(T realInstance, string connectionStringCommands,  ILogger log)
        {
        	this.log = log;
            cts = new CancellationTokenSource();
            connectionString = connectionStringCommands;
            if (realInstance == null)
            {
                throw new ArgumentNullException("realInstance");
            }
            _realInstance = realInstance;
            server = new ResponseSocket(connectionString);
            
            // fix timeout behind NAT
			server.Options.TcpKeepalive = true;
			server.Options.TcpKeepaliveInterval = new TimeSpan(0, 0, 55);
			server.Options.TcpKeepaliveIdle = new TimeSpan(0, 0, 25);            
        }

        ~RpcServerCoordinator()
        {
            cts.Dispose();
        }        

        private void Init()
        {
        }

        Thread t;        
        public void Start()
        {
            Init();
            //_tunnel.SubscribeAsync<RpcRequest>(_serverId ?? typeof(T).Name, HandleMesage);
            t = new Thread(() => { Run(cts.Token); });            
            t.Start();            
        }
        
        public void Stop() {
        	running = false;
        	try {
                server.Unbind(connectionString);
                server.Close();                
        		server.Dispose();
                cts.Cancel();
                t.Interrupt();
            } catch (Exception ex) {
                log.LogError(ex, ex.Message);
        	}
        }        

        bool running = true;

        void Run(CancellationToken ct) {
            server.ReceiveReady += Server_ReceiveReady;
            
        	while(running) 
            {
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        log.LogDebug("Cancelllation requested");
                        break;
                    }
                    server.Poll(TimeSpan.FromMilliseconds(100));
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message, ex.StackTrace);
                }
                /*
                // Receive the message from the server socket
                string jsonRequest = server.ReceiveFrameString();
                log.LogDebug("Server Rcvd: {0}", jsonRequest);

                RpcRequest rpcRequest = JSON.DeSerializeRequest(jsonRequest);					
                HandleMessage(rpcRequest);
                */
            }
        }

        private void Server_ReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            // Receive the message from the server socket
            try
            {
                string jsonRequest = server.ReceiveFrameString();
                //log.LogDebug("Server Rcvd: {0}", jsonRequest);                

                RpcRequest rpcRequest = JSON.DeSerializeRequest(jsonRequest);
                List<string> paramList = new List<string>();
                foreach (var param in rpcRequest.Params)
                {
                    if (param.Value != null && param.Value.GetType() == typeof(byte[]))
                        paramList.Add(param.Key);
                }

                DebugMessage(paramList, jsonRequest, "Server Rcvd", "Params");
                HandleMessage(rpcRequest);
            } catch (Exception ex)
            {
                log.LogError(ex.Message, ex.StackTrace);
            }
        }

        void DebugMessage(List<string> paramList, string json, string message,string paramsName)
        {
            if (paramList.Count > 0)
            {
                JObject o = JObject.Parse(json);
                var chp = o[paramsName];
                if (chp != null)
                {
                    foreach (string property in paramList)
                    {
                        foreach (JProperty child in chp.Children<JProperty>())
                        {
                            if (child.Name == property) child.Value = "..bytes hidden..";
                        }
                    }
                    log.LogDebug("{0}: {1}", message, o.ToString());
                }
                else log.LogDebug("{0}: {1}", message, o.ToString());
            }
            else log.LogDebug("{0}: {1}", message, json);
        }

        void SendResponse (RpcResponse rpcResponse) {        	
        	string jsonResponse = JsonConvert.SerializeObject(rpcResponse);            
            List<string> paramList = new List<string>();
            foreach(var param in rpcResponse.ChangedParams)
            {
                if (param.Value != null && param.Value.GetType() == typeof(byte[]))
                    paramList.Add(param.Key);
            }
            DebugMessage(paramList, jsonResponse, "Server Send", "ChangedParams");
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