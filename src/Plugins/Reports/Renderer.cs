﻿namespace BGuidinger.Reports
{
    using Base;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Runtime.Serialization.Json;
    using System.Text;

    public class Renderer
    {
        private CookieContainer _cookies = new CookieContainer();

        private readonly string _resource;
        private readonly string _accessToken;

        public Renderer(string resource, string username, string password)
        {
            _resource = resource;

            _accessToken = GetToken(username, password);
        }

        private string GetToken(string username, string password)
        {
            var uri = $"https://login.microsoftonline.com/common/oauth2/token";

            var parameters = new Dictionary<string, string>()
            {
                ["grant_type"] = "password",
                ["client_id"] = "2ad88395-b77d-4561-9441-d0e40824f9bc",
                ["username"] = username,
                ["password"] = password,
                ["resource"] = _resource,
            };
            var request = WebRequest.CreateHttp(uri);
            request.Method = "POST";
            request.Write(parameters);

            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(Token));
                var token = serializer.ReadObject(stream) as Token;

                return token.AccessToken;
            }
        }

        public Tuple<string, string> GetSession(Guid reportId)
        {
            var url = "/CRMReports/RSViewer/ReportViewer.aspx";
            var data = new Dictionary<string, string>()
            {
                ["id"] = reportId.ToString("B"),
                ["iscustomreport"] = "true"
            };

            var response = Encoding.UTF8.GetString(GetResponse(GetRequest("POST", url, data)));

            var sessionId = response.Substring(response.LastIndexOf("ReportSession=") + 14, 24);
            var controlId = response.Substring(response.LastIndexOf("ControlID=") + 10, 32);

            return new Tuple<string, string>(sessionId, controlId);
        }

        public byte[] Render(Guid reportId, string format, string parameters)
        {
            var session = GetSession(reportId);

            var url = "/Reserved.ReportViewerWebControl.axd";
            var data = new Dictionary<string, string>()
            {
                ["OpType"] = "Export",
                ["Format"] = format,
                ["ContentDisposition"] = "AlwaysAttachment",
                ["FileName"] = format == "PDF" ? "report.pdf" : "report.xls",
                ["Culture"] = "1033",
                ["CultureOverrides"] = "True",
                ["UICulture"] = "1033",
                ["UICultureOverrides"] = "True",
                ["ReportSession"] = session.Item1,
                ["ControlID"] = session.Item2
            };

            return GetResponse(GetRequest("GET", $"{url}?{data.UrlEncode()}"));
        }

        private HttpWebRequest GetRequest(string method, string url, Dictionary<string, string> data = null)
        {
            var request = WebRequest.CreateHttp($"{_resource}{url}");
            request.Method = method;
            request.CookieContainer = _cookies;
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");
            request.AutomaticDecompression = DecompressionMethods.GZip;

            if (data != null)
            {
                var body = Encoding.ASCII.GetBytes(data.UrlEncode());

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = body.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }
            }

            return request;
        }

        private byte[] GetResponse(HttpWebRequest request)
        {
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                using (var stream = response.GetResponseStream())
                using (var stream2 = new MemoryStream())
                {
                    stream.CopyTo(stream2);
                    return stream2.ToArray();
                }
            }
        }
    }
}