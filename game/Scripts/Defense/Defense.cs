// 역할: 공유 바리케이드 체력, 적 생성, 용병 목록과 방호 저장 상태를 관리한다.
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class Defense : MonoBehaviour
{
    [SerializeField]
    private Rect perimeter = new(-20, -16, 40, 48);
    [SerializeField]
    private Rect spawnBounds = new(-100, -100, 200, 200);
    [SerializeField]
    private Sprite primitiveSprite;
    [SerializeField]
    private Transform actorParent;
    [SerializeField]
    private DefenseMap navigation;
    [SerializeField]
    private UnitSkin[] unitSkins;
    [SerializeField]
    private UnitSkin zombieSkin;
    [SerializeField]
    private float zombieBaseHealth = 5, zombieHealthPerLevel = 2, spawnSeconds = 8;
    [SerializeField]
    private int maxVisibleZombies = 80;
    [Header("Mercenary balance")]
    [Min(1)]
    [SerializeField]
    private float infantryDamage = 8, damagePerTier = 12;
    [Min(.1f)]
    [SerializeField]
    private float patrolSpeed = 2, combatSpeed = 14, detectionRange = 28, attackRange = 6;
    [Min(.1f)]
    [SerializeField]
    private float barricadeAlertDistance = 3;
    private readonly List<Zombie> zombies = new();
    private readonly List<Mercenary> mercenaries = new();
    private ProgressState state;
    private float nextSpawn;
    private int pendingDamage;
    private bool breachSent;
    public int PendingDamage => pendingDamage;

    public const int MaxHealth = 10000;
    public int Health => Mathf.Clamp((state?.hp ?? MaxHealth) - pendingDamage, 0, MaxHealth);

    public float DistanceToBarricade(Vector2 p)
    {
        Rect r = navigation != null ? navigation.barrierBounds : perimeter;
        return Vector2.Distance(p, new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax)));
    }

    public bool IsThreat(Zombie zombie) => zombie != null && zombie.Alive && DistanceToBarricade(zombie.transform.position) <= barricadeAlertDistance;
    public long ProtectedUntil => state?.protectedUntil ?? 0;
    public IReadOnlyList<Zombie> Zombies => zombies;
    public Rect Perimeter => perimeter;
    public DefenseMap Navigation => navigation;
    public float PatrolSpeed => patrolSpeed;
    public float CombatSpeed => combatSpeed;
    public float DetectionRange => detectionRange;
    public float AttackRange => attackRange;

    public float AttackDamage(int tier) => infantryDamage + Mathf.Max(0, tier) * damagePerTier;
    public float ZombieHealthAtLevel(long level) => zombieBaseHealth + zombieHealthPerLevel * Mathf.Max(0, level - 1);
    public Sprite ZombieIcon => zombieSkin != null ? zombieSkin.directions[3].idle[0] : null;

    public Sprite UnitIcon(int tier) => unitSkins != null && tier >= 0 && tier < unitSkins.Length ? unitSkins[tier].directions[3].idle[0] : null;
    public event Action Changed;
    public event Action ShotFired;
    public event Action<Vector2> BarricadeHit;
    public void Restore(ProgressState next)
    {
        state = next;
        breachSent = false;
        if (ProtectedUntil > GameManager.Instance.Progression.Now)
            ClearZombies();
        var ids = new HashSet<string>();
        foreach (var data in next.mercenaries)
        {
            ids.Add(data.id);
            var unit = mercenaries.Find(m => m.Id == data.id);
            if (unit == null)
            {
                var go = CreateActor("Mercenary_" + data.id, Color.white, 1);
                go.AddComponent<UnitAnimation>().Configure(unitSkins[data.tier]);
                unit = go.AddComponent<Mercenary>();
                unit.Initialize(this, data, mercenaries.Count);
                mercenaries.Add(unit);
            }
            else
                unit.RestoreHealth(data.health);
        }

        for (int i = mercenaries.Count - 1; i >= 0; i--)
            if (!ids.Contains(mercenaries[i].Id))
            {
                Destroy(mercenaries[i].gameObject);
                mercenaries.RemoveAt(i);
            }

        Changed?.Invoke();
    }

    private void Update()
    {
        var progression = GameManager.Instance?.Progression;
        if (state == null || progression == null || !progression.IsReady)
            return;
        if (state.protectedUntil > progression.Now)
            return;
        if (state.protectedUntil > 0)
        {
            if (!breachSent && GameManager.Instance.Inventory.CanSubmitInventoryMutation)
            {
                progression.SaveDefense();
                breachSent = progression.IsBusy;
            }

            return;
        }

        if (Health <= 0)
        {
            ClearZombies();
            if (!progression.IsBusy && !breachSent && GameManager.Instance.Inventory.CanSubmitInventoryMutation)
            {
                progression.SaveDefense();
                breachSent = progression.IsBusy;
            }

            return;
        }

        if (Time.time < nextSpawn || zombies.Count >= maxVisibleZombies)
            return;
        float rate = GameEvents.Instance == null ? 1 : GameEvents.Instance.GetMultiplier(StatType.ZombieSpawnRate);
        nextSpawn = Time.time + Mathf.Max(.5f, spawnSeconds / (1 + Mathf.Log(1 + (float)state.level) * .35f) / rate);
        if (navigation == null || navigation.SpawnPoints.Count == 0)
            return;
        Vector2 spawn = navigation.SpawnPoints[UnityEngine.Random.Range(0, navigation.SpawnPoints.Count)];
        var go = CreateActor("Zombie", Color.white, 1);
        go.transform.position = spawn;
        go.AddComponent<UnitAnimation>().Configure(zombieSkin);
        var zombie = go.AddComponent<Zombie>();
        float hp = ZombieHealthAtLevel(state.level) * (GameEvents.Instance == null ? 1 : GameEvents.Instance.GetMultiplier(StatType.ZombieHealth));
        zombie.Initialize(this, spawn, hp);
        zombies.Add(zombie);
    }

    public void HitBarricade() => HitBarricade(new Vector2(perimeter.center.x, perimeter.yMin));
    public void HitBarricade(Vector2 hitPosition)
    {
        if (Health <= 0 || ProtectedUntil > (GameManager.Instance?.Progression?.Now ?? 0))
            return;
        pendingDamage++;
        Changed?.Invoke();
        BarricadeHit?.Invoke(hitPosition);
    }

    public Mercenary FindNearbyGuard(Vector2 position)
    {
        foreach (var guard in mercenaries)
            if (guard.Alive && Vector2.Distance(position, guard.transform.position) < 1)
                return guard;
        return null;
    }

    public void AcknowledgeDamage(int count) => pendingDamage = Mathf.Max(0, pendingDamage - count);
    public void Remove(Zombie unit)
    {
        zombies.Remove(unit);
        Destroy(unit.gameObject);
    }

    private void ClearZombies()
    {
        foreach (var z in zombies)
            if (z != null)
                Destroy(z.gameObject);
        zombies.Clear();
    }

    public MercenaryState[] CaptureMercenaries()
    {
        var result = new MercenaryState[mercenaries.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = mercenaries[i].Capture();
        return result;
    }

    public float LoopLength => 2 * (perimeter.width + perimeter.height);

    public void ShowShot(Vector2 from, Vector2 to)
    {
        ShotFired?.Invoke();
        var shot = CreateActor("Shot", new Color(1, .85f, .2f), 1);
        Vector2 delta = to - from;
        shot.transform.position = (from + to) * .5f;
        shot.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        shot.transform.localScale = new Vector3(delta.magnitude, .07f, 1);
        Destroy(shot, .08f);
    }

    public Vector2 PointOnPath(float distance)
    {
        float d = Mathf.Repeat(distance, LoopLength);
        if (d < perimeter.height)
            return new Vector2(perimeter.xMin, perimeter.yMin + d);
        d -= perimeter.height;
        if (d < perimeter.width)
            return new Vector2(perimeter.xMin + d, perimeter.yMax);
        d -= perimeter.width;
        if (d < perimeter.height)
            return new Vector2(perimeter.xMax, perimeter.yMax - d);
        d -= perimeter.height;
        return new Vector2(perimeter.xMax - d, perimeter.yMin);
    }

    public float NearestPathDistance(Vector2 target)
    {
        float best = 0, squared = float.MaxValue;
        for (float d = 0; d < LoopLength; d += .5f)
        {
            float e = (PointOnPath(d) - target).sqrMagnitude;
            if (e < squared)
            {
                squared = e;
                best = d;
            }
        }

        return best;
    }

    private GameObject CreateActor(string label, Color color, float size)
    {
        var go = new GameObject(label);
        go.transform.SetParent(actorParent == null ? transform : actorParent, false);
        go.transform.localScale = Vector3.one * size;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = primitiveSprite;
        renderer.color = color;
        renderer.sortingOrder = 35;
        return go;
    }

    private static Color ColorForTier(int tier) => tier == 0 ? new Color(.3f, .65f, 1) : tier == 1 ? new Color(.9f, .75f, .2f) : new Color(.75f, .4f, 1);
}
