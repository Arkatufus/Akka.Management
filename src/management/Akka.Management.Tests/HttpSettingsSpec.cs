// -----------------------------------------------------------------------
//  <copyright file="HttpSettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Net;
using Akka.Configuration;
using Akka.Remote;
using FluentAssertions;
using Xunit;

namespace Akka.Management.Tests
{
    public class HttpSettingsSpec
    {
        [Fact(DisplayName = "Http settings should contain default values")]
        public void DefaultSettings()
        {
            var settings = new AkkaManagementSettings(
                AkkaManagementProvider.DefaultConfiguration()
                    .WithFallback(ConfigurationFactory.FromResource<RemoteSettings>("Akka.Remote.Configuration.Remote.conf")));

            settings.Http.Hostname.Should().Be(GetHostIp());
            settings.Http.Port.Should().Be(8558);
            settings.Http.EffectiveBindHostname.Should().Be(settings.Http.Hostname);
            settings.Http.EffectiveBindPort.Should().Be(settings.Http.Port);
            settings.Http.BasePath.Should().Be("");
            settings.Http.RouteProvidersReadOnly.Should().BeTrue();
        }
        
        [Fact(DisplayName = "Http settings hostname should default to dot-netty public-hostname")]
        public void DefaultFallbackSettings()
        {
            var settings = new AkkaManagementSettings(
                ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.public-hostname = dotnettyHostname")
                    .WithFallback(AkkaManagementProvider.DefaultConfiguration())
                    .WithFallback(ConfigurationFactory.FromResource<RemoteSettings>("Akka.Remote.Configuration.Remote.conf")));

            settings.Http.Hostname.Should().Be("dotnettyHostname");
        }
        
        [Fact(DisplayName = "Custom Http settings should contain proper values")]
        public void CustomSettings()
        {
            var settings = new AkkaManagementSettings(
                ConfigurationFactory.ParseString(@"
akka.management.http {
    hostname = hostname
    port = 9999
    bind-hostname = bind-hostname
    bind-port = 19999
    base-path = base
    route-providers-read-only = false
}")
                    .WithFallback(AkkaManagementProvider.DefaultConfiguration())
                    .WithFallback(ConfigurationFactory.FromResource<RemoteSettings>("Akka.Remote.Configuration.Remote.conf")));

            settings.Http.Hostname.Should().Be("hostname");
            settings.Http.Port.Should().Be(9999);
            settings.Http.EffectiveBindHostname.Should().Be("bind-hostname");
            settings.Http.EffectiveBindPort.Should().Be(19999);
            settings.Http.BasePath.Should().Be("base");
            settings.Http.RouteProvidersReadOnly.Should().BeFalse();
        }

        private static string GetHostIp()
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());
            return addresses
                .First(i => 
                    !Equals(i, IPAddress.Any) && 
                    !Equals(i, IPAddress.Loopback) && 
                    !Equals(i, IPAddress.IPv6Any) &&
                    !Equals(i, IPAddress.IPv6Loopback))
                .ToString();
        }
    }
}