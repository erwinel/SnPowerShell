using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Management.Automation;
using System.Xml;

namespace SnWebServices
{
    public class SnRestAPIBase
    {
        public abstract class GetUriParamsBase : ISnUriParams
        {
            HttpMethodType ISnUriParams.Method { get { return HttpMethodType.GET; } }
        }
        
        public abstract class PostUriParamsBase : ISnUriParams
        {
            HttpMethodType ISnUriParams.Method { get { return HttpMethodType.POST; } }
        }
        
        public abstract class PutUriParamsBase : ISnUriParams
        {
            HttpMethodType ISnUriParams.Method { get { return HttpMethodType.PUT; } }
        }
        
        public abstract class PatchUriParamsBase : ISnUriParams
        {
            HttpMethodType ISnUriParams.Method { get { return HttpMethodType.PATCH; } }
        }
        
        public abstract class DeleteUriParamsBase : ISnUriParams
        {
            HttpMethodType ISnUriParams.Method { get { return HttpMethodType.DELETE; } }
        }
    }

    public interface ISnUriParams
    {
        HttpMethodType Method { get; }
    }
    public enum HttpMethodType
    {
        GET,
        POST,
        PUT,
        PATCH,
        DELETE
    }
}