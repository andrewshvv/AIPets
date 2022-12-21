using System.Threading.Tasks;
using BepInEx.Logging;
using Grpc.Core;

namespace AIPets.grpc
{
    // game starts creates grpc and buffered channel
    // game initialises the grpc with buffered channel
    // game works normal, waits for env reset update
    // grpc: env reset request comes
    // grpc: sends the update over buffered channel, waits for response in env buffer 
    // game: 

    // game update ai works for X frames than stops and changes the variables
    // sets the update ready true

    // agent starts, sends the reset env request
    // grpc: receive the request takes the Buffer and sets the reset env variable and waits for this variable being false again
    // 
    // when game is ready and first update ai goes through


    public class EnvironmentService : Environment.EnvironmentBase
    {
        private readonly EnvStream _stream;
        private readonly ManualLogSource _logger;

        public EnvironmentService(EnvStream stream, ManualLogSource logger)
        {
            _stream = stream;
            _logger = logger;
        }

        public override Task<Feedback> Step(Action action, ServerCallContext context)
        {
            _logger.LogInfo("GRPC: Step");
            if (!_stream.SendAction(action)) return null;
            
            Feedback? feedback = _stream.WaitFeedback();
            if (feedback is null)
            {
                _logger.LogInfo("GRPC: Feedback skipped");
                return null;
            }
            
            return Task.FromResult(new Feedback
            {
                Done = (bool)feedback?.Done,
                Reward = (int)feedback?.Reward,
                State = new State
                {
                    PlayerDirection = feedback?.State.PlayerDirection,
                    PlayerPosition = feedback?.State.PlayerPosition,
                    WolfDirection = feedback?.State.WolfDirection,
                    WolfPosition = feedback?.State.WolfPosition
                }
            });
        }

        public override Task<State> Reset(NoneRequest request, ServerCallContext context)
        {
            _logger.LogInfo("GRPC: Reset");
            if (!_stream.Reset())
            {
                _logger.LogInfo("GRPC: Reset skipped, can't reset");
                return null;
            }
            
            Feedback? feedback = _stream.WaitFeedback();
            if (feedback is null)
            {
                _logger.LogInfo("GRPC: Reset skipped, state null");
                return null;
            }
            
            return Task.FromResult(new State
            {
                PlayerDirection = feedback?.State.PlayerDirection,
                PlayerPosition = feedback?.State.PlayerPosition,
                WolfDirection = feedback?.State.WolfDirection,
                WolfPosition = feedback?.State.WolfPosition
            });
        }
        
        public override Task<NoneResponse> Eject(NoneRequest request, ServerCallContext context)
        {
            _logger.LogInfo("GRPC: Eject");
            _stream.Stop();

            return Task.FromResult(new NoneResponse());
        }
    }
}