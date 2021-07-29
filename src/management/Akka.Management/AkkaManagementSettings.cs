using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Akka.Configuration;

namespace Akka.Management
{
    public class AkkaManagementSettings
    {
        public class GrpcSettings
        {
            public GrpcSettings(Config managementConfig)
            {
                var config = managementConfig.GetConfig("grpc");

                var host = 
                
                Hostname = config.GetString("hostname");
                if (string.IsNullOrWhiteSpace(Hostname) || Hostname == "<hostname>")
                    Hostname = Dns.GetHostName();

                Port = config.GetInt("port");
                if (Port > 65535 || Port < 0)
                    throw new ArgumentException($"akka.management.gprc.port must be 0 through 65535 (was {Port})");

                EffectiveBindHostname = config.GetString("bind-hostname");
                if (string.IsNullOrWhiteSpace(EffectiveBindHostname))
                    EffectiveBindHostname = Hostname;

                var bindPort = config.GetString("bind-port");
                if (string.IsNullOrWhiteSpace(bindPort))
                    EffectiveBindPort = Port;
                else
                {
                    EffectiveBindPort = int.Parse(bindPort);
                    if (EffectiveBindPort > 65535 || EffectiveBindPort < 0)
                        throw new ArgumentException($"akka.management.gprc.bind-port must be 0 through 65535 (was {EffectiveBindPort})");
                }

                BasePath = config.GetString("base-path");
                if (string.IsNullOrWhiteSpace(BasePath))
                    BasePath = null;

                bool ValidFqcn(string value)
                    => value != null && value != "null" && !string.IsNullOrWhiteSpace(value);

                RouteProviders = config.GetConfig("routes").AsEnumerable()
                    .Select(kvp => new NamedRouteProvider(kvp.Key, kvp.Value.GetString()))
                    .Where(route => ValidFqcn(route.Fqcn))
                    .ToImmutableList();

            }
            
            public string Hostname { get; }
            public int Port { get; }
            
            public string EffectiveBindHostname { get; }
            public int EffectiveBindPort { get; }
            public string BasePath { get; }
            public ImmutableList<NamedRouteProvider> RouteProviders { get; }
        }

        public AkkaManagementSettings(Config config)
        {
            Grpc = new GrpcSettings(config.GetConfig("akka.management"));
        }
        
        public GrpcSettings Grpc { get; } 
    }

    public class NamedRouteProvider
    {
        public NamedRouteProvider(string name, string fqcn)
        {
            Name = name;
            Fqcn = fqcn;
        }

        public string Name { get; }
        public string Fqcn { get; }
    }
}