using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerSanityEffects : MonoBehaviour
{
    [System.Serializable]
    public class SanitySoundConfig
    {
        public string name = "Sanity Loop";
        public AudioClip clip;
        [Range(0f, 1f)] public float maxVolume = 0.5f;
        [Tooltip("Повышать ли скорость/питч звука по мере падения рассудка (полезно для сердцебиения)")]
        public bool speedUpPulse = false;
        [Tooltip("Максимальный питч при рассудке = 0")]
        public float maxPitch = 1.35f;
        
        [Tooltip("Источник звука, привязанный к объекту")]
        public AudioSource source;
    }

    [Header("Звуки безумия (Sanity Sounds)")]
    [Tooltip("Список постоянных цикличных звуков, громкость которых зависит от уровня безумия")]
    public System.Collections.Generic.List<SanitySoundConfig> sanitySounds = new System.Collections.Generic.List<SanitySoundConfig>();

    private Volume sanityVolume;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;

    private AudioDistortionFilter audioDistortion;
    private AudioLowPassFilter audioLowPass;
    private AudioListener cachedAudioListener;

    private Transform fpsCamera;
    private float currentEffectT = 0f;

    private MouseLook cachedMouseLook;

    void Start()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            fpsCamera = mainCam.transform;
        }
        else
        {
            fpsCamera = GetComponentInChildren<Camera>()?.transform;
        }
        
        if (fpsCamera != null)
        {
            cachedMouseLook = fpsCamera.GetComponentInParent<MouseLook>() ?? fpsCamera.GetComponentInChildren<MouseLook>();
        }
        
        CreateSanityPostProcessVolume();
        
        foreach (var sound in sanitySounds)
        {
            if (sound.source != null)
            {
                if (sound.clip != null)
                {
                    sound.source.clip = sound.clip;
                }
                sound.source.loop = true;
                sound.source.playOnAwake = false;
            }
        }
    }

    private void CreateSanityPostProcessVolume()
    {
        GameObject volumeObj = new GameObject("SanityPostProcessVolume");
        volumeObj.transform.SetParent(transform);
        
        Volume volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 90;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        
        chromaticAberration = profile.Add<ChromaticAberration>();
        chromaticAberration.active = true;
        chromaticAberration.intensity.Override(0f);

        lensDistortion = profile.Add<LensDistortion>();
        lensDistortion.active = true;
        lensDistortion.intensity.Override(0f);
        lensDistortion.xMultiplier.Override(1f);
        lensDistortion.yMultiplier.Override(1f);
        lensDistortion.scale.Override(1f);

        vignette = profile.Add<Vignette>();
        vignette.active = true;
        vignette.intensity.Override(0f);
        vignette.color.Override(Color.black);

        colorAdjustments = profile.Add<ColorAdjustments>();
        colorAdjustments.active = true;
        colorAdjustments.saturation.Override(0f);

        volume.profile = profile;
        sanityVolume = volume;
    }

    void LateUpdate()
    {
        if (PlayerStats.Instance == null) return;

        PlayerStats stats = PlayerStats.Instance;
        float currentSanity = stats.currentSanity;
        
        float targetT = 0f;
        if (currentSanity <= stats.sanityThreshold)
        {
            targetT = Mathf.InverseLerp(stats.sanityThreshold, 0f, currentSanity);
        }
        
        currentEffectT = Mathf.MoveTowards(currentEffectT, targetT, Time.deltaTime * stats.effectTransitionSpeed);
        bool isCameraControlled = false;
        
        if (fpsCamera != null)
        {
            if (cachedMouseLook == null)
            {
                cachedMouseLook = fpsCamera.GetComponentInParent<MouseLook>() ?? fpsCamera.GetComponentInChildren<MouseLook>();
            }
            isCameraControlled = cachedMouseLook != null && cachedMouseLook.enabled;
        }
        
        bool isInInteraction = TVChairController.activeChair != null || PeepholeController.activePeephole != null;
        float wobbleStrength = isCameraControlled ? 1.0f : 0.0f;
        
        if (isInInteraction)
        {
            wobbleStrength = 0.12f;
        }

        if (currentEffectT > 0.001f)
        {
            ApplyPostProcessingEffects(currentEffectT);
            UpdateAudioFilters(currentEffectT);
            UpdateSanitySounds(currentEffectT);

            if (fpsCamera != null && wobbleStrength > 0.001f)
            {
                ApplyCameraWobble(currentEffectT, wobbleStrength);
            }
        }
        else
        {
            DisableEffects();
            DisableAudioFilters();
            StopSanitySounds();
        }
    }

    private void ApplyPostProcessingEffects(float t)
    {
        if (sanityVolume == null || PlayerStats.Instance == null) return;
        PlayerStats stats = PlayerStats.Instance;

        // Хроматическая аберрация с периодическими резкими вспышками (глюками)
        float glitchChance = Mathf.Lerp(0.01f, 0.15f, t);
        float baseChrIntensity = Mathf.Lerp(0f, stats.maxChromaticAberration, t);
        
        if (Random.value < glitchChance)
        {
            chromaticAberration.intensity.Override(baseChrIntensity + Random.Range(0.2f, 0.35f));
        }
        else
        {
            chromaticAberration.intensity.Override(Mathf.Lerp(chromaticAberration.intensity.value, baseChrIntensity, Time.deltaTime * 5f));
        }
        
        float distortionAmplitude = Mathf.Lerp(0f, stats.maxLensDistortion, t);
        float wave = Mathf.Sin(Time.time * Mathf.Lerp(1.5f, 6.0f, t)) * distortionAmplitude * 0.4f;
        
        if (Random.value < glitchChance * 0.4f)
        {
            lensDistortion.intensity.Override(distortionAmplitude + Random.Range(-0.08f, 0.08f));
        }
        else
        {
            lensDistortion.intensity.Override(Mathf.Lerp(lensDistortion.intensity.value, distortionAmplitude + wave, Time.deltaTime * 3.5f));
        }
        
        float vignetteBase = Mathf.Lerp(0f, stats.maxVignetteIntensity, t);
        float pulseSpeed = Mathf.Lerp(1.5f, 4.5f, t);
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.04f;
        
        vignette.intensity.Override(Mathf.Max(0f, vignetteBase + pulse));
        vignette.color.Override(Color.Lerp(Color.black, stats.vignetteColor, t));
        vignette.smoothness.Override(Mathf.Lerp(0.2f, 0.48f, t));
        float targetSaturation = Mathf.Lerp(0f, stats.maxSaturationDecrease, t);
        colorAdjustments.saturation.Override(targetSaturation);
    }

    private void UpdateAudioFilters(float t)
    {
        if (audioDistortion == null || audioLowPass == null)
        {
            if (cachedAudioListener == null)
            {
                cachedAudioListener = FindFirstObjectByType<AudioListener>();
            }

            if (cachedAudioListener != null)
            {
                audioDistortion = cachedAudioListener.GetComponent<AudioDistortionFilter>();
                if (audioDistortion == null) audioDistortion = cachedAudioListener.gameObject.AddComponent<AudioDistortionFilter>();
                
                audioLowPass = cachedAudioListener.GetComponent<AudioLowPassFilter>();
                if (audioLowPass == null) audioLowPass = cachedAudioListener.gameObject.AddComponent<AudioLowPassFilter>();
            }
        }

        if (audioDistortion != null && audioLowPass != null)
        {
            audioDistortion.distortionLevel = Mathf.Lerp(0f, 0.35f, t);
            audioLowPass.cutoffFrequency = Mathf.Lerp(22000f, 2000f, t);
        }
    }

    private void ApplyCameraWobble(float t, float multiplier)
    {
        if (PlayerStats.Instance == null || multiplier <= 0.001f) return;
        PlayerStats stats = PlayerStats.Instance;
        
        float frequency = Mathf.Lerp(stats.wobbleFrequency * 0.5f, stats.wobbleFrequency * 1.5f, t);
        float amplitude = Mathf.Lerp(stats.wobbleAmplitude * 0.15f, stats.wobbleAmplitude, t) * multiplier;

        float yawWobble = Mathf.Sin(Time.time * frequency) * amplitude;
        float pitchWobble = Mathf.Cos(Time.time * frequency * 0.85f) * amplitude;
        float rollWobble = Mathf.Sin(Time.time * frequency * 0.65f) * (amplitude * 1.6f);
        
        if (t > 0.45f && Random.value < Mathf.Lerp(0.001f, 0.04f, t))
        {
            yawWobble += Random.Range(-1.8f, 1.8f) * multiplier;
            pitchWobble += Random.Range(-1.8f, 1.8f) * multiplier;
            rollWobble += Random.Range(-2.8f, 2.8f) * multiplier;
        }
        
        fpsCamera.localRotation = fpsCamera.localRotation * Quaternion.Euler(pitchWobble, yawWobble, rollWobble);
    }

    private void DisableEffects()
    {
        if (chromaticAberration != null) chromaticAberration.intensity.Override(0f);
        if (lensDistortion != null) lensDistortion.intensity.Override(0f);
        if (vignette != null) vignette.intensity.Override(0f);
        if (colorAdjustments != null) colorAdjustments.saturation.Override(0f);
    }

    private void DisableAudioFilters()
    {
        if (audioDistortion != null)
        {
            audioDistortion.distortionLevel = 0f;
        }
        if (audioLowPass != null)
        {
            audioLowPass.cutoffFrequency = 22000f;
        }
    }

    void OnDisable()
    {
        DisableAudioFilters();
        StopSanitySounds();
    }

    void OnDestroy()
    {
        DisableAudioFilters();
        StopSanitySounds();
        if (sanityVolume != null && sanityVolume.profile != null)
        {
            Destroy(sanityVolume.profile);
        }
    }

    private void UpdateSanitySounds(float t)
    {
        foreach (var sound in sanitySounds)
        {
            if (sound.source != null)
            {
                if (!sound.source.isPlaying)
                {
                    sound.source.Play();
                }
                
                sound.source.volume = t * sound.maxVolume;
                if (sound.speedUpPulse)
                {
                    sound.source.pitch = Mathf.Lerp(1.0f, sound.maxPitch, t);
                }
                else
                {
                    sound.source.pitch = 1.0f;
                }
            }
        }
    }

    private void StopSanitySounds()
    {
        foreach (var sound in sanitySounds)
        {
            if (sound.source != null && sound.source.isPlaying)
            {
                sound.source.Stop();
            }
        }
    }
}
