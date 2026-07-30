using UnityEngine;
using System.Collections;

public class SummonedDoor : MonoBehaviour
{
    private Vector3 targetGroundPos;
    private Vector3 playerPosition;
    private AudioClip fallingClip;
    private AudioClip landingClip;
    private AudioClip openingClip;
    private float openDelay;

    private float initialFallSpeed;
    private float extraGravity;

    private Rigidbody rb;
    private AudioSource fallingAudioSource;
    private bool hasLanded = false;
    
    // Высота приземления двери
    private float targetGroundY;

    public void Initialize(Vector3 targetGround, Vector3 playerPos, AudioClip falling, AudioClip landing, AudioClip opening, float delay, float initSpeed, float addGravity)
    {
        targetGroundPos = targetGround;
        playerPosition = playerPos;
        fallingClip = falling;
        landingClip = landing;
        openingClip = opening;
        openDelay = delay;
        initialFallSpeed = initSpeed;
        extraGravity = addGravity;
        targetGroundY = targetGround.y;

        SetupPhysics();
        SetupAudio();
    }

    private void SetupPhysics()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = 200f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Замораживаем вращение, чтобы дверь падала ровно вертикально
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // Придаем начальную скорость вниз
        rb.linearVelocity = new Vector3(0f, -initialFallSpeed, 0f);
    }

    private void SetupAudio()
    {
        if (fallingClip != null)
        {
            fallingAudioSource = gameObject.AddComponent<AudioSource>();
            fallingAudioSource.clip = fallingClip;
            fallingAudioSource.loop = true;
            fallingAudioSource.spatialBlend = 1f; // 3D sound
            fallingAudioSource.minDistance = 2f;
            fallingAudioSource.maxDistance = 30f;
            fallingAudioSource.volume = 0.8f;
            fallingAudioSource.Play();
        }
    }

    private void Update()
    {
        // Страховочный триггер: если дверь опустилась ниже уровня пола, принудительно приземляем ее
        if (!hasLanded && transform.position.y <= targetGroundY + 0.05f)
        {
            Land();
        }
    }

    private void FixedUpdate()
    {
        // Прикладываем постоянную силу вниз для быстрого ускорения
        if (!hasLanded && rb != null && !rb.isKinematic)
        {
            rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player")) return;

        if (!hasLanded)
        {
            Land();
        }
    }

    private void Land()
    {
        hasLanded = true;

        // Останавливаем звук падения
        if (fallingAudioSource != null && fallingAudioSource.isPlaying)
        {
            fallingAudioSource.Stop();
            Destroy(fallingAudioSource);
        }

        // Выключаем физику (сначала сбрасываем скорости, чтобы избежать предупреждений в логах) и удаляем Rigidbody
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
            Destroy(rb);
        }

        // Выравниваем дверь ровно по высоте земли
        Vector3 finalPos = transform.position;
        finalPos.y = targetGroundY;
        transform.position = finalPos;

        // Звук удара о землю
        if (landingClip != null)
        {
            PlaySound(landingClip);
        }

        // Начинаем открывать дверь
        StartCoroutine(OpenSequence());
    }

    private IEnumerator OpenSequence()
    {
        yield return new WaitForSeconds(openDelay);

        // Звук открытия
        if (openingClip != null)
        {
            PlaySound(openingClip);
        }

        // Открываем все части дверей (поддержка двустворчатых дверей и дочерних DoorController-ов)
        DoorController[] doorControllers = GetComponentsInChildren<DoorController>();
        if (doorControllers != null && doorControllers.Length > 0)
        {
            foreach (var dc in doorControllers)
            {
                dc.isLocked = false;
                if (!dc.isOpen)
                {
                    dc.TryOpenDoor(playerPosition);
                }
            }
            Debug.Log($"[SummonedDoor] Успешно открыто DoorController компонентов: {doorControllers.Length}.");
        }
        else
        {
            Debug.LogWarning("[SummonedDoor] Компонент DoorController не найден на призванной двери!");
        }
    }

    private void PlaySound(AudioClip clip)
    {
        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 30f;
        audioSource.volume = 1.0f;
        audioSource.Play();
        Destroy(audioSource, clip.length + 0.5f);
    }
}
