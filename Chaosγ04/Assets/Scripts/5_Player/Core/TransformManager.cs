using System.Collections.Generic;
using UnityEngine;

public class TransformManager : MonoBehaviour
{
    [Header("监测设置")]
    [Tooltip("搜索与监测的时间间隔（秒），避免每帧 FindGameObjectsWithTag 导致 GC 卡顿")]
    [SerializeField] private float searchInterval = 0.5f;

    [Header("实时监测结果（仅供 Inspector 预览）")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private List<Transform> enemyTransforms = new List<Transform>();

    private float timer;

    public Transform PlayerTransform => playerTransform;
    public IReadOnlyList<Transform> EnemyTransforms => enemyTransforms;

    private void Update()
    {
        timer += Time.deltaTime;

        // 定时轮询监测，兼顾实时性与性能
        if (timer >= searchInterval)
        {
            timer = 0f;
            ScanTargets();
        }

        // 每帧执行具体的追踪/距离检测逻辑
        TrackLogic();
    }

    /// <summary>
    /// 自动扫描并更新 Player 与 Monster 的根/父级 Transform
    /// </summary>
    private void ScanTargets()
    {
        // 1. 监测 Player（确保取顶层父物体）
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform.root;
        }
        else
        {
            playerTransform = null;
        }

        // 2. 监测 Monster 列表
        GameObject[] monsterObjs = GameObject.FindGameObjectsWithTag("Monster");

        enemyTransforms.Clear();

        foreach (GameObject monster in monsterObjs)
        {
            if (monster == null) continue;

            // 获取最外层的父物体（Root Transform）
            Transform rootMonster = monster.transform.root;

            // 防止带有 Monster 标签的多个子物体重复添加同一个父物体
            if (!enemyTransforms.Contains(rootMonster))
            {
                enemyTransforms.Add(rootMonster);
            }
        }
    }

    /// <summary>
    /// 实时监测业务逻辑（根据需要编写）
    /// </summary>
    private void TrackLogic()
    {
        if (playerTransform == null) return;

        for (int i = 0; i < enemyTransforms.Count; i++)
        {
            Transform enemy = enemyTransforms[i];
            if (enemy == null) continue;

            // 示例：实时获取玩家与各怪物父物体的距离
            float distance = Vector3.Distance(playerTransform.position, enemy.position);

            // 在此添加你的逻辑（如：计算最近敌人、范围预警等）
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 调试绘制：绿色球代表玩家父物体，红色球代表怪物父物体
        if (playerTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerTransform.position, 1f);
        }

        Gizmos.color = Color.red;
        foreach (Transform enemy in enemyTransforms)
        {
            if (enemy != null)
            {
                Gizmos.DrawWireSphere(enemy.position, 0.8f);
            }
        }
    }
}