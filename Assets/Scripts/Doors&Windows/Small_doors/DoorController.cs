using UnityEngine;
using System.Collections;

public class DoorController : MonoBehaviour
{
    [Header("Статус двери")]
    public bool isOpen = false;
    public bool isLocked = false; 

    [Header("Настройки замка (Визуал)")]
    public GameObject padlockVisual;
    
    [Tooltip("Нужен ли ключ? Если НЕТ (галочка снята), игрок может повесить или снять замок руками по клику.")]
    public bool requiresKey = true;

    [Header("Настройки инвентаря (Замки и Ключи)")]
    [Tooltip("ID замка в инвентаре (чтобы скрипт не ломался от смены языков!)")]
    public int padlockItemID = 29; 
    
    [Tooltip("Префаб замка (PickUpItem), который добавится в инвентарь при снятии")]
    public PickUpItem padlockPrefabToGive;

    [Tooltip("ID ключа в инвентаре (если requiresKey = true)")]
    public int keyItemID = -1;

    [Header("Настройки открывания")]
    [Tooltip("На сколько градусов открывается дверь (обычно 90 или -90)")]
    public float openAngleOffset = 90f; 
    public float swingSpeed = 5f;

    [Header("Настройки направления (открытие ОТ игрока/НПС)")]
    [Tooltip("Автоматически определять ось стороны по размерам меша/коллайдера")]
    public bool autoDetectAxis = true;
    [Tooltip("Использовать локальную ось Y вместо X для определения стороны, с которой находится персонаж")]
    public bool useLocalYForSide = false;
    [Tooltip("Инвертировать направление открытия")]
    public bool invertOpenDirection = false;
    
    private Quaternion closedRotation;
    private Quaternion openRotation;
    
    // Переменные для покачивания замка
    private Quaternion padlockOriginalRot;
    private Coroutine wiggleCoroutine;

    // Переменные для тряски самой двери
    private bool isShaking = false;
    private Coroutine doorShakeCoroutine;

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.AngleAxis(openAngleOffset, Vector3.forward);

        if (autoDetectAxis)
        {
            MeshFilter mf = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                useLocalYForSide = mf.sharedMesh.bounds.size.x > mf.sharedMesh.bounds.size.y;
            }
            else
            {
                BoxCollider box = GetComponent<BoxCollider>() ?? GetComponentInChildren<BoxCollider>();
                if (box != null)
                {
                    useLocalYForSide = box.size.x > box.size.y;
                }
            }
        }

        if (padlockVisual != null)
        {
            padlockOriginalRot = padlockVisual.transform.localRotation;
            padlockVisual.SetActive(isLocked);
        }
    }

    void Update()
    {
        // Если дверь трясется, мы отменяем плавное открывание/закрывание, чтобы не мешать
        if (isShaking) return; 

        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * swingSpeed);
    }

    // === ДЕЙСТВИЕ 1: ПОПЫТКА ОТКРЫТЬ ДВЕРЬ (Кнопка E) ===
    public void TryOpenDoor(Vector3? interactorPosition = null)
    {
        if (isLocked)
        {
            Debug.Log("Дверь заперта");
            if (doorShakeCoroutine != null) StopCoroutine(doorShakeCoroutine);
            // Запускаем двойную тряску (Дверь + Замок)
            doorShakeCoroutine = StartCoroutine(ShakeDoorAndLock());
            return;
        }

        // Обычное открытие/закрытие
        if (!isOpen)
        {
            // Определяем позицию того, кто открывает
            Vector3 pos;
            if (interactorPosition.HasValue)
            {
                pos = interactorPosition.Value;
            }
            else
            {
                // Если позиция не передана, пытаемся найти игрока
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                pos = (player != null) ? player.transform.position : transform.position;
            }

            Vector3 localPos = transform.InverseTransformPoint(pos);
            float side = useLocalYForSide ? localPos.y : localPos.x;
            float sign = (side >= 0f) ? -1f : 1f;
            if (invertOpenDirection) sign = -sign;

            openRotation = closedRotation * Quaternion.AngleAxis(openAngleOffset * sign, Vector3.forward);
        }

        isOpen = !isOpen;
    }

    // === ДЕЙСТВИЕ 2: ВЗАИМОДЕЙСТВИЕ С ЗАМКОМ (Кнопка F) ===
    public void InteractWithLock()
    {
        int itemInHandID = -1;
        InventorySlot activeSlot = null;

        // Проверяем, держит ли игрок что-то в активном слоте хотбара
        if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
        {
            int index = InventoryManager.Instance.selectedSlotIndex;
            if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
            {
                activeSlot = InventoryManager.Instance.hotbarSlots[index];
                if (!activeSlot.IsEmpty()) 
                {
                    itemInHandID = activeSlot.currentItemID; // БЕРЕМ ID ПРЕДМЕТА!
                }
            }
        }

        // А) ПОПЫТКА ПОВЕСИТЬ ЗАМОК (Если дверь не заперта и у нас в руках замок)
        if (!isLocked && itemInHandID != -1)
        {
            if (itemInHandID == padlockItemID) // ПРОВЕРЯЕМ ПО ЦИФРАМ, А НЕ ПО ТЕКСТУ
            {
                // Если дверь была открыта, автоматически ее захлопываем!
                if (isOpen) isOpen = false; 
                
                LockDoor();
                
                // Забираем замок из рук
                if (activeSlot != null)
                {
                    if (activeSlot.itemData != null && activeSlot.itemData.amount > 1)
                    {
                        activeSlot.itemData.amount--;
                        activeSlot.UpdateSlotUI();
                    }
                    else
                    {
                        activeSlot.ClearSlot();
                    }
                    InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
                }
                return;
            }
        }

        // Б) ПОПЫТКА СНЯТЬ ЗАМОК (Если дверь заперта)
        if (isLocked)
        {
            if (requiresKey)
            {
                // Проверяем наличие ключа по ID
                if (itemInHandID != -1 && itemInHandID == keyItemID)
                {
                    Debug.Log("Дверь успешно открыта ключом!");
                    UnlockDoor();
                    GivePadlockToPlayer();
                }
                else
                {
                    Debug.Log("Дверь заперта! Нужен ключ.");
                    if (wiggleCoroutine != null) StopCoroutine(wiggleCoroutine);
                    // Дергаем только замок (как отказ)
                    wiggleCoroutine = StartCoroutine(WigglePadlock());
                }
            }
            else
            {
                // Если ключ не нужен, просто снимаем руками
                Debug.Log("Игрок снял замок голыми руками.");
                UnlockDoor();
                GivePadlockToPlayer();
            }
        }
    }

    private void GivePadlockToPlayer()
    {
        if (padlockPrefabToGive != null && InventoryManager.Instance != null)
        {
            // ИСПРАВЛЕНИЕ: Создаем слепок данных из префаба замка (для новой системы инвентаря)
            InventoryItemData data = new InventoryItemData(padlockPrefabToGive);
            bool added = InventoryManager.Instance.AddItem(data);
            
            // Если инвентарь полон - роняем замок на пол
            if (!added)
            {
                Vector3 spawnPos = padlockVisual != null ? padlockVisual.transform.position : transform.position;
                Instantiate(padlockPrefabToGive.gameObject, spawnPos, Quaternion.identity);
            }
        }
    }

    // Корутина простого покачивания замка (Если тыкаем без ключа/руками по замку)
    private IEnumerator WigglePadlock()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float zRotation = Mathf.Sin(elapsed * 30f) * 15f; 
            padlockVisual.transform.localRotation = padlockOriginalRot * Quaternion.Euler(0f, 0f, zRotation);
            yield return null;
        }

        padlockVisual.transform.localRotation = padlockOriginalRot;
    }

    // Двойная корутина (Трясет дверь вперед-назад, а замок влево-вправо)
    private IEnumerator ShakeDoorAndLock()
    {
        isShaking = true;
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Трясем саму дверь
            float doorShakeAngle = Mathf.Sin(elapsed * 50f) * 2f; 
            transform.localRotation = closedRotation * Quaternion.AngleAxis(doorShakeAngle, Vector3.forward);

            // Одновременно трясем замок на ушках
            if (padlockVisual != null && padlockVisual.activeSelf)
            {
                float padlockShakeAngle = Mathf.Sin(elapsed * 50f) * 15f; 
                padlockVisual.transform.localRotation = padlockOriginalRot * Quaternion.Euler(0f, 0f, padlockShakeAngle);
            }

            yield return null;
        }

        // Возвращаем все на свои места
        transform.localRotation = closedRotation;
        if (padlockVisual != null) padlockVisual.transform.localRotation = padlockOriginalRot;
        
        isShaking = false;
    }

    public void UnlockDoor()
    {
        if (!isLocked) return;
        isLocked = false;
        if (padlockVisual != null) padlockVisual.SetActive(false);
    }

    public void LockDoor()
    {
        if (isOpen) return;
        
        isLocked = true;
        if (padlockVisual != null) 
        {
            padlockVisual.SetActive(true);
            padlockVisual.transform.localRotation = padlockOriginalRot;
        }
    }
}