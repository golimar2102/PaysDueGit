using UnityEngine;
using System.Collections.Generic;

public class ShovelController : MonoBehaviour
{
    [Header("Защита от багов (ВАЖНО!)")]
    [Tooltip("ID лопаты в инвентаре. Скрипт сработает ТОЛЬКО с ней!")]
    public int shovelItemID = 3;

    [Header("Префабы")]
    public GameObject realHolePrefab;
    public GameObject previewHolePrefab;

    [Header("Настройки копания")]
    public float digDistance = 4f;
    public LayerMask groundLayer;
    public float heightOffset = 0.1f;

    [Header("Ограничения и удаление")]
    public float minDistance = 1.2f;
    public Color validColor = new Color(0.2f, 1f, 0.2f, 0.5f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);

    [Header("Камера и Анимация")]
    public Camera mainCam;
    public Animator animator;
    public string digAnimationTrigger = "Dig";
    public AudioSource digSound;

    private GameObject currentPreview;
    private Renderer[] previewRenderers;

    // Оптимизация рендерера (без дублирования материалов в куче)
    private MaterialPropertyBlock propBlock;
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp     = Shader.PropertyToID("_Color");

    // Оптимизация физики (без выделения памяти/GC-аллокаций на OverlapSphere)
    private readonly Collider[] overlapResults = new Collider[10];

    // Хэши параметров аниматора
    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int SpeedHash    = Animator.StringToHash("Speed");

    private const float WalkSpeed = 2.5f;
    private const float RunSpeed  = 8f;

    void Start()
    {
        if (mainCam == null) mainCam = Camera.main;

        propBlock = new MaterialPropertyBlock();

        if (previewHolePrefab != null)
        {
            currentPreview = Instantiate(previewHolePrefab);
            currentPreview.SetActive(false);

            Collider[] colliders = currentPreview.GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders) col.enabled = false;

            previewRenderers = currentPreview.GetComponentsInChildren<Renderer>();
        }
    }

    void Update()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            HidePreview();
            if (animator != null) animator.SetBool(IsAimingHash, false);
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive)
        {
            HidePreview();
            if (animator != null) animator.SetBool(IsAimingHash, false);
            return;
        }

        // --- БРОНЕБОЙНАЯ ЗАЩИТА ---
        bool isHoldingShovel = false;
        if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
        {
            int activeIndex = InventoryManager.Instance.selectedSlotIndex;
            if (activeIndex >= 0 && activeIndex < InventoryManager.Instance.hotbarSlots.Length)
            {
                InventorySlot activeSlot = InventoryManager.Instance.hotbarSlots[activeIndex];

                if (!activeSlot.IsEmpty() && activeSlot.currentItemID == shovelItemID)
                {
                    isHoldingShovel = true;
                }
            }
        }

        if (!isHoldingShovel)
        {
            HidePreview();
            if (animator != null) animator.SetBool(IsAimingHash, false);
            return;
        }

        bool isAiming = Input.GetMouseButton(1);

        if (animator != null)
            animator.SetBool(IsAimingHash, isAiming);

        if (isAiming)
        {
            HandlePreviewAndBuilding();
        }
        else
        {
            HidePreview();

            if (Input.GetMouseButtonDown(0))
                TryRemoveHole();
        }

        HandleMovementAnimation();
    }

    private void HandleMovementAnimation()
    {
        if (animator == null) return;
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        float currentSpeed = 0f;

        if (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f)
            currentSpeed = Input.GetKey(KeyCode.LeftShift) ? RunSpeed : WalkSpeed;

        animator.SetFloat(SpeedHash, currentSpeed);
    }

    private void HandlePreviewAndBuilding()
    {
        if (mainCam == null) return;

        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, digDistance, groundLayer))
        {
            Vector3 spawnPos = hit.point + (Vector3.up * heightOffset);

            if (currentPreview != null)
            {
                currentPreview.SetActive(true);
                currentPreview.transform.position = spawnPos;

                Vector3 playerForward = mainCam.transform.forward;
                playerForward.y = 0f;

                Quaternion slopeRotation;
                if (playerForward.sqrMagnitude > 0.001f)
                    slopeRotation = Quaternion.LookRotation(playerForward, hit.normal);
                else
                    slopeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

                currentPreview.transform.rotation = slopeRotation * previewHolePrefab.transform.rotation;
            }

            bool canBuild = true;
            int numColliders = Physics.OverlapSphereNonAlloc(hit.point, minDistance, overlapResults, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < numColliders; i++)
            {
                Collider col = overlapResults[i];
                if (col.CompareTag("Hole") || col.CompareTag("PlantedHole"))
                {
                    canBuild = false;
                    break;
                }
            }

            // Очищаем ссылки на коллайдеры для избежания утечек памяти
            for (int i = 0; i < numColliders; i++)
            {
                overlapResults[i] = null;
            }

            if (previewRenderers != null)
            {
                Color targetColor = canBuild ? validColor : invalidColor;
                foreach (Renderer r in previewRenderers)
                {
                    if (r != null && r.sharedMaterial != null)
                    {
                        r.GetPropertyBlock(propBlock);
                        if (r.sharedMaterial.HasProperty(BaseColorProp))
                            propBlock.SetColor(BaseColorProp, targetColor);
                        else if (r.sharedMaterial.HasProperty(ColorProp))
                            propBlock.SetColor(ColorProp, targetColor);
                        r.SetPropertyBlock(propBlock);
                    }
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (canBuild) 
                {
                    BuildHole(spawnPos, currentPreview.transform.rotation);
                }
            }
        }
        else
        {
            HidePreview();
        }
    }

    private void TryRemoveHole()
    {
        if (mainCam == null) return;

        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, digDistance))
        {
            if (hit.collider.CompareTag("Hole"))
            {
                Destroy(hit.collider.gameObject);

                if (digSound != null) digSound.Play();
                if (animator != null) animator.SetTrigger(digAnimationTrigger);
            }
        }
    }

    private void BuildHole(Vector3 position, Quaternion rotation)
    {
        if (realHolePrefab != null)
        {
            Instantiate(realHolePrefab, position, rotation);
        }

        if (digSound != null) digSound.Play();
        if (animator != null) animator.SetTrigger(digAnimationTrigger);

        HidePreview();
    }

    private void HidePreview()
    {
        if (currentPreview != null && currentPreview.activeSelf)
            currentPreview.SetActive(false);
    }

    void OnDisable() { HidePreview(); }
    void OnDestroy() { if (currentPreview != null) Destroy(currentPreview); }
}