using System;
using System.Diagnostics.CodeAnalysis;

namespace RPC4NetMq.Attributes
{
    /// <summary>
    /// Use this attribute to decorate on a void method which does not have any 'out' parameter to make the asynchronous rpc call 
    /// </summary>
    [ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Method)]
    public class AsyncAttribute : Attribute
    {
    }
}