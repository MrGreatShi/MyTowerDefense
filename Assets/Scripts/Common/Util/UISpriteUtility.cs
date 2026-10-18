// <summary>
// 职责：提供 Image、Button 的统一 Sprite 更换入口，复用 SpriteLoader 共享资源。
// 模块：MyTowerDefense.Util；创建日期：2026-10-11。
// 修改记录：2026-10-11，新增同步、异步换图及迟到请求保护。
// </summary>
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MyTowerDefense.Util
{
    /// <summary>
    /// UI 换图扩展方法。所有方法须在 Unity 主线程调用；无需挂载组件。
    /// 只修改 Image.sprite，不修改 overrideSprite 或 Button 的 SpriteSwap 状态图。
    /// </summary>
    public static class UISpriteUtility
    {
        // 弱引用键避免静态表长期持有已经销毁的 UI 包装对象。
        private static readonly ConditionalWeakTable<Image, RequestState> _requestStates =
            new ConditionalWeakTable<Image, RequestState>();

        /// <summary>
        /// 直接更换图片；sprite 为空时清空图片，同时使该 Image 的旧异步请求失效。
        /// </summary>
        /// <param name="image">目标 Image。</param>
        /// <param name="sprite">要显示的 Sprite，允许为空。</param>
        /// <param name="setNativeSize">是否改为图片原始尺寸，默认保留 UI 尺寸。</param>
        /// <returns>目标有效且完成赋值时返回 true。</returns>
        public static bool SetSprite(this Image image, Sprite sprite, bool setNativeSize = false)
        {
            if (image == null)
            {
                return false;
            }

            RequestState state = _requestStates.GetValue(image, CreateRequestState);
            unchecked
            {
                state.Version++;
            }

            AssignSprite(image, sprite, setNativeSize);
            return true;
        }

        /// <summary>
        /// 更换 Button.targetGraphic 的基础图片；目标不是 Image 时返回 false。
        /// 若需更换按钮内独立图标，请直接对该图标的 Image 调用 SetSprite。
        /// </summary>
        /// <param name="button">目标 Button。</param>
        /// <param name="sprite">要显示的 Sprite，允许为空。</param>
        /// <param name="setNativeSize">是否改为图片原始尺寸。</param>
        /// <returns>目标有效且完成赋值时返回 true。</returns>
        public static bool SetSprite(this Button button, Sprite sprite, bool setNativeSize = false)
        {
            if (button == null)
            {
                return false;
            }

            return SetSprite(button.targetGraphic as Image, sprite, setNativeSize);
        }

        /// <summary>
        /// 按 Address 异步换图。同一 Image 只接受最后一次请求的结果。
        /// 无效地址、加载失败、目标销毁或取消时保留当前图片并返回 false。
        /// </summary>
        /// <param name="image">目标 Image。</param>
        /// <param name="address">Addressables 中 Sprite 的完整 Address。</param>
        /// <param name="setNativeSize">是否改为图片原始尺寸。</param>
        /// <param name="cancellationToken">仅取消本次赋值，不取消或释放公共资源加载。</param>
        /// <returns>本次请求实际完成赋值时返回 true；被新请求替代时返回 false。</returns>
        public static Task<bool> SetSpriteAsync(
            this Image image,
            string address,
            bool setNativeSize = false,
            CancellationToken cancellationToken = default)
        {
            return LoadAndAssignAsync(image, null, address, setNativeSize, cancellationToken);
        }

        /// <summary>
        /// 按 Address 异步更换 Button.targetGraphic 的基础图片。
        /// 加载期间 Button 销毁或更换 targetGraphic 时，不向旧 Image 写入结果。
        /// </summary>
        /// <param name="button">目标 Button。</param>
        /// <param name="address">Addressables 中 Sprite 的完整 Address。</param>
        /// <param name="setNativeSize">是否改为图片原始尺寸。</param>
        /// <param name="cancellationToken">仅取消本次赋值，不取消或释放公共资源加载。</param>
        /// <returns>本次请求实际完成赋值时返回 true，否则返回 false。</returns>
        public static Task<bool> SetSpriteAsync(
            this Button button,
            string address,
            bool setNativeSize = false,
            CancellationToken cancellationToken = default)
        {
            if (button == null)
            {
                return Task.FromResult(false);
            }

            return LoadAndAssignAsync(
                button.targetGraphic as Image, button, address, setNativeSize, cancellationToken);
        }

        private static async Task<bool> LoadAndAssignAsync(
            Image image,
            Button owner,
            string address,
            bool setNativeSize,
            CancellationToken cancellationToken)
        {
            if (image == null)
            {
                return false;
            }

            // 先登记版本，再校验参数；最新请求即使无效，也不能被旧结果反向覆盖。
            RequestState state = _requestStates.GetValue(image, CreateRequestState);
            long version;
            unchecked
            {
                version = ++state.Version;
            }

            if (string.IsNullOrWhiteSpace(address) || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            // 不用 Unity 的 == 判定是否传入 owner，否则销毁后的 Button 会被当成未传入。
            bool hasOwner = !ReferenceEquals(owner, null);

            try
            {
                // 保留 Unity 同步上下文。共享缓存的加载不能由单个 UI 请求提前释放。
                Sprite sprite = await SpriteLoader.Instance.LoadAsync(address);

                if (image == null || cancellationToken.IsCancellationRequested || state.Version != version)
                {
                    return false;
                }

                if (hasOwner && (owner == null || owner.targetGraphic != image))
                {
                    return false;
                }

                if (sprite == null)
                {
                    return false;
                }

                // 不能调用公开 SetSprite：它会再次递增版本，干扰本次请求的状态。
                AssignSprite(image, sprite, setNativeSize);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UISpriteUtility] Sprite 换图失败，Address='{address}'。\n{exception}");
                return false;
            }
        }

        private static RequestState CreateRequestState(Image image)
        {
            return new RequestState();
        }

        private static void AssignSprite(Image image, Sprite sprite, bool setNativeSize)
        {
            image.sprite = sprite;
            if (setNativeSize && sprite != null)
            {
                image.SetNativeSize();
            }
        }

        private sealed class RequestState
        {
            // 同一 Image 的请求编号，仅用于比较先后顺序，无时间单位。
            public long Version;
        }
    }
}
