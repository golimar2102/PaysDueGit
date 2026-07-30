using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct AudioVolumeClip
{
    [Tooltip("Аудиоклип шага/приземления.")]
    public AudioClip clip;
    
    [Range(0f, 1f)]
    [Tooltip("Индивидуальная громкость для этого клипа.")]
    public float volume;
}

[System.Serializable]
public struct DatabasePhysicsMaterialGroup
{
    [Tooltip("Физический материал поверхности.")]
    public PhysicsMaterial physicsMaterial;
    
    [Range(0f, 2f)]
    [Tooltip("Общий множитель громкости для этой поверхности.")]
    public float volumeMultiplier;
    
    [Tooltip("Аудиоклипы для шагов с индивидуальной громкостью.")]
    public AudioVolumeClip[] footstepClips;
    
    [Tooltip("Аудиоклипы для приземления с индивидуальной громкостью. Если пусто, используются клипы шагов.")]
    public AudioVolumeClip[] landClips;
}

[System.Serializable]
public struct DatabaseTerrainLayerGroup
{
    [Tooltip("Слой террейна.")]
    public TerrainLayer terrainLayer;
    
    [Range(0f, 2f)]
    [Tooltip("Общий множитель громкости для этой поверхности.")]
    public float volumeMultiplier;
    
    [Tooltip("Аудиоклипы для шагов с индивидуальной громкостью.")]
    public AudioVolumeClip[] footstepClips;
    
    [Tooltip("Аудиоклипы для приземления с индивидуальной громкостью. Если пусто, используются клипы шагов.")]
    public AudioVolumeClip[] landClips;
}

/// <summary>
/// Общая база данных звуков шагов на основе ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "NewFootstepDatabase", menuName = "Audio/Footstep Database", order = 1)]
public class FootstepDatabase : ScriptableObject
{
    [Header("Таблицы звуков")]
    [Tooltip("Настройки звуков для конкретных Physics Materials.")]
    public List<DatabasePhysicsMaterialGroup> physicsMaterialGroups = new List<DatabasePhysicsMaterialGroup>();
    
    [Tooltip("Настройки звуков для слоев террейна.")]
    public List<DatabaseTerrainLayerGroup> terrainLayerGroups = new List<DatabaseTerrainLayerGroup>();

    [Header("Дефолтные звуки (если соответствие не найдено)")]
    [Tooltip("Дефолтные аудиоклипы для шагов.")]
    public AudioVolumeClip[] defaultFootstepClips;
    
    [Tooltip("Дефолтные аудиоклипы для приземления.")]
    public AudioVolumeClip[] defaultLandClips;
}
