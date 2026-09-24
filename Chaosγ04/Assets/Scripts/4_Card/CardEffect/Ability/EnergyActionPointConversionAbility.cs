using UnityEngine;

/// <summary>
/// 3001 星尘共鸣：
/// 每累计消耗 3 点能量，获得 1 点行动点；
/// 每累计消耗 5 点行动点，获得 1 点能量。
/// </summary>
public class EnergyActionPointConversionAbility : AbilityCore
{
    private const int EnergyPerActionPoint = 3;
    private const int ActionPointsPerEnergy = 5;

    private CombatStatsManager combatStats;
    private int spentEnergy;
    private int spentActionPoints;

    protected override void OnActivated()
    {
        combatStats = CombatStatsManager.Instance;
        if (combatStats == null)
        {
            return;
        }

        combatStats.OnEnergySpent += HandleEnergySpent;
        combatStats.OnActionPointSpent += HandleActionPointSpent;
    }

    protected override void OnDeactivated()
    {
        if (combatStats != null)
        {
            combatStats.OnEnergySpent -= HandleEnergySpent;
            combatStats.OnActionPointSpent -= HandleActionPointSpent;
        }

        combatStats = null;
    }

    private void HandleEnergySpent(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        spentEnergy += amount;
        int actionPointsToGain = spentEnergy / EnergyPerActionPoint;
        if (actionPointsToGain <= 0)
        {
            return;
        }

        spentEnergy %= EnergyPerActionPoint;
        combatStats.ModifyActionPoint(actionPointsToGain, allowExceedMax: true);
    }

    private void HandleActionPointSpent(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        spentActionPoints += amount;
        int energyToGain = spentActionPoints / ActionPointsPerEnergy;
        if (energyToGain <= 0)
        {
            return;
        }

        spentActionPoints %= ActionPointsPerEnergy;
        combatStats.ModifyEnergy(energyToGain, allowExceedMax: true);
    }
}
