/// <summary>
/// 表示选择器选中了一个对象。
/// RunSession 根据自身保存的 ISelectionActionSource 判断本次点击是第一目标还是第二目标。
/// 创建日期：2026-09-29。
/// </summary>
public sealed class ObjectSelectedEvent
{
    public ISelectable Selectable { get; }
    public ObjectSelectedEvent(ISelectable selectable)
    {
        Selectable = selectable;
    }
}
