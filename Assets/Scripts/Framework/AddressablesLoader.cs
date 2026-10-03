using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class AddressablesLoader<T>
{
    // key -> handle（prefab 句柄）
    private readonly Dictionary<string, AsyncOperationHandle<T>> _prefabHandles = new();

    // key -> 正在加载的任务（防止并发重复加载）
    private readonly Dictionary<string, Task<AsyncOperationHandle<T>>> _inflight = new();

    public async Task<AsyncOperationHandle<T>> LoadPrefabHandleAsync(string key)
    {
        // 已缓存 handle
        if (_prefabHandles.TryGetValue(key, out var cached) && cached.IsValid())
            return cached;

        // 正在加载中
        if (_inflight.TryGetValue(key, out var t))
            return await t;

        var task = LoadInternalAsync(key);
        _inflight[key] = task;

        try
        {
            var handle = await task;
            _prefabHandles[key] = handle;
            return handle;
        }
        finally
        {
            _inflight.Remove(key);
        }
    }

    private async Task<AsyncOperationHandle<T>> LoadInternalAsync(string key)
    {
        var handle = Addressables.LoadAssetAsync<T>(key);
        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            // 失败就释放句柄，防止残留
            if (handle.IsValid()) Addressables.Release(handle);
            throw new System.Exception($"Addressables load failed: {key}");
        }
        return handle;
    }

    public void Release(string key)
    {
        if (_prefabHandles.TryGetValue(key, out var handle))
        {
            if (handle.IsValid()) Addressables.Release(handle);
            _prefabHandles.Remove(key);
        }
    }

    public void ReleaseAll()
    {
        foreach (var kv in _prefabHandles)
        {
            var handle = kv.Value;
            if (handle.IsValid()) Addressables.Release(handle);
        }
        _prefabHandles.Clear();
        _inflight.Clear();
    }
}