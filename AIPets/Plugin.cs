using System;
using BepInEx;
using Grpc.Core;
using GrpcService;
using HarmonyLib;
using UnityEngine;

namespace AIPets;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static Server _server;
    private static Player _player;
    private static readonly Harmony _harmony = new Harmony("andrewshvv.ValheimMod");

    private void Awake()
    {
        _server = new Server
        {
            Services = { Greeter.BindService(new GreeterService()) },
            Ports = { new ServerPort("0.0.0.0", 50051, ServerCredentials.Insecure) }
        };

        _server.Start();
        Logger.LogInfo($"gRPC server listening on port 50051");

        _harmony.PatchAll();
    }

    private void OnDestroy()
    {
        _server.ShutdownAsync().Wait();
    }

    [HarmonyPatch]
    class Environment
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
            Debug.Log($"Console initialised {__instance is not null}");
        }

        public static void InitEnvironment()
        {
            var dt = (int)DateTime.UtcNow.Subtract(_initTime).TotalSeconds;
            if (dt < InitDelay)
            {
                Debug.Log($"Environment: Wait {InitDelay - dt} seconds to init environment");
                return;
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

            // go to some distance from wolf or move the wolf?

            Debug.Log("Environment initialised");
            _initTime = DateTime.UtcNow;
        }
    }

    // Patch awake to initialise the game instance
    [HarmonyPatch]
    private class Initialiser
    {
        [HarmonyPatch(typeof(Game), "Awake")]
        static void Postfix(Game __instance)
        {
            if (__instance == null)
            {
                return;
            }

            Debug.Log($"Game initialised {__instance != null}");
            // game = __instance;
        }

        // Patch destroy to delete the game instance
        [HarmonyPatch(typeof(Game), "OnDestroy")]
        static void Postfix()
        {
            Debug.Log("Game destroyed");
            // game = null;
        }
    }


    [HarmonyPatch]
    class CustomKeyboard
    {
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.Update))]
        [HarmonyPostfix]
        static void InitGlobalKeyBoardListener(ZInput __instance)
        {
            if (ZInput.GetButton("InitEnvironment"))
            {
                Environment.InitEnvironment();
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
            Debug.Log($"Buttons initialised {__instance is not null}");
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

            Debug.Log($"Player initialised {__instance != null}");
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

            Debug.Log("==========");
            Debug.Log($"Delta {Time.deltaTime}");
            Debug.Log($"Unity time passed {_timer}");
            Debug.Log($"Real time passed {DateTime.UtcNow.Subtract(_realTimer).TotalSeconds}");

            _timer = 0f;
            _realTimer = DateTime.UtcNow;

            bool showInfo = ZInput.GetButton("GetInfo");
            if (!showInfo) return true;

            Debug.Log("Get info pressed");
            // System.Threading.Thread.Sleep(50);

            Vector3 playerPosition = _player.transform.position;
            Vector3 playerMoveDir = _player.GetComponent<Character>().GetMoveDir();

            Vector3 wolfPosition = _wolf.transform.position;
            Vector3 wolfMoveDir = _wolf.GetComponent<Character>().GetMoveDir();

            Debug.Log($"Player position {playerPosition}");
            Debug.Log($"Player move dir {playerMoveDir}");
            Debug.Log($"Wolf position {wolfPosition}");
            Debug.Log($"Wolf move dir {wolfMoveDir}");

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
            Debug.Log("Wolf unfollowed, and uninitialized");

            return true;
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.SetFollowTarget))]
        [HarmonyPostfix]
        static void WolfInit(MonsterAI __instance, Character ___m_character, GameObject go)
        {
            var isWolf = ___m_character.m_name == "$enemy_wolf";
            if (!isWolf) return;

            var isTamed = ___m_character.IsTamed();
            if (!isTamed) return;

            if (go is null && _wolf is not null)
            {
                _wolf = null;
                Debug.Log("Wolf unfollowed, and uninitialized");
                return;
            }

            if (go is null) return;
            if (_player is null) return;
            if (_wolf is not null) return;

            bool playerIsFollowTarget = ReferenceEquals(go, _player.gameObject);
            if (!playerIsFollowTarget) return;

            _wolf = __instance;
            Debug.Log($"Wolf following, and initialised {_wolf is not null}");
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