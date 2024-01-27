using BepInEx;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.IO;
using System;
using ScrapButton.MonoBehaviors;
using GameNetcodeStuff;

namespace ScrapButton
{
    
    [BepInPlugin(GUID, "ScrapButton", "1.0.0.0")]
    [BepInProcess("Lethal Company.exe")]
    public class Plugin : BaseUnityPlugin    
    {
        private const string GUID = "gloveman.ScrapButton";
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

        public GameObject indoorFlooding;
        private AudioClip waterRush;
        private AudioClip burstSFX;
        //private AudioClip warningSFX;
        private static bool flag = false;


        private void Awake()
        {
            logger = BepInEx.Logging.Logger.CreateLogSource(GUID);
            logger.LogInfo("ScrapButton is awake!");

            if(Instance == null){
                Instance = this;
            }

            bombChance = Config.Bind("General","Bomb_Chance",0.3,"Chance that a button press will explode (out of 1)");
            floodChance = Config.Bind("General","Flood_Chance",0.001,"Chance that a button press will flood the facility (out of 1)");
            spawnRate = Config.Bind("General", "Spawn Rate",25,"How much the device will spawn as scrap. Higher is more common.");
            leaveScrap = Config.Bind("General", "Leave Scrap", false, "Should players drop scrap when teleporting with the device?");

            assets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"assets"));
            
            UnpackAssets(assets);

            DoPatches();

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
        public void UnpackAssets(AssetBundle assetBundle){
            buttonItem = assetBundle.LoadAsset<Item>("Assets/AssetBundles/Bundle/ButtonItem.asset");
            var blueGlow = assetBundle.LoadAsset<Material>("Assets/AssetBundles/Bundle/glow2.mat");
            var redGlow = assetBundle.LoadAsset<Material>("Assets/AssetBundles/Bundle/glow.mat");
            var pressed = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/Press.ogg");
            burstSFX = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/Burst2.ogg");
            waterRush = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/WaterRush.ogg");
            var beep = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/MineTrigger.ogg");
            var detonate = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/MineDetonate.ogg");
            var beamUpAudio = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/ShipTeleporterBeamPlayerBody.ogg");
            var beamUpAudio2 = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/ShipTeleporterBeam.ogg");
            var tpSFX = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/ShipTeleporterSpin.ogg");
            var inverseSFX = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/ShipTeleporterSpinInverse.ogg");
            //warningSFX  = assetBundle.LoadAsset<AudioClip>("Assets/AssetBundles/Bundle/Warning.ogg");
            ButtonItem script = buttonItem.spawnPrefab.AddComponent<ButtonItem>();
            script.grabbable = true;
            script.grabbableToEnemies = true;
            script.itemProperties = buttonItem;
            script.redGlow = redGlow;
            script.blueGlow = blueGlow;
            script.mainAudio = buttonItem.spawnPrefab.GetComponent<AudioSource>();
            script.pressed = pressed;
            script.beep = beep;
            script.detonate = detonate;
            script.tpSFX = tpSFX;
            script.inverseSFX = inverseSFX;
            script.beamUpAudio = beamUpAudio;
            script.beamUpAudio2 = beamUpAudio2;
            AudioSource[] sources = buttonItem.spawnPrefab.GetComponentsInChildren<AudioSource>();
            foreach(AudioSource source in sources){
                var mixer = source.outputAudioMixerGroup.audioMixer.FindMatchingGroups(source.outputAudioMixerGroup.name)[0];
                if(mixer != null){
                    source.outputAudioMixerGroup = mixer;
                }
            }
        }

        public void DoPatches(){

            var sor_awake = AccessTools.Method(typeof(Terminal),"Awake");
            var mAwake = typeof(Plugin).GetMethod("TerminalAwake");

            var sor_opendoors = AccessTools.Method(typeof(StartOfRound), "openingDoorsSequence");
            var mOpenDoors = typeof(Plugin).GetMethod("OpenDoors");

            var et_teleportplayer = AccessTools.Method(typeof(EntranceTeleport), "TeleportPlayer");
            var mTeleportPlayer = typeof(Plugin).GetMethod("TeleportPlayer");

            var mPreSink = typeof(Plugin).GetMethod("GrossHack1");
            var mPostSink = typeof(Plugin).GetMethod("GrossHack2");
            var qt_ontriggerstay = AccessTools.Method(typeof(QuicksandTrigger), "OnTriggerStay");

            var sor_endofgame = AccessTools.Method(typeof(StartOfRound), "EndOfGameClientRpc");
            var mEndOfGame = typeof(Plugin).GetMethod("EndOfGame");

            var mPostTeleport = typeof(Plugin).GetMethod("PostTeleport");
            var pcb_teleport = AccessTools.Method(typeof(PlayerControllerB), "TeleportPlayer");

            harmony.Patch(sor_awake, postfix: new HarmonyMethod(mAwake));
            harmony.Patch(sor_opendoors, new HarmonyMethod(mOpenDoors));
            harmony.Patch(et_teleportplayer, new HarmonyMethod(mTeleportPlayer));
            harmony.Patch(qt_ontriggerstay, new HarmonyMethod(mPreSink), new HarmonyMethod(mPostSink));
            harmony.Patch(sor_endofgame, postfix: new HarmonyMethod(mEndOfGame));
            harmony.Patch(pcb_teleport, postfix: new HarmonyMethod(mPostTeleport));
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
                    Instance.logger.LogInfo("Added to scrap to pool.");
                }
            }
            
            startOfRound.allItemsList.itemsList.Add(Instance.buttonItem);
            if(Instance.indoorFlooding == null){
                try{
                    Instance.indoorFlooding = Instantiate(GameObject.Find("Systems/GameSystems/TimeAndWeather/Flooding"));
                    Instance.indoorFlooding.SetActive(false);
                    Destroy(Instance.indoorFlooding.GetComponent<FloodWeather>());
                    var script = Instance.indoorFlooding.AddComponent<IndoorFlood>();
                    script.waterAudio = Instance.indoorFlooding.GetComponentInChildren<AudioSource>();
                    script.burstSFX = Instance.burstSFX;
                    script.waterRush = Instance.waterRush;
                    //script.warningSFX = Instance.warningSFX;
                } catch(Exception e){
                    Instance.logger.LogError("Encountered error grabbing GameObject: " + e.Message);
                    Instance.floodChance.Value = 0;
                }
            }
        }
        public static void OpenDoors(){
            ButtonItem.NewSeed();
        }

        public static bool TeleportPlayer(EntranceTeleport __instance){
            if(!__instance.isEntranceToBuilding){
                if(IndoorFlood.active){
                    SoundManager.Instance.ambienceAudio.loop = false;
                    SoundManager.Instance.ambienceAudio.Stop();
                }
                return true;
            }
            var fs = FindObjectsOfType<IndoorFlood>();
            foreach(var f in fs){
                if(f.enabled && f.transform.position.y > RoundManager.FindMainEntrancePosition().y){
                    HUDManager.Instance.DisplayTip("???", "The entrance appears to be blocked.", false, false, "LC_Tip1");
			        return false;
                } else {
                    if(IndoorFlood.active){
                        SoundManager.Instance.ambienceAudio.loop = true;
                        SoundManager.Instance.ambienceAudio.clip = Instance.waterRush;
                        SoundManager.Instance.ambienceAudio.Play();
                    }
                }
            
            }
            return true;
        }
        public static void GrossHack1(QuicksandTrigger __instance){
            if(__instance.GetComponentInParent<IndoorFlood>() != null){
                GameNetworkManager.Instance.localPlayerController.isInsideFactory = false;
                flag = true;

            }
        }
        public static void GrossHack2(){
            if(flag){
                GameNetworkManager.Instance.localPlayerController.isInsideFactory = true;
            }
        }
        public static void EndOfGame(){
                var fs = FindObjectsOfType<IndoorFlood>();
                foreach(var f in fs){
                    if(f.enabled){
                        Destroy(f.gameObject);
                    }
                }
                IndoorFlood.active = false;
        }

        public static void PostTeleport(){
            if(!IndoorFlood.active){ return; }
            if(GameNetworkManager.Instance.localPlayerController.isInsideFactory){
                SoundManager.Instance.ambienceAudio.loop = true;
                SoundManager.Instance.ambienceAudio.clip = Instance.waterRush;
                SoundManager.Instance.ambienceAudio.Play();
            } else {
                SoundManager.Instance.ambienceAudio.loop = false;
                SoundManager.Instance.ambienceAudio.Stop();
            }
        }
    }
}
