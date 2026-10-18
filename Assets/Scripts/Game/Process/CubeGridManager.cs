using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyTowerDefense.Game
{
    /// <summary>
    /// Game 模块的方块网格管理器，负责在初始化时缓存场景中所有 Cube 的二维位置关系。
    /// 创建日期：2026-09-30。
    /// 修改记录：初始版本，提供行、列及矩形区域查询。
    /// </summary>
    public sealed class CubeGridManager : MonoBehaviour
    {
        /// <summary>当前场景已完成初始化的方块网格管理器。</summary>
        public static CubeGridManager Instance { get; private set; }

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("相邻 Cube 中心点的世界坐标间距，单位：世界单位。")]
        private float _cellSize = 2f;

        [SerializeField]
        [Tooltip("网格坐标 (0, 0) 对应的世界坐标原点；X 表示列，Z 表示行。")]
        private Vector3 _gridOrigin = Vector3.zero;

        [SerializeField]
        [Tooltip("所有 Cube 标签方块的共同父物体，例如场景中的 plane。")]
        private Transform _cubeRoot;

        private Transform[,] _cubes = new Transform[0, 0];
        private CubeOccupancy[,] _occupancies = new CubeOccupancy[0, 0];
        private int _minColumn;
        private int _minRow;

        /// <summary>当前已缓存网格的列数。</summary>
        public int ColumnCount => _cubes.GetLength(0);

        /// <summary>当前已缓存网格的行数。</summary>
        public int RowCount => _cubes.GetLength(1);

        /// <summary>最左列的全局网格坐标，不保证为零。</summary>
        public int MinColumn => _minColumn;

        /// <summary>最上行的全局网格坐标，按当前固定视角对应最大 Z。</summary>
        public int MaxRow => _minRow + RowCount - 1;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            if (_cellSize <= 0f)
            {
                Debug.LogError($"{nameof(CubeGridManager)} 的网格间距必须大于 0。", this);
                enabled = false;
                return;
            }

            if (_cubeRoot == null)
            {
                Debug.LogError($"{nameof(CubeGridManager)} 未配置 Cube 父物体，请在 Inspector 的 Cube Root 字段中指定 plane。", this);
                enabled = false;
                return;
            }

            CacheSceneCubes();

            if (Instance != null && Instance != this)
            {
                Debug.LogError($"场景中存在多个 {nameof(CubeGridManager)}，仅允许保留一个已启用实例。", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 根据全局网格坐标取得对应 Cube。
        /// </summary>
        /// <param name="column">X 方向的列坐标。</param>
        /// <param name="row">Z 方向的行坐标。</param>
        /// <returns>对应的 Cube Transform；坐标越界或位置无 Cube 时返回 null。</returns>
        public Transform GetCube(int column, int row)
        {
            int arrayColumn = column - _minColumn;
            int arrayRow = row - _minRow;

            if (arrayColumn < 0 || arrayColumn >= ColumnCount || arrayRow < 0 || arrayRow >= RowCount)
            {
                return null;
            }

            return _cubes[arrayColumn, arrayRow];
        }

        /// <summary>
        /// 根据世界坐标取得对应的 Cube。
        /// </summary>
        /// <param name="worldPosition">待查询的位置。</param>
        /// <param name="cube">查询到的 Cube Transform。</param>
        /// <returns>世界坐标位于已缓存 Cube 时返回 true。</returns>
        public bool TryGetCubeAtWorldPosition(Vector3 worldPosition, out Transform cube)
        {
            Vector2Int cell = WorldToCell(worldPosition);
            cube = GetCube(cell.x, cell.y);
            return cube != null;
        }

        /// <summary>
        /// 根据世界坐标取得其对应的有效网格坐标。
        /// </summary>
        /// <param name="worldPosition">待查询的位置。</param>
        /// <param name="cell">查询到的网格坐标；X 表示列，Y 表示行。</param>
        /// <returns>该位置对应已缓存 Cube 时返回 true。</returns>
        public bool TryGetCellAtWorldPosition(Vector3 worldPosition, out Vector2Int cell)
        {
            cell = WorldToCell(worldPosition);
            return GetCube(cell.x, cell.y) != null;
        }

        /// <summary>
        /// 根据网格坐标取得对应 Cube 的占用记录器。
        /// </summary>
        /// <param name="cell">X 为列、Y 为行的网格坐标。</param>
        /// <param name="occupancy">查询到的占用记录器。</param>
        /// <returns>Cube 与占用记录器均存在时返回 true。</returns>
        public bool TryGetCubeOccupancyAtCell(Vector2Int cell, out CubeOccupancy occupancy)
        {
            int arrayColumn = cell.x - _minColumn;
            int arrayRow = cell.y - _minRow;
            occupancy = null;

            if (arrayColumn < 0 || arrayColumn >= ColumnCount || arrayRow < 0 || arrayRow >= RowCount)
            {
                return false;
            }

            occupancy = _occupancies[arrayColumn, arrayRow];
            return _cubes[arrayColumn, arrayRow] != null && occupancy != null;
        }

        /// <summary>
        /// 根据世界坐标取得对应 Cube 及其预缓存的占用记录器。
        /// </summary>
        /// <param name="worldPosition">待查询的位置。</param>
        /// <param name="cube">查询到的 Cube Transform。</param>
        /// <param name="occupancy">查询到的 CubeOccupancy。</param>
        /// <returns>Cube 与占用记录器均存在时返回 true。</returns>
        public bool TryGetCubeOccupancyAtWorldPosition(
            Vector3 worldPosition,
            out Transform cube,
            out CubeOccupancy occupancy)
        {
            Vector2Int cell = WorldToCell(worldPosition);
            cube = null;
            if (!TryGetCubeOccupancyAtCell(cell, out occupancy))
            {
                return false;
            }

            cube = GetCube(cell.x, cell.y);
            return true;
        }

        /// <summary>
        /// 将指定行内存在的 Cube 依列坐标由小到大写入结果列表。
        /// </summary>
        /// <param name="row">Z 方向的行坐标。</param>
        /// <param name="results">用于接收结果的列表；调用前会被清空。</param>
        public void GetRow(int row, List<Transform> results)
        {
            ValidateResults(results);
            results.Clear();

            for (int column = _minColumn; column < _minColumn + ColumnCount; column++)
            {
                Transform cube = GetCube(column, row);
                if (cube != null)
                {
                    results.Add(cube);
                }
            }
        }

        /// <summary>
        /// 将指定列内存在的 Cube 依行坐标由小到大写入结果列表。
        /// </summary>
        /// <param name="column">X 方向的列坐标。</param>
        /// <param name="results">用于接收结果的列表；调用前会被清空。</param>
        public void GetColumn(int column, List<Transform> results)
        {
            ValidateResults(results);
            results.Clear();

            for (int row = _minRow; row < _minRow + RowCount; row++)
            {
                Transform cube = GetCube(column, row);
                if (cube != null)
                {
                    results.Add(cube);
                }
            }
        }

        /// <summary>
        /// 将指定闭区间矩形内存在的 Cube 写入结果列表。
        /// </summary>
        /// <param name="min">区域左下角网格坐标。</param>
        /// <param name="max">区域右上角网格坐标。</param>
        /// <param name="results">用于接收结果的列表；调用前会被清空。</param>
        public void GetArea(Vector2Int min, Vector2Int max, List<Transform> results)
        {
            ValidateResults(results);
            results.Clear();

            int minColumn = Mathf.Min(min.x, max.x);
            int maxColumn = Mathf.Max(min.x, max.x);
            int minRow = Mathf.Min(min.y, max.y);
            int maxRow = Mathf.Max(min.y, max.y);

            for (int column = minColumn; column <= maxColumn; column++)
            {
                for (int row = minRow; row <= maxRow; row++)
                {
                    Transform cube = GetCube(column, row);
                    if (cube != null)
                    {
                        results.Add(cube);
                    }
                }
            }
        }

        /// <summary>
        /// 为指定 Cube 集合附加持续效果，并将每个 Cube 的执行时序交由其占用记录器管理。
        /// </summary>
        /// <param name="cubes">要生效的 Cube 集合；重复项只会生效一次。</param>
        /// <param name="caster">本次效果的施放者。</param>
        /// <param name="effectFunc">每次触发时执行的效果函数，参数依次为当前格和施放者。</param>
        /// <param name="duration">效果总持续时间，单位：秒，必须大于 0。</param>
        /// <param name="interval">效果触发间隔，单位：秒，必须大于 0。</param>
        /// <param name="effectPrefab">每个有效 Cube 上生成的可选特效预制体。</param>
        /// <param name="canApplyToFrozenCube">为 false 时跳过被冻结的 Cube。</param>
        public void ApplyCubeEffect(
            List<Transform> cubes,
            HeroBase caster,
            Action<CubeOccupancy, HeroBase> effectFunc,
            float duration,
            float interval,
            GameObject effectPrefab = null,
            bool canApplyToFrozenCube = true)
        {
            if (cubes == null || cubes.Count == 0 || caster == null || effectFunc == null)
            {
                return;
            }

            if (duration <= 0f || interval <= 0f)
            {
                Debug.LogWarning($"{nameof(ApplyCubeEffect)} 的持续时间和触发间隔必须大于 0。", this);
                return;
            }

            HashSet<CubeOccupancy> affectedOccupancies = new HashSet<CubeOccupancy>();
            foreach (Transform cube in cubes)
            {
                if (cube == null || !cube.TryGetComponent(out CubeOccupancy occupancy))
                {
                    continue;
                }

                if (!canApplyToFrozenCube && occupancy.IsFrozen)
                {
                    continue;
                }

                if (!affectedOccupancies.Add(occupancy))
                {
                    continue;
                }

                GameObject effectInstance = null;
                if (effectPrefab != null)
                {
                    // 保留特效预制体自身的朝向；该粒子资源依赖根节点的 -90° 旋转贴合地表。
                    effectInstance = Instantiate(effectPrefab, cube, false);
                    // Variant 可能保存了制作时的位置偏移，运行时统一放置在目标 Cube 表面中心。
                    effectInstance.transform.localPosition = Vector3.up * 0.05f;
                }

                occupancy.AddTimedEffect(caster, effectFunc, duration, interval, effectInstance);
            }
        }

        private void CacheSceneCubes()
        {
            _cubes = new Transform[0, 0];
            _occupancies = new CubeOccupancy[0, 0];
            _minColumn = 0;
            _minRow = 0;

            // 仅扫描指定父物体的子层级，避免遍历场景中无关的 Collider。
            Transform[] transforms = _cubeRoot.GetComponentsInChildren<Transform>(true);
            List<CubeEntry> entries = new List<CubeEntry>();

            int minColumn = int.MaxValue;
            int maxColumn = int.MinValue;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;

            foreach (Transform cubeTransform in transforms)
            {
                if (!cubeTransform.CompareTag("Cube"))
                {
                    continue;
                }

                Vector2Int cell = WorldToCell(cubeTransform.position);
                entries.Add(new CubeEntry(cell, cubeTransform));

                minColumn = Mathf.Min(minColumn, cell.x);
                maxColumn = Mathf.Max(maxColumn, cell.x);
                minRow = Mathf.Min(minRow, cell.y);
                maxRow = Mathf.Max(maxRow, cell.y);
            }

            if (entries.Count == 0)
            {
                Debug.LogWarning($"{_cubeRoot.name} 的子层级中未找到 Tag 为 Cube 的对象，方块网格为空。", this);
                return;
            }

            _minColumn = minColumn;
            _minRow = minRow;
            _cubes = new Transform[maxColumn - minColumn + 1, maxRow - minRow + 1];
            _occupancies = new CubeOccupancy[_cubes.GetLength(0), _cubes.GetLength(1)];

            foreach (CubeEntry entry in entries)
            {
                int arrayColumn = entry.Cell.x - _minColumn;
                int arrayRow = entry.Cell.y - _minRow;

                if (_cubes[arrayColumn, arrayRow] != null)
                {
                    Debug.LogError($"网格坐标 {entry.Cell} 存在多个 Cube：{_cubes[arrayColumn, arrayRow].name} 与 {entry.Transform.name}。", this);
                    continue;
                }

                _cubes[arrayColumn, arrayRow] = entry.Transform;
                CubeOccupancy occupancy = entry.Transform.GetComponent<CubeOccupancy>();
                if (occupancy == null)
                {
                    Debug.LogError($"Cube {entry.Transform.name} 缺少 {nameof(CubeOccupancy)} 组件，无法记录单位占用。", entry.Transform);
                    continue;
                }

                _occupancies[arrayColumn, arrayRow] = occupancy;
            }
        }

        private Vector2Int WorldToCell(Vector3 worldPosition)
        {
            int column = Mathf.RoundToInt((worldPosition.x - _gridOrigin.x) / _cellSize);
            int row = Mathf.RoundToInt((worldPosition.z - _gridOrigin.z) / _cellSize);
            return new Vector2Int(column, row);
        }

        private static void ValidateResults(List<Transform> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }
        }
        private readonly struct CubeEntry
        {
            public CubeEntry(Vector2Int cell, Transform transform)
            {
                Cell = cell;
                Transform = transform;
            }

            public Vector2Int Cell { get; }
            public Transform Transform { get; }
        }
    }
}
