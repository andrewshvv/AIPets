using System;
using System.Collections.Generic;
using AIPets.grpc;
using BepInEx;
using BepInEx.Logging;
using Grpc.Core;
using HarmonyLib;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace AIPets;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static Server _server;
    private static Player _player;
    private static EnvStream _stream;
    private static ManualLogSource _logger;
    private static readonly Harmony Harmony = new Harmony("andrewshvv.AIPets");

    private void Awake()
    {
        _logger = Logger;
        _stream = new EnvStream();
        _server = new Server
        {
            Services = { grpc.Environment.BindService(new EnvironmentService(_stream, _logger)) },
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
            _logger.LogInfo($"gRPC server listening on port 50051");
        }
    }

    private void OnDestroy()
    {
        _stream.Stop();
        _server.ShutdownAsync().Wait();
        _logger.LogInfo($"gRPC stopped");
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
            _logger.LogInfo($"Console initialised {_console is not null}");
        }

        public static void InitEnvironment(bool force = false)
        {
            if (!force)
            {
                var dt = (int)DateTime.UtcNow.Subtract(_initTime).TotalSeconds;
                if (dt < InitDelay)
                {
                    _logger.LogInfo($"Environment: Wait {InitDelay - dt} seconds to init environment");
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
                
            List<Character> allCharacters = Character.GetAllCharacters();
            foreach (Character character in allCharacters)
            {
                if (character.IsDead()) continue;
                if (character.m_name != "$enemy_wolf") continue;
                if (!character.IsTamed()) continue;

                ((MonsterAI)character.GetBaseAI()).SetFollowTarget(_player.gameObject);
                _logger.LogInfo($"{character.m_name} set followed");
            }
            
            // go to some distance from wolf or move the wolf?
            
            _logger.LogInfo("Environment initialised");
            _initTime = DateTime.UtcNow;
        }
    }

    // Patch awake to initialise the game instance
    // [HarmonyPatch]
    // private class Initialiser
    // {
    //     [HarmonyPatch(typeof(Game), "Awake")]
    //     static void Postfix(Game __instance)
    //     {
    //         if (__instance == null) return;
    //         _logger.LogInfo($"Game initialised {__instance != null}");
    //         // game = __instance;
    //     }
    //
    //     // Patch destroy to delete the game instance
    //     [HarmonyPatch(typeof(Game), "OnDestroy")]
    //     static void Postfix()
    //     {
    //         _logger.LogInfo("Game destroyed");
    //         // game = null;
    //     }
    // }


    [HarmonyPatch]
    private class CustomKeyboard
    {
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.Update))]
        [HarmonyPostfix]
        static void InitGlobalKeyBoardListener(ZInput __instance)
        {
            if (_stream.NeedReset())
            {
                ValheimEnvironment.InitEnvironment(true);
                _stream.Start();
                _stream.IsReseted();
                return;
            }
            
            if (ZInput.GetButton("InitEnvironment"))
            {
                ValheimEnvironment.InitEnvironment();
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
            _logger.LogInfo($"Buttons initialised {__instance is not null}");
        }
    }

    [HarmonyPatch]
    class WolfControl
    {
        private static MonsterAI _wolf;
        private static float _timer = 0.0f;
        private const float _step = 0.2f;

        private static DateTime _realTimer = DateTime.UtcNow;


        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        static void InitPlayer(Player __instance)
        {
            // TODO: What if there are two players?
            if (__instance == null)
            {
                return;
            }

            _logger.LogInfo($"Player initialised {__instance != null}");
            _player = __instance;
        }

        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPrefix]
        static bool Step(MonsterAI __instance, float dt)
        {
            if (_wolf is null) return true;
            if (__instance is null) return true;

            bool isOurWolf = ReferenceEquals(_wolf.gameObject, __instance.gameObject);
            if (!isOurWolf) return true;

            _timer += Time.deltaTime;
            if (_timer < _step) return true;

            _logger.LogInfo("==========");
            _logger.LogInfo($"Delta {Time.deltaTime}");
            _logger.LogInfo($"Unity time passed {_timer}");
            _logger.LogInfo($"Real time passed {DateTime.UtcNow.Subtract(_realTimer).TotalSeconds}");

            _timer = 0f;
            _realTimer = DateTime.UtcNow;

            // Wait for gRPC request
            if (!_stream.IsWorking()) return true;


            // bool showInfo = ZInput.GetButton("GetInfo");
            // if (!showInfo) return true;

            _logger.LogInfo("Get info pressed");
            // System.Threading.Thread.Sleep(50);


            EnvState envState = default;
            envState.Done = false;
            envState.Reward = 0;
            envState.State.PlayerPosition = EnvStream.ConvertVec3(_player.transform.position);
            envState.State.PlayerMoveDir = EnvStream.ConvertVec3(_player.GetComponent<Character>().GetMoveDir());
            envState.State.WolfPosition = EnvStream.ConvertVec3(_wolf.transform.position);
            envState.State.WolfMoveDir = EnvStream.ConvertVec3(_wolf.GetComponent<Character>().GetMoveDir());
            _stream.SendState(envState);

            // _logger.LogInfo($"Player position {_stream.playerPosition}");
            // _logger.LogInfo($"Player move dir {_stream.playerMoveDir}");
            // _logger.LogInfo($"Wolf position {_stream.wolfPosition}");
            // _logger.LogInfo($"Wolf move dir {_stream.wolfMoveDir}");

            // print player position
            // print wolf position
            // current wolf direction
            // current player direction 

            return true;
        }

        [HarmonyPatch(typeof(BaseAI), "OnDeath")]
        [HarmonyPrefix]
        static bool OnWolfsDeath(BaseAI __instance)
        {
            if (__instance is null) return true;
            if (_wolf is null) return true;

            bool isOurWolf = ReferenceEquals(__instance.gameObject, _wolf.gameObject);
            if (!isOurWolf) return true;

            _wolf = null;
            _logger.LogInfo("OnWolfsDeath: Wolf unfollowed, and uninitialized");

            return true;
        }

        // [HarmonyPatch(typeof(MonsterAI), "Awake")]
        // [HarmonyPostfix]
        // static void WolfCreation(MonsterAI __instance, Character ___m_character)
        // {
        //     _logger.LogInfo("Wolf creation enter target");
        //     
        //     if (__instance is null) return;
        //     if (_player?.gameObject is null) return;
        //     if (___m_character is null) return;
        //     
        //     var isWolf = ___m_character.m_name == "$enemy_wolf";
        //     if (!isWolf) return;
        //
        //     var isTamed = ___m_character.IsTamed();
        //     if (!isTamed) return;
        //     
        //     _player.
        //     __instance.SetFollowTarget(_player.gameObject);
        //     _logger.LogInfo("Wolf set follow target");
        // }

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
                _logger.LogInfo("WolfInitToggle: Wolf unfollowed, and uninitialized");
                return;
            }

            if (go is null) return;
            if (_player is null) return;

            bool playerIsFollowTarget = ReferenceEquals(go, _player.gameObject);
            if (!playerIsFollowTarget) return;

            _wolf = __instance;
            _logger.LogInfo($"WolfInitToggle: Wolf following, and initialised {_wolf is not null}");
        }
    }

    /*[HarmonyReversePatch]
    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
    static void OriginalMoveTowards(Vector3 dir, bool run)
    {
        // its a stub so it has no initial content
        throw new System.NotImplementedException("It's a stub");
    }*/
}