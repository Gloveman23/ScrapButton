using System;
using System.Collections;
using GameNetcodeStuff;
using Mono.Cecil.Cil;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UIElements;
using static MysteryButton.ExtensionMethod.PlayerControllerExtension;

namespace MysteryButton.MonoBehaviors{
    internal class ButtonItem : PhysicsProp{
        private System.Random random;
        private int bomb;
        private int flood;
        private float boomTimer = 0f;
        public AudioSource mainAudio;
        public AudioClip beep;
        public AudioClip detonate;
        public AudioClip pressed;
        private Light light;
        private MeshRenderer innerRenderer;
        public Material redGlow;
        public Material blueGlow;
        public Material defMat;
        private bool lastLit = true;
        public bool exploding = false;
        public bool isTeleporting = false;
        public PlayerControllerB playerLastHeldBy;
        public ParticleSystem beamUpParticle;
        public AudioClip beamUpAudio;
        
        public override void Start()
        {
            base.Start();
            random = new System.Random(StartOfRound.Instance.randomMapSeed);
            bomb = (int)(Plugin.Instance.bombChance.Value * 10000);
            flood = (int)(Plugin.Instance.floodChance.Value * 10000);
            light = GetComponentInChildren<Light>();
            var child = gameObject.transform.GetChild(0).GetChild(4);
            innerRenderer = child.GetComponent<MeshRenderer>();
            defMat = innerRenderer.GetMaterial();
            beep = RoundManager.Instance.spawnableMapObjects[0].prefabToSpawn.GetComponentInChildren<Landmine>().mineTrigger;
            detonate = RoundManager.Instance.spawnableMapObjects[0].prefabToSpawn.GetComponentInChildren<Landmine>().mineDetonate;
            mainAudio = gameObject.GetComponent<AudioSource>();
            beamUpParticle = gameObject.GetComponent<ParticleSystem>();
            var beamRef = StartOfRound.Instance.localPlayerController.beamUpParticle;
            var fieldInfo = beamRef.GetType().GetFields();
            foreach(var field in fieldInfo){
                field.SetValue(beamUpParticle, field.GetValue(beamRef));
            }
            var tpRef = StartOfRound.Instance.unlockablesList.unlockables[5].prefabObject.GetComponent<ShipTeleporter>();
            beamUpAudio = tpRef.beamUpPlayerBodySFX;

        }
        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used,buttonDown);
            
            NetworkManager networkManager = base.NetworkManager;
            if(networkManager == null || !networkManager.IsListening){
                return;
            }
            if(playerHeldBy == null){
                return;
            }
            currentUseCooldown = 3f;
            var playerId = playerHeldBy.playerClientId;
            mainAudio.PlayOneShot(pressed);
            if(StartOfRound.Instance.inShipPhase){
                return;
            }
            if(base.IsHost || base.IsServer){
                ExecuteButtonPressOnServer(playerId);
            } else {
                RequestButtonPressServerRpc();
            }
        }
        public override void Update()
        {
            base.Update();
            if(playerHeldBy != null){
                playerLastHeldBy = playerHeldBy;
            }
            if(boomTimer > 0f){
                boomTimer -= Time.deltaTime;
                bool lit = Math.Sin(Math.PI*(boomTimer+0.1)/0.2) > 0;
                if(lit != lastLit){
                    if(lit){
                        innerRenderer.SetMaterial(redGlow);
                        light.color = Color.red;
                        light.enabled = true;
                    } else {
                        innerRenderer.SetMaterial(defMat);
                        light.enabled = false;
                    }
                }
                lastLit = lit;
            } else if(exploding){
                Detonate();
                Destroy(gameObject);
            }
            if(isTeleporting){
                if(playerHeldBy == null){
                    if(playerLastHeldBy != null){
                        if(playerLastHeldBy.beamUpParticle.isPlaying){
                            playerLastHeldBy.beamUpParticle.Stop();
                        }
                        playerLastHeldBy = null;
                    }
                    if(!beamUpParticle.isPlaying){
                        beamUpParticle.Play();
                    }
                } else {
                    if(!playerHeldBy.beamUpParticle.isPlaying){
                        playerHeldBy.beamUpParticle.Play();
                    }
                    if(beamUpParticle.isPlaying){
                        beamUpParticle.Stop();
                    }
                }
            }
        }

        public void ExecuteButtonPressOnServer(ulong id){
            StartCoroutine(DecideFunction(id));
        }

        public IEnumerator DecideFunction(ulong id){
            
            yield return new WaitForSeconds(1f);
            
            var i = random.Next(10000);
        
            if(i < flood){
                ExecuteFloodOnServer();
            } else if(i < bomb + flood){
                ExecuteBombTriggerOnServer();
            } else {
                ExecuteTeleportOnServer(id);
            }
        }

        public void ExecuteTeleportOnServer(ulong id){
            ExecuteTeleportClientRpc(Plugin.Instance.leaveScrap.Value);
        }
        public void ExecuteBombTriggerOnServer(){
            boomTimer = 1.7f;
            exploding = true;
            StartBlinkingClientRpc();
        }
        public void Detonate(){
            mainAudio.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
		    mainAudio.PlayOneShot(detonate, 1f);
		    Landmine.SpawnExplosion(base.transform.position + Vector3.up, true, 1f, 4.4f);
        }
        public void ExecuteFloodOnServer(){

        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestButtonPressServerRpc(ServerRpcParams serverRpcParams = default){
            var id = serverRpcParams.Receive.SenderClientId;
            ExecuteButtonPressOnServer(id);
        }
        [ClientRpc]
        public void StartBlinkingClientRpc(){
            boomTimer = 1.7f;
            exploding = true;
            mainAudio.PlayOneShot(beep);
            innerRenderer.SetMaterial(redGlow);
            light.color = Color.red;
            light.enabled = true;
        }
        [ClientRpc]
        public void ExecuteTeleportClientRpc(bool dropScrap){
            isTeleporting = true;
            StartCoroutine(BeamToShip(dropScrap));


        }

        public IEnumerator BeamToShip(bool dropScrap){
            mainAudio.PlayOneShot(beamUpAudio);
            yield return new WaitForSeconds(3f);
            Vector3 shipPos;
            var tpOptional = FindObjectOfType<ShipTeleporter>();
            if(tpOptional != null){
                shipPos = tpOptional.teleporterPosition.position;
            } else {
                shipPos = StartOfRound.Instance.
            }
            if(playerHeldBy != null){
                if(dropScrap){
                    playerHeldBy.DropAllHeldItemsExcept(this);
                }
                playerHeldBy.TeleportPlayer(shipPos,true, 160f, false, true);
            }
        }

    }
}