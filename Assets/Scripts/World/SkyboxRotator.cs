using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    public Transform skyboxModel;
    public float rotationSpeed = 1f;
    public Vector3 rotationAxis = Vector3.up;

    void Start()
    {
        if (skyboxModel == null)
        {
            skyboxModel = transform;
        }
    }

    void Update()
    {
        if (skyboxModel != null)
        {
            skyboxModel.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}