using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MyTowerDefense.Game
{
    /// <summary>
    /// Game 模块：负责开局英雄资源加载、按地块部署及实例清理，不负责关卡状态。
    /// 创建日期：2026-10-08。
    /// 修改记录：从 RunSession 拆离英雄部署，保持原有出生位置及占用规则。
    /// </summary>
    /// <remarks>由关卡会话持有，无需挂载。所有方法须在 Unity 主线程调用。</remarks>
    public sealed class HeroDeploymentService : IDisposable
    {
        // 仅用于组织层级；部署位置使用地块的世界坐标。
        private readonly Transform _parent;
        private readonly AddressablesLoader<GameObject> _loader = new AddressablesLoader<GameObject>();
        private readonly List<GameObject> _spawnedHeroes = new List<GameObject>();
        private bool _isDeploying;
        private bool _isDeployed;
        private bool _isDisposed;

        /// <summary>创建英雄部署服务，parent 为生成英雄的父节点。</summary>
        public HeroDeploymentService(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _parent = parent;
        }

        /// <summary>
        /// 先验证全部 Prefab，再从左到右、每列从上到下部署；失败时回滚本轮实例。
        /// </summary>
        /// <param name="addresses">英雄 Address 列表；null 或空列表视为成功。</param>
        /// <param name="gridManager">已在 Awake 缓存场景地块的网格管理器。</param>
        /// <returns>完整部署成功为 true；配置错误、重复请求或会话结束为 false。</returns>
        public async Task<bool> DeployAsync(IReadOnlyList<string> addresses, CubeGridManager gridManager)
        {
            if (_isDisposed) return false;
            if (_isDeploying || _isDeployed)
            {
                Debug.LogWarning("英雄部署请求被拒绝：正在部署或已完成部署。", _parent);
                return false;
            }

            _isDeploying = true;
            try
            {
                if (_parent == null || !_parent.gameObject.activeInHierarchy) return false;
                if (addresses == null || addresses.Count == 0)
                {
                    _isDeployed = true;
                    return true;
                }
                if (gridManager == null || gridManager.ColumnCount == 0 || gridManager.RowCount == 0)
                    throw new InvalidOperationException("英雄部署失败：方块网格未初始化或为空。");

                // 快照避免 Inspector 在等待资源期间修改配置，改变本轮阵容及顺序。
                List<string> snapshot = new List<string>(addresses);
                List<GameObject> prefabs = new List<GameObject>(snapshot.Count);
                foreach (string address in snapshot)
                {
                    if (string.IsNullOrWhiteSpace(address))
                        throw new InvalidOperationException("heroes 中包含空 Address。");

                    var handle = await _loader.LoadPrefabHandleAsync(address);
                    // Dispose 不强制中断 Addressables；迟到结果由 finally 统一释放。
                    if (_isDisposed || _parent == null || !_parent.gameObject.activeInHierarchy)
                        return false;

                    GameObject prefab = handle.Result;
                    if (prefab == null || !prefab.activeSelf || prefab.GetComponent<HeroBase>() == null)
                        throw new InvalidOperationException($"英雄 Prefab 无效：{address}。根节点需挂载 HeroBase 且处于启用状态。");
                    prefabs.Add(prefab);
                }

                if (gridManager == null)
                    throw new InvalidOperationException("英雄部署失败：加载期间网格已销毁。");

                int heroIndex = 0;
                for (int columnOffset = 0; columnOffset < gridManager.ColumnCount; columnOffset++)
                {
                    int column = gridManager.MinColumn + columnOffset;
                    for (int rowOffset = 0; rowOffset < gridManager.RowCount; rowOffset++)
                    {
                        if (heroIndex == prefabs.Count) break;
                        if (_isDisposed || _parent == null || !_parent.gameObject.activeInHierarchy)
                            return false;

                        int row = gridManager.MaxRow - rowOffset;
                        if (!gridManager.TryGetCubeOccupancyAtCell(new Vector2Int(column, row), out CubeOccupancy occupancy) ||
                            occupancy.IsFrozen || occupancy.IsOccupied)
                            continue;

                        Transform cube = gridManager.GetCube(column, row);
                        if (cube == null) continue;
                        Vector3 position = new Vector3(cube.position.x, 0f, cube.position.z);
                        // 开局低频创建；不引入对象池。位置为世界坐标，朝向世界 +X。
                        GameObject hero = UnityEngine.Object.Instantiate(prefabs[heroIndex], position,
                            Quaternion.LookRotation(Vector3.right, Vector3.up), _parent);
                        _spawnedHeroes.Add(hero);
                        // HeroBase.OnEnable 负责占用登记，服务只检查结果，不维护第二份占用。
                        if (_isDisposed || hero == null || occupancy == null || occupancy.StationaryOccupant != hero.transform)
                            throw new InvalidOperationException($"英雄 {snapshot[heroIndex]} 未成功登记地块 ({column}, {row})。");
                        heroIndex++;
                    }
                    if (heroIndex == prefabs.Count) break;
                }

                if (heroIndex != prefabs.Count)
                    throw new InvalidOperationException($"英雄部署失败：仅有 {heroIndex} 个可用地块，需要 {prefabs.Count} 个。");

                _isDeployed = true;
                return true;
            }
            catch (Exception exception)
            {
                if (!_isDisposed) Debug.LogException(exception, _parent);
                return false;
            }
            finally
            {
                _isDeploying = false;
                if (_isDisposed || !_isDeployed)
                {
                    ClearSpawnedHeroes();
                }
            }
        }

        /// <summary>
        /// 清理本服务已创建的英雄并释放其资源；清理完成后允许重新部署。
        /// 若需终止尚未完成的部署，请调用 Dispose，而不是此方法。
        /// </summary>
        public void ClearSpawnedHeroes()
        {
            foreach (GameObject hero in _spawnedHeroes)
            {
                if (hero == null) continue;
                // 禁用立即触发 HeroBase.OnDisable 注销占用；销毁则由 Unity 延迟执行。
                hero.SetActive(false);
                UnityEngine.Object.Destroy(hero);
            }
            _spawnedHeroes.Clear();
            _isDeployed = false;
            // 尚在 await 中时不能清除加载器的在途任务；由 DeployAsync.finally 收尾。
            if (!_isDeploying) _loader.ReleaseAll();
        }

        /// <summary>结束服务生命周期；可重复调用，后续不再接受部署请求。</summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            ClearSpawnedHeroes();
        }
    }
}
