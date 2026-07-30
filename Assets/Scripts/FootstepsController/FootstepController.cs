using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Универсальный контроллер звуков шагов и приземления для игрока и НПС.
/// Определяет тип поверхности через Physic Material или Terrain Layer под персонажем.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FootstepController : MonoBehaviour
{
    public enum ActorType { AutoDetect, Player, NPC }

    [Header("Тип персонажа")]
    [Tooltip("AutoDetect автоматически определит тип по наличию компонентов PlayerMovement или CharacterController.")]
    public ActorType actorType = ActorType.AutoDetect;

    [Header("База данных звуков")]
    [Tooltip("Общая база данных звуков шагов.")]
    public FootstepDatabase footstepDatabase;

    [Header("Настройки интервалов шагов (в секундах)")]
    public float walkStepInterval = 0.5f;
    public float sprintStepInterval = 0.32f;
    public float crouchStepInterval = 0.8f;

    [Header("Настройки громкости")]
    [Range(0f, 1f)] public float walkVolume = 0.4f;
    [Range(0f, 1f)] public float sprintVolume = 0.8f;
    [Range(0f, 1f)] public float crouchVolume = 0.15f;
    [Range(0f, 2f)] [Tooltip("Глобальный множитель громкости шагов.")]
    public float footstepVolumeMultiplier = 1.0f;

    [Header("Тональность (Pitch)")]
    [Range(0.5f, 2.0f)] public float minPitch = 0.85f;
    [Range(0.5f, 2.0f)] public float maxPitch = 1.15f;

    [Header("Громкость приземления")]
    [Range(0f, 1f)] public float landVolume = 0.7f;

    [Header("Настройки луча (Raycast)")]
    public LayerMask groundLayerMask = ~0;
    [Tooltip("На сколько приподнять начало луча относительно позиции объекта (чтобы не застрять внутри геометрии).")]
    public float rayStartOffset = 0.5f;
    [Tooltip("Длина луча вниз от точки старта.")]
    public float rayDistance = 0.7f;

    private AudioSource audioSource;
    private CharacterController characterController;
    private NavMeshAgent navMeshAgent;
    private PlayerMovement playerMovement;
    private EnemyAI enemyAI;

    private float stepTimer = 0f;
    private bool wasGrounded = true;
    private bool isPlayer = false;
    private Vector3 lastPosition;

    // Оптимизация памяти (GC Allocations) и производительности
    private Collider myCollider;
    private readonly RaycastHit[] hitsBuffer = new RaycastHit[8];
    private float groundCheckTimer = 0f;
    private bool cachedGrounded = true;
    private const float GroundCheckInterval = 0.05f; // Проверяем приземление НПС 20 раз в секунду вместо каждого кадра

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        characterController = GetComponent<CharacterController>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        playerMovement = GetComponent<PlayerMovement>();
        enemyAI = GetComponent<EnemyAI>();
        myCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        // Автоопределение типа персонажа
        if (actorType == ActorType.AutoDetect)
        {
            isPlayer = (playerMovement != null || characterController != null);
        }
        else
        {
            isPlayer = (actorType == ActorType.Player);
        }

        // Конфигурируем пространственное звучание (spatial blend)
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            if (!isPlayer)
            {
                // Для НПС звук должен быть полностью трехмерным (3D)
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 25f;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }
            else
            {
                // Для игрока шаги воспроизводятся как 2D
                audioSource.spatialBlend = 0f;
            }
        }

        lastPosition = transform.position;
        wasGrounded = CheckIsGrounded();
    }

    private void Update()
    {
        if (audioSource == null) return;
        
        // Если это НПС и он мертв — звуки не проигрываем
        if (!isPlayer && enemyAI != null && enemyAI.currentState == EnemyAI.NPCState.Dead) return;

        bool isGrounded = CheckIsGrounded();
        float currentSpeed = GetCurrentSpeed();



        // 1. Приземление (были в воздухе, стали на земле)
        if (isGrounded && !wasGrounded)
        {
            PlayLandSound();
        }
        wasGrounded = isGrounded;

        // 2. Шаги (находимся на земле и двигаемся)
        if (isGrounded && currentSpeed > 0.15f)
        {
            // Дополнительные проверки для игрока
            if (isPlayer && playerMovement != null)
            {
                // Если на лестнице (там свои звуки) или сидит на полу — не шагаем
                if (playerMovement.IsOnLadder || playerMovement.IsSittingOnGround)
                {
                    stepTimer = 0f;
                    lastPosition = transform.position; // Обновляем позицию перед выходом
                    return;
                }
            }

            float interval = walkStepInterval;
            float volume = walkVolume;

            DetermineMovementState(currentSpeed, out interval, out volume);

            stepTimer += Time.deltaTime;
            if (stepTimer >= interval)
            {
                PlayFootstepSound(volume);
                stepTimer = 0f;
            }
        }
        else
        {
            // Плавно сбрасываем таймер шагов при остановке
            stepTimer = Mathf.MoveTowards(stepTimer, 0f, Time.deltaTime * 2f);
        }

        // Запоминаем текущую позицию для следующего кадра
        lastPosition = transform.position;
    }

    private bool CheckIsGrounded()
    {
        // Пытаемся получить информацию от скрипта игрока
        if (isPlayer && playerMovement != null)
        {
            return playerMovement.IsGrounded;
        }
        // Или от контроллера (только для игрока!)
        if (isPlayer && characterController != null)
        {
            return characterController.isGrounded;
        }

        // Для НПС оптимизируем: пускаем луч не каждый кадр, а с интервалом GroundCheckInterval
        if (Time.time >= groundCheckTimer)
        {
            groundCheckTimer = Time.time + GroundCheckInterval;
            RaycastHit hit;
            cachedGrounded = GetGroundHit(out hit);
        }
        return cachedGrounded;
    }

    private bool GetGroundHit(out RaycastHit groundHit)
    {
        groundHit = new RaycastHit();
        
        Vector3 rayStart = transform.position;
        float castDistance = 0.5f;

        if (characterController != null)
        {
            rayStart = transform.position + characterController.center;
            castDistance = (characterController.height / 2f) + 2.0f; // Запас 2 метра для игрока
        }
        else if (navMeshAgent != null)
        {
            rayStart = transform.position + Vector3.up * (navMeshAgent.height / 2f);
            castDistance = (navMeshAgent.height / 2f) + 5.0f; // Запас 5 метров для НПС (на случай неровностей или парения)
        }
        else if (myCollider != null)
        {
            rayStart = myCollider.bounds.center;
            castDistance = myCollider.bounds.extents.y + 5.0f; // Запас 5 метров для НПС
        }
        else
        {
            rayStart = transform.position + Vector3.up * rayStartOffset;
            castDistance = rayStartOffset + 5.0f;
        }



        // Используем pre-allocated массив, чтобы избежать выделения памяти в куче (GC Alloc) каждый кадр
        int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, hitsBuffer, castDistance, groundLayerMask, QueryTriggerInteraction.Ignore);
        


        if (hitCount > 0)
        {
            // Находим ближайший валидный хит за один проход O(N) вместо сортировки O(N log N)
            float minDistance = float.MaxValue;
            bool foundValidHit = false;
            RaycastHit closestHit = default;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hitsBuffer[i];
                
                // Игнорируем любые собственные коллайдеры персонажа (включая родителей, детей и части тела)
                if (hit.collider.transform.root == transform.root)
                {
                    continue;
                }

                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    closestHit = hit;
                    foundValidHit = true;
                }
            }

            if (foundValidHit)
            {
                groundHit = closestHit;
                
                #if UNITY_EDITOR
                // Отрисовка луча: зеленый до точки столкновения, красный далее
                Debug.DrawLine(rayStart, closestHit.point, Color.green);
                Debug.DrawLine(closestHit.point, rayStart + Vector3.down * castDistance, Color.red);
                #endif
                
                return true;
            }
        }
        
        #if UNITY_EDITOR
        // Отрисовка неудачного луча полностью красным
        Debug.DrawLine(rayStart, rayStart + Vector3.down * castDistance, Color.red);
        #endif
        
        return false;
    }

    private float GetCurrentSpeed()
    {
        // Вычисляем горизонтальное смещение относительно прошлого кадра
        Vector3 displacement = transform.position - lastPosition;
        displacement.y = 0f; // Игнорируем прыжки и падения

        // Предотвращаем деление на ноль
        return Time.deltaTime > 0f ? (displacement.magnitude / Time.deltaTime) : 0f;
    }

    private void DetermineMovementState(float speed, out float interval, out float volume)
    {
        if (isPlayer)
        {
            if (playerMovement != null)
            {
                if (playerMovement.IsCrouching)
                {
                    interval = crouchStepInterval;
                    volume = crouchVolume;
                    return;
                }

                // Спринт игрока
                bool isSprintingInput = Input.GetKey(KeyCode.LeftShift);
                if (isSprintingInput && speed > playerMovement.walkSpeed * 1.05f)
                {
                    interval = sprintStepInterval;
                    volume = sprintVolume;
                    return;
                }
            }
            else
            {
                // Фаллбек для игрока без PlayerMovement
                if (speed > 10f)
                {
                    interval = sprintStepInterval;
                    volume = sprintVolume;
                    return;
                }
            }
        }
        else
        {
            // Логика НПС
            if (enemyAI != null)
            {
                // Бег
                if (speed > (enemyAI.walkSpeed + enemyAI.runSpeed) * 0.5f)
                {
                    interval = sprintStepInterval;
                    volume = sprintVolume;
                    return;
                }
                // Скрытность / Крадущийся шаг
                else if (speed < (enemyAI.walkSpeed + enemyAI.sneakSpeed) * 0.5f + 0.1f)
                {
                    interval = crouchStepInterval;
                    volume = crouchVolume;
                    return;
                }
            }
            else
            {
                // Фаллбек для НПС без EnemyAI
                if (speed > 4.5f)
                {
                    interval = sprintStepInterval;
                    volume = sprintVolume;
                    return;
                }
                else if (speed < 1.5f)
                {
                    interval = crouchStepInterval;
                    volume = crouchVolume;
                    return;
                }
            }
        }

        // По умолчанию: обычная ходьба
        interval = walkStepInterval;
        volume = walkVolume;
    }

    private struct SelectedFootstepSound
    {
        public AudioClip clip;
        public float volumeMultiplier;
    }

    private void PlayFootstepSound(float volume)
    {
        SelectedFootstepSound sound;
        if (TryGetSurfaceSound(false, out sound) && sound.clip != null)
        {
            ConfigureAudioSource(volume * sound.volumeMultiplier);
            audioSource.PlayOneShot(sound.clip);
        }
    }

    private void PlayLandSound()
    {
        SelectedFootstepSound sound;
        if (TryGetSurfaceSound(true, out sound) && sound.clip != null)
        {
            ConfigureAudioSource(landVolume * sound.volumeMultiplier);
            audioSource.PlayOneShot(sound.clip);
        }
    }

    private void ConfigureAudioSource(float baseVolume)
    {
        // Добавляем случайную модуляцию для более естественного звучания
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.volume = baseVolume * footstepVolumeMultiplier * Random.Range(0.9f, 1.1f);
    }

    private bool TryGetSurfaceSound(bool isLand, out SelectedFootstepSound selectedSound)
    {
        selectedSound = new SelectedFootstepSound { clip = null, volumeMultiplier = 1.0f };
        
        RaycastHit hit;
        if (GetGroundHit(out hit))
        {
            // 1. Проверяем террейн
            Terrain terrain = hit.collider.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                int layerIndex = GetTerrainTextureIndex(terrain, hit.point);
                if (layerIndex >= 0 && layerIndex < terrain.terrainData.terrainLayers.Length)
                {
                    TerrainLayer activeLayer = terrain.terrainData.terrainLayers[layerIndex];
                    
                    if (footstepDatabase != null)
                    {
                        foreach (var group in footstepDatabase.terrainLayerGroups)
                        {
                            if (group.terrainLayer == activeLayer)
                            {
                                return SelectClipFromGroup(group.footstepClips, group.landClips, group.volumeMultiplier, isLand, out selectedSound);
                            }
                        }
                    }
                }
            }

            // 2. Проверяем Physics Material
            PhysicsMaterial mat = hit.collider.sharedMaterial;
            if (mat != null)
            {
                if (footstepDatabase != null)
                {
                    foreach (var group in footstepDatabase.physicsMaterialGroups)
                    {
                        if (group.physicsMaterial == mat)
                        {
                            return SelectClipFromGroup(group.footstepClips, group.landClips, group.volumeMultiplier, isLand, out selectedSound);
                        }
                    }
                }
            }
        }

        // Фаллбек на дефолтные звуки
        AudioVolumeClip[] defaultClips = null;
        if (footstepDatabase != null)
        {
            defaultClips = isLand ? 
                (footstepDatabase.defaultLandClips.Length > 0 ? footstepDatabase.defaultLandClips : footstepDatabase.defaultFootstepClips) : 
                footstepDatabase.defaultFootstepClips;
        }

        if (defaultClips != null && defaultClips.Length > 0)
        {
            AudioVolumeClip chosen = defaultClips[Random.Range(0, defaultClips.Length)];
            selectedSound.clip = chosen.clip;
            selectedSound.volumeMultiplier = chosen.volume <= 0f ? 1.0f : chosen.volume;
            return true;
        }

        return false;
    }

    private bool SelectClipFromGroup(AudioVolumeClip[] steps, AudioVolumeClip[] lands, float groupMultiplier, bool isLand, out SelectedFootstepSound selectedSound)
    {
        selectedSound = new SelectedFootstepSound { clip = null, volumeMultiplier = 1.0f };
        AudioVolumeClip[] targetArray = isLand ? (lands != null && lands.Length > 0 ? lands : steps) : steps;

        if (targetArray != null && targetArray.Length > 0)
        {
            AudioVolumeClip chosen = targetArray[Random.Range(0, targetArray.Length)];
            selectedSound.clip = chosen.clip;
            
            float clipVol = chosen.volume <= 0f ? 1.0f : chosen.volume;
            float groupVol = groupMultiplier <= 0f ? 1.0f : groupMultiplier;
            
            selectedSound.volumeMultiplier = clipVol * groupVol;
            return true;
        }
        return false;
    }

    private int GetTerrainTextureIndex(Terrain terrain, Vector3 worldPos)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // Переводим глобальные координаты в координаты карты смешивания (splatmap)
        int mapX = Mathf.RoundToInt(((worldPos.x - terrainPos.x) / terrainData.size.x) * terrainData.alphamapWidth);
        int mapZ = Mathf.RoundToInt(((worldPos.z - terrainPos.z) / terrainData.size.z) * terrainData.alphamapHeight);

        mapX = Mathf.Clamp(mapX, 0, terrainData.alphamapWidth - 1);
        mapZ = Mathf.Clamp(mapZ, 0, terrainData.alphamapHeight - 1);

        // Получаем значение смешивания в этой точке
        float[,,] splatmapData = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

        float maxMix = 0f;
        int maxIndex = 0;

        // Ищем слой с максимальным влиянием (весом)
        for (int i = 0; i < splatmapData.GetLength(2); i++)
        {
            if (splatmapData[0, 0, i] > maxMix)
            {
                maxMix = splatmapData[0, 0, i];
                maxIndex = i;
            }
        }

        return maxIndex;
    }
}
