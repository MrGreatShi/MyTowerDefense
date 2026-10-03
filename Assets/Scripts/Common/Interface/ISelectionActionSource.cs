using System.Threading.Tasks;

/// <summary>
/// 定义已选中的第一对象如何校验并操作第二对象。
/// 具体对象自行决定交互规则，ObjectSelector 不再依赖 Tag 或职业分支。
/// 创建日期：2026-09-29。
/// </summary>
public interface ISelectionActionSource
{
    /// <summary>判断 target 是否是当前对象可操作的目标。</summary>
    bool CanOperate(ISelectable target);

    /// <summary>对已通过校验的 target 执行操作。</summary>
    void Operate(ISelectable target);
}
