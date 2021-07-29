using Grpc.Core;

namespace Akka.Management
{
    public interface IGrpcServiceDefinitionProvider
    {
        ServerServiceDefinition GetServiceDefinition();
    }
}