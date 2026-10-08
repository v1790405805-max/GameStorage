using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配置怪物对不同阵营的初始仇恨。该组件只提供基础仇恨数据，
/// 仇恨计算和目标选择仍由 MonsterHateSystem 负责。
/// </summary>
[DisallowMultipleComponent]
public sealed class BaseHateManager : MonoBehaviour
{
    public enum HateFaction
    {
        Player = 0,
        Dog = 1,
        Crocodile = 2
    }

    [Serializable]
    public struct FactionHateEntry
    {
        [Tooltip("目标阵营")]
        public HateFaction faction;

        [Tooltip("对该阵营的初始仇恨值")]
        [Min(0)]
        public int hate;
    }

    [Header("基础仇恨配置")]
    [Tooltip("可添加多条记录，分别配置对不同阵营的初始仇恨。")]
    [SerializeField] private List<FactionHateEntry> factionHateEntries =
        new List<FactionHateEntry>();

    public IReadOnlyList<FactionHateEntry> FactionHateEntries => factionHateEntries;

    public int GetHate(HateFaction faction)
    {
        if (factionHateEntries == null)
        {
            return 0;
        }

        if (IsSelfFaction(faction))
        {
            return 0;
        }

        int totalHate = 0;
        foreach (FactionHateEntry entry in factionHateEntries)
        {
            if (entry.faction == faction)
            {
                totalHate += entry.hate;
            }
        }

        return totalHate;
    }

    public int GetHate(MonsterIdentityManager.MonsterFaction faction)
    {
        HateFaction targetFaction = faction == MonsterIdentityManager.MonsterFaction.Dog
            ? HateFaction.Dog
            : HateFaction.Crocodile;
        return GetHate(targetFaction);
    }

    public int GetPlayerHate()
    {
        return GetHate(HateFaction.Player);
    }

    public bool IsSelfFaction(HateFaction faction)
    {
        MonsterIdentityManager identity = GetComponent<MonsterIdentityManager>();
        return identity != null &&
               faction == (identity.faction == MonsterIdentityManager.MonsterFaction.Dog
                   ? HateFaction.Dog
                   : HateFaction.Crocodile);
    }

    private void Reset()
    {
        factionHateEntries = new List<FactionHateEntry>();

        MonsterIdentityManager identity = GetComponent<MonsterIdentityManager>();
        if (identity != null && identity.faction == MonsterIdentityManager.MonsterFaction.Dog)
        {
            factionHateEntries.Add(new FactionHateEntry
            {
                faction = HateFaction.Crocodile,
                hate = 8
            });
        }
    }
}
