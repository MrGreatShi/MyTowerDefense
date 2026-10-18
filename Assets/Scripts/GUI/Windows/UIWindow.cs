using UnityEngine;

public enum UILayer { Background, Normal, Popup, Top }

public abstract class UIWindow : MonoBehaviour
{
    /// <summary>Addressables 资源地址，例如 "UI/MainWindow"。</summary>
    public abstract string Key { get; }

    public virtual UILayer Layer => UILayer.Normal;
    public virtual bool IsModal => false;
    public virtual bool UseStack => true;
    public virtual bool DestroyOnHide => false;

    public bool IsVisible { get; private set; }

    public virtual void OnCreate() { }
    public virtual void OnShow(object args) { }
    public virtual void OnHide() { }
    public virtual void OnDestroyWindow() { }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        gameObject.SetActive(visible);
    }
}