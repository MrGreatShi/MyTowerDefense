using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainWindow : UIWindow
{
    public override string Key => "UI/MainWindow";
    public Button levelBtn;
    public override void OnShow(object args)
    {
        levelBtn.onClick.AddListener(OnLevelBtnClicked);
    }
    public override void OnHide()
    {
        levelBtn.onClick.RemoveListener(OnLevelBtnClicked);
    }
    private async void OnLevelBtnClicked()
    {
        await GUIManager.Instance.ShowAsync<LevelPrepareWindow>("UI/LevelPrepareWindow");
    }
}
