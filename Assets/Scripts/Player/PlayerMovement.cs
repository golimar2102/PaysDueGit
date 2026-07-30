using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Ссылки")]
    public CharacterController controller;
    public Animator animator;

    [Header("Камера (для приседа)")]
    public Transform playerCamera;
    public float normalCameraHeight = 3f;
    public float crouchCameraHeight = 1.5f;

    [Header("Настройки покачивания головы (Head Bobbing)")]
    public bool useHeadBob = true;
    public float bobSpeedIdle = 1.5f;
    public float bobAmountIdle = 0.012f; // Очень легкое дыхание стоя
    public float bobSpeedWalk = 8.5f;
    public float bobAmountWalk = 0.035f; // Плавный шаг
    public float bobSpeedCrouch = 6.5f;
    public float bobAmountCrouch = 0.02f; // Мягкий шаг в приседе
    public float bobSpeedSprint = 12.5f;
    public float bobAmountSprint = 0.06f; // Чуть сильнее при беге

    private float bobTimer = 0f;
    private float currentBaseCameraHeight;

    [Header("Настройки скорости")]
    public float walkSpeed = 9f;
    public float sprintSpeed = 20f;
    public float crouchSpeed = 2.5f;

    [Header("Прыжок и Гравитация")]
    public float jumpHeight = 1.5f;
    public float gravity = -19.81f;

    [Header("Настройки приседа")]
    public float normalHeight = 7f;
    public float crouchHeight = 3f;
    [Tooltip("Насколько плавно садится и встает")]
    public float crouchTransitionSpeed = 10f; 

    [Header("Трата стамины")]
    [Tooltip("Сколько стамины тратится за секунду бега")]
    public float sprintStaminaCost = 15f;
    [Tooltip("Сколько стамины тратится на один прыжок")]
    public float jumpStaminaCost = 15f;

    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching = false;
    private bool isSittingOnGround = false;
    
    // Переменная для хранения смещения пяток
    private float capsuleBottomOffset;
    private PlayerStats playerStats; // <-- НОВОЕ: Ссылка на стамину

    [Header("Лестница (HL1-style)")]
    public float ladderClimbSpeed = 6f;
    private int ladderCount = 0;
    public bool IsOnLadder => ladderCount > 0;
    public bool IsGrounded => isGrounded;
    public bool IsCrouching => isCrouching;
    public bool IsSittingOnGround => isSittingOnGround;

    [Header("Звуки лестницы")]
    public AudioSource ladderAudioSource;
    public AudioClip[] ladderClimbClips;
    public float climbStepInterval = 0.4f;
    private float climbStepTimer = 0f;

    void Start()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        
        // Ищем скрипт стамины везде, чтобы он точно не потерялся!
        playerStats = GetComponent<PlayerStats>(); 
        if (playerStats == null) playerStats = GetComponentInParent<PlayerStats>();
        if (playerStats == null) playerStats = GetComponentInChildren<PlayerStats>();
        
        // ВАЖНО: При старте вычисляем, где находится "дно" капсулы относительно центра объекта.
        capsuleBottomOffset = controller.center.y - (controller.height / 2f);
        currentBaseCameraHeight = normalCameraHeight;

        // Ищем или создаем AudioSource для звуков лестницы
        if (ladderAudioSource == null) ladderAudioSource = GetComponent<AudioSource>();
        if (ladderAudioSource == null) ladderAudioSource = GetComponentInChildren<AudioSource>();
        if (ladderAudioSource == null) ladderAudioSource = GetComponentInParent<AudioSource>();
        if (ladderAudioSource == null)
        {
            ladderAudioSource = gameObject.AddComponent<AudioSource>();
            ladderAudioSource.spatialBlend = 0f; // 2D для первого лица
            ladderAudioSource.playOnAwake = false;
        }
    }

    void Update()
    {
        // ЗАЩИТА ОТ ОШИБОК: Не пытаемся двигаться, если контроллер временно выключен (например, при телепорте)
        if (!controller.enabled) return;

        // Блокировка движения во время разговора
        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive)
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }
            // Применяем гравитацию, чтобы игрок не завис в воздухе
            if (!controller.isGrounded)
            {
                velocity.y += gravity * Time.deltaTime;
                controller.Move(velocity * Time.deltaTime);
            }
            else
            {
                velocity.y = -2f;
            }
            return;
        }

        // 1. Продвинутая проверка на землю (твоя логика с Raycast)
        isGrounded = controller.isGrounded;
        if (!isGrounded)
        {
            float rayLength = (controller.height / 2f) + 0.15f;
            isGrounded = Physics.Raycast(transform.position + controller.center, Vector3.down, rayLength);
        }

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; 
        }

        // 2. Считываем ввод для передвижения
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // 3. ЛОГИКА СИДЕНИЯ НА ПОЛУ (Кнопка C)
        if (Input.GetKeyDown(KeyCode.C) && isGrounded && !isCrouching)
        {
            isSittingOnGround = !isSittingOnGround;
        }

        // Отменяем сидение, если пошли или прыгнули
        if (isSittingOnGround && (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f || Input.GetButtonDown("Jump")))
        {
            isSittingOnGround = false;
        }

        if (IsOnLadder)
        {
            velocity = Vector3.zero; // Сбрасываем гравитационное ускорение

            // В HL1 движение по лестнице идет в направлении взгляда (3D полет)
            Vector3 cameraForward = playerCamera != null ? playerCamera.forward : transform.forward;
            Vector3 cameraRight = playerCamera != null ? playerCamera.right : transform.right;

            Vector3 move = cameraForward * z + cameraRight * x;

            controller.Move(move * ladderClimbSpeed * Time.deltaTime);

            // Воспроизведение звуков шагов на лестнице
            if (move.magnitude > 0.05f)
            {
                if (climbStepTimer <= 0f)
                {
                    PlayLadderStepSound();
                    climbStepTimer = climbStepInterval;
                }
                climbStepTimer -= Time.deltaTime;
            }
            else
            {
                if (climbStepTimer < 0f)
                {
                    climbStepTimer = 0f;
                }
            }

            // Кнопка прыжка позволяет спрыгнуть
            if (Input.GetButtonDown("Jump"))
            {
                // При прыжке даем толчок от лестницы
                Vector3 pushDirection = -transform.forward * 5f + Vector3.up * 4f;
                velocity = pushDirection;
            }

            // Обновляем параметры аниматора для лестницы
            if (animator != null)
            {
                animator.SetBool("IsCrouching", false);
                animator.SetBool("IsSitting", false);
                animator.SetBool("IsGrounded", false);
                animator.SetFloat("ClimbSpeed", z);
                animator.SetFloat("Speed", 0f);
            }
        }
        else
        {
            // 4. Вызываем функции управления
            HandleCrouch();
            HandleMovement(x, z);
            HandleJump();

            // 5. Применяем гравитацию
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }

    private void HandleCrouch()
    {
        // Читаем кнопку (Левый Ctrl)
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isSittingOnGround)
            isCrouching = true;
        else if (Input.GetKeyUp(KeyCode.LeftControl))
            isCrouching = false;

        // Принудительно отключаем присед, если сели на пол
        if (isSittingOnGround) 
            isCrouching = false;

        // Определяем, к каким цифрам стремимся прямо сейчас
        float targetHeight = isCrouching ? crouchHeight : normalHeight;
        float targetCamHeight = isCrouching ? crouchCameraHeight : normalCameraHeight;

        // 1. Плавно меняем ВЫСОТУ капсулы
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        // 2. УМНОЕ ИЗМЕНЕНИЕ ЦЕНТРА (Моя защита от провала под землю)
        controller.center = new Vector3(
            controller.center.x, 
            capsuleBottomOffset + (controller.height / 2f), 
            controller.center.z
        );

        // 3. Вычисляем покачивание головы (Head Bobbing)
        currentBaseCameraHeight = Mathf.Lerp(currentBaseCameraHeight, targetCamHeight, Time.deltaTime * crouchTransitionSpeed);

        if (playerCamera != null)
        {
            float bobOffsetY = 0f;
            float bobOffsetX = 0f;

            if (useHeadBob)
            {
                float speed = 0f;
                float amount = 0f;

                bool isMoving = (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f) && isGrounded && !isSittingOnGround;

                if (!isGrounded || isSittingOnGround)
                {
                    speed = 0f;
                    amount = 0f;
                }
                else if (isMoving)
                {
                    if (isCrouching)
                    {
                        speed = bobSpeedCrouch;
                        amount = bobAmountCrouch;
                    }
                    else if (Input.GetKey(KeyCode.LeftShift) && playerStats != null && playerStats.currentStamina > 0f)
                    {
                        speed = bobSpeedSprint;
                        amount = bobAmountSprint;
                    }
                    else
                    {
                        speed = bobSpeedWalk;
                        amount = bobAmountWalk;
                    }
                }
                else
                {
                    // Стоим на месте (Дыхание)
                    speed = bobSpeedIdle;
                    amount = bobAmountIdle;
                }

                if (speed > 0f)
                {
                    bobTimer += Time.deltaTime * speed;
                }
                else
                {
                    // Плавно возвращаем таймер к нулю для предотвращения резкого сброса
                    bobTimer = Mathf.MoveTowards(bobTimer, 0f, Time.deltaTime);
                }

                bobOffsetY = Mathf.Sin(bobTimer) * amount;
                bobOffsetX = Mathf.Cos(bobTimer * 0.5f) * amount * 0.4f; // Легкое покачивание влево-вправо
            }
            else
            {
                bobTimer = 0f;
            }

            Vector3 camPos = playerCamera.localPosition;
            camPos.y = currentBaseCameraHeight + bobOffsetY;
            camPos.x = bobOffsetX;
            playerCamera.localPosition = camPos;
        }
    }

    private void HandleMovement(float x, float z)
    {
        Vector3 move = transform.right * x + transform.forward * z;

        // Запрещаем двигаться, если сидим
        if (isSittingOnGround)
        {
            move = Vector3.zero;
        }

        // Выбираем текущую скорость
        float currentSpeed = walkSpeed;
        if (isCrouching) 
        {
            currentSpeed = crouchSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftShift) && move.magnitude > 0.1f) 
        {
            // <-- Проверяем и тратим стамину на бег (Берем из Инспектора)
            if (playerStats != null)
            {
                if (playerStats.currentStamina > 0)
                {
                    currentSpeed = sprintSpeed;
                    playerStats.UseStamina(sprintStaminaCost * Time.deltaTime);
                }
            }
            else
            {
                currentSpeed = sprintSpeed; // Если скрипта стамины нет
            }
        }

        // Двигаем контроллер
        controller.Move(move * currentSpeed * Time.deltaTime);

        // --- ЛОГИКА АНИМАЦИИ (Твоя система) ---
        if (animator != null)
        {
            animator.SetBool("IsCrouching", isCrouching); 
            animator.SetBool("IsSitting", isSittingOnGround); 
            animator.SetBool("IsGrounded", isGrounded);

            if (isSittingOnGround)
            {
                animator.SetFloat("Speed", 0f);
            }
            else
            {
                float animSpeed = move.magnitude > 0.1f ? currentSpeed : 0f;
                animator.SetFloat("Speed", animSpeed);
            }
        }
    }

    private void HandleJump()
    {
        // Прыгаем, только если на земле, НЕ в приседе и НЕ сидим
        if (Input.GetButtonDown("Jump") && isGrounded && !isCrouching && !isSittingOnGround)
        {
            // <-- Проверяем и тратим стамину на прыжок (Берем из Инспектора)
            if (playerStats != null)
            {
                // Если стамины меньше стоимости прыжка - прыгнуть не можем
                if (!playerStats.HasStamina(jumpStaminaCost)) return; 
                
                playerStats.UseStamina(jumpStaminaCost);
            }

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            
            // Запускаем анимацию прыжка
            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    // === НОВАЯ ФУНКЦИЯ ТЕЛЕПОРТАЦИИ ===
    public void Teleport(Transform destination)
    {
        if (controller == null) return;

        // 1. Отключаем контроллер, чтобы он не блокировал перемещение
        controller.enabled = false;

        // 2. КРИТИЧЕСКИ ВАЖНО: Сбрасываем накопленную гравитацию!
        // Именно из-за нее персонаж с огромной силой вбивался в пол 
        // и физика Unity "выплевывала" его вперед от точки спавна.
        velocity = Vector3.zero;

        // 3. Жесткий перенос в координаты точки
        transform.position = destination.position;
        transform.rotation = Quaternion.Euler(0f, destination.eulerAngles.y, 0f);

        // 4. Синхронизируем физику (движок моментально забывает старую позицию)
        Physics.SyncTransforms();

        // 5. Включаем контроллер обратно
        controller.enabled = true;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        if (controller == null) return;

        controller.enabled = false;
        velocity = Vector3.zero;
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        Physics.SyncTransforms();
        controller.enabled = true;
    }

    public void RegisterNearLadder(Ladder ladder)
    {
        ladderCount++;
    }

    public void UnregisterNearLadder(Ladder ladder)
    {
        ladderCount = Mathf.Max(0, ladderCount - 1);
    }

    private void PlayLadderStepSound()
    {
        if (ladderAudioSource == null || ladderClimbClips == null || ladderClimbClips.Length == 0) return;

        int index = Random.Range(0, ladderClimbClips.Length);
        AudioClip clip = ladderClimbClips[index];
        if (clip != null)
        {
            ladderAudioSource.PlayOneShot(clip);
        }
    }
}