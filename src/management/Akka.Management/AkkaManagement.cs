using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Grpc.Core;

namespace Akka.Management
{
    public class AkkaManagement : IExtension
    {
        public static Config DefaultConfiguration()
            => ConfigurationFactory.FromResource<AkkaManagement>("Akka.Management.reference.conf");

        public static AkkaManagement Get(ActorSystem system)
            => system.WithExtension<AkkaManagement, AkkaManagementProvider>();

        private readonly ExtendedActorSystem _system;
        private readonly AkkaManagementSettings _settings;
        
        private Server _grpcServer;
        private ILoggingAdapter _log_DoNotUseDirectly;

        private ILoggingAdapter Log
        {
            get
            {
                if (_log_DoNotUseDirectly == null)
                    _log_DoNotUseDirectly = Logging.GetLogger(_system, typeof(AkkaManagement));
                return _log_DoNotUseDirectly;
            }
        }

        public AkkaManagement(ExtendedActorSystem system)
        {
            _system = system;
            _settings = new AkkaManagementSettings(system.Settings.Config);
        }

        public void Start()
        {
            _grpcServer = new Server();
            _grpcServer.Ports.Add(new ServerPort(
                _settings.Grpc.EffectiveBindHostname, 
                _settings.Grpc.EffectiveBindPort, ServerCredentials.Insecure));

            foreach (var provider in _settings.Grpc.RouteProviders)
            {
                var type = Type.GetType(provider.Fqcn);
                if (type == null)
                {
                    Log.Error($"Could not convert FQCN to type. Type: {provider.Fqcn}");
                    continue;
                }
                
                Log.Info("Including gRPC management routes for {0}", Logging.SimpleName(provider));
                var impl = (IGrpcServiceDefinitionProvider)Activator.CreateInstance(type);
                _grpcServer.Services.Add(impl.GetServiceDefinition());
            }
            
            _grpcServer.Start();
        }

        public async Task Stop()
        {
            await _grpcServer.ShutdownAsync();
        }
    }
    
    public class AkkaManagementProvider : ExtensionIdProvider<AkkaManagement>
    {
        public override AkkaManagement CreateExtension(ExtendedActorSystem system)
            => new AkkaManagement(system);
    }
}