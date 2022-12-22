using System.Threading.Tasks;
using AIPets.unityenv;
using BepInEx.Logging;
using Grpc.Core;

namespace AIPets.grpc
{
    
    public class EnvironmentService : Environment.EnvironmentBase
    {
        private readonly UnityEnv _manager;
        private readonly ManualLogSource _logger;

        public EnvironmentService(UnityEnv manager, ManualLogSource logger)
        {
            _manager = manager;
            _logger = logger;
        }

        public override Task<Feedback> Step(Action action, ServerCallContext context)
        {
            _logger.LogDebug("GRPC: Step");
            if (!_manager.NotifyAction(action)) return null;
            
            Feedback? feedback = _manager.WaitFeedback();
            if (feedback is null)
            {
                _logger.LogDebug("GRPC: Feedback skipped");
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
            _logger.LogDebug("GRPC: Reset");
            if (!_manager.Start())
            {
                _logger.LogDebug("GRPC: Reset skipped, can't reset");
                return null;
            }
            
            Feedback? feedback = _manager.WaitFeedback();
            if (feedback is null)
            {
                _logger.LogDebug("GRPC: Reset skipped, state null");
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
            _logger.LogDebug("GRPC: Eject");
            _manager.Stop();

            return Task.FromResult(new NoneResponse());
        }
    }
}