using UnityEngine;

public class Ladder : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            PlayerMovement pm = other.GetComponent<PlayerMovement>();
            if (pm == null) pm = other.GetComponentInParent<PlayerMovement>();
            if (pm == null) pm = other.GetComponentInChildren<PlayerMovement>();

            if (pm != null)
            {
                pm.RegisterNearLadder(this);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            PlayerMovement pm = other.GetComponent<PlayerMovement>();
            if (pm == null) pm = other.GetComponentInParent<PlayerMovement>();
            if (pm == null) pm = other.GetComponentInChildren<PlayerMovement>();

            if (pm != null)
            {
                pm.UnregisterNearLadder(this);
            }
        }
    }
}