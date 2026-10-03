using UnityEngine;

/// <summary>
/// 为角色、地块、建筑或技能对象提供统一的可选中适配层。
/// 子 Collider 命中时，ObjectSelector 会通过该组件回到配置的选择根节点。
/// 创建日期：2026-09-29。
/// </summary>
public sealed class SelectableObject : MonoBehaviour, ISelectable
{
    [SerializeField] private bool isSelectable = true;
    [SerializeField] private SelectionHighlighter highlighter;
    private SelectionVisualState _visualState;

    public bool IsSelectable => isSelectable && isActiveAndEnabled;
    public Transform SelectionRoot => transform;

    private void Awake()
    {
        if (highlighter == null)
        {
            highlighter = GetComponent<SelectionHighlighter>();
        }
    }

    /// <summary>把状态委托给可替换的高亮表现组件。</summary>
    public void SetVisualState(SelectionVisualState state)
    {
        _visualState = state;
        if (highlighter != null)
        {
            highlighter.SetState(state);
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
