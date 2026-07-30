using UnityEngine;

public class MeatChunk : MonoBehaviour
{
    public GameObject bloodDecalPrefab;
    public float lifetime = 6f;
    private float shrinkDuration = 1.5f;
    private float aliveTimer = 0f;
    private Vector3 initialScale;
    private int bounces = 0;
    private int maxBounces = 4;
    private float lastBounceTime = 0f;
    private Rigidbody rb;
    
    // Время постоянного контакта с поверхностью после взрыва
    private float groundContactTime = 0f;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        initialScale = transform.localScale;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Collider myCollider = GetComponent<Collider>();
            Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
            foreach (Collider pCol in playerColliders)
            {
                Physics.IgnoreCollision(myCollider, pCol, true);
            }
        }
    }

    void Update()
    {
        aliveTimer += Time.deltaTime;
        if (aliveTimer >= lifetime - shrinkDuration)
        {
            float shrinkProgress = (aliveTimer - (lifetime - shrinkDuration)) / shrinkDuration;
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, shrinkProgress);
        }
        if (aliveTimer >= lifetime)
        {
            Destroy(gameObject);
        }

        // Бэкап: принудительно останавливаем вращение и полет через 3 секунды
        if (aliveTimer >= 3.0f && rb != null && !rb.isKinematic)
        {
            StopMovement();
        }
    }

    void FixedUpdate()
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(Physics.gravity * 3f, ForceMode.Acceleration);

            // Если кусок мяса движется очень медленно после первого отскока, останавливаем
            if (bounces >= 1 && rb.linearVelocity.magnitude < 0.2f)
            {
                StopMovement();
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (bounces >= maxBounces)
        {
            StopMovement();
            return;
        }
        if (Time.time - lastBounceTime < 0.2f) return;
        if (collision.gameObject.GetComponent<MeatChunk>() != null) return;
        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Player")) return;
        if (collision.gameObject.GetComponentInParent<PickUpItem>() != null) return;
        
        if (collision.relativeVelocity.magnitude > 2f)
        {
            bounces++;
            lastBounceTime = Time.time;
            if (bloodDecalPrefab != null)
            {
                ContactPoint contact = collision.contacts[0];
                Vector3 spawnPos = contact.point + contact.normal * 0.01f;
                Quaternion spawnRot = Quaternion.FromToRotation(Vector3.up, contact.normal);
                spawnRot *= Quaternion.Euler(0, Random.Range(0, 360), 0);
                GameObject decal = Instantiate(bloodDecalPrefab, spawnPos, spawnRot);
                Destroy(decal, 20f);
            }

            if (bounces >= maxBounces)
            {
                StopMovement();
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        // Если первоначальный разлет завершился (прошло больше 0.8 сек) и мы тремся о поверхность
        if (rb != null && !rb.isKinematic && aliveTimer > 0.8f)
        {
            if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy") || collision.gameObject.GetComponent<MeatChunk>() != null)
                return;

            groundContactTime += Time.deltaTime;

            // Если тремся/катимся по земле дольше 0.4 секунд, останавливаем полностью
            if (groundContactTime > 0.4f)
            {
                StopMovement();
            }
        }
    }

    private void StopMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }
}