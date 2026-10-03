using System.Collections;
using UnityEngine;

public class PooledObject : MonoBehaviour
{
    [HideInInspector]
    public SimpleObjectPool pool;

    public void Despawn(float delay = 0f)
    {
        if (delay <= 0f)
        {
            ReturnToPool();
            return;
        }
        StartCoroutine(DespawnAfterDelay(delay));
    }

    private void ReturnToPool()
    {
        if (pool != null) pool.Return(gameObject);
        else gameObject.SetActive(false);
    }

    private IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }
}
