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
    public float bobAmountIdle = 0.012f;
    public float bobSpeedWalk = 8.5f;
    public float bobAmountWalk = 0.035f;
    public float bobSpeedCrouch = 6.5f;
    public float bobAmountCrouch = 0.02f;
    public float bobSpeedSprint = 12.5f;
    public float bobAmountSprint = 0.06f;

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

    private float capsuleBottomOffset;
    private PlayerStats playerStats;

    [Header("Лестница")]
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
    
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");
    private static readonly int ClimbSpeedHash = Animator.StringToHash("ClimbSpeed");

    void Start()
    {
        controller ??= GetComponent<CharacterController>();
        animator ??= GetComponentInChildren<Animator>();
        playerStats ??= GetComponent<PlayerStats>() ?? GetComponentInParent<PlayerStats>() ?? GetComponentInChildren<PlayerStats>();

        capsuleBottomOffset = controller.center.y - (controller.height / 2f);
        currentBaseCameraHeight = normalCameraHeight;

        if (ladderAudioSource == null)
        {
            ladderAudioSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>() ?? GetComponentInParent<AudioSource>();
            if (ladderAudioSource == null)
            {
                ladderAudioSource = gameObject.AddComponent<AudioSource>();
                ladderAudioSource.spatialBlend = 0f;
                ladderAudioSource.playOnAwake = false;
            }
        }
    }

    void Update()
    {
        if (!controller.enabled) return;
        
        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive)
        {
            if (animator != null) animator.SetFloat(SpeedHash, 0f);
            
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

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        
        if (Input.GetKeyDown(KeyCode.C) && isGrounded && !isCrouching)
        {
            isSittingOnGround = !isSittingOnGround;
        }

        if (isSittingOnGround && (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f || Input.GetButtonDown("Jump")))
        {
            isSittingOnGround = false;
        }
        
        if (IsOnLadder)
        {
            UpdateLadderMovement(x, z);
            return;
        }
        
        HandleCrouch();
        HandleMovement(x, z);
        HandleJump();
        
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateLadderMovement(float x, float z)
    {
        velocity = Vector3.zero;

        Vector3 cameraForward = playerCamera != null ? playerCamera.forward : transform.forward;
        Vector3 cameraRight = playerCamera != null ? playerCamera.right : transform.right;

        Vector3 move = cameraForward * z + cameraRight * x;
        controller.Move(move * ladderClimbSpeed * Time.deltaTime);
        
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
            if (climbStepTimer < 0f) climbStepTimer = 0f;
        }
        
        if (Input.GetButtonDown("Jump"))
        {
            velocity = -transform.forward * 5f + Vector3.up * 4f;
        }

        if (animator != null)
        {
            animator.SetBool(IsCrouchingHash, false);
            animator.SetBool(IsSittingHash, false);
            animator.SetBool(IsGroundedHash, false);
            animator.SetFloat(ClimbSpeedHash, z);
            animator.SetFloat(SpeedHash, 0f);
        }
    }

    private void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isSittingOnGround)
            isCrouching = true;
        else if (Input.GetKeyUp(KeyCode.LeftControl))
            isCrouching = false;

        if (isSittingOnGround) isCrouching = false;

        float targetHeight = isCrouching ? crouchHeight : normalHeight;
        float targetCamHeight = isCrouching ? crouchCameraHeight : normalCameraHeight;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(
            controller.center.x,
            capsuleBottomOffset + (controller.height / 2f),
            controller.center.z
        );

        currentBaseCameraHeight = Mathf.Lerp(currentBaseCameraHeight, targetCamHeight, Time.deltaTime * crouchTransitionSpeed);

        UpdateHeadBob();
    }

    private void UpdateHeadBob()
    {
        if (playerCamera == null) return;

        if (!useHeadBob)
        {
            bobTimer = 0f;
            Vector3 defaultCamPos = playerCamera.localPosition;
            defaultCamPos.y = currentBaseCameraHeight;
            defaultCamPos.x = 0f;
            playerCamera.localPosition = defaultCamPos;
            return;
        }

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
            speed = bobSpeedIdle;
            amount = bobAmountIdle;
        }

        if (speed > 0f)
            bobTimer += Time.deltaTime * speed;
        else
            bobTimer = Mathf.MoveTowards(bobTimer, 0f, Time.deltaTime);

        float bobOffsetY = Mathf.Sin(bobTimer) * amount;
        float bobOffsetX = Mathf.Cos(bobTimer * 0.5f) * amount * 0.4f;

        Vector3 camPos = playerCamera.localPosition;
        camPos.y = currentBaseCameraHeight + bobOffsetY;
        camPos.x = bobOffsetX;
        playerCamera.localPosition = camPos;
    }

    private void HandleMovement(float x, float z)
    {
        Vector3 move = transform.right * x + transform.forward * z;
        if (isSittingOnGround) move = Vector3.zero;

        float currentSpeed = walkSpeed;
        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftShift) && move.magnitude > 0.1f)
        {
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
                currentSpeed = sprintSpeed;
            }
        }

        controller.Move(move * currentSpeed * Time.deltaTime);

        if (animator != null)
        {
            animator.SetBool(IsCrouchingHash, isCrouching);
            animator.SetBool(IsSittingHash, isSittingOnGround);
            animator.SetBool(IsGroundedHash, isGrounded);

            float animSpeed = (!isSittingOnGround && move.magnitude > 0.1f) ? currentSpeed : 0f;
            animator.SetFloat(SpeedHash, animSpeed);
        }
    }

    private void HandleJump()
    {
        if (!Input.GetButtonDown("Jump") || !isGrounded || isCrouching || isSittingOnGround) return;

        if (playerStats != null)
        {
            if (!playerStats.HasStamina(jumpStaminaCost)) return;
            playerStats.UseStamina(jumpStaminaCost);
        }

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        if (animator != null)
        {
            animator.SetTrigger(JumpTriggerHash);
        }
    }

    public void Teleport(Transform destination)
    {
        if (destination == null) return;
        Teleport(destination.position, destination.rotation);
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