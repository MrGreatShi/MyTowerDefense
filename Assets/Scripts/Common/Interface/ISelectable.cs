using UnityEngine;

/// <summary>
/// 定义可被玩家选择的对象。
/// 选择器只依赖此接口，因此角色、地块、建筑和技能均可采用相同的选中流程。
/// 创建日期：2026-09-29。
/// </summary>
public interface ISelectable
{
    /// <summary>对象当前是否允许被选择。</summary>
    bool IsSelectable { get; }
    /// <summary>选择器在子 Collider 命中时会回到此根节点，作为选择的目标对象。
    Transform SelectionRoot { get; }
    /// <summary>更新由选择系统驱动的视觉状态。</summary>
    void SetVisualState(SelectionVisualState state);

    /// <summary>叠加视觉状态，不影响其他状态标记。</summary>
    void AddVisualState(SelectionVisualState state);

    /// <summary>移除指定视觉状态，保留其余状态标记。</summary>
    void RemoveVisualState(SelectionVisualState state);
}
