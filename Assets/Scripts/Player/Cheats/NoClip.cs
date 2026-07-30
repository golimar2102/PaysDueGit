using UnityEngine;

public class NoClip : MonoBehaviour
{
    public Transform cam;
    public float speed = 10f;
    public float fastSpeed = 25f;

    private bool isNoClip = false;
    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Переключение на N
        if (Input.GetKeyDown(KeyCode.V))
        {
            isNoClip = !isNoClip;

            if (controller != null)
                controller.enabled = !isNoClip;
        }

        if (!isNoClip) return;

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : speed;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 forward = cam.forward;
        Vector3 right = cam.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * v + right * h;

        if (Input.GetKey(KeyCode.Space))
            move += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl))
            move += Vector3.down;

        transform.position += move * currentSpeed * Time.deltaTime;
    }
}