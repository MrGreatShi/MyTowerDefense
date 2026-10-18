using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyTowerDefense.Util;

public class LevelHeroInfoBar : MonoBehaviour
{
    public LevelPrepareWindow _window;
    public Button portrait;
    public TextMeshProUGUI nameText;
    public Image passiveSkillImg;
    public Image activeSkillImg;
    public Image ultimateImg;
    public Image[] talentImgs = new Image[4];
    public Image equipmentImg;
    /// <summary>更新英雄名称，并按 Address 异步更换被动技能图标。</summary>
    public async void ShowInfo(string hero, int talentState, int equipment)
    {
        await UISpriteUtility.SetSpriteAsync(portrait.image, hero + "_Portrait");
        nameText.text = hero;
        await UISpriteUtility.SetSpriteAsync(passiveSkillImg, hero + "_PassiveSkill");
        await UISpriteUtility.SetSpriteAsync(activeSkillImg, hero + "_ActiveSkill");
        await UISpriteUtility.SetSpriteAsync(ultimateImg, hero + "_Ultimate");
        for(int i =0; i < 4; ++i)
        {
            string talentSpriteName = "Talent_" + i + ((talentState >> i) & 1);
            await UISpriteUtility.SetSpriteAsync(talentImgs[i], talentSpriteName);
        }
        await UISpriteUtility.SetSpriteAsync(equipmentImg, "Equipment_" + equipment);
    }
}
