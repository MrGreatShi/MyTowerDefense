using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 全局共享 Sprite 加载入口，无需挂载；窗口关闭不释放公共缓存。
/// 修改记录：2026-10-10，限制单例创建、检查参数、增加统一清理及退出释放。
/// 所有方法在 Unity 主线程调用。
/// </summary>
public sealed class SpriteLoader
{
    private static SpriteLoader instance;
    public static SpriteLoader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new SpriteLoader();
            }
            return instance;
        }
    }
    private readonly AddressablesLoader<Sprite> loader = new AddressablesLoader<Sprite>();
    private SpriteLoader() { }

    /// <summary>按 Address 获取共享图片；底层合并同地址的请求。</summary>
    public async Task<Sprite> LoadAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Sprite Address 不能为空。", nameof(key));
        var handle = await loader.LoadPrefabHandleAsync(key);
        return handle.Result;
    }

    /// <summary>清理公共缓存；须先清空所有使用这些图片的 UI 引用。</summary>
    public void ClearCache()
    {
        loader.ReleaseAll();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        // 兼容关闭 Domain Reload，旧加载结果不能污染本轮缓存。
        instance?.ClearCache();
        instance = null;
        Application.quitting -= OnApplicationQuitting;
        Application.quitting += OnApplicationQuitting;
    }

    private static void OnApplicationQuitting()
    {
        instance?.ClearCache();
        instance = null;
    }
}
