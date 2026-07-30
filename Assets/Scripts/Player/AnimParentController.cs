using UnityEngine;

public class AnimParentController : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Аниматор, который висит на самом Anim_Parent")]
    public Animator animator;

    [Header("Настройки скоростей (Для Blend Tree)")]
    public float walkSpeedValue = 2.5f;
    public float runSpeedValue = 8f;

    // Хэш параметра — быстрее строки при каждом вызове SetFloat
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            if (animator != null) animator.SetFloat(SpeedHash, 0f);
            return;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float currentSpeed = 0f;

        if (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f)
        {
            currentSpeed = walkSpeedValue;

            if (Input.GetKey(KeyCode.LeftShift))
            {
                currentSpeed = runSpeedValue;
            }
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, currentSpeed);
        }
    }
}