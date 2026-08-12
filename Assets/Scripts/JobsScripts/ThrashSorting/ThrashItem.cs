using UnityEngine;

public class TrashItem : MonoBehaviour
{
    public enum ItemCategory
    {
        Normal = 0,
        SmashTarget = 1,
        DivertTarget = 2
    }

    [Header("Данные предмета")]
    public TrashItemData itemData;
    public ItemCategory category = ItemCategory.Normal;
    public float moveSpeed = 3f;

    [Header("Состояние")]
    public bool isSmashed = false;
    public bool isDiverted = false;

    private TrashSortingController controller;
    private Transform dividerPoint;
    private Transform rightEndPoint;
    private Transform middleEndPoint;

    private enum MovePhase { TowardsDivider, TowardsRightEnd, TowardsMiddleEnd }
    private MovePhase currentPhase = MovePhase.TowardsDivider;

    public void Initialize(
        TrashSortingController sortingController, 
        TrashItemData data, 
        ItemCategory itemCategory, 
        Transform dividerJunction, 
        Transform rightEnd, 
        Transform middleEnd, 
        float speed)
    {
        controller = sortingController;
        itemData = data;
        category = itemCategory;
        dividerPoint = dividerJunction;
        rightEndPoint = rightEnd;
        middleEndPoint = middleEnd;
        moveSpeed = speed;

        // Отключаем лишние компоненты предмета (PickUpItem, подбор, физику)
        PickUpItem pickup = GetComponent<PickUpItem>() ?? GetComponentInChildren<PickUpItem>();
        if (pickup != null)
        {
            pickup.isFloating = false;
            pickup.enabled = false;
        }

        Outline outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (data != null && data.rotationOffset != Vector3.zero)
        {
            transform.rotation *= Quaternion.Euler(data.rotationOffset);
        }

        currentPhase = MovePhase.TowardsDivider;
    }

    void Update()
    {
        if (isSmashed || controller == null || !controller.IsMinigameActive) return;

        switch (currentPhase)
        {
            case MovePhase.TowardsDivider:
                MoveTowardsPoint(dividerPoint != null ? dividerPoint.position : transform.position, () =>
                {
                    // Достигли развилки - проверяем состояние перегородки
                    if (controller.IsDividerOpen)
                    {
                        isDiverted = true;
                        currentPhase = MovePhase.TowardsMiddleEnd;
                    }
                    else
                    {
                        isDiverted = false;
                        currentPhase = MovePhase.TowardsRightEnd;
                    }
                });
                break;

            case MovePhase.TowardsRightEnd:
                MoveTowardsPoint(rightEndPoint != null ? rightEndPoint.position : transform.position, () =>
                {
                    controller.OnTrashItemReachedEnd(this, false);
                    Destroy(gameObject);
                });
                break;

            case MovePhase.TowardsMiddleEnd:
                MoveTowardsPoint(middleEndPoint != null ? middleEndPoint.position : transform.position, () =>
                {
                    controller.OnTrashItemReachedEnd(this, true);
                    Destroy(gameObject);
                });
                break;
        }
    }

    private void MoveTowardsPoint(Vector3 targetPos, System.Action onReached)
    {
        Vector3 newPos = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
        transform.position = newPos;

        if (Vector3.Distance(transform.position, targetPos) < 0.05f)
        {
            onReached?.Invoke();
        }
    }

    public void Smash()
    {
        if (isSmashed) return;
        isSmashed = true;

        if (controller != null)
        {
            controller.OnTrashItemSmashed(this);
        }

        Destroy(gameObject);
    }
}
