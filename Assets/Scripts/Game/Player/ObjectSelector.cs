using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MyTowerDefense.Game;

/// <summary>
/// 负责鼠标命中检测、悬停高亮和点击事件发布。
/// 第一/第二目标状态与对象操作均由 RunSession 管理，避免选择器持有业务状态。
/// </summary>
public sealed class ObjectSelector : MonoBehaviour
{
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private LayerMask selectableLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float clickInterval = 0.15f;

    private ISelectable _hoveredObject;
    private float _clickCooldown;
    private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();
    private EventSystem _eventSystem;
    private PointerEventData _pointerEventData;

    private void Awake()
    {
        if (selectionCamera == null)
        {
            selectionCamera = Camera.main;
        }

        CacheEventSystem(EventSystem.current);
    }

    private void Update()
    {
        if (selectionCamera == null)
        {
            return;
        }

        if (IsPointerOverGui())
        {
            ClearHoveredObject();
            return;
        }

        _clickCooldown = Mathf.Max(0f, _clickCooldown - Time.deltaTime);
        UpdateHoveredObject();

        if (Input.GetMouseButtonDown(0) && _clickCooldown <= 0f)
        {
            HandleLeftClick();
        }

        if (Input.GetMouseButtonDown(1))
        {
            EventBus.Publish(new ObjectSelectionCancelledEvent());
        }
    }

    private void UpdateHoveredObject()
    {
        ISelectable newHoveredObject = GetSelectableUnderMouse();
        if (ReferenceEquals(newHoveredObject, _hoveredObject))
        {
            return;
        }

        ClearHoveredObject();
        _hoveredObject = newHoveredObject;
        _hoveredObject?.AddVisualState(SelectionVisualState.Hover);
    }

    private ISelectable GetSelectableUnderMouse()
    {
        Ray ray = selectionCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectableLayers, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        SelectableObject selectableObject = hit.collider.GetComponent<SelectableObject>();
        if (selectableObject == null || !selectableObject.IsSelectable || IsFrozenCube(selectableObject.SelectionRoot))
        {
            return null;
        }

        return selectableObject;
    }

    /// <summary>
    /// 冻结状态只限制 Cube 自身的选择，不影响其他可选择对象。
    /// 通过网格缓存查询，避免在每帧输入检测中重复获取组件。
    /// </summary>
    private static bool IsFrozenCube(Transform selectionRoot)
    {
        CubeGridManager gridManager = CubeGridManager.Instance;
        return gridManager != null &&
               gridManager.TryGetCubeOccupancyAtWorldPosition(
                   selectionRoot.position,
                   out Transform cube,
                   out CubeOccupancy occupancy) &&
               cube == selectionRoot &&
               occupancy.IsFrozen;
    }

    private void HandleLeftClick()
    {
        if (_hoveredObject == null)
        {
            EventBus.Publish(new ObjectSelectionCancelledEvent());
            return;
        }
        Debug.Log("Selected object: " + _hoveredObject.SelectionRoot.name);
        EventBus.Publish(new ObjectSelectedEvent(_hoveredObject));
        _clickCooldown = clickInterval;
    }

    /// <summary>
    /// 优先判定 UI 图形射线；仅 GraphicRaycaster 命中会阻止场景物理射线，
    /// 避免场景 PhysicsRaycaster 被误认为 GUI。
    /// </summary>
    private bool IsPointerOverGui()
    {
        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem == null)
        {
            return false;
        }

        if (_eventSystem != currentEventSystem || _pointerEventData == null)
        {
            CacheEventSystem(currentEventSystem);
        }

        _pointerEventData.Reset();
        _pointerEventData.position = Input.mousePosition;
        _uiRaycastResults.Clear();
        _eventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);

        foreach (RaycastResult raycastResult in _uiRaycastResults)
        {
            if (!(raycastResult.module is GraphicRaycaster))
            {
                continue;
            }

            GameObject uiObject = raycastResult.gameObject;
            bool isSelectableControl = uiObject.GetComponentInParent<Selectable>() != null;
            bool hasClickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(uiObject) != null;
            if (isSelectableControl || hasClickHandler)
            {
                return true;
            }
        }

        return false;
    }

    private void CacheEventSystem(EventSystem eventSystem)
    {
        _eventSystem = eventSystem;
        _pointerEventData = _eventSystem == null ? null : new PointerEventData(_eventSystem);
    }

    private void ClearHoveredObject()
    {
        _hoveredObject?.RemoveVisualState(SelectionVisualState.Hover);
        _hoveredObject = null;
    }

    private void OnDisable()
    {
        ClearHoveredObject();
        EventBus.Publish(new ObjectSelectionCancelledEvent());
    }
}
