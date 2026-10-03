using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class LevelHeroWindow : UIWindow
{
    public override string Key => "UI/LevelHeroWindow";
    public HeroBase heroBase;
    public Image portrait;
    public Image healthBar;
    public Image passiveSkillLabel;
    public Image passiveSkillMask;
    public Button passiveSkillUpgradeBtn;
    public Button activeSkillLabel;
    public Image activeSkillMask;
    public Button activeSkillUpgradeBtn;
    public Button ultimateLabel;
    public Image ultimateMask;
    public Button ultimateUpgradeBtn;
    readonly AddressablesLoader<Sprite> loader = new AddressablesLoader<Sprite>();
    public override void OnCreate() 
    { 

    }
    public override async void OnShow(object args) 
    {
        if(args is HeroBase hero)
        {
            heroBase = hero;
            portrait.sprite = await LoadSprite(hero.key + "_Portrait");
            passiveSkillLabel.sprite = await LoadSprite(hero.key + "_PassiveSkill");
            SetButtonSprite(activeSkillLabel, await LoadSprite(hero.key + "_ActiveSkill"));
            SetButtonSprite(ultimateLabel, await LoadSprite(hero.key + "_Ultimate"));
        }
    }
    /// <summary>
    /// 更新英雄状态UI
    /// </summary>
    private void Update()
    {
        if(heroBase == null) return;
        healthBar.fillAmount = (float)heroBase.health / heroBase._maxHealth;
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
    async System.Threading.Tasks.Task<Sprite> LoadSprite(string key)
    {
        var handle = await loader.LoadPrefabHandleAsync(key);
        return handle.Result;
    }

    void SetButtonSprite(Button button, Sprite sprite)
    {
        if (button != null && button.targetGraphic is Image image)
            image.sprite = sprite;
    }
    public override void OnHide() { }
    public override void OnDestroyWindow()
    {
        loader.ReleaseAll();
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
