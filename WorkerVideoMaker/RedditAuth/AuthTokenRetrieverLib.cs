﻿using Newtonsoft.Json;
using RedditAuth.AuthTokenRetriever.EventArgs;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using uhttpsharp;
using uhttpsharp.Listeners;
using uhttpsharp.RequestProviders;

namespace RedditAuth.AuthTokenRetriever
{
    public class AuthTokenRetrieverLib
    {
        /// <summary>
        /// Your Reddit App ID
        /// </summary>
        internal string AppId
        {
            get;
            private set;
        }

        /// <summary>
        /// Your Reddit App Secret (leave empty for installed apps)
        /// </summary>
        internal string AppSecret
        {
            get;
            private set;
        }

        /// <summary>
        /// The port to listen on for the callback (default: 8080)
        /// </summary>
        internal int Port
        {
            get;
            private set;
        }

        /// <summary>
        /// The address to bind for the callback
        /// </summary>
        internal string Host
        {
            get;
            private set;
        }

        /// <summary>
        /// The Redirect URI specified in your app settings on Reddit (default: https://localhost:(port)/Reddit.NET/oauthRedirect)
        /// </summary>
        internal string RedirectURI
        {
            get;
            private set;
        }

        internal HttpServer HttpServer
        {
            get;
            private set;
        }

        public string AccessToken
        {
            get;
            private set;
        }

        public string RefreshToken
        {
            get;
            private set;
        }

        public event EventHandler<AuthSuccessEventArgs> AuthSuccess;

        /// <summary>
        /// Create a new instance of the Reddit.NET OAuth Token Retriever library.
        /// </summary>
        /// <param name="appId">Your Reddit App ID</param>
        /// <param name="port">The port to listen on for the callback (recommended: 8080)</param>
        /// <param name="host">The host to bind for the callback (default: 127.0.0.1)</param>
        /// <param name="redirectUri">The Redirect URI specified in your app settings on Reddit (default: https://(host):(port)/Reddit.NET/oauthRedirect)</param>
        /// <param name="appSecret">Your Reddit App Secret (leave empty for installed apps)</param>
        public AuthTokenRetrieverLib(string appId, int port, string host = null, string redirectUri = null, string appSecret = null)
        {
            AppId = appId;
            AppSecret = appSecret;
            Port = port;
            Host = host ?? "localhost";
            RedirectURI = redirectUri ?? "http://" + Host + ":" + Port.ToString() + "/Reddit.NET/oauthRedirect";
        }

        public void AwaitCallback()
        {
            using (HttpServer = new HttpServer(new HttpRequestProvider()))
            {
                HttpServer.Use(new TcpListenerAdapter(new TcpListener(IPAddress.Parse(Host.Equals("localhost")
                    ? IPAddress.Loopback.ToString() : Host), Port)));

                HttpServer.Use((context, next) =>
                {
                    string code = null;
                    string state = null;
                    try
                    {
                        code = context.Request.QueryString.GetByName("code");
                        state = context.Request.QueryString.GetByName("state");  // This app formats state as:  AppId + ":" [+ AppSecret]
                    }
                    catch (KeyNotFoundException)
                    {
                        context.Response = new uhttpsharp.HttpResponse(HttpResponseCode.Ok, Encoding.UTF8.GetBytes("<b>ERROR:  No code and/or state received!</b>"), false);
                        throw new Exception("ERROR:  Request received without code and/or state!");
                    }

                    if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(state))
                    {
                        // Send request with code and JSON-decode the return for token retrieval. 
                        RestRequest restRequest = new RestRequest("/api/v1/access_token", Method.POST);

                        restRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(state)));
                        restRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");

                        restRequest.AddParameter("grant_type", "authorization_code");
                        restRequest.AddParameter("code", code);
                        restRequest.AddParameter("redirect_uri",
                            "http://" + Host + ":" + Port.ToString() + "/Reddit.NET/oauthRedirect");  // This must be an EXACT match in the app settings on Reddit! 

                        OAuthToken oAuthToken = JsonConvert.DeserializeObject<OAuthToken>(ExecuteRequest(restRequest));

                        // Set the token properties.
                        AccessToken = oAuthToken.AccessToken;
                        RefreshToken = oAuthToken.RefreshToken;

                        // Fire the auth success event with the token in the event args.
                        AuthSuccess?.Invoke(this, new AuthSuccessEventArgs { AccessToken = oAuthToken.AccessToken, RefreshToken = oAuthToken.RefreshToken });
                        // Generate the success page. 
                        string[] sArr = state.Split(':');
                        if (sArr == null || sArr.Length == 0)
                        {
                            throw new Exception("State must consist of 'appId:appSecret'!");
                        }

                        string appId = sArr[0];
                        string appSecret = (sArr.Length >= 2 ? sArr[1] : null);

                        string html;
                        using (FileStream fs = File.Open("RedditAuth/Templates/Success.html", FileMode.Open, FileAccess.ReadWrite))
                        {
                            using (StreamReader sr = new StreamReader(fs))
                            {
                                html = sr.ReadToEnd();
                            }
                        }

                        html = html.Replace("REDDIT_OAUTH_ACCESS_TOKEN", oAuthToken.AccessToken);
                        html = html.Replace("REDDIT_OAUTH_REFRESH_TOKEN", oAuthToken.RefreshToken);

                        // Send the success page.  
                        context.Response = new uhttpsharp.HttpResponse(HttpResponseCode.Ok, Encoding.UTF8.GetBytes(html), false);
                    }

                    return Task.Factory.GetCompleted();
                });

                HttpServer.Start();
            }
        }

        public void StopListening()
        {
            HttpServer.Dispose();
        }

        public string AuthURL(string scope = "creddits%20modcontributors%20modmail%20modconfig%20subscribe%20structuredstyles%20vote%20wikiedit%20mysubreddits%20submit%20modlog%20modposts%20modflair%20save%20modothers%20read%20privatemessages%20report%20identity%20livemanage%20account%20modtraffic%20wikiread%20edit%20modwiki%20modself%20history%20flair")
        {
            return "https://www.reddit.com/api/v1/authorize?client_id=" + AppId + "&response_type=code"
                + "&state=" + AppId + ":" + AppSecret
                + "&redirect_uri=http://" + Host + ":" + Port.ToString() + "/Reddit.NET/oauthRedirect&duration=permanent"
                + "&scope=" + scope;
        }

        public string ExecuteRequest(RestRequest restRequest)
        {
            IRestResponse res = new RestClient("https://www.reddit.com").Execute(restRequest);
            if (res != null && res.IsSuccessful)
            {
                return res.Content;
            }
            else
            {
                Exception ex = new Exception("API returned non-success response.");

                ex.Data.Add("res", res);

                throw ex;
            }
        }
    }
}
