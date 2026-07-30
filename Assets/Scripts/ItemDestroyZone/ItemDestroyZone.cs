using UnityEngine;

public class ItemDestroyZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            return;
        }

        PickUpItem pickup = other.GetComponentInParent<PickUpItem>();
        if (pickup != null)
        {
            Destroy(pickup.gameObject);
            return;
        }

        if (other.attachedRigidbody != null)
        {
            Destroy(other.attachedRigidbody.gameObject);
            return;
        }

        Destroy(other.gameObject);
    }
}