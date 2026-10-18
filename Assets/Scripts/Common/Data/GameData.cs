using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace MyTowerDefense.Data
{
    /// <summary>
    /// 职责：提供整个运行周期唯一的只读游戏配置目录和名称查询入口。
    /// 模块：Data；创建日期：2026-10-12；修改记录：首次实现。
    /// 所有访问在 Unity 主线程进行；数据由 Core 的 GameDataManager 发布。
    /// </summary>
    public static class GameData
    {
        private static ReadOnlyCollection<string> _heroes;
        private static ReadOnlyCollection<string> _equipments;
        private static HashSet<string> _heroKeys;
        private static HashSet<string> _equipmentKeys;

        // 用托管对象身份记录发布者；Unity 对象销毁后不能因“假空值”误认所有者。
        private static object _owner;

        /// <summary>所有目录均已校验并发布时为 true。</summary>
        public static bool IsInitialized => !ReferenceEquals(_owner, null);

        /// <summary>配置顺序下的全部英雄 Key；初始化前访问会抛出明确异常。</summary>
        public static IReadOnlyList<string> Heroes
        {
            get
            {
                EnsureInitialized();
                return _heroes;
            }
        }

        /// <summary>配置顺序下的全部装备 Key；不会返回源配置中的可修改 List。</summary>
        public static IReadOnlyList<string> Equipments
        {
            get
            {
                EnsureInitialized();
                return _equipments;
            }
        }

        /// <summary>按区分大小写的完整 Key 查询英雄是否存在；空 Key 返回 false。</summary>
        public static bool ContainsHero(string key)
        {
            EnsureInitialized();
            return !string.IsNullOrWhiteSpace(key) && _heroKeys.Contains(key);
        }

        /// <summary>按区分大小写的完整 Key 查询装备是否存在；空 Key 返回 false。</summary>
        public static bool ContainsEquipment(string key)
        {
            EnsureInitialized();
            return !string.IsNullOrWhiteSpace(key) && _equipmentKeys.Contains(key);
        }

        internal static void Initialize(GameCatalogConfig config, object owner)
        {
            if (ReferenceEquals(owner, null))
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (IsInitialized)
            {
                if (ReferenceEquals(_owner, owner))
                {
                    return;
                }

                throw new InvalidOperationException("GameData 已由另一个 Manager 初始化，不能覆盖。");
            }

            if (config == null)
            {
                throw new InvalidOperationException("GameDataManager 未绑定有效的 GameCatalogConfig。");
            }

            // 先在局部变量构建全部快照；任意目录错误都不能发布半初始化状态。
            ReadOnlyCollection<string> heroes = CreateSnapshot(config.HeroKeys, "英雄", out var heroKeys);
            ReadOnlyCollection<string> equipments = CreateSnapshot(config.EquipmentKeys, "装备", out var equipmentKeys);

            _heroes = heroes;
            _equipments = equipments;
            _heroKeys = heroKeys;
            _equipmentKeys = equipmentKeys;
            _owner = owner;
        }

        internal static bool IsOwnedBy(object owner)
        {
            return IsInitialized && ReferenceEquals(_owner, owner);
        }

        internal static void Release(object owner)
        {
            // 重复 Manager 的销毁回调不得清空有效 Manager 发布的数据。
            if (!ReferenceEquals(owner, null) && ReferenceEquals(_owner, owner))
            {
                ResetSession();
            }
        }

        private static ReadOnlyCollection<string> CreateSnapshot(
            IReadOnlyList<string> source, string category, out HashSet<string> keys)
        {
            if (source == null)
            {
                throw new InvalidOperationException($"{category}目录为 null，请检查配置资产。");
            }

            keys = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                string key = source[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new InvalidOperationException($"{category}目录 Element {i} 的 Key 为空。");
                }

                // 不自动 Trim：悄悄改写 Key 可能导致与 Addressables 地址不一致。
                if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{category} Key '{key}' 含首尾空格，请修正配置。");
                }

                if (!keys.Add(key))
                {
                    throw new InvalidOperationException($"{category}目录存在重复 Key：'{key}'。");
                }

                items.Add(key);
            }

            return items.AsReadOnly();
        }

        private static void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "GameData 尚未初始化。请从 Core 启动，并等待 GameDataManager 初始化成功后读取。");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // 兼容关闭 Domain Reload；每次 Play 不复用上一次的静态配置状态。
            _owner = null;
            _heroes = null;
            _equipments = null;
            _heroKeys = null;
            _equipmentKeys = null;
        }
    }
}
