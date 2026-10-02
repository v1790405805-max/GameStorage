using UnityEngine;

/// <summary>
/// Adds baseline hate toward the player when this Dog sees the Dog Leader attacked by the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class HateChange_WhenDogLeaderAttacked : MonoBehaviour
{
    [SerializeField, Min(0)] private int playerBaselineHate = 999;

    private MonsterIdentityManager selfIdentity;
    private MonsterHateSystem hateSystem;

    private void Awake()
    {
        selfIdentity = GetComponent<MonsterIdentityManager>();
        hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
    }

    private void OnEnable()
    {
        MonsterStats.PlayerAttackedMonster += HandlePlayerAttackedMonster;
    }

    private void OnDisable()
    {
        MonsterStats.PlayerAttackedMonster -= HandlePlayerAttackedMonster;
    }

    private void HandlePlayerAttackedMonster(MonsterIdentityManager attackedMonster)
    {
        if (selfIdentity == null || !selfIdentity.gameObject.activeInHierarchy)
        {
            return;
        }

        if (selfIdentity.faction != MonsterIdentityManager.MonsterFaction.Dog)
        {
            return;
        }

        if (attackedMonster == null || !attackedMonster.IsDogLeader)
        {
            return;
        }

        if (hateSystem == null)
        {
            hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
        }

        hateSystem?.AddBaselineHateForPlayer(playerBaselineHate);
    }
}
