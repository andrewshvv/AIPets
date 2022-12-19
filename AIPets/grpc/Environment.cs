using System.Threading.Tasks;
using Grpc.Core;

namespace AIPets.grpc
{
    // game update ai works for X frames than stops and changes the variables
    // sets the update ready true
    
    // agent starts, sends the reset env request
    // grpc: receive the request takes the Buffer and sets the reset env variable and waits for this variable being false again
    // 
    // when game is ready and first update ai goes through
    
    
    public class EnvironmentService : Environment.EnvironmentBase
    {
        // private readonly Debug _logger;
        public EnvironmentService()
        {
            // _logger = logger;
        }

        public override Task<StepResponse> Step(StepRequest request, ServerCallContext context)
        {
            return Task.FromResult(new StepResponse
            {

                Done = false,
                Reward = 0,
                State = new State
                {
                    PlayerDirection = new Vector3 { X = 0, Y = 0, Z = 0 },
                    PlayerPosition = new Vector3 { X = 0, Y = 0, Z = 0 },
                    WolfDirection = new Vector3 { X = 0, Y = 0, Z = 0 },
                    WolfPosition = new Vector3 { X = 0, Y = 0, Z = 0 }
                }
            });
        }
        
        public override Task<State> Reset(ResetRequest request, ServerCallContext context)
        {
            return Task.FromResult(new State
            {
                PlayerDirection = new Vector3 { X = 0, Y = 0, Z = 0 },
                PlayerPosition = new Vector3 { X = 0, Y = 0, Z = 0 },
                WolfDirection = new Vector3 { X = 0, Y = 0, Z = 0 },
                WolfPosition = new Vector3 { X = 0, Y = 0, Z = 0 }
            });
        }
    }
}
