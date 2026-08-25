using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲームで扱う全FlowerDataへの参照をまとめたデータベース。
/// FlowerDataAssetGeneratorから自動更新されます。
/// </summary>
[CreateAssetMenu(fileName = "FlowerDatabase", menuName = "HanaMonogatari/Flower Database")]
public class FlowerDatabase : ScriptableObject
{
    public List<FlowerData> flowers = new();
}
