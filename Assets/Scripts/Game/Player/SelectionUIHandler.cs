using System;
using UnityEngine;

namespace MyTowerDefense.Player
{
    /// <summary>
    /// Player 模块：由 SelectableObject 传入选择状态，根据对象标签更新对应 UI。
    /// 创建日期：2026-10-09。
    /// 修改记录：从 SelectionController 迁移 UI 调度；不缓存角色组件，不修改选择接口。
    /// 2026-10-09：不保存 GUIManager 引用，显示及清理统一访问当前 Instance。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionUIHandler : MonoBehaviour
    {
        private const string HeroWindowKey = "UI/LevelHeroWindow";
        private SelectionVisualState _state;

        // 所有对象共享一个选择面板调度器，避免多个组件同时创建相同窗口。
        private static SelectionUIHandler _currentOwner;
        private static int _requestVersion;
        private static bool _isRefreshing;
        private static string _shownWindowKey;
        private static int _lifecycleVersion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            // 支持关闭 Domain Reload；上一轮游戏的异步任务不能修改本轮共享状态。
            _lifecycleVersion++;
            _currentOwner = null;
            _requestVersion = 0;
            _isRefreshing = false;
            _shownWindowKey = null;
        }

        /// <summary>
        /// 保存完整状态，仅在 Selected 标记变化时开关面板；悬浮和受击不影响 UI。
        /// </summary>
        public void SetState(SelectionVisualState state)
        {
            bool wasSelected = (_state & SelectionVisualState.Selected) != 0;
            // 禁用组件不接受新的选中请求，但仍可以接受清理通知。
            _state = isActiveAndEnabled ? state : state & ~SelectionVisualState.Selected;
            bool isSelected = (_state & SelectionVisualState.Selected) != 0;
            if (wasSelected == isSelected) return;

            if (isSelected)
            {
                _currentOwner = this;
                _requestVersion++;
                HideCurrentPanel();
                RefreshPanelAsync();
            }
            else if (ReferenceEquals(_currentOwner, this))
            {
                // 旧对象的迟到取消通知不能关闭新对象的面板。
                _currentOwner = null;
                _requestVersion++;
                HideCurrentPanel();
            }
        }

        private static bool IsOwnerValid(SelectionUIHandler owner)
        {
            return owner != null && owner.isActiveAndEnabled &&
                (owner._state & SelectionVisualState.Selected) != 0;
        }

        private static void HideCurrentPanel()
        {
            string key = _shownWindowKey;
            _shownWindowKey = null;
            if (GUIManager.Instance != null && !string.IsNullOrEmpty(key))
            {
                GUIManager.Instance.Hide(key);
            }
        }

        private static async void RefreshPanelAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            int lifecycle = _lifecycleVersion;
            try
            {
                while (lifecycle == _lifecycleVersion && IsOwnerValid(_currentOwner))
                {
                    SelectionUIHandler owner = _currentOwner;
                    int version = _requestVersion;
                    if (GUIManager.Instance == null)
                    {
                        Debug.LogWarning("SelectionUIHandler 无法显示 UI：GUIManager 不存在。", owner);
                        return;
                    }

                    // 项目尚无敌人面板，不虚构 Address；后续在这里增加 Enemy 等标签分支。
                    switch (owner.gameObject.tag)
                    {
                        case "Hero":
                            // 仅在实际显示时获取，HeroBase 不作为组件字段持续保存。
                            if (!owner.TryGetComponent(out HeroBase hero))
                            {
                                Debug.LogWarning("Hero 标签对象缺少 HeroBase，无法显示英雄面板。", owner);
                                return;
                            }
                            _shownWindowKey = HeroWindowKey;
                            try
                            {
                                await GUIManager.Instance.ShowAsync<LevelHeroWindow>(HeroWindowKey, hero);
                            }
                            catch (Exception exception)
                            {
                                if (lifecycle != _lifecycleVersion) return;
                                HideCurrentPanel();
                                Debug.LogException(exception);
                                if (version != _requestVersion) continue;
                                return;
                            }
                            break;
                        case "Enemy":
                        default:
                            return;
                    }

                    if (lifecycle != _lifecycleVersion) return;
                    if (version == _requestVersion && ReferenceEquals(owner, _currentOwner) && IsOwnerValid(owner))
                        return;

                    // ShowAsync 本身会显示窗口，因此迟到结果必须立即隐藏，再处理最新选择。
                    if (GUIManager.Instance != null) GUIManager.Instance.Hide(HeroWindowKey);
                    HideCurrentPanel();
                }
            }
            catch (Exception exception)
            {
                if (lifecycle == _lifecycleVersion) HideCurrentPanel();
                Debug.LogException(exception);
            }
            finally
            {
                if (lifecycle == _lifecycleVersion) _isRefreshing = false;
            }
        }

        private void OnDisable()
        {
            SetState(SelectionVisualState.None);
        }

        private void OnDestroy()
        {
            SetState(SelectionVisualState.None);
        }
    }
}
