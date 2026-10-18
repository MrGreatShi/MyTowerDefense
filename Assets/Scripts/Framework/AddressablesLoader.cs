using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 缓存资源并合并同 key 并发请求，所有调用须在 Unity 主线程。
/// 修改记录：2026-10-10，修复失败释放与清理后的迟到请求回填。
/// </summary>
public sealed class AddressablesLoader<T>
{
    private readonly Dictionary<string, AsyncOperationHandle<T>> _prefabHandles = new();
    private readonly Dictionary<string, Task<AsyncOperationHandle<T>>> _inflight = new();

    /// <summary>取得共享句柄；不得由调用者直接释放句柄。</summary>
    public Task<AsyncOperationHandle<T>> LoadPrefabHandleAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new System.ArgumentException("Addressables key 不能为空。", nameof(key));
        if (_prefabHandles.TryGetValue(key, out var cached) && cached.IsValid() &&
            cached.Status == AsyncOperationStatus.Succeeded && cached.Result != null)
            return Task.FromResult(cached);
        if (_inflight.TryGetValue(key, out var pending)) return pending;
        // 先登记共享任务再启动加载，支持同步完成与回调重入。
        var completion = new TaskCompletionSource<AsyncOperationHandle<T>>();
        _inflight[key] = completion.Task;
        _ = LoadInternalAsync(key, completion);
        return completion.Task;
    }

    private async Task LoadInternalAsync(string key, TaskCompletionSource<AsyncOperationHandle<T>> completion)
    {
        AsyncOperationHandle<T> handle = default;
        bool cached = false;
        System.Exception failure = null;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(key);
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                throw new System.InvalidOperationException($"Addressables load failed: {key}", handle.OperationException);
            if (!_inflight.TryGetValue(key, out var current) || !ReferenceEquals(current, completion.Task))
                throw new System.OperationCanceledException($"Addressables request released: {key}");
            _prefabHandles[key] = handle;
            cached = true;
        }
        catch (System.Exception exception)
        {
            failure = exception;
        }
        finally
        {
            // 包括 await 抛异常的路径，未转交缓存的句柄只释放一次。
            if (!cached && handle.IsValid()) Addressables.Release(handle);
            // 旧请求不得误删清理后建立的新请求。
            if (_inflight.TryGetValue(key, out var current) && ReferenceEquals(current, completion.Task))
                _inflight.Remove(key);
        }
        if (failure is System.OperationCanceledException) completion.TrySetCanceled();
        else if (failure != null) completion.TrySetException(failure);
        else completion.TrySetResult(handle);
    }

    /// <summary>释放指定资源；在途请求返回后自行释放，不再回填。</summary>
    public void Release(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        _inflight.Remove(key);
        if (_prefabHandles.TryGetValue(key, out var handle))
        {
            _prefabHandles.Remove(key);
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }

    /// <summary>释放全部资源，调用前须确保 UI 和实例不再使用。</summary>
    public void ReleaseAll()
    {
        _inflight.Clear();
        var handles = new List<AsyncOperationHandle<T>>(_prefabHandles.Values);
        _prefabHandles.Clear();
        foreach (var handle in handles)
            if (handle.IsValid()) Addressables.Release(handle);
    }
}
