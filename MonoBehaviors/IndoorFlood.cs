using System.Collections;
using ScrapButton.ExtensionMethod;
using UnityEngine;

namespace ScrapButton.MonoBehaviors{

    

    public class IndoorFlood : MonoBehaviour{

        public AudioSource waterAudio;
        public AudioClip burstSFX;
        public AudioClip waterRush;
        public AudioClip warningSFX;
        public float entranceY;
        public float factoryBoundsY;
        public float surfaceBoundsY;
        public float floodLevel;
        public float age;
        public static bool active = false;
        public GameObject extraCollider;
        public void Awake(){
            transform.position = new Vector3(0f,0f, 0f);
            age = 0f;
            extraCollider = Instantiate(new GameObject("Collider"),transform.position, Quaternion.identity);
            extraCollider.SetActive(false);
            //var script = extraCollider.AddComponent<QuicksandTrigger>();
            var script = extraCollider.AddComponent<CustomQuicksand>();
            script.audioClipIndex = 1;
            script.isWater = true;
            script.movementHinderance = 0.6f;
            script.sinkingSpeedMultiplier = 0.08f;
            var box = extraCollider.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var body = extraCollider.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            extraCollider.layer = LayerMask.NameToLayer("Triggers");
        }

        public static bool CheckBounds(){
            float[] bounds = FindBounds();
            float one = bounds[0];
            float two = bounds[1];
            if(float.IsNaN(one) || float.IsNaN(two) || one == two){
                return false;
            }
            return true;
        }

        public void OnEnable(){
            float[] bounds = FindBounds();
            factoryBoundsY = Mathf.Min(bounds);
            surfaceBoundsY = Mathf.Max(bounds);
            transform.position = new Vector3(0f,factoryBoundsY,0f);
            entranceY = RoundManager.FindMainEntrancePosition(false,false).y;
            active = true;
            extraCollider.SetActive(true);
            extraCollider.transform.localScale = transform.GetChild(0).localScale;
        }

        public void Start(){
            StartCoroutine(PlayEffects());
        }

        // this might break with custom maps, testing to be done
        static float[] FindBounds(){
            int i = 0;
            float[] ret = new float[2]{float.NaN, float.NaN};
            var b = FindObjectsOfType<OutOfBoundsTrigger>();
            foreach(var t in b){
                if(t.enabled){
                    ret[i] = t.transform.position.y;
                    i++;
                    if(i > 1){
                        break;
                    }
                }
            }
            return ret;
        }

        public void OnDisable(){
            this.waterAudio.volume = 0f;
            this.floodLevel = 0f;
            transform.position = new Vector3(0f,-50f,0f);
            extraCollider.SetActive(false);
        }
        public void OnDestroy(){
            Destroy(extraCollider);
        }


        public void Update(){
            age += Time.deltaTime;
            if (!GameNetworkManager.Instance.localPlayerController.isInsideFactory)
		    {
			    this.waterAudio.volume = 0f;
			    return;
		    }
            if(age < 45f){
                transform.position = new Vector3(0f, Mathf.Lerp(factoryBoundsY, entranceY, age/45f), 0f);
            } else if(age < 90f){
                transform.position = new Vector3(0f, Mathf.Lerp(entranceY, surfaceBoundsY, (age-45f)/45f), 0f);
            }
            var centerPos = (transform.position.y + factoryBoundsY) / 2f;
            var height = transform.position.y - factoryBoundsY;
            var a = extraCollider.transform.position;
            var b = extraCollider.transform.localScale;
            extraCollider.transform.position = new Vector3(a.x, centerPos, a.z);
            extraCollider.transform.localScale = new Vector3(b.x, height, b.z);

            this.waterAudio.transform.position = new Vector3(GameNetworkManager.Instance.localPlayerController.transform.position.x,base.transform.position.y + 3f, GameNetworkManager.Instance.localPlayerController.transform.position.z);
            if (Physics.Linecast(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, this.waterAudio.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		    {
			    this.waterAudio.volume = Mathf.Lerp(this.waterAudio.volume, 0f, 0.5f * Time.deltaTime);
			    return;
		    }
            this.waterAudio.volume = Mathf.Lerp(this.waterAudio.volume, 1f, 0.5f * Time.deltaTime);
        }

        public IEnumerator PlayEffects(){
            if(GameNetworkManager.Instance.localPlayerController.isInsideFactory){
                GameNetworkManager.Instance.localPlayerController.movementAudio.PlayOneShot(burstSFX, 1f);
            } else {
                GameNetworkManager.Instance.localPlayerController.movementAudio.PlayOneShot(burstSFX, 0.2f);
            }
            yield return new WaitForSeconds(1f);
            if(GameNetworkManager.Instance.localPlayerController.isInsideFactory){
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            } else {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
            }
            yield return new WaitForSeconds(2f);
            HUDManager.Instance.DisplayFloodWarning();
            //SoundManager.Instance.PlaySoundAroundLocalPlayer(warningSFX, 1f);
            if(GameNetworkManager.Instance.localPlayerController.isInsideFactory){
                SoundManager.Instance.ambienceAudio.loop = true;
                SoundManager.Instance.ambienceAudio.clip = waterRush;
                SoundManager.Instance.ambienceAudio.Play();
            }
                
        }
    }

}