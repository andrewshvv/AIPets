using System;
using System.Collections.Generic;
using AIPets.grpc;
using AIPets.unityenv;
using BepInEx;
using BepInEx.Logging;
using Grpc.Core;
using HarmonyLib;
using UnityEngine;
using Feedback = AIPets.grpc.Feedback;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace AIPets;

// todo: calculate the reward
// - distance between me a wolf, if within R than reward 1

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static Server _server;
    private static Player _player;
    private static UnityEnv _gymEnv;
    private static ManualLogSource _logger;
    private static readonly Harmony Harmony = new("andrewshvv.AIPets");

    private void Awake()
    {
        _logger = Logger;
        _gymEnv = new UnityEnv(0.2f, 200, _logger);
        _gymEnv.OnReset = WolfControl.OnReset;
        _gymEnv.OnIncomingAction = WolfControl.OnIncomingAction;
        _gymEnv.OnFeedbackRequest = WolfControl.OnFeedbackRequest;
        
        _server = new Server
        {
            Services = { grpc.Environment.BindService(new EnvironmentService(_gymEnv, _logger)) },
            Ports = { new ServerPort("0.0.0.0", 50051, ServerCredentials.Insecure) }
        };

        Harmony.PatchAll();
    }


    [HarmonyPatch]
    private class Init
    {
        [HarmonyPatch(typeof(Minimap), "LoadMapData")]
        [HarmonyPostfix]
        static void InitGrpc()
        {
            _server.Start();
            _logger.LogDebug($"gRPC server listening on port 50051");
        }
    }


    private void OnDestroy()
    {
        _gymEnv.Stop();
        _server.ShutdownAsync().Wait();
        _logger.LogDebug($"gRPC stopped");
        Harmony.UnpatchAll();
    }

    [HarmonyPatch]
    private class ValheimEnvironment
    {
        private static Console _console;
        private static DateTime _initTime;
        private const int InitDelay = 3;
        private static bool _debugMode;


        [HarmonyPatch(typeof(Console), nameof(Console.Awake))]
        [HarmonyPostfix]
        static void InitConsole(Console __instance)
        {
            if (__instance is null) return;
            if (_console is not null) return;

            _console = __instance;
            _initTime = DateTime.UtcNow.AddSeconds(-InitDelay);
            _logger.LogDebug($"Console initialised {_console is not null}");
        }

        public static void ResetEnvironment(bool force = false)
        {
            if (!force)
            {
                var dt = (int)DateTime.UtcNow.Subtract(_initTime).TotalSeconds;
                if (dt < InitDelay)
                {
                    _logger.LogDebug($"Environment: Wait {InitDelay - dt} seconds to init environment");
                    return;
                }
            }


            if (!_debugMode)
            {
                _console.TryRunCommand("devcommands");
                _console.TryRunCommand("debugmode");
                _console.TryRunCommand("god");
                _console.TryRunCommand("ghost");
                _debugMode = true;
            }

            _console.TryRunCommand("tod 0.5");
            _console.TryRunCommand("killall");
            _console.TryRunCommand("removedrops");
            _console.TryRunCommand("spawn Wolf");
            _console.TryRunCommand("tame");

            WolfControl._wolf.transform.position =
                _player.transform.position + _player.transform.forward * 4f + Vector3.up;

            List<Character> allCharacters = Character.GetAllCharacters();
            foreach (Character character in allCharacters)
            {
                if (character.IsDead()) continue;
                if (character.m_name != "$enemy_wolf") continue;
                if (!character.IsTamed()) continue;

                ((MonsterAI)character.GetBaseAI()).SetFollowTarget(_player.gameObject);
                _logger.LogDebug($"{character.m_name} set followed");
            }

            // go to some distance from wolf or move the wolf?

            _logger.LogDebug("Environment initialised");
            _initTime = DateTime.UtcNow;
        }
    }

    [HarmonyPatch]
    private class CustomKeyboard
    {
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.Update))]
        [HarmonyPostfix]
        static void InitGlobalKeyBoardListener(ZInput __instance)
        {
            if (ZInput.GetButton("InitEnvironment"))
            {
                ValheimEnvironment.ResetEnvironment();
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.Reset))]
        [HarmonyPostfix]
        static void AddOurButtons(ZInput __instance)
        {
            float repeatDelay = 0.3f;
            float repeatInterval = 0.1f;
            __instance.AddButton("WolfForward", KeyCode.Y, repeatDelay, repeatInterval);
            __instance.AddButton("WolfLeft", KeyCode.G, repeatDelay, repeatInterval);
            __instance.AddButton("WolfBackward", KeyCode.H, repeatDelay, repeatInterval);
            __instance.AddButton("WolfRight", KeyCode.J, repeatDelay, repeatInterval);
            __instance.AddButton("WolfStop", KeyCode.U);
            __instance.AddButton("WolfRun", KeyCode.T);
            __instance.AddButton("GetInfo", KeyCode.O);
            __instance.AddButton("InitEnvironment", KeyCode.L, repeatDelay, repeatInterval);
            _logger.LogDebug($"Buttons initialised {__instance is not null}");
        }
    }

    [HarmonyPatch]
    class WolfControl
    {
        public static MonsterAI _wolf;
        private static Vector3 _wolfDir;


        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        static void InitPlayer(Player __instance)
        {
            // TODO: What if there are two players?
            if (__instance == null) return;

            _logger.LogDebug($"Player initialised {__instance != null}");
            _player = __instance;
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
        public static void UnpachedMoveTowards(object instance, Vector3 dir, bool run)
        {
            // This method is just a stub, the actual code which is executed
            // when this method called is original patched method.
            return;
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.StopMoving))]
        public static void UnpatchedStopMoving(object instance)
        {
            // This method is just a stub, the actual code which is executed
            // when this method called is original patched method.
            return;
        }


        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.StopMoving))]
        [HarmonyPrefix]
        private static bool RemoveValheimControlOverMoveDir(BaseAI __instance)
        {
            if (__instance is null) return true;
            if (_wolf is null) return true;

            bool isOurWolf = ReferenceEquals(__instance.gameObject, _wolf.gameObject);
            if (!isOurWolf) return true;

            // If it is a wolf and is it directed not by patch than stop it
            return false;
        }

        public static bool OnReset()
        {
            _logger.LogDebug("reset handler running");
            ValheimEnvironment.ResetEnvironment(true);
            return true;
        }

        public static bool OnIncomingAction(grpc.Action action)
        {
            _logger.LogDebug("incoming action handler running");
            Vector2 direction2d = utils.ConvertGrpcVec2(action.WolfDirection);
            Vector3 direction3d = Vector3.zero;
            direction3d.x = direction2d.x;
            direction3d.z = direction2d.y; // weird valheim coordinates
            direction3d.y = 0;

            if (direction3d.magnitude != 0)
            {
                _logger.LogDebug("calling original method");
                UnpachedMoveTowards(_wolf, direction3d, false);
                return true;
            }

            UnpatchedStopMoving(_wolf);
            return true;
        }

        public static grpc.Feedback? OnFeedbackRequest()
        {
            _logger.LogDebug("feedback handler running");
            
            grpc.Feedback feedback = new()
            {
                State = new State(),
                Done = _wolf is null || _gymEnv.Iter(),
                Reward = 0
            };

            if (feedback.Done) return feedback;
            
            float dist = Vector3.Distance(_player.transform.position, _wolf.transform.position);
            if (dist < 2)
            {
                feedback.Reward = 1;
                feedback.Done = true;
            }
            if (dist >= 8)
            {
                feedback.Done = true;
                feedback.Reward = -1;
            }

            _logger.LogInfo($"Distance {dist}, Reward {feedback.Reward}");
            feedback.State.PlayerPosition = utils.ConvertUnitVec3(_player.transform.position);
            feedback.State.PlayerDirection = utils.ConvertUnitVec3(_player.GetComponent<Character>().GetMoveDir());
            feedback.State.WolfPosition = utils.ConvertUnitVec3(_wolf.transform.position);
            feedback.State.WolfDirection = utils.ConvertUnitVec3(_wolf.GetComponent<Character>().GetMoveDir());
            return feedback;
        }
        
        [HarmonyPatch(typeof(Minimap), "UpdateDynamicPins")]
        [HarmonyPrefix]
        static bool Step(float dt)
        {            
            _gymEnv.Timestep(dt);
            return true;
        }

        [HarmonyPatch(typeof(BaseAI), "OnDeath")]
        [HarmonyPatch(typeof(BaseAI), "OnDestroy")]
        [HarmonyPrefix]
        static bool OnWolfsDeath(BaseAI __instance)
        {
            if (__instance is null) return true;
            if (_wolf is null) return true;

            bool isOurWolf = ReferenceEquals(__instance.gameObject, _wolf.gameObject);
            if (!isOurWolf) return true;

            _wolf = null;
            _logger.LogDebug("OnWolfsDeath: Wolf unfollowed, and uninitialized");

            return true;
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.SetFollowTarget))]
        [HarmonyPostfix]
        static void WolfInitToggle(MonsterAI __instance, Character ___m_character, GameObject go)
        {
            var isWolf = ___m_character.m_name == "$enemy_wolf";
            if (!isWolf) return;

            var isTamed = ___m_character.IsTamed();
            if (!isTamed) return;

            if (go is null && _wolf is not null)
            {
                _wolf = null;
                _logger.LogDebug("WolfInitToggle: Wolf unfollowed, and uninitialized");
                return;
            }

            if (go is null) return;
            if (_player is null) return;

            bool playerIsFollowTarget = ReferenceEquals(go, _player.gameObject);
            if (!playerIsFollowTarget) return;

            _wolf = __instance;
            _logger.LogDebug($"WolfInitToggle: Wolf following, and initialised {_wolf is not null}");
        }

        static void MoveWithKeyboard()
        {
            Vector3 direction = WolfKeyboardMoveDir();
            if (direction.magnitude != 0)
            {
                _logger.LogDebug("calling original method");
                UnpachedMoveTowards(_wolf, direction, false);
            }
            else
            {
                UnpatchedStopMoving(_wolf);
            }
        }
        
        public static Vector3 WolfKeyboardMoveDir()
        {
            Vector3 direction = Vector3.zero;
            if (ZInput.GetButton("WolfForward"))
            {
                _logger.LogDebug("Forward");
                direction.z += 1f;
            }

            if (ZInput.GetButton("WolfBackward"))
            {
                _logger.LogDebug("Backward");
                direction.z -= 1f;
            }

            if (ZInput.GetButton("WolfLeft"))
            {
                _logger.LogDebug("Left");
                direction.x -= 1f;
            }

            if (ZInput.GetButton("WolfRight"))
            {
                _logger.LogDebug("Right");
                direction.x += 1f;
            }

            if (ZInput.GetButton("WolfStop"))
            {
                _logger.LogDebug("Stop");
                direction.x = 0;
                direction.z = 0;
            }

            if (ZInput.GetButton("WolfRun"))
            {
                _logger.LogDebug("Run");
            }

            direction.Normalize();
            return direction;
        }
    }
}