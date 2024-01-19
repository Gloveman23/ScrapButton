using GameNetcodeStuff;
using MysteryButton.MonoBehaviors;
using UnityEngine;

namespace MysteryButton.ExtensionMethod{
    public static class PlayerControllerExtension{
        public static void DropAllHeldItemsExcept(this PlayerControllerB pcb,GrabbableObject except,bool itemsFall = true, bool disconnecting = false)
    {
        for (int i = 0; i < pcb.ItemSlots.Length; i++)
        {
            GrabbableObject grabbableObject = pcb.ItemSlots[i];
            if (!(grabbableObject != null))
            {
                continue;
            }
            if(grabbableObject == except){
                continue;
            }

            if (itemsFall)
            {
                grabbableObject.parentObject = null;
                grabbableObject.heldByPlayerOnServer = false;
                if (pcb.isInElevator)
                {
                    grabbableObject.transform.SetParent(pcb.playersManager.elevatorTransform, worldPositionStays: true);
                }
                else
                {
                    grabbableObject.transform.SetParent(pcb.playersManager.propsContainer, worldPositionStays: true);
                }

                pcb.SetItemInElevator(pcb.isInHangarShipRoom, pcb.isInElevator, grabbableObject);
                grabbableObject.EnablePhysics(enable: true);
                grabbableObject.EnableItemMeshes(enable: true);
                grabbableObject.transform.localScale = grabbableObject.originalScale;
                grabbableObject.isHeld = false;
                grabbableObject.isPocketed = false;
                grabbableObject.startFallingPosition = grabbableObject.transform.parent.InverseTransformPoint(grabbableObject.transform.position);
                grabbableObject.FallToGround(randomizePosition: true);
                grabbableObject.fallTime = UnityEngine.Random.Range(-0.3f, 0.05f);
                if (pcb.IsOwner)
                {
                    grabbableObject.DiscardItemOnClient();
                }
                else if (!grabbableObject.itemProperties.syncDiscardFunction)
                {
                    grabbableObject.playerHeldBy = null;
                }
            }

            if (pcb.IsOwner && !disconnecting)
            {
                ((Behaviour)(object)HUDManager.Instance.holdingTwoHandedItem).enabled = false;
                ((Behaviour)(object)HUDManager.Instance.itemSlotIcons[i]).enabled = false;
                HUDManager.Instance.ClearControlTips();
                pcb.activatingItem = false;
            }

            pcb.ItemSlots[i] = null;
        }

        if (pcb.isHoldingObject)
        {
            pcb.isHoldingObject = false;
            if (pcb.currentlyHeldObjectServer != null)
            {
                pcb.SetSpecialGrabAnimationBool(setTrue: false, pcb.currentlyHeldObjectServer);
            }

            pcb.playerBodyAnimator.SetBool("cancelHolding", value: true);
            pcb.playerBodyAnimator.SetTrigger("Throw");
        }

        pcb.activatingItem = false;
        pcb.twoHanded = false;
        pcb.carryWeight = 1f;
        pcb.currentlyHeldObjectServer = null;
    }



    }
}