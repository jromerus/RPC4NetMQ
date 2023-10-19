using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using RPC4NetMq.Client;
using RPC4NetMq.Server;
using System;
using System.Diagnostics.CodeAnalysis;

namespace RPC4NetMq
{
    public static class RpcFactory
    {        
        /// <summary>
        /// Create an RPC netMQ client
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connectionStringCommands"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        public static T CreateClient<T>(string connectionStringCommands, params IMethodFilter[] filters) where T : class
        {
            return CreateClient<T>(connectionStringCommands, null, filters);
        }       

        public static T CreateClient<T>(string connectionStringCommands, ILogger log, params IMethodFilter[] filters) where T : class
        {
            return CreateClient<T>(new RpcClientInterceptor(connectionStringCommands, log, filters));            
        }

        internal static T CreateClient<T>(RpcClientInterceptor interceptor) where T : class
        {
            var proxy = new ProxyGenerator();
            return proxy.CreateInterfaceProxyWithoutTarget<T>(interceptor);
        }
        
        public static IRpcServerCoordinator CreateServer<T>(T realImplementation, string connectionStringCommands, ILogger log) where T : class
        {
            return new RpcServerCoordinator<T>(realImplementation, connectionStringCommands, log);
        }

        /// <summary>
        /// Change the IMethodMatcher of the library if you wish to but I don't think of any good reason to do that ;) 
        /// </summary>
        /// <param name="methodMatcher"></param>
        [ExcludeFromCodeCoverage]
        public static void RegisterMethodMatcher(IMethodMatcher methodMatcher)
        {
            if (methodMatcher == null)
            {
                throw new ArgumentNullException("methodMatcher");
            }
            InternalDependencies.MethodMatcher = methodMatcher;
        }
    }
}