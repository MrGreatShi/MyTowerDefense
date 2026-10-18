using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MyTowerDefense.Data;
using MyTowerDefense.Util;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 关卡准备界面，展示可选英雄及配置。
/// 修改记录：2026-10-12，列表头像使用 UISpriteUtility，并阻止关闭后的迟到请求。
/// 修改记录：2026-10-12，移除旧兼容入口，统一读取 GameData 的英雄目录。
/// </summary>
public class LevelPrepareWindow : UIWindow
{
    public override string Key => "UI/LevelPrepareWindow";
    public static int HeroNum = 5;
    public LevelHeroInfoBar[] heroInfoBar = new LevelHeroInfoBar[HeroNum];
    private string[] heroes = new string[HeroNum];
    private int[] talents = new int[HeroNum];
    private int[] equipments = new int[HeroNum];
    public TextMeshProUGUI levelName;
    [SerializeField] private GameObject itemPrefab;
    public Transform selectionList;
    public Button BattleBtn;
    public Button ExitBtn;

    // 生命周期取消仅影响本窗口，不影响其他 UI 使用的共享图片。
    private CancellationTokenSource _spriteLoadCancellation;

    /// <summary>加载英雄列表图片；请求仍有效时继续执行原有界面初始化。</summary>
    public override async void OnShow(object args)
    {
        CancelSpriteLoading();
        _spriteLoadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _spriteLoadCancellation.Token;

        try
        {
            await LoadSelectionList(cancellationToken);
            // await 后可能已关闭、销毁或重新打开，不得再执行旧一轮初始化。
            if (this == null || cancellationToken.IsCancellationRequested ||
                !isActiveAndEnabled || !IsVisible)
            {
                return;
            }

            InitHeroInfo();
            InitHeroInfoBars();
            BattleBtn.onClick.AddListener(OnBattleBtnClick);
        }
        catch (OperationCanceledException)
        {
            // 关闭或切换窗口是正常取消。
        }
        catch (Exception exception)
        {
            // 防止 async void 回调把异常抛到 Unity 同步上下文。
            if (this != null && !cancellationToken.IsCancellationRequested)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    /// <summary>取消当前窗口的图片赋值，不清理公共资源缓存。</summary>
    public override void OnHide()
    {
        CancelSpriteLoading();
        BattleBtn.onClick.RemoveListener(OnBattleBtnClick);
    }

    /// <summary>GUIManager 销毁窗口时取消未完成的换图请求。</summary>
    public override void OnDestroyWindow()
    {
        CancelSpriteLoading();
    }

    private void OnDisable()
    {
        CancelSpriteLoading();
    }

    private void OnDestroy()
    {
        CancelSpriteLoading();
    }

    private void CancelSpriteLoading()
    {
        CancellationTokenSource cancellation = _spriteLoadCancellation;
        _spriteLoadCancellation = null;
        if (cancellation == null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }
    public void OnHeroInfoBarClick(int index)
    {
        if (index < 0 || index >= heroInfoBar.Length)
        {
            Debug.LogWarning($"OnHeroInfoBarClick: 索引 {index} 越界，范围应在 [0, {heroInfoBar.Length}) 之间。", this);
            return;
        }
        string hero = heroes[index];
        int talent = talents[index];
        int equipment = equipments[index];
        GUIManager.Instance.ShowAsync<HeroDetailWindow>("UI/HeroDetailWindow", new HeroDetailArgs
        (
            hero,
            talent,
            equipment
        ));
    }
    public async void OnBattleBtnClick()
    {
        await SceneTransitionManager.Instance.SwitchSceneAsync("LevelScene");
    }
    public void OnExitBtnClick()
    {
    }
    private async Task LoadSelectionList(CancellationToken cancellationToken)
    {
        foreach(string hero in GameData.Heroes)
        {
            // 检查放在生成列表项之前，关闭窗口后不再生成后续项目。
            if (this == null || cancellationToken.IsCancellationRequested ||
                !isActiveAndEnabled || !IsVisible)
            {
                return;
            }

            GameObject item = Instantiate(itemPrefab, selectionList);
            Image image = item.GetComponent<Image>();
            await image.SetSpriteAsync(hero + "_Portrait", cancellationToken: cancellationToken);
        }
    }
    private void InitHeroInfo() 
    {
        IReadOnlyList<string> heroCatalog = GameData.Heroes;
        if (heroCatalog.Count == 0)
        {
            Debug.LogWarning("英雄目录为空，已跳过随机英雄初始化，请检查 GameCatalogConfig。", this);
            return;
        }

        for(int i = 0; i < HeroNum; i++)
        {
            // 整数 Range 的上限不包含在结果中；从全局目录取值，而不是本地阵容数组。
            heroes[i] = heroCatalog[UnityEngine.Random.Range(0, heroCatalog.Count)];
            talents[i] = UnityEngine.Random.Range(0, 15);
            equipments[i] = UnityEngine.Random.Range(1, 4);
            Debug.Log($"随机英雄 {i}: {heroes[i]}，天赋 {talents[i]}，装备 {equipments[i]}", this);
        }
    }
    private void InitHeroInfoBars()
    {
        for (int i = 0; i < heroInfoBar.Length; i++)
        {
            heroInfoBar[i].ShowInfo(heroes[i], talents[i], equipments[i]);
        }
    }
    public void UpdateHeroInfoBar(int i, int talent, int equipment)
    {
        if (i < 0 || i >= heroInfoBar.Length)
        {
            Debug.LogWarning($"UpdateHeroInfoBar: 索引 {i} 越界，范围应在 [0, {heroInfoBar.Length}) 之间。", this);
            return;
        }
        heroInfoBar[i].ShowInfo(heroes[i], talent, equipment);
    }
}
