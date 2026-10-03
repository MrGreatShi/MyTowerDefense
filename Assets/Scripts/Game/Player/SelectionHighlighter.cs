using UnityEngine;

/// <summary>
/// 负责把选择状态映射为 Renderer 的颜色与自发光属性。
/// 使用 MaterialPropertyBlock，避免鼠标悬停时反复实例化和销毁材质。
/// 创建日期：2026-09-29。
/// </summary>
public sealed class SelectionHighlighter : MonoBehaviour
{
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private Color hoverColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color validTargetColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color invalidTargetColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField, Range(0f, 1f)] private float colorBlend = 0.45f;
    [SerializeField, Range(0f, 2f)] private float emissionIntensity = 0.6f;

    private MaterialPropertyBlock _propertyBlock;
    private SelectionVisualState _currentState;
    private bool _isFlashActive;
    private int _flashEventId = -1;

    private void Awake()
    {
        // Unity 原生对象必须在组件生命周期内创建，不能放在字段初始化器中。
        _propertyBlock = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    /// <summary>应用选择视觉状态；相同状态不重复写入 Renderer。</summary>
    public void SetState(SelectionVisualState state)
    {
        if (_currentState == state)
        {
            return;
        }

        _currentState = state;
        if (_isFlashActive)
        {
            return;
        }

        ApplyVisual(GetHighlightColor(state), state != SelectionVisualState.None);
    }

    /// <summary>
    /// 临时覆盖当前选择颜色；闪烁结束后恢复闪烁期间最新的选择状态。
    /// </summary>
    public void Flash(Color color, float duration)
    {
        if (_propertyBlock == null)
        {
            return;
        }

        if (_flashEventId >= 0)
        {
            TimeEventBus.Cancel(_flashEventId);
        }

        _isFlashActive = true;
        ApplyVisual(color, true);
        _flashEventId = TimeEventBus.Schedule(Mathf.Max(0f, duration), EndFlash);
    }

    private void EndFlash()
    {
        _flashEventId = -1;
        _isFlashActive = false;
        ApplyVisual(GetHighlightColor(_currentState), _currentState != SelectionVisualState.None);
    }

    private void ApplyVisual(Color highlightColor, bool shouldHighlight)
    {

        foreach (Renderer targetRenderer in targetRenderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                if (!shouldHighlight)
                {
                    targetRenderer.SetPropertyBlock(null, materialIndex);
                    continue;
                }

                targetRenderer.GetPropertyBlock(_propertyBlock, materialIndex);
                ApplyColorProperties(material, highlightColor);
                targetRenderer.SetPropertyBlock(_propertyBlock, materialIndex);
            }
        }
    }

    private void ApplyColorProperties(Material material, Color highlightColor)
    {
        if (material.HasProperty("_BaseColor"))
        {
            _propertyBlock.SetColor("_BaseColor", Color.Lerp(material.GetColor("_BaseColor"), highlightColor, colorBlend));
        }

        if (material.HasProperty("_Color"))
        {
            _propertyBlock.SetColor("_Color", Color.Lerp(material.color, highlightColor, colorBlend));
        }

        if (material.HasProperty("_EmissionColor"))
        {
            _propertyBlock.SetColor("_EmissionColor", highlightColor * emissionIntensity);
        }
    }

    private Color GetHighlightColor(SelectionVisualState state)
    {
        if ((state & SelectionVisualState.Selected) != 0)
        {
            return selectedColor;
        }

        if ((state & SelectionVisualState.ValidTarget) != 0)
        {
            return validTargetColor;
        }

        if ((state & SelectionVisualState.InvalidTarget) != 0)
        {
            return invalidTargetColor;
        }

        return hoverColor;
    }

    private void OnDisable()
    {
        if (_flashEventId >= 0)
        {
            TimeEventBus.Cancel(_flashEventId);
            _flashEventId = -1;
        }

        _isFlashActive = false;
        _currentState = SelectionVisualState.None;
        ApplyVisual(Color.clear, false);
    }
}
