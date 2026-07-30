using UnityEngine;

public class VendingFallingItem : MonoBehaviour
{
    private VendingMachineController controller;
    private VendingItemSlot targetSlot;
    private bool hasLanded = false;

    public void Init(VendingMachineController vmc, VendingItemSlot slot)
    {
        controller = vmc;
        targetSlot = slot;
    }

    void OnTriggerEnter(Collider other)
    {
        CheckLanding(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        CheckLanding(collision.gameObject);
    }

    private void CheckLanding(GameObject hitObject)
    {
        if (hasLanded) return;

        if (controller != null)
        {
            if (controller.trayDropTrigger != null && (hitObject == controller.trayDropTrigger.gameObject || hitObject.transform.IsChildOf(controller.trayDropTrigger.transform)))
            {
                hasLanded = true;
                controller.OnFallingItemTouchTray(gameObject, targetSlot);
            }
            else if (controller.trayDropTrigger == null)
            {
                hasLanded = true;
                controller.OnFallingItemTouchTray(gameObject, targetSlot);
            }
        }
    }
}