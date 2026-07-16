using System;
using UnityEngine;

public interface IDamageable
{
    void TakeHit(HitData hitData);
}

public interface IEnemy
{
    bool HasMark { get; }
    void RemoveMark();
}

public interface IHealthTarget
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDead { get; }

    event Action<IHealthTarget> HealthChanged;
    event Action<IHealthTarget> Died;
}

public struct HitData
{
    public float damage;
    public float knockbackForce;
    public Vector2 sourcePosition;
    public bool applyMark;
}
