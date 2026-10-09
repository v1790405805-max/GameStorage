using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在指定地格周围的小区域内，随机选择地图未占用的普通地形召唤 Dog。
/// 地格间距采用 XZ 平面上的曼哈顿距离，同列不同层视为距离 0。
/// </summary>
[DisallowMultipleComponent]
public sealed class SummonDogAction : MonsterActionBase
{
    [Header("Dog 预制体")]
    [Tooltip("要召唤的 Dog 预制体，需要在 Inspector 中直接引用。")]
    [SerializeField] private GameObject dogPrefab;

    [Header("召唤规则")]
    [Tooltip("本 Action 每次召唤的 Dog 数量。")]
    [SerializeField, Min(1)] private int summonCount = 3;
    [Tooltip("召唤中心地格；留空时自动使用当前怪物所在的地格。")]
    [SerializeField] private CellManager summonCenterCell;
    [Tooltip("召唤候选地格与召唤中心在 XZ 平面上的最大曼哈顿距离（格）。")]
    [SerializeField, Min(0)] private int summonRadius = 2;
    [Tooltip("每只 Dog 之间的最小曼哈顿距离（格）。")]
    [SerializeField, Min(0)] private int dogSpacing = 5;
    [Tooltip("召唤位置与每个已占用地格之间需要保持的最小曼哈顿距离（格）。")]
    [SerializeField, Min(0)] private int occupiedSpacing = 2;
    [Tooltip("相邻两次 Dog 召唤之间的等待时间（秒）。")]
    [SerializeField, Min(0f)] private float summonInterval = 1f;

    [Header("冷却")]
    [Tooltip("触发后需要跳过的完整回合数；填写 2 表示触发回合后的两回合内不可再次触发。")]
    [SerializeField, Min(0)] private int cooldownRounds = 2;

    [Header("网格引用")]
    [Tooltip("网格管理器；未手动指定时会自动查找。")]
    public GridManager gridManager;

    private Coroutine summonCoroutine;
    private int lastTriggeredRound = int.MinValue;
    private MonsterIdentityManager selfIdentity;

    public bool IsOnCooldown
    {
        get
        {
            if (cooldownRounds <= 0 || lastTriggeredRound == int.MinValue)
            {
                return false;
            }

            int currentRound = GetCurrentRound();
            return currentRound >= lastTriggeredRound &&
                   currentRound - lastTriggeredRound <= cooldownRounds;
        }
    }

    public override bool CanExecute(MonsterActionContext context)
    {
        ResolveReferences();

        if (WhenDogLairDestroyed.IsDestroyed)
        {
            return false;
        }

        if (gridManager == null || dogPrefab == null)
        {
            return false;
        }

        return !IsOnCooldown;
    }

    protected override void OnStart()
    {
        ResolveReferences();

        if (dogPrefab == null)
        {
            Debug.LogError($"[{name}] SummonDogAction 未配置 Dog 预制体。");
            CompleteAction();
            return;
        }

        if (gridManager == null)
        {
            Debug.LogError($"[{name}] SummonDogAction 缺少 GridManager。");
            CompleteAction();
            return;
        }

        gridManager.EnsureGridSystemInitialized();

        CellManager centerCell = ResolveSummonCenterCell();
        if (centerCell == null)
        {
            Debug.LogWarning(
                $"[{name}] 无法确定召唤中心地格，已跳过本次召唤。");
            CompleteAction();
            return;
        }

        List<CellManager> spawnCells = SelectSpawnCells(centerCell);
        if (spawnCells.Count < summonCount)
        {
            Debug.LogWarning(
                $"[{name}] 没有足够的合法地格召唤 {summonCount} 只 Dog。" +
                $"召唤范围为召唤中心周围 {summonRadius} 格，Dog 之间的最小间距为 {dogSpacing} 格，" +
                $"与已占用地格的最小间距为 {occupiedSpacing} 格。");
            CompleteAction();
            return;
        }

        summonCoroutine = StartCoroutine(SummonRoutine(spawnCells));
    }

    public override void CancelAction()
    {
        base.CancelAction();

        if (summonCoroutine != null)
        {
            StopCoroutine(summonCoroutine);
            summonCoroutine = null;
        }
    }

    private IEnumerator SummonRoutine(List<CellManager> spawnCells)
    {
        // 召唤开始前，先终止其他正在执行的动作。
        MonsterActionManager.RequestTerminationOfOtherActions(this);

        for (int i = 0; i < summonCount; i++)
        {
            SummonOneDog(spawnCells[i]);

            if (i < summonCount - 1 && summonInterval > 0f)
            {
                yield return new WaitForSeconds(summonInterval);
            }
        }

        lastTriggeredRound = GetCurrentRound();

        // 等待物理系统处理新生成单位的触发器碰撞体，再结束本 Action。
        yield return new WaitForFixedUpdate();

        // SummonDogAction 会请求终止其他动作，导致 MonsterActionManager
        // 在 OnActionFinished 后提前 break，从而跳过统一的 PostActionDelay。
        // 因此这里由 Action 自己等待这段延迟，避免召唤后立即进入下一个行动。
        if (PostActionDelay > 0f)
        {
            yield return new WaitForSeconds(PostActionDelay);
        }

        summonCoroutine = null;
        CompleteAction();
    }

    private void SummonOneDog(CellManager spawnCell)
    {
        GameObject dogInstance = Instantiate(
            dogPrefab,
            spawnCell.transform.position,
            dogPrefab.transform.rotation);

        RegisterSummonedDog(dogInstance);
    }

    private void RegisterSummonedDog(GameObject dogInstance)
    {
        if (dogInstance == null)
        {
            return;
        }

        MonsterActionManager dogActionManager =
            dogInstance.GetComponentInChildren<MonsterActionManager>(true);
        if (dogActionManager == null)
        {
            Debug.LogWarning(
                $"[{name}] 召唤出的 Dog 缺少 MonsterActionManager，无法注册到敌方行动顺序。");
            return;
        }

        if (TurnManager.Instance == null)
        {
            return;
        }

        // MonsterActionManager.OnEnable 可能已经注册过；RegisterEnemyBehaviour 内部会去重。
        TurnManager.Instance.RegisterEnemyBehaviour(dogActionManager);
    }

    private List<CellManager> SelectSpawnCells(CellManager centerCell)
    {
        List<CellManager> allCells = GetAllCells();
        List<CellManager> occupiedCells = new List<CellManager>();

        foreach (CellManager cell in allCells)
        {
            if (IsOccupied(cell))
            {
                occupiedCells.Add(cell);
            }
        }

        List<CellManager> candidates = new List<CellManager>();
        foreach (CellManager cell in allCells)
        {
            if (!IsAvailableCell(cell))
            {
                continue;
            }

            if (!IsWithinSummonRadius(cell, centerCell))
            {
                continue;
            }

            if (!HasRequiredSpacing(cell, occupiedCells, occupiedSpacing))
            {
                continue;
            }

            candidates.Add(cell);
        }

        Shuffle(candidates);

        List<CellManager> selected = new List<CellManager>();
        if (TrySelectSpawnCells(candidates, 0, selected))
        {
            return selected;
        }

        return new List<CellManager>();
    }

    private bool IsWithinSummonRadius(CellManager cell, CellManager centerCell)
    {
        return GetGridDistance(cell, centerCell) <= summonRadius;
    }

    private List<CellManager> GetAllCells()
    {
        List<CellManager> result = new List<CellManager>();

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
                {
                    if (cell != null)
                    {
                        result.Add(cell);
                    }
                }
            }
        }

        return result;
    }

    private bool IsAvailableCell(CellManager cell)
    {
        return cell != null &&
               cell.gameObject.activeInHierarchy &&
               !cell.IsSpecialTerrain &&
               !cell.IsHardLocked &&
               !IsOccupied(cell);
    }

    private static bool IsOccupied(CellManager cell)
    {
        return cell != null && (cell.IsPlayerInside || cell.HasMonsterInside);
    }

    private bool TrySelectSpawnCells(
        List<CellManager> candidates,
        int candidateIndex,
        List<CellManager> selected)
    {
        int remaining = summonCount - selected.Count;
        if (remaining <= 0)
        {
            return true;
        }

        if (candidateIndex >= candidates.Count ||
            candidates.Count - candidateIndex < remaining)
        {
            return false;
        }

        int lastCandidateIndex = candidates.Count - remaining;
        for (int i = candidateIndex; i <= lastCandidateIndex; i++)
        {
            CellManager candidate = candidates[i];
            if (!HasRequiredSpacing(candidate, selected, dogSpacing))
            {
                continue;
            }

            selected.Add(candidate);
            if (TrySelectSpawnCells(candidates, i + 1, selected))
            {
                return true;
            }

            selected.RemoveAt(selected.Count - 1);
        }

        return false;
    }

    private bool HasRequiredSpacing(
        CellManager candidate,
        List<CellManager> otherCells,
        int minimumDistance)
    {
        if (minimumDistance <= 0 || otherCells == null || otherCells.Count == 0)
        {
            return true;
        }

        foreach (CellManager otherCell in otherCells)
        {
            if (GetGridDistance(candidate, otherCell) < minimumDistance)
            {
                return false;
            }
        }

        return true;
    }

    private int GetGridDistance(CellManager first, CellManager second)
    {
        if (gridManager == null || first == null || second == null)
        {
            return int.MaxValue;
        }

        (int firstX, int firstZ) = gridManager.GetCellGridPosition(first);
        (int secondX, int secondZ) = gridManager.GetCellGridPosition(second);

        if (firstX < 0 || firstZ < 0 || secondX < 0 || secondZ < 0)
        {
            return int.MaxValue;
        }

        return Mathf.Abs(firstX - secondX) + Mathf.Abs(firstZ - secondZ);
    }

    private static void Shuffle(List<CellManager> cells)
    {
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (cells[i], cells[randomIndex]) = (cells[randomIndex], cells[i]);
        }
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (selfIdentity == null)
        {
            selfIdentity = GetComponent<MonsterIdentityManager>();
        }
    }

    private CellManager ResolveSummonCenterCell()
    {
        if (summonCenterCell != null)
        {
            return summonCenterCell;
        }

        if (selfIdentity == null || gridManager == null)
        {
            return null;
        }

        return MonsterPathfinding.FindMonsterCell(gridManager, selfIdentity);
    }

    private static int GetCurrentRound()
    {
        return TurnManager.Instance != null
            ? TurnManager.Instance.currentRoundCount
            : 0;
    }
}
