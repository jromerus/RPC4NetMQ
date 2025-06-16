using System;
using System.Reflection;
using Newtonsoft.Json;
using RPC4NetMq.MessengingTypes;

namespace RPC4NetMq.Serialization
{

	public static class JSON
	{		
		public static RpcRequest DeSerializeRequest( string json) {
			RpcRequest request =  JsonConvert.DeserializeObject<RpcRequest>(json);
			string typename = request.DeclaringType +"," + request.DeclaringAssembly ; 
			Type dataType = Type.GetType (typename);
			MethodInfo mi = dataType.GetMethod (request.MethodName);
			
			ParameterInfo[] prms = mi.GetParameters();
			foreach (var p in prms) {
				if (request.Params.ContainsKey (p.Name)) {
					Type paramType = p.ParameterType;
					  request.Params[p.Name] = Sanitize (paramType,  request.Params[p.Name]);
				}
			}
			return request;
		}		
		
		public static RpcResponse DeserializeResponse ( RpcRequest request, string json ) {
			RpcResponse response =  JsonConvert.DeserializeObject<RpcResponse>(json);
			
			string typename = request.DeclaringType +"," + request.DeclaringAssembly ; // = "test.ICalculator,test";
			Type dataType = Type.GetType (typename); // request.DeclaringType);
			MethodInfo mi = dataType.GetMethod (request.MethodName);
			Type returnType = mi.ReturnType;
			
			response.ReturnValue = Sanitize (returnType, response.ReturnValue);						
			
			return response;
		}
		
		static object Sanitize (Type type, object o) {
			if (o==null) return null;
			string json = JsonConvert.SerializeObject (o);
			object data =   JsonConvert.DeserializeObject (json, type);
			return data;
		}
				
	}
}
