using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MageAttack : MonoBehaviour
{
    public HeroBase mage;
    public float movingSpeed = 8f;
    void Start()
    {
        transform.LookAt(transform.position + Vector3.right);
    }

    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector3.forward * movingSpeed * Time.deltaTime);
        if(transform.position.x > 20f)
        {
            Destroy(gameObject);
        }
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            IHealth targetHealth = other.GetComponent<IHealth>();
            if (targetHealth != null && targetHealth.IsAlive())
            {
                targetHealth.TakeDamage(mage._attack, AttackType.magic, mage);
            }
            Destroy(gameObject);
        }
    }
}
