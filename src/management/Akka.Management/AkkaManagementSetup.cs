﻿// -----------------------------------------------------------------------
//  <copyright file="AkkaManagementSetup.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Akka.Actor.Setup;

namespace Akka.Management
{
    public sealed class AkkaManagementSetup: Setup
    {
        public HttpSetup Http { get; set; }

        internal AkkaManagementSettings Apply(AkkaManagementSettings settings)
        {
            return Http is null ? settings : new AkkaManagementSettings(Http.Apply(settings.Http));
        }
    }

    public sealed class HttpSetup: Setup
    {
        /// <summary>
        /// The hostname where the HTTP Server for Http Cluster Management will be started.
        /// This defines the interface to use.
        /// akka.remote.dot-netty.tcp.public-hostname is used if not overriden or empty.
        /// if akka.remote.dot-netty.tcp.public-hostname is empty, <see cref="Dns.GetHostName"/> is used.
        /// </summary>
        public string Hostname { get; set; }
        
        /// <summary>
        /// The port where the HTTP Server for Http Cluster Management will be bound.
        /// The value will need to be from 0 to 65535.
        /// </summary>
        public int? Port { get; set; }
        
        /// <summary>
        /// Use this setting to bind a network interface to a different hostname or ip
        /// than the HTTP Server for Http Cluster Management.
        /// Use "0.0.0.0" to bind to all interfaces.
        /// </summary>
        public string BindHostname { get; set; }
        
        /// <summary>
        /// Use this setting to bind a network interface to a different port
        /// than the HTTP Server for Http Cluster Management. This may be used
        /// when running akka nodes in a separated networks (under NATs or docker containers).
        /// Use 0 if you want a random available port.
        /// </summary>
        public int? BindPort { get; set; }
        
        /// <summary>
        /// Path prefix for all management routes, usually best to keep the default value here. If
        /// specified, you'll want to use the same value for all nodes that use akka management so
        /// that they can know which path to access each other on.
        /// </summary>
        public string BasePath { get; set; }
        
        /// <summary>
        /// Definition of management route providers which shall contribute routes to the management HTTP endpoint.
        /// Management route providers should be regular extensions that additionally extend the
        /// <see cref="IManagementRouteProvider"/> interface.
        /// 
        /// By default the <see cref="HealthCheckRoutes"/> is enabled.
        /// 
        /// Route providers included by a library (from reference.conf) can be excluded by an application
        /// by using null as type, for example:
        ///
        /// RouteProviders["health-check"] = null; 
        /// </summary>
        public Dictionary<string, Type> RouteProviders { get; set; } = new Dictionary<string, Type>();
        
        /// <summary>
        /// Should Management route providers only expose read only endpoints? It is up to each route provider
        /// to adhere to this property
        /// </summary>
        public bool? RouteProvidersReadOnly { get; set; }

        internal Http Apply(Http settings)
        {
            var routeProviders = RouteProviders != null && RouteProviders.Count > 0
                ? RouteProviders?.Select(kvp => new NamedRouteProvider(kvp.Key, kvp.Value?.AssemblyQualifiedName ?? ""))
                : null;
            
            return settings.Copy(
                hostname: Hostname,
                port: Port,
                effectiveBindHostname: BindHostname,
                effectiveBindPort: BindPort,
                basePath: BasePath,
                routeProviders: routeProviders,
                routeProvidersReadOnly: RouteProvidersReadOnly);
        }
    }
}