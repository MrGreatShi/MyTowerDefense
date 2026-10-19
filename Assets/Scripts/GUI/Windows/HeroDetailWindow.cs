using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using MyTowerDefense.Managers;
using MyTowerDefense.Util;

/// <summary>英雄详情的展示参数；heroName 保留原接口名称，实际传入资源标识。</summary>
public struct HeroDetailArgs
{
    public string heroName;
    public int talentState;
    public int equipment;
    /// <summary>创建详情参数，heroName 例如 Knight，不是中文显示名。</summary>
    public HeroDetailArgs(string heroName, int talentState, int equipment)
    {
        this.heroName = heroName;
        this.talentState = talentState;
        this.equipment = equipment;
    }
}
/// <summary>
/// 显示英雄共享配置的基础属性，不创建战斗实例或修改 SO。
/// 修改记录：2026-10-18，统一配置入口，并按窗口生命周期取消过期数据及图标赋值。
/// </summary>
public class HeroDetailWindow : UIWindow
{
    public override string Key => "UI/HeroDetailWindow";
    [Header("英雄设置")]
    public TextMeshProUGUI heroName;
    public Image portrait;
    public TextMeshProUGUI health;
    public TextMeshProUGUI attack;
    public TextMeshProUGUI attackRange;
    public TextMeshProUGUI physicalDefense;
    public TextMeshProUGUI magicDefense;
    public TextMeshProUGUI speed;
    [Header("技能图标")]
    public Image passiveSkillIcon;
    public Image activeSkillIcon;
    public Image ultimateSkillIcon;
    [Header("天赋")]
    public Button[] talentButtons_0;
    public Button[] talentButtons_1;
    public Button[] talentButtons_2;
    public Button[] talentButtons_3;
    [Header("装备信息")]
    public Image equipmentIcon;
    public TextMeshProUGUI equipmentDescription;
    [Header("装备备选栏")]
    [SerializeField] private GameObject itemPrefab;
    public Transform selectionList;
    // 只控制本窗口的结果交付；不能取消或释放其他界面共用的资源加载。
    private CancellationTokenSource _initializationCancellation;

    /// <summary>显示或切换英雄；async void 仅用于框架回调，所有加载异常在此处理。</summary>
    public override async void OnShow(object args)
    {
        CancelInitialization();
        ClearDetails();
        if (!(args is HeroDetailArgs heroDetailArgs))
        {
            SetText(heroName, "参数错误");
            Debug.LogWarning("HeroDetailWindow: 参数类型不匹配，无法初始化窗口。", this);
            return;
        }

        SetText(heroName, "加载中…");
        _initializationCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _initializationCancellation.Token;
        try
        {
            await InitAsync(heroDetailArgs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 窗口关闭或切换是正常取消；Manager 本身失效时也不能保留上一位英雄的数据。
            if (this != null && isActiveAndEnabled && !cancellationToken.IsCancellationRequested)
            {
                ClearDetails();
                SetText(heroName, "加载已取消");
            }
        }
        catch (Exception exception)
        {
            if (this != null && isActiveAndEnabled && !cancellationToken.IsCancellationRequested)
            {
                ClearDetails();
                SetText(heroName, "加载失败");
                Debug.LogException(exception, this);
            }
        }
    }

    private async Task InitAsync(HeroDetailArgs args, CancellationToken cancellationToken)
    {
        GameDataManager dataManager = GameDataManager.Instance;
        if (dataManager == null)
        {
            throw new InvalidOperationException("英雄详情加载失败：Core 中没有有效的 GameDataManager。");
        }

        // 配置保存在本次请求的局部变量，避免等待图标期间被另一位英雄的请求替换。
        HeroSettingConfig config = await dataManager.GetHeroConfigAsync(args.heroName, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        SetText(heroName, string.IsNullOrWhiteSpace(config.heroName) ? args.heroName : config.heroName);
        SetText(health, config.health.ToString());
        SetText(attack, config.attack.ToString());
        SetText(attackRange, config.attackRange.ToString());
        SetText(physicalDefense, config.physicalDefense.ToString());
        SetText(magicDefense, config.magicDefense.ToString());
        SetText(speed, config.speed.ToString());

        // 各图片独立失败不会阻止属性展示；全部使用本轮 Token，旧结果不能覆盖新界面。
        await Task.WhenAll(
            portrait.SetSpriteAsync(args.heroName + "_Portrait", cancellationToken: cancellationToken),
            passiveSkillIcon.SetSpriteAsync(args.heroName + "_PassiveSkill", cancellationToken: cancellationToken),
            activeSkillIcon.SetSpriteAsync(args.heroName + "_ActiveSkill", cancellationToken: cancellationToken),
            ultimateSkillIcon.SetSpriteAsync(args.heroName + "_Ultimate", cancellationToken: cancellationToken),
            equipmentIcon.SetSpriteAsync("Equipment_" + args.equipment, cancellationToken: cancellationToken));
    }

    /// <summary>窗口隐藏时停止本次赋值，不释放全局配置或图标缓存。</summary>
    public override void OnHide()
    {
        CancelInitialization();
    }

    /// <summary>窗口框架销毁回调；也兼容直接销毁 GameObject 的路径。</summary>
    public override void OnDestroyWindow()
    {
        CancelInitialization();
    }

    private void OnDisable()
    {
        CancelInitialization();
    }

    private void OnDestroy()
    {
        CancelInitialization();
    }

    private void CancelInitialization()
    {
        if (_initializationCancellation == null)
        {
            return;
        }

        _initializationCancellation.Cancel();
        _initializationCancellation.Dispose();
        _initializationCancellation = null;
    }

    private void ClearDetails()
    {
        SetText(heroName, string.Empty);
        SetText(health, string.Empty);
        SetText(attack, string.Empty);
        SetText(attackRange, string.Empty);
        SetText(physicalDefense, string.Empty);
        SetText(magicDefense, string.Empty);
        SetText(speed, string.Empty);
        SetText(equipmentDescription, string.Empty);
        portrait.SetSprite(null);
        passiveSkillIcon.SetSprite(null);
        activeSkillIcon.SetSprite(null);
        ultimateSkillIcon.SetSprite(null);
        equipmentIcon.SetSprite(null);
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }
}
