using UnityEngine;
using MyTowerDefense.Player;

/// <summary>
/// 为角色、地块、建筑或技能对象提供统一的可选中适配层。
/// 选择根节点为当前对象，由该组件统一调度高亮与 UI 状态。
/// 创建日期：2026-09-29。
/// 修改记录：2026-10-09，添加 SelectionUIHandler 状态调度，不扩展 ISelectable。
/// </summary>
public sealed class SelectableObject : MonoBehaviour, ISelectable
{
    [SerializeField] private bool isSelectable = true;
    [SerializeField] private SelectionHighlighter highlighter;
    private SelectionUIHandler _selectionUIHandler;
    private SelectionVisualState _visualState;

    public bool IsSelectable => isSelectable && isActiveAndEnabled;
    public Transform SelectionRoot => transform;

    private void Awake()
    {
        if (highlighter == null)
        {
            highlighter = GetComponent<SelectionHighlighter>();
        }
        _selectionUIHandler = GetComponent<SelectionUIHandler>();
    }

    /// <summary>将同一组合状态传给高亮和 UI；UI 组件自行判断 Selected 是否变化。</summary>
    public void SetVisualState(SelectionVisualState state)
    {
        _visualState = state;
        if (highlighter != null)
        {
            highlighter.SetState(state);
        }
        if (_selectionUIHandler != null)
        {
            _selectionUIHandler.SetState(state);
        }
    }

    /// <inheritdoc />
    public void AddVisualState(SelectionVisualState state)
    {
        SetVisualState(_visualState | state);
    }

    /// <inheritdoc />
    public void RemoveVisualState(SelectionVisualState state)
    {
        SetVisualState(_visualState & ~state);
    }

    private void OnDisable()
    {
        SetVisualState(SelectionVisualState.None);
    }
}
