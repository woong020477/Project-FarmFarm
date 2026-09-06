// 역할: 순찰 경로를 따라 위협을 선택·추격하고 사거리/시야/공격 주기를 통과하면 사격한다.
using UnityEngine;

/// <summary>Patrols the perimeter, selects nearby threats and fires only with line of sight.</summary>
public sealed class Mercenary : MonoBehaviour
{
    private Defense owner;
    private MercenaryState state;
    private float pathDistance, nextThink, nextShot;
    private Zombie target;
    private UnitAnimation animationView;
    public string Id => state.id;
    public bool Alive => state != null && state.health > 0;
    public bool InCombat => target != null && target.Alive;
    public float CurrentMoveSpeed => (InCombat ? owner.CombatSpeed : owner.PatrolSpeed) * (GameEvents.Instance == null ? 1 : GameEvents.Instance.GetMultiplier(StatType.MercenarySpeed));

    public void Damage(float amount)
    {
        state.health = Mathf.Max(0, state.health - amount);
        if (!Alive)
            GetComponent<SpriteRenderer>().color = Color.gray;
    }

    public void Initialize(Defense defense, MercenaryState data, int index)
    {
        owner = defense;
        state = data;
        animationView = GetComponent<UnitAnimation>();
        pathDistance = index * 3;
        transform.position = owner.PointOnPath(pathDistance);
    }

    public void RestoreHealth(float health) => state.health = Mathf.Min(state.health, health);
    public MercenaryState Capture() => new()
    {
        id = state.id,
        tier = state.tier,
        health = state.health
    };
    private void Update()
    {
        if (owner == null || state.health <= 0 || GameManager.Instance?.Progression?.IsReady != true)
            return;
        if (Time.time >= nextThink)
        {
            nextThink = Time.time + .1f;
            FindTarget();
        }

        if (target != null && !target.Alive)
            target = null;
        float speed = CurrentMoveSpeed;
        if (target == null)
            pathDistance += Time.deltaTime * speed;
        else
        {
            float goal = owner.NearestPathDistance(target.transform.position);
            float delta = PathDelta(goal);
            pathDistance += Mathf.Clamp(delta, -Time.deltaTime * speed, Time.deltaTime * speed);
            TryFire();
        }

        pathDistance = Mathf.Repeat(pathDistance, owner.LoopLength);
        transform.position = owner.PointOnPath(pathDistance);
        if (state.health <= 0)
            GetComponent<SpriteRenderer>().color = Color.gray;
    }

    // Shared alarm, shortest perimeter distance. Guards never cut across a building.
    private void FindTarget()
    {
        target = null;
        float nearest = float.MaxValue;
        foreach (Zombie zombie in owner.Zombies)
        {
            if (!owner.IsThreat(zombie))
                continue;
            float distance = Mathf.Abs(PathDelta(owner.NearestPathDistance(zombie.transform.position)));
            if (distance >= nearest)
                continue;
            nearest = distance;
            target = zombie;
        }
    }

    private float PathDelta(float goal) => Mathf.Repeat(goal - pathDistance + owner.LoopLength * .5f, owner.LoopLength) - owner.LoopLength * .5f;
    // A presentation event is raised only after range, cooldown and occlusion checks.
    private void TryFire()
    {
        if (Vector2.Distance(transform.position, target.transform.position) >= owner.AttackRange || Time.time < nextShot || !owner.Navigation.HasSight(transform.position, target.transform.position))
            return;
        float rate = GameEvents.Instance == null ? 1 : GameEvents.Instance.GetMultiplier(StatType.MercenaryAttackSpeed);
        float attack = GameEvents.Instance == null ? 1 : GameEvents.Instance.GetMultiplier(StatType.MercenaryAttack);
        nextShot = Time.time + 1f / ((1 + state.tier * .5f) * rate);
        animationView?.PlayAction(target.transform.position - transform.position);
        owner.ShowShot(transform.position, target.transform.position);
        target.Damage(owner.AttackDamage(state.tier) * attack);
    }
}
