using System;
using UnityEngine;

[Serializable]
public class MemoryLevelDatabase
{
    public MemoryLevelData[] levels;
}

[Serializable]
public class MemoryLevelData
{
    public int columns;
    public int rows;

    public float timeLimitSeconds;
    public float bonusTimePerMatch;

    public int TotalCards => columns * rows;
}