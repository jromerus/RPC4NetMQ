using RPC4NetMq.MessengingTypes;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace RPC4NetMq
{
    /// <summary>
    /// Implement this interface to find a method info from the request which will be used to invoke a method on real instance of the RpcServer
    /// </summary>
    public interface IMethodMatcher
    {
        MethodInfo Match<T>(RpcRequest request) where T : class;
        string GetMethodSignature(MethodInfo methodInfo);
    }

    internal class MethodMatcher : IMethodMatcher
    {
        public MethodInfo Match<T>(RpcRequest request) where T : class
        {
            var type = typeof(T);            
            var method = MatchMethodSignature(request.MethodSignature, type);

            if (method != null) return method;
            else
            {   // If still here, search in implemented interfaces, this is the case of an interface that declares other interfaces
                // public interface IInterface1 : IInterface2, IInterface3
                foreach (var inter in type.GetTypeInfo().ImplementedInterfaces)
                {
                    method = MatchMethodSignature(request.MethodSignature, inter);
                    if (method != null) return method;
                }
                return null;
            }
        }

        private MethodInfo MatchMethodSignature(string methodSignature, System.Type type)
        {
            var methods = type.GetMethods();

            foreach (var method in methods)
            {
                if (methodSignature == GetMethodSignature(method))
                {
                    return method;
                }
            }
            return null;
        }

        internal static ConcurrentDictionary<MethodInfo, string> SignaturesCaches = new ConcurrentDictionary<MethodInfo, string>();
        public string GetMethodSignature(MethodInfo methodInfo)
        {
            if (!SignaturesCaches.ContainsKey(methodInfo))
            {
                var sig = new StringBuilder();
                sig.Append(methodInfo.DeclaringType != null ? methodInfo.DeclaringType.FullName : "");
                sig.Append(methodInfo.Name);
                var @params = methodInfo.GetParameters();
                foreach (var param in @params)
                {
                    sig.Append(param.ParameterType.FullName);
                    sig.Append(param.Name);
                }

                byte[] toEncodeAsBytes = Encoding.ASCII.GetBytes(sig.ToString());
                SignaturesCaches[methodInfo] = System.Convert.ToBase64String(toEncodeAsBytes);
            }
            return SignaturesCaches[methodInfo];
        }
    }
}