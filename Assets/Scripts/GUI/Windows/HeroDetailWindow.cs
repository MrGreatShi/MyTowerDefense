using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using MyTowerDefense.Managers;
using MyTowerDefense.Util;

public struct HeroDetailArgs
{
    public string heroName;
    public int talentState;
    public int equipment;
    public HeroDetailArgs(string heroName, int talentState, int equipment)
    {
        this.heroName = heroName;
        this.talentState = talentState;
        this.equipment = equipment;
    }
}
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
    private HeroSettingConfig heroSetting;

    public override void OnShow(object args)
    {
        if (args is HeroDetailArgs heroDetailArgs)
        {

            Init(heroDetailArgs);
            
        }
        else
        {
            Debug.LogWarning("HeroDetailWindow: 参数类型不匹配，无法初始化窗口。", this);
        }
    }
    private async void Init(HeroDetailArgs args)
    {
        heroSetting = await GameDataManager.Instance.LoadScriptableAssetAsync<HeroSettingConfig>(args.heroName + "_Setting");
        heroName.text = args.heroName;
        await UISpriteUtility.SetSpriteAsync(portrait, args.heroName + "_Portrait");
        health.text = heroSetting.health.ToString();
        attack.text = heroSetting.attack.ToString();
        attackRange.text = heroSetting.attackRange.ToString();
        physicalDefense.text = heroSetting.physicalDefense.ToString();
        magicDefense.text = heroSetting.magicDefense.ToString();
        speed.text = heroSetting.speed.ToString();
        await UISpriteUtility.SetSpriteAsync(passiveSkillIcon, args.heroName + "_PassiveSkill");
        await UISpriteUtility.SetSpriteAsync(activeSkillIcon, args.heroName + "_ActiveSkill");
        await UISpriteUtility.SetSpriteAsync(ultimateSkillIcon, args.heroName + "_Ultimate");
        await UISpriteUtility.SetSpriteAsync(equipmentIcon, "Equipment_" + args.equipment);

    }
}
