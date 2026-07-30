using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LanternCenterOfMass : MonoBehaviour
{
    [Header("Смещение веса (Центр масс)")]
    [Tooltip("Двигай X, Y или Z так, чтобы красный шарик оказался ровно на дне колбы!")]
    public Vector3 weightOffset = new Vector3(0f, 0f, -2.85f);

    [Header("Ручной тормоз (Фикс бага Unity)")]
    [Tooltip("Заменяет сломанный Angular Damping. 0 - вечная раскачка, 8 - быстро замирает.")]
    public float customAngularDamping = 8f;

    [Header("Ощущение тяжести")]
    [Tooltip("Множитель гравитации. 1 - обычная, 2-3 - лампа падает быстрее и ощущается как тяжелая гиря.")]
    public float gravityMultiplier = 2.5f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // 1. Смещаем центр тяжести по настроенным осям
        rb.centerOfMass = weightOffset;

        // 2. ИСПРАВЛЕНИЕ ДЛЯ ДВУХ КРЮЧКОВ: Ищем ВСЕ суставы на объекте
        CharacterJoint[] joints = GetComponents<CharacterJoint>();
        Collider[] myColliders = GetComponentsInChildren<Collider>(); 
        
        foreach (CharacterJoint joint in joints)
        {
            if (joint.connectedBody != null)
            {
                Collider[] handleColliders = joint.connectedBody.GetComponentsInChildren<Collider>(); 
                
                // Отключаем коллизию между колбой и ручкой для каждого крючка
                foreach (Collider myCol in myColliders)
                {
                    foreach (Collider handleCol in handleColliders)
                    {
                        Physics.IgnoreCollision(myCol, handleCol, true);
                    }
                }
            }
        }
    }

    // МАГИЯ ЗДЕСЬ: Ручное торможение и Искусственная масса
    void FixedUpdate()
    {
        if (rb != null)
        {
            // Тормозим вращение
            if (customAngularDamping > 0f)
            {
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, customAngularDamping * Time.fixedDeltaTime);
            }

            // Добавляем искусственную тяжесть (усиленную гравитацию)
            if (gravityMultiplier != 1f)
            {
                // Physics.gravity - это стандартные (0, -9.81, 0). 
                // Мы умножаем их на наш множитель и толкаем лампу вниз!
                rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            }
        }
    }

    // Рисуем красный шарик в редакторе для удобства
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 centerOfMassWorld = transform.TransformPoint(weightOffset);
        Gizmos.DrawSphere(centerOfMassWorld, 0.05f);
    }
}