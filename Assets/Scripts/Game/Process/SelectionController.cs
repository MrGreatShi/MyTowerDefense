using System;
using UnityEngine;

namespace MyTowerDefense.Game
{
    /// <summary>
    /// 接收点击事件，管理第一目标、第二目标操作和选择状态，不依赖对象类型及 UI。
    /// </summary>
    /// <remarks>
    /// 模块：Game；创建日期：2026-10-08。
    /// 修改记录：从 RunSession 拆出选择逻辑，使关卡流程不再管理选择状态。
    /// 2026-10-09：UI 改由 SelectableObject 的状态调度，移除英雄及面板依赖。
    /// 场景中应只启用一个控制器；挂载后无需绑定 RunSession。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SelectionController : MonoBehaviour
    {
        private ISelectable _selectedObject;
        private ISelectionActionSource _selectedActionSource;

        private void OnEnable()
        {
            EventBus.Subscribe<ObjectSelectedEvent>(OnObjectSelected);
            EventBus.Subscribe<ObjectSelectionCancelledEvent>(OnObjectSelectionCancelled);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ObjectSelectedEvent>(OnObjectSelected);
            EventBus.Unsubscribe<ObjectSelectionCancelledEvent>(OnObjectSelectionCancelled);
            ClearSelection();
        }

        private void Update()
        {
            // 接口引用不会自动执行 Unity 的已销毁对象判断，访问其属性前先检查底层对象。
            if (!ReferenceEquals(_selectedObject, null) && !IsValid(_selectedObject))
            {
                ClearSelection();
            }
        }

        private static bool IsValid(ISelectable selectable)
        {
            return selectable != null &&
                (!(selectable is UnityEngine.Object unityObject) || unityObject != null) &&
                selectable.SelectionRoot != null;
        }

        private void OnObjectSelected(ObjectSelectedEvent selectionEvent)
        {
            if (selectionEvent == null || !IsValid(selectionEvent.Selectable))
            {
                return;
            }

            try
            {
                bool validActionSource = _selectedActionSource != null &&
                    (!(_selectedActionSource is UnityEngine.Object sourceObject) || sourceObject != null);
                if (IsValid(_selectedObject) && validActionSource &&
                    _selectedActionSource.CanOperate(selectionEvent.Selectable))
                {
                    // 操作可能同步发布取消选择事件，清理操作保持幂等。
                    _selectedActionSource.Operate(selectionEvent.Selectable);
                    ClearSelection();
                    return;
                }

                ClearSelection();
                SelectObject(selectionEvent.Selectable);
            }
            catch (Exception exception)
            {
                ClearSelection();
                Debug.LogException(exception, this);
            }
        }

        private void OnObjectSelectionCancelled(ObjectSelectionCancelledEvent cancelEvent)
        {
            ClearSelection();
        }

        private void SelectObject(ISelectable selectable)
        {
            if (!IsValid(selectable)) return;
            _selectedObject = selectable;
            _selectedObject.AddVisualState(SelectionVisualState.Selected);
            Transform root = selectable.SelectionRoot;
            root.TryGetComponent(out _selectedActionSource);
        }

        /// <summary>清除选择及操作源；状态变化由对象自身调度高亮与 UI。</summary>
        public void ClearSelection()
        {
            ISelectable previousObject = _selectedObject;
            _selectedObject = null;
            _selectedActionSource = null;

            if (IsValid(previousObject))
            {
                previousObject.RemoveVisualState(SelectionVisualState.Selected);
            }
        }

    }
}
