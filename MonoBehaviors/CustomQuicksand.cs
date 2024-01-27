using GameNetcodeStuff;
using UnityEngine;

namespace ScrapButton.MonoBehaviors{
    public class CustomQuicksand : QuicksandTrigger{

        // I changed almost nothing, yet this one works and the other one doesn't. I'm not touching it anymore.
       new public void OnTriggerStay(Collider other)
	    {
            
		if (this.isWater)
		{
			if (!other.gameObject.CompareTag("Player"))
			{
				return;
			}
			PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
			if (component != GameNetworkManager.Instance.localPlayerController && component != null && component.underwaterCollider != this)
			{
				component.underwaterCollider = base.gameObject.GetComponent<Collider>();
				return;
			}
		}
		if (!this.isWater && !other.gameObject.CompareTag("Player"))
		{
			return;
		}
		PlayerControllerB component2 = other.gameObject.GetComponent<PlayerControllerB>();
		if (component2 != GameNetworkManager.Instance.localPlayerController)
		{
			return;
		}
		if (this.isWater && !component2.isUnderwater)
		{
			component2.underwaterCollider = base.gameObject.GetComponent<Collider>();
			component2.isUnderwater = true;
		}
		component2.statusEffectAudioIndex = this.audioClipIndex;
		if (component2.isSinking)
		{
			return;
		}
		if (this.sinkingLocalPlayer)
		{
			if (!component2.CheckConditionsForSinkingInQuicksand())
			{
				this.StopSinkingLocalPlayer(component2);
			}
			return;
		}
		if (component2.CheckConditionsForSinkingInQuicksand())
		{
			Debug.Log("Set local player to sinking!");
			this.sinkingLocalPlayer = true;
			component2.sourcesCausingSinking++;
			component2.isMovementHindered++;
			component2.hinderedMultiplier *= this.movementHinderance;
			if (this.isWater)
			{
				component2.sinkingSpeedMultiplier = 0f;
				return;
			}
			component2.sinkingSpeedMultiplier = this.sinkingSpeedMultiplier;
		}
	}

	// Token: 0x060003C3 RID: 963 RVA: 0x0002208D File Offset: 0x0002028D
	new public void OnTriggerExit(Collider other)
	{
		this.OnExit(other);
	}

	// Token: 0x060003C4 RID: 964 RVA: 0x00022098 File Offset: 0x00020298
	new public void OnExit(Collider other)
	{
		if (!this.sinkingLocalPlayer)
		{
			if (this.isWater)
			{
				if (!other.CompareTag("Player"))
				{
					return;
				}
				if (other.gameObject.GetComponent<PlayerControllerB>() == GameNetworkManager.Instance.localPlayerController)
				{
					return;
				}
				other.gameObject.GetComponent<PlayerControllerB>().isUnderwater = false;
			}
			return;
		}
		if (!other.CompareTag("Player"))
		{
			return;
		}
		PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
		if (component != GameNetworkManager.Instance.localPlayerController)
		{
			return;
		}
		this.StopSinkingLocalPlayer(component);
	}

	// Token: 0x060003C5 RID: 965 RVA: 0x00022128 File Offset: 0x00020328
	new public void StopSinkingLocalPlayer(PlayerControllerB playerScript)
	{
		if (!this.sinkingLocalPlayer)
		{
			return;
		}
		this.sinkingLocalPlayer = false;
		playerScript.sourcesCausingSinking = Mathf.Clamp(playerScript.sourcesCausingSinking - 1, 0, 100);
		playerScript.isMovementHindered = Mathf.Clamp(playerScript.isMovementHindered - 1, 0, 100);
		playerScript.hinderedMultiplier = Mathf.Clamp(playerScript.hinderedMultiplier / this.movementHinderance, 1f, 100f);
		if (playerScript.isMovementHindered == 0 && this.isWater)
		{
			playerScript.isUnderwater = false;
		}
	}

    }
}
