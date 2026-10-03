using System;

/// <summary>
/// 选择系统向可选对象传递的视觉状态。
/// 使用 Flags 是为了允许后续组合显示状态，而不让选择流程依赖具体的高亮实现。
/// 创建日期：2026-09-29。
/// </summary>
[Flags]
public enum SelectionVisualState
{
    None = 0,
    Hover = 1 << 0,
    Selected = 1 << 1,
    ValidTarget = 1 << 2,
    InvalidTarget = 1 << 3
}
