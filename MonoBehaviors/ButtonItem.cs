using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using static ScrapButton.ExtensionMethod.PlayerControllerExtension;

namespace ScrapButton.MonoBehaviors{
    internal class ButtonItem : PhysicsProp {
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
        public PlayerControllerB playerLastHeldBy;
        public AudioClip beamUpAudio;
        public AudioClip beamUpAudio2;
        public AudioClip tpSFX;
        public AudioClip inverseSFX;
        //public AudioClip evacNotice;
        private static System.Random tpSeed;

        public override void Start()
        {
            base.Start();
            random = new System.Random(StartOfRound.Instance.randomMapSeed);
            bomb = (int)(Plugin.Instance.bombChance.Value * 10000);
            flood = (int)(Plugin.Instance.floodChance.Value * 10000);
            //flood = 0;
            light = GetComponentInChildren<Light>();
            var child = gameObject.transform.GetChild(0).GetChild(4);
            innerRenderer = child.GetComponent<MeshRenderer>();
            defMat = innerRenderer.GetMaterial();

            mainAudio = gameObject.GetComponent<AudioSource>();

        }



        public static void NewSeed() {
            tpSeed = new System.Random(StartOfRound.Instance.randomMapSeed + 17 + (int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        }



        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);

            NetworkManager networkManager = base.NetworkManager;
            if (networkManager == null || !networkManager.IsListening) {
                return;
            }
            if (playerHeldBy == null) {
                return;
            }
            currentUseCooldown = 3.5f;
            var playerId = playerHeldBy.playerClientId;
            mainAudio.PlayOneShot(pressed);
            if (StartOfRound.Instance.inShipPhase) {
                return;
            }
            if (base.IsHost || base.IsServer) {
                ExecuteButtonPressOnServer(playerId);
            } else {
                RequestButtonPressServerRpc();
            }
        }
        public override void Update()
        {
            base.Update();
            if (playerHeldBy != null) {
                playerLastHeldBy = playerHeldBy;
            }
            if (boomTimer > 0f) {
                boomTimer -= Time.deltaTime;
                bool lit = Math.Sin(Math.PI * (boomTimer + 0.1) / 0.2) > 0;
                if (lit != lastLit) {
                    if (lit) {
                        innerRenderer.SetMaterial(redGlow);
                        light.color = Color.red;
                        light.enabled = true;
                    } else {
                        innerRenderer.SetMaterial(defMat);
                        light.enabled = false;
                    }
                }
                lastLit = lit;
            } else if (exploding) {
                Detonate();
                if (this.IsHost || this.IsServer)
                {
                    Destroy(gameObject);
                } else
                {
                    DestroyThisServerRpc();
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void DestroyThisServerRpc()
        {
            if (gameObject != null) { 
            Destroy(gameObject);
            } 
        }

        public override void OnDestroy(){
            StopAllCoroutines();
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
            ExecuteTeleportClientRpc(Plugin.Instance.leaveScrap.Value, id);
        }
        public void ExecuteBombTriggerOnServer(){
            boomTimer = 1.7f;
            exploding = true;
            StartBlinkingClientRpc();
        }
        public void Detonate(){
            mainAudio.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
		    mainAudio.PlayOneShot(detonate, 1f);
		    Landmine.SpawnExplosion(base.transform.position + UnityEngine.Vector3.up, true, 1f, 4.4f);
        }
        public void ExecuteFloodOnServer(){
            if(IndoorFlood.active){ return; }
            if(IndoorFlood.CheckBounds()){
                StartFloodClientRpc();
            } else {
                ExecuteErrorOnServer();
            }
        }
        public void ExecuteErrorOnServer(){
            Plugin.Instance.logger.LogError(string.Format("Flood function can't find the level bounds on: ${0}. If this is a custom map, please fill out a bug report with a link to the map. For now, get a prize.", RoundManager.Instance.currentLevel.name));
            PlayPrizeSoundClientRpc();
            var bar = Instantiate(GameObject.Find("GoldBar"), transform.position, Quaternion.identity);
            bar.GetComponent<PhysicsProp>().SetScrapValue(210);
            bar.GetComponent<NetworkObject>().Spawn();
            Destroy(gameObject);
        }
        [ClientRpc]
        public void PlayPrizeSoundClientRpc(){
            mainAudio.PlayOneShot(FindObjectOfType<GiftBoxItem>().openGiftAudio);
        }

        [ClientRpc]
        public void StartFloodClientRpc(){
            innerRenderer.SetMaterial(blueGlow);
            light.color = Color.cyan;
            light.enabled = true;
            var iFlood = Instantiate(Plugin.Instance.indoorFlooding);
            iFlood.SetActive(true);
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
        public void ExecuteTeleportClientRpc(bool dropScrap, ulong id){
            if(StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(transform.position)){
                StartCoroutine(BeamToFactory(dropScrap, id));
            } else {
                StartCoroutine(BeamToShip(dropScrap, id));
            }

        }

        public IEnumerator BeamToFactory(bool dropScrap, ulong id){
            var playerToTp = playerHeldBy;
            if(playerHeldBy == null){
                playerToTp = StartOfRound.Instance.allPlayerObjects[(int)id].GetComponent<PlayerControllerB>();
            }
            playerToTp.beamOutBuildupParticle.Play();
            playerToTp.movementAudio.PlayOneShot(beamUpAudio);
            mainAudio.PlayOneShot(inverseSFX, 0.6f);

            yield return new WaitForSeconds(5f);
            if(playerToTp != null){
                Vector3 vector = RoundManager.Instance.insideAINodes[tpSeed.Next(0, RoundManager.Instance.insideAINodes.Length)].transform.position;
                vector = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(vector, 10f, default, tpSeed, -1);
                if(playerToTp.deadBody != null){
                    DeadBodyInfo deadBody = playerToTp.deadBody;
                    if (deadBody != null)
		{
			deadBody.attachedTo = null;
			deadBody.attachedLimb = null;
			deadBody.secondaryAttachedLimb = null;
			deadBody.secondaryAttachedTo = null;
			if (deadBody.grabBodyObject != null && deadBody.grabBodyObject.isHeld && deadBody.grabBodyObject.playerHeldBy != null)
			{
				deadBody.grabBodyObject.playerHeldBy.DropAllHeldItems(true, false);
			}
			deadBody.isInShip = false;
			deadBody.parentedToShip = false;
			deadBody.transform.SetParent(null, true);
			deadBody.SetRagdollPositionSafely(vector, true);
		}
                } else {
                    if(dropScrap){
                        playerToTp.DropAllHeldItemsExcept(this);
                    }
                    playerToTp.isInElevator = false;
                    playerToTp.isInHangarShipRoom = false;
                    playerToTp.isInsideFactory = true;
                    playerToTp.averageVelocity = 0f;
                    playerToTp.velocityLastFrame = Vector3.zero;
                    playerToTp.TeleportPlayer(vector, false, 0f, false, true);
                    playerToTp.beamOutParticle.Play();
                    playerToTp.movementAudio.PlayOneShot(beamUpAudio2);
                    if(playerToTp == GameNetworkManager.Instance.localPlayerController){
                        HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
                    }
                }
            }
        }

        public IEnumerator BeamToShip(bool dropScrap, ulong id){
            var playerToTp = playerHeldBy;
            if(playerHeldBy == null){
                playerToTp = StartOfRound.Instance.allPlayerObjects[(int)id].GetComponent<PlayerControllerB>();
            }      
            playerToTp.beamUpParticle.Play();
            playerToTp.movementAudio.PlayOneShot(beamUpAudio);
            mainAudio.PlayOneShot(tpSFX, 0.6f);
            ShipTeleporter[] tpers = FindObjectsOfType<ShipTeleporter>();
            foreach(ShipTeleporter tp in tpers){
                if(!tp.isInverseTeleporter){
                    tp.shipTeleporterAudio.PlayOneShot(tp.teleporterSpinSFX);
                    tp.teleporterAnimator.SetTrigger("useTeleporter");
                    tp.cooldownTime = tp.cooldownAmount;
                    tp.buttonTrigger.interactable = false;
                    break;
                }
            }
            yield return new WaitForSeconds(3f);
            UnityEngine.Vector3 shipPos;
            ShipTeleporter tpOptional = null;
            ShipTeleporter[] tpOptionals = FindObjectsOfType<ShipTeleporter>();
            foreach(ShipTeleporter tp in tpOptionals){
                if(!tp.isInverseTeleporter){
                    tpOptional = tp;
                }
            }
            if(tpOptional != null){
                shipPos = tpOptional.teleporterPosition.position;
            } else {
                shipPos = StartOfRound.Instance.middleOfShipNode.position;
            }
            if(playerToTp != null){
                if (playerToTp.deadBody != null)
		{
			if (playerToTp.deadBody.grabBodyObject == null || !playerToTp.deadBody.grabBodyObject.isHeldByEnemy)
			{
				playerToTp.deadBody.attachedTo = null;
				playerToTp.deadBody.attachedLimb = null;
				playerToTp.deadBody.secondaryAttachedLimb = null;
				playerToTp.deadBody.secondaryAttachedTo = null;
				playerToTp.deadBody.SetRagdollPositionSafely(shipPos, true);
				playerToTp.deadBody.transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
				if (playerToTp.deadBody.grabBodyObject != null && playerToTp.deadBody.grabBodyObject.isHeld && playerToTp.deadBody.grabBodyObject.playerHeldBy != null)
				{
					playerToTp.deadBody.grabBodyObject.playerHeldBy.DropAllHeldItems(true, false);
				}
			}
		} else {
                if(dropScrap){
                    playerToTp.DropAllHeldItemsExcept(this);
                }
                if (FindObjectOfType<AudioReverbPresets>()){
				    FindObjectOfType<AudioReverbPresets>().audioPresets[3].ChangeAudioReverbForPlayer(playerToTp);
			    }
                playerToTp.isInElevator = true;
                playerToTp.isInHangarShipRoom = true;
                playerToTp.isInsideFactory = false;
                playerToTp.averageVelocity = 0f;
                playerToTp.velocityLastFrame = UnityEngine.Vector3.zero;
                playerToTp.TeleportPlayer(shipPos,true, 160f, false, true);
                playerToTp.movementAudio.PlayOneShot(beamUpAudio2);
                if(GameNetworkManager.Instance.localPlayerController.isInHangarShipRoom){
                    HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
                }
                playerToTp.beamUpParticle.Stop();
        }
            }
        }
    }
}