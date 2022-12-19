using Grpc.Core;
using System.Threading.Tasks;

namespace GrpcService
{
    public class GreeterService : Greeter.GreeterBase
    {
        // private readonly Debug _logger;
        public GreeterService()
        {
            // _logger = logger;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            // Debug.Log("SayHello started");
                
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }
    }
}
