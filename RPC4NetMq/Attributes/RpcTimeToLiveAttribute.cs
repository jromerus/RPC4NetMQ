using System;
using System.Diagnostics.CodeAnalysis;

namespace RPC4NetMq.Attributes
{
    /// <summary>
    /// Use this attribute to decorate on method to attach a expiry time on the request
    /// </summary>
    [ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Method)]
    public class RpcTimeToLiveAttribute : Attribute
    {
        public RpcTimeToLiveAttribute(int timeToLiveInSeconds)
        {
            Seconds = timeToLiveInSeconds;
        }
        public int Seconds { get; set; }
    }
}