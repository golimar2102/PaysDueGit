using UnityEngine;
using System.Collections.Generic;
using System.Linq;


// Этот скрипт вешается на префабы предметов (например, на фонарь),
// чтобы их можно было включать/выключать, пока они лежат на земле или столе.
public class WorldToggleDevice : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Текст, который будет написан рядом с кнопкой F")]
    public string promptText = "Вкл / Выкл";
    public bool isOn = true;
    
    [Header("Тип объекта")]
    [Tooltip("Отметьте, если это настенный выключатель, который должен подсвечиваться (Outline)")]
    public bool isSwitch = false;

    [Header("Электричество")]
    [Tooltip("Зависит ли прибор от работы генератора?")]
    public bool requiresPower = false;
    [Tooltip("Конкретный генератор, от которого зависит это устройство. Если пустой, используется глобальный Instance.")]
    public GeneratorController targetGenerator;

    private bool lastPowerState = false;

    private Outline outline;
    private Dictionary<Renderer, Color> originalEmissionColors = new Dictionary<Renderer, Color>();

    [Header("Авто-поиск компонентов (Особенно для выключателей)")]
    [Tooltip("Добавьте сюда корневые объекты (например, целую лампу). Скрипт сам найдет внутри неё Light и Renderer с Emission.")]
    public GameObject[] targetGroupsToScan;

    [Header("Что включать/выключать?")]
    public Light[] lightsToToggle;
    public ParticleSystem[] particlesToToggle;
    public GameObject[] objectsToToggle;
    public Renderer[] renderersToToggleEmission;
    
    [Header("Анимация выключателя (Опционально)")]
    public Transform switchTransform;
    public Vector3 onRotation;
    public Vector3 offRotation;

    void Awake()
    {
        // Автоматически добавляем BoxCollider, если на объекте вообще нет коллайдеров.
        // Это нужно, чтобы луч от камеры (Raycast) мог попасть по выключателю.
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        // Ищем Outline как на самом объекте, так и на его дочерних
        outline = GetComponentInChildren<Outline>(true);
        if (outline != null) outline.enabled = false;

        // Автосбор компонентов из targetGroupsToScan
        if (targetGroupsToScan != null && targetGroupsToScan.Length > 0)
        {
            List<Light> foundLights = new List<Light>(lightsToToggle != null ? lightsToToggle : new Light[0]);
            List<ParticleSystem> foundParticles = new List<ParticleSystem>(particlesToToggle != null ? particlesToToggle : new ParticleSystem[0]);
            List<Renderer> foundRenderers = new List<Renderer>(renderersToToggleEmission != null ? renderersToToggleEmission : new Renderer[0]);

            foreach (var group in targetGroupsToScan)
            {
                if (group == null) continue;

                foundLights.AddRange(group.GetComponentsInChildren<Light>(true));
                foundParticles.AddRange(group.GetComponentsInChildren<ParticleSystem>(true));
                
                Renderer[] renderers = group.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    // Проверяем, есть ли у материала Emission. 
                    // Используем sharedMaterial, чтобы не создавать инстансы при поиске
                    if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_EmissionColor"))
                    {
                        foundRenderers.Add(r);
                    }
                }
            }

            // Убираем дубликаты
            lightsToToggle = foundLights.Distinct().ToArray();
            particlesToToggle = foundParticles.Distinct().ToArray();
            renderersToToggleEmission = foundRenderers.Distinct().ToArray();
        }
    }

    protected virtual void Start()
    {
        foreach (Renderer r in renderersToToggleEmission)
        {
            if (r != null && r.material != null && r.material.HasProperty("_EmissionColor"))
            {
                originalEmissionColors[r] = r.material.GetColor("_EmissionColor");
            }
        }
        
        lastPowerState = IsPowerWorking();
        UpdateDeviceState();
    }

    protected bool IsPowerWorking()
    {
        if (targetGenerator != null)
        {
            return targetGenerator.isWorking;
        }
        return GeneratorController.IsGeneratorWorking;
    }

    public void Update()
    {
        if (requiresPower)
        {
            bool currentPower = IsPowerWorking();
            if (currentPower != lastPowerState)
            {
                lastPowerState = currentPower;
                UpdateDeviceState();
            }
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (isSwitch && outline != null) outline.enabled = isHighlighted;
    }
    [Header("Звуки")]
    [Tooltip("Звук при включении (или для обоих действий, если звук выключения не задан)")]
    public AudioSource toggleSound;
    [Tooltip("Звук при выключении (Опционально)")]
    public AudioSource toggleOffSound;
    
    public virtual void SetState(bool state)
    {
        isOn = state;
        UpdateDeviceState();
    }

    // Вызывается при нажатии E/F игроком
    public virtual void Toggle()
    {
        isOn = !isOn;
        
        if (isOn)
        {
            if (toggleSound != null) toggleSound.Play();
        }
        else
        {
            if (toggleOffSound != null) toggleOffSound.Play();
            else if (toggleSound != null) toggleSound.Play();
        }
        
        UpdateDeviceState();
    }

    protected virtual void UpdateDeviceState()
    {
        bool shouldBeActive = isOn;
        if (requiresPower && !IsPowerWorking())
        {
            shouldBeActive = false;
        }

        foreach (Light l in lightsToToggle) 
            if (l != null) l.enabled = shouldBeActive;

        foreach (GameObject obj in objectsToToggle) 
            if (obj != null) obj.SetActive(shouldBeActive);

        foreach (ParticleSystem p in particlesToToggle)
        {
            if (p != null)
            {
                if (shouldBeActive) p.Play();
                else p.Stop();
            }
        }

        foreach (Renderer r in renderersToToggleEmission)
        {
            if (r != null && r.material != null)
            {
                if (shouldBeActive) 
                {
                    r.material.EnableKeyword("_EMISSION");
                    if (originalEmissionColors.ContainsKey(r))
                        r.material.SetColor("_EmissionColor", originalEmissionColors[r]);
                }
                else 
                {
                    r.material.DisableKeyword("_EMISSION");
                    if (r.material.HasProperty("_EmissionColor"))
                        r.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        if (switchTransform != null)
        {
            switchTransform.localEulerAngles = isOn ? onRotation : offRotation;
        }
    }
}