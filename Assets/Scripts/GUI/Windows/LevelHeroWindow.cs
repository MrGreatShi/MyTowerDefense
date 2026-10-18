using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;
using System.Threading;
using System.Threading.Tasks;
using MyTowerDefense.Util;

/// <summary>
/// 显示当前英雄状态与技能操作 UI。
/// 修改记录：2026-10-12，统一使用 UISpriteUtility，并按窗口生命周期取消换图请求。
/// </summary>
public class LevelHeroWindow : UIWindow
{
    public override string Key => "UI/LevelHeroWindow";
    public HeroBase heroBase;
    public Image portrait;
    public Image healthBar;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI phsicalDefenseText;
    public TextMeshProUGUI magicDefenseText;
    public Image passiveSkillLabel;
    public Image passiveSkillMask;
    public Button passiveSkillUpgradeBtn;
    public Button activeSkillLabel;
    public Image activeSkillMask;
    public Button activeSkillUpgradeBtn;
    public Button ultimateLabel;
    public Image ultimateMask;
    public Button ultimateUpgradeBtn;
    // 只控制本轮 UI 赋值，不取消或释放 SpriteLoader 的共享资源加载。
    private CancellationTokenSource _spriteLoadCancellation;

    /// <summary>切换英雄后，并发加载头像和三个技能图标。</summary>
    public override async void OnShow(object args)
    {
        CancelSpriteLoading();
        heroBase = args as HeroBase;
        portrait.SetSprite(null);
        passiveSkillLabel.SetSprite(null);
        activeSkillLabel.SetSprite(null);
        ultimateLabel.SetSprite(null);

        if (heroBase == null)
        {
            return;
        }

        _spriteLoadCancellation = new CancellationTokenSource();
        // 捕获本轮 Token，旧请求结束时不能读取新一轮的取消源。
        CancellationToken cancellationToken = _spriteLoadCancellation.Token;
        string key = heroBase.key;
        if (speedText != null)
        {
            speedText.text = heroBase.defaultMoveSpeed.ToString();
        }

        try
        {
            // 工具类检查迟到请求和目标销毁；单张失败不阻止其他图片显示。
            await Task.WhenAll(
                portrait.SetSpriteAsync(key + "_Portrait", cancellationToken: cancellationToken),
                passiveSkillLabel.SetSpriteAsync(key + "_PassiveSkill", cancellationToken: cancellationToken),
                activeSkillLabel.SetSpriteAsync(key + "_ActiveSkill", cancellationToken: cancellationToken),
                ultimateLabel.SetSpriteAsync(key + "_Ultimate", cancellationToken: cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // 窗口关闭或缓存清理是正常取消，不作为未处理异常。
        }
        catch (Exception exception)
        {
            if (this != null && !cancellationToken.IsCancellationRequested)
            {
                Debug.LogException(exception, this);
            }
        }
    }
    /// <summary>
    /// 更新英雄状态UI
    /// </summary>
    private void Update()
    {
        if(heroBase == null) return;
        healthBar.fillAmount = (float)heroBase.health / heroBase._maxHealth;
        healthText.text = $"{heroBase.health}/{heroBase._maxHealth}";
        attackText.text = heroBase._attack.ToString();
        phsicalDefenseText.text = heroBase._physicalDefense.ToString();
        magicDefenseText.text = heroBase._magicDefense.ToString();
        passiveSkillMask.fillAmount = heroBase.passiveSkillCooldownPersent;
        activeSkillMask.fillAmount = heroBase.activeSkillCooldownPersent;
        ultimateMask.fillAmount = heroBase.ultimateCooldownPersent;
        if(activeSkillMask.fillAmount > 0 || heroBase.activeSkillLevel == 0)
        {
            activeSkillLabel.interactable = false;
        }
        else
        {
            activeSkillLabel.interactable = true;
            activeSkillLabel.onClick.RemoveAllListeners();
            activeSkillLabel.onClick.AddListener(OnActiveSkillBtnClick);
        }
        if(ultimateMask.fillAmount > 0 || heroBase.ultimateLevel == 0)
        {
            ultimateLabel.interactable = false;
        }
        else
        {
            ultimateLabel.interactable = true;
            ultimateLabel.onClick.RemoveAllListeners();
            ultimateLabel.onClick.AddListener(OnUltimateBtnClick);
        }
        int goldCount = RunSession.Instance.GoldCount;
        if (heroBase.passiveSkillLevel < 3 && goldCount >= heroBase.passiveSkillUpgradeCost[heroBase.passiveSkillLevel])
        {
            passiveSkillUpgradeBtn.gameObject.SetActive(true);
            passiveSkillUpgradeBtn.onClick.RemoveAllListeners();
            passiveSkillUpgradeBtn.onClick.AddListener(OnPassiveSkillUpgradeBtnClick);
        }
        else
        {
            passiveSkillUpgradeBtn.gameObject.SetActive(false);
        }
        if (heroBase.activeSkillLevel < 3 && goldCount >= heroBase.activeSkillUpgradeCost[heroBase.activeSkillLevel])
        {
            activeSkillUpgradeBtn.gameObject.SetActive(true);
            activeSkillUpgradeBtn.onClick.RemoveAllListeners();
            activeSkillUpgradeBtn.onClick.AddListener(OnActiveSkillUpgradeBtnClick);
        }
        else
        {
            activeSkillUpgradeBtn.gameObject.SetActive(false);
        }
        if (heroBase.ultimateLevel < 3 && goldCount >= heroBase.ultimateUpgradeCost[heroBase.ultimateLevel])
        {
            ultimateUpgradeBtn.gameObject.SetActive(true);
            ultimateUpgradeBtn.onClick.RemoveAllListeners();
            ultimateUpgradeBtn.onClick.AddListener(OnUltimateUpgradeBtnClick);
        }
        else
        {
            ultimateUpgradeBtn.gameObject.SetActive(false);
        }
    }
    /// <summary>隐藏窗口时阻止未完成的图片请求继续赋值。</summary>
    public override void OnHide()
    {
        CancelSpriteLoading();
        heroBase = null;
    }

    /// <summary>GUIManager 销毁窗口时终止本窗口换图请求，不释放公共资源。</summary>
    public override void OnDestroyWindow()
    {
        CancelSpriteLoading();
        heroBase = null;
    }

    private void OnDisable()
    {
        CancelSpriteLoading();
        heroBase = null;
    }

    private void OnDestroy()
    {
        CancelSpriteLoading();
        heroBase = null;
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
    void OnActiveSkillBtnClick()
    {
        if (heroBase != null)
        {
            if(heroBase.NeedTargetForA_Skill)
            {
                bool cancel = heroBase.preState == HeroPreState.ASkill;
                heroBase.preState = cancel ? HeroPreState.NoPre : HeroPreState.ASkill;
                if (cancel)
                    ClearButtonSelection();
            }
            else
            {
                heroBase.ApplyA_Skill();
                EventBus.Publish(new ObjectSelectionCancelledEvent());
            }
        }
    }
    void OnUltimateBtnClick()
    {
        if (heroBase != null)
        {
            if(heroBase.NeedTargetForUltimate)
            {
                bool cancel = heroBase.preState == HeroPreState.Ultimate;
                heroBase.preState = cancel ? HeroPreState.NoPre : HeroPreState.Ultimate;
                if (cancel)
                    ClearButtonSelection();
            }
            else
            {
                heroBase.ApplyUltimate();
                EventBus.Publish(new ObjectSelectionCancelledEvent());
            }
        }
    }
    void OnPassiveSkillUpgradeBtnClick()
    {
        if (heroBase != null)
        {
            RunSession.Instance.costGold(heroBase.passiveSkillUpgradeCost[heroBase.passiveSkillLevel]);
            heroBase.passiveSkillLevel++;
        }
    }
    void OnActiveSkillUpgradeBtnClick()
    {
        if (heroBase != null)
        {
            RunSession.Instance.costGold(heroBase.activeSkillUpgradeCost[heroBase.activeSkillLevel]);
            heroBase.activeSkillLevel++;
        }
    }
    void OnUltimateUpgradeBtnClick()
    {
        if (heroBase != null)
        {
            RunSession.Instance.costGold(heroBase.ultimateUpgradeCost[heroBase.ultimateLevel]);
            heroBase.ultimateLevel++;
        }
    }
    void ClearButtonSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
}
