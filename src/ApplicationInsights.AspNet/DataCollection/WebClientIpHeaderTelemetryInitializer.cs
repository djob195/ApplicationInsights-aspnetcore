﻿
namespace Microsoft.ApplicationInsights.AspNet.DataCollection
{
    using Microsoft.ApplicationInsights.Extensibility;
    using System;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.Framework.DependencyInjection;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.AspNet.Http;
    using Microsoft.AspNet.Http.Interfaces;
    using Microsoft.ApplicationInsights.AspNet.Implementation;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Net;

    /// <summary>
    /// This telemetry initializer extracts client IP address and populates telemetry.Context.Location.Ip property.
    /// Lot's of code reuse from Microsoft.ApplicationInsights.Extensibility.Web.TelemetryInitializers.WebClientIpHeaderTelemetryInitializer
    /// </summary>
    public class WebClientIpHeaderTelemetryInitializer : ITelemetryInitializer
    {
        private IServiceProvider serviceProvider;

        private readonly char[] HeaderValuesSeparatorDefault = new char[] { ',' };
        private const string HeaderNameDefault = "X-Forwarded-For";

        private char[] headerValueSeparators;

        private readonly ICollection<string> headerNames;


        public WebClientIpHeaderTelemetryInitializer(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.headerNames = new List<string>();
            this.HeaderNames.Add(HeaderNameDefault);
            this.UseFirstIp = true;
            this.headerValueSeparators = HeaderValuesSeparatorDefault;
        }

        /// <summary>
        /// Gets or sets comma separated list of request header names that is used to check client id.
        /// </summary>
        public ICollection<string> HeaderNames
        {
            get
            {
                return this.headerNames;
            }
        }

        /// <summary>
        /// Gets or sets a header values separator.
        /// </summary>
        public string HeaderValueSeparators
        {
            get
            {
                return string.Concat(this.headerValueSeparators);
            }

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    this.headerValueSeparators = value.ToCharArray();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the first or the last IP should be used from the lists of IPs in the header.
        /// </summary>
        public bool UseFirstIp { get; set; }

        public void Initialize(ITelemetry telemetry)
        {
            var request = this.serviceProvider.GetService<RequestTelemetry>();
            if (!string.IsNullOrEmpty(request.Context.Location.Ip))
            {
                telemetry.Context.Location.Ip = request.Context.Location.Ip;
            }
            else
            {
                var context = this.serviceProvider.GetService<HttpContextHolder>().Context;

                string resultIp = null;
                foreach (var name in this.HeaderNames)
                {
                    var headerValue = context.Request.Headers[name];
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        var ip = GetIpFromHeader(headerValue);
                        ip = CutPort(ip);
                        if (IsCorrectIpAddress(ip))
                        {
                            resultIp = ip;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(resultIp))
                {
                    var connectionFeature = context.GetFeature<IHttpConnectionFeature>();

                    if (connectionFeature != null)
                    {
                        resultIp = connectionFeature.RemoteIpAddress.ToString();
                    }
                }

                request.Context.Location.Ip = resultIp;
                telemetry.Context.Location.Ip = resultIp;
            }
        }

        private static string CutPort(string address)
        {
            // For Web sites in Azure header contains ip address with port e.g. 50.47.87.223:54464
            int portSeparatorIndex = address.IndexOf(":", StringComparison.OrdinalIgnoreCase);

            if (portSeparatorIndex > 0)
            {
                return address.Substring(0, portSeparatorIndex);
            }

            return address;
        }

        private static bool IsCorrectIpAddress(string address)
        {
            IPAddress outParameter;
            address = address.Trim();

            // Core SDK does not support setting Location.Ip to malformed ip address
            if (IPAddress.TryParse(address, out outParameter))
            {
                // Also SDK supports only ipv4!
                if (outParameter.AddressFamily == AddressFamily.InterNetwork)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetIpFromHeader(string clientIpsFromHeader)
        {
            var ips = clientIpsFromHeader.Split(this.headerValueSeparators, StringSplitOptions.RemoveEmptyEntries);
            return this.UseFirstIp ? ips[0].Trim() : ips[ips.Length - 1].Trim();
        }

    }
}