using BepInEx;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Diagnostics;
using System.IO;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using System;
using MysteryButton.MonoBehaviors;
using MysteryButton.Network;

namespace MysteryButton
{
    
    [BepInPlugin(GUID, "Mystery Button", "1.0.0.0")]
    [BepInProcess("Lethal Company.exe")]
    public class Plugin : BaseUnityPlugin    
    {
        private const string GUID = "gloveman.MysteryButton";
        private AssetBundle assets;
        public Item buttonItem;
        public ConfigEntry<double> bombChance;
        public ConfigEntry<double> floodChance;
        private ConfigEntry<int> spawnRate;
        public ConfigEntry<bool> leaveScrap;
        private readonly Harmony harmony = new Harmony(GUID);
        public ManualLogSource logger;

        public static Plugin Instance;
        public GameObject networkManagerObject;


        private void Awake()
        {
            logger = BepInEx.Logging.Logger.CreateLogSource(GUID);
            logger.LogInfo("MysteryButton is awake! YIPPEEEE");

            if(Instance == null){
                Instance = this;
            }

            bombChance = Config.Bind("General","Bomb_Chance",0.3,"Chance that a button press will explode (out of 1)");
            floodChance = Config.Bind("General","Flood_Chance",0.001,"Chance that a button press will flood the facility (out of 1)");
            spawnRate = Config.Bind("General", "Spawn Rate",15,"How much the device will spawn as scrap. Higher is more common.");
            leaveScrap = Config.Bind("General", "Leave Scrap", false, "Should players drop scrap when teleporting with the device?");

            assets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"assets"));
            buttonItem = assets.LoadAsset<Item>("Assets/AssetBundles/Bundle/ButtonItem.asset");
            var blueGlow = assets.LoadAsset<Material>("Assets/AssetBundles/Bundle/glow2.mat");
            var redGlow = assets.LoadAsset<Material>("Assets/AssetBundles/Bundle/glow.mat");
            var pressed = assets.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/Press.ogg");
            ButtonItem script = buttonItem.spawnPrefab.AddComponent<ButtonItem>();
            script.grabbable = true;
            script.grabbableToEnemies = true;
            script.itemProperties = buttonItem;
            script.redGlow = redGlow;
            script.blueGlow = blueGlow;
            script.mainAudio = buttonItem.spawnPrefab.GetComponent<AudioSource>();
            script.pressed = pressed;
            AudioSource[] sources = buttonItem.spawnPrefab.GetComponentsInChildren<AudioSource>();
            foreach(AudioSource source in sources){
                var mixer = source.outputAudioMixerGroup.audioMixer.FindMatchingGroups(source.outputAudioMixerGroup.name)[0];
                if(mixer != null){
                    source.outputAudioMixerGroup = mixer;
                }
            }

            var sor_awake = AccessTools.Method(typeof(Terminal),"Awake");
            var mAwake = typeof(Plugin).GetMethod("TerminalAwake");

            var sor_update = AccessTools.Method(typeof(StartOfRound),"Update");
            var debug = typeof(Plugin).GetMethod("Debug");

            var gnm_start = AccessTools.Method(typeof(GameNetworkManager),"Start");
            var mGNMStart = typeof(Plugin).GetMethod("NetworkStart");

            harmony.Patch(sor_awake, postfix: new HarmonyMethod(mAwake));
            harmony.Patch(sor_update,prefix: new HarmonyMethod(debug));
            harmony.Patch(gnm_start,new HarmonyMethod(mGNMStart));

            var types = Assembly.GetExecutingAssembly().GetTypes();
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                        if (attributes.Length > 0)
                        {
                             method.Invoke(null, null);
                        }
                    }
                }
        }


        public static void TerminalAwake(){
            var startOfRound = StartOfRound.Instance;
            foreach(SelectableLevel level in startOfRound.levels){
                var buttonSpawnable = new SpawnableItemWithRarity
                {
                    rarity = Instance.spawnRate.Value,
                    spawnableItem = Instance.buttonItem
                };
                if(level.spawnableScrap.TrueForAll(scrap => scrap.spawnableItem != Instance.buttonItem)){
                    level.spawnableScrap.Add(buttonSpawnable);
                }
            }
            
            startOfRound.allItemsList.itemsList.Add(Instance.buttonItem);
        }
        public static void Debug(StartOfRound __instance){
            if (Keyboard.current[Key.F1].wasPressedThisFrame)
            {
                var item = Instantiate(Instance.buttonItem.spawnPrefab,__instance.localPlayerController.gameplayCamera.transform.position,Quaternion.identity);
                item.GetComponent<NetworkObject>().Spawn();
            }
            if (Keyboard.current[Key.F2].wasPressedThisFrame)
            {
                var list = __instance.allItemsList.itemsList;
                foreach(Item item1 in list){
                    if(item1.spawnPrefab is FlashlightItem){
                        var item = Instantiate(item1.spawnPrefab,__instance.localPlayerController.gameplayCamera.transform.position,Quaternion.identity);
                        item.GetComponent<NetworkObject>().Spawn();
                    }
                }
                
            }
        }
        public static void NetworkStart(GameNetworkManager __instance){
            if(!NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(Instance.buttonItem.spawnPrefab)){
                NetworkManager.Singleton.AddNetworkPrefab(Instance.buttonItem.spawnPrefab);
            }
        }
        public static void spawnNetPrefab(StartOfRound __instance){
            if(__instance.IsHost){
                GameObject go = Instantiate(Instance.networkManagerObject);
                go.GetComponent<NetworkObject>().Spawn();
            }
        }
    }
}
