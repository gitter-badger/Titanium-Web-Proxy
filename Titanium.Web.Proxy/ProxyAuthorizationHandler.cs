﻿using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Exceptions;
using Titanium.Web.Proxy.Helpers;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Shared;

namespace Titanium.Web.Proxy
{
    public partial class ProxyServer
    {
        private async Task<bool> CheckAuthorization(HttpResponseWriter clientStreamWriter, SessionEventArgs session)
        {
            if (AuthenticateUserFunc == null)
            {
                return true;
            }

            var httpHeaders = session.WebSession.Request.Headers;

            try
            {
                var header = httpHeaders.GetFirstHeader(KnownHeaders.ProxyAuthorization);
                if (header == null)
                {
                    session.WebSession.Response = await SendAuthentication407Response(clientStreamWriter, "Proxy Authentication Required");
                    return false;
                }

                var headerValueParts = header.Value.Split(ProxyConstants.SpaceSplit);
                if (headerValueParts.Length != 2 || !headerValueParts[0].Equals("basic", StringComparison.CurrentCultureIgnoreCase))
                {
                    //Return not authorized
                    session.WebSession.Response = await SendAuthentication407Response(clientStreamWriter, "Proxy Authentication Invalid");
                    return false;
                }

                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(headerValueParts[1]));
                int colonIndex = decoded.IndexOf(':');
                if (colonIndex == -1)
                {
                    //Return not authorized
                    session.WebSession.Response = await SendAuthentication407Response(clientStreamWriter, "Proxy Authentication Invalid");
                    return false;
                }

                string username = decoded.Substring(0, colonIndex);
                string password = decoded.Substring(colonIndex + 1);
                return await AuthenticateUserFunc(username, password);
            }
            catch (Exception e)
            {
                ExceptionFunc(new ProxyAuthorizationException("Error whilst authorizing request", e, httpHeaders));

                //Return not authorized
                session.WebSession.Response = await SendAuthentication407Response(clientStreamWriter, "Proxy Authentication Invalid");
                return false;
            }
        }

        private async Task<Response> SendAuthentication407Response(HttpResponseWriter clientStreamWriter, string description)
        {
            var response = new Response
            {
                HttpVersion = HttpHeader.Version11,
                StatusCode = (int)HttpStatusCode.ProxyAuthenticationRequired,
                StatusDescription = description
            };

            response.Headers.AddHeader(KnownHeaders.ProxyAuthenticate, $"Basic realm=\"{ProxyRealm}\"");
            response.Headers.AddHeader(KnownHeaders.ProxyConnection, KnownHeaders.ProxyConnectionClose);

            await clientStreamWriter.WriteResponseAsync(response);
            return response;
        }
    }
}
