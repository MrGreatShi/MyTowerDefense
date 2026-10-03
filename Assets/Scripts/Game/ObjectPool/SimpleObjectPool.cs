using System;
using System.Collections.Generic;
using UnityEngine;

public class SimpleObjectPool
{
    readonly GameObject prefab;
    readonly Transform poolRoot;
    readonly Queue<GameObject> inactive = new Queue<GameObject>();
    readonly HashSet<GameObject> allObjects = new HashSet<GameObject>();
        
    public SimpleObjectPool(GameObject prefab, Transform poolRoot, int preloadCount = 10)
    {
        if (prefab == null)
            throw new ArgumentNullException("prefab");

        this.prefab = prefab;
        this.poolRoot = poolRoot;
        Preload(preloadCount);
    }

    public void Preload(int count)
    {
        for (int i = 0; i < count; i++)
            Return(CreateNew());
    }

    GameObject CreateNew()
    {
        GameObject go = UnityEngine.Object.Instantiate(prefab, poolRoot);
        go.name = prefab.name;
        allObjects.Add(go);

        PooledObject pooledObject = go.GetComponent<PooledObject>();
        if (pooledObject == null)
            pooledObject = go.AddComponent<PooledObject>();
        pooledObject.pool = this;

        go.SetActive(false);
        return go;
    }

    public GameObject Get(Vector3 position, Quaternion rotation, Transform parent = null)
    {
        GameObject go = null;
        while (inactive.Count > 0 && go == null)
            go = inactive.Dequeue();

        if (go == null)
        {
            go = CreateNew();
        }

        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    public void Return(GameObject go)
    {
        if (go == null || !allObjects.Contains(go))
            return;

        go.SetActive(false);
        go.transform.SetParent(poolRoot, false);
        if (!inactive.Contains(go))
            inactive.Enqueue(go);
    }

    public void Clear()
    {
        foreach (GameObject go in allObjects)
        {
            if (go != null)
                UnityEngine.Object.Destroy(go);
        }

        inactive.Clear();
        allObjects.Clear();
    }
}
