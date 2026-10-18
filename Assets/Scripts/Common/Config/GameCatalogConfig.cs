using System.Collections.Generic;
using UnityEngine;

namespace MyTowerDefense.Data
{
    /// <summary>
    /// 职责：在配置资产中保存全游戏共用的英雄、装备资源 Key 目录。
    /// 模块：Data；创建日期：2026-10-12；修改记录：首次实现。
    /// 不是玩家存档；应在编辑器维护，由 GameDataManager 创建运行时只读快照。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Game Catalog Config", fileName = "GameCatalogConfig")]
    public sealed class GameCatalogConfig : ScriptableObject
    {
        [Header("英雄目录")]
        [Tooltip("填写稳定资源 Key，例如 Knight；不是中文显示名。顺序用于 UI 展示。")]
        [SerializeField] private List<string> _heroes = new List<string>();

        [Header("装备目录")]
        [Tooltip("填写装备的稳定 Key；暂时没有装备时允许为空列表。")]
        [SerializeField] private List<string> _equipments = new List<string>();

        // 仅向本程序集的初始化入口提供源数据，不向业务公开可修改的 List。
        internal IReadOnlyList<string> HeroKeys => _heroes;
        internal IReadOnlyList<string> EquipmentKeys => _equipments;
    }
}
