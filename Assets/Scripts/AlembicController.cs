using UnityEngine;
using UnityEngine.Formats.Alembic.Importer; // Обязательно добавь это пространство имен!

public class AlembicController : MonoBehaviour
{
    private AlembicStreamPlayer alembicPlayer;

    [Header("Настройки анимации")]
    public float speed = 1f;       // Скорость воспроизведения
    public bool loop = true;       // Зациклить?
    
    void Start()
    {
        // Ищем компонент на объекте
        alembicPlayer = GetComponent<AlembicStreamPlayer>();
        
        if (alembicPlayer == null)
        {
            Debug.LogError("На объекте нет компонента AlembicStreamPlayer!");
        }
    }

    void Update()
    {
        if (alembicPlayer == null) return;

        // Вручную двигаем время анимации вперед
        float duration = (float)(alembicPlayer.EndTime - alembicPlayer.StartTime);
        
        if (duration > 0)
        {
            float newTime = Time.time * speed;
            
            if (loop)
            {
                // Зацикливаем время через остаток от деления
                alembicPlayer.CurrentTime = (newTime % duration) + (float)alembicPlayer.StartTime;
            }
            else
            {
                // Просто проигрываем до конца
                alembicPlayer.CurrentTime = Mathf.Min(newTime + (float)alembicPlayer.StartTime, (float)alembicPlayer.EndTime);
            }
        }
    }
}