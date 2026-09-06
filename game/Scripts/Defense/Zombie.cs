// 역할: 통행 가능한 경로를 따라 기지로 이동하고 도착한 대상을 공격한다.
using UnityEngine;

public sealed class Zombie : MonoBehaviour
{
    private Defense owner;
    private Vector2 destination;
    private float health, nextAttack;
    private bool hasWaypoint, atBarrier;
    private UnitAnimation animationView;
    public bool Alive => health > 0;

    public void Initialize(Defense defense, Vector2 target, float hp)
    {
        owner = defense;
        destination = target;
        health = hp;
        hasWaypoint = false;
        animationView = GetComponent<UnitAnimation>();
    }

    private void Update() => Tick(Time.deltaTime);
    public void Tick(float deltaTime)
    {
        if (!Alive || owner == null || GameManager.Instance?.Progression?.IsReady != true || owner.ProtectedUntil > GameManager.Instance.Progression.Now)
            return;
        if (owner.Navigation == null)
            return;
        if (!hasWaypoint)
        {
            if (!owner.Navigation.Next(transform.position, out destination, out atBarrier))
                return;
            hasWaypoint = true;
        }

        Vector2 next = Vector2.MoveTowards(transform.position, destination, 1.4f * deltaTime);
        if (!owner.Navigation.IsWalkable(next))
            return;
        transform.position = next;
        if (Vector2.Distance(next, destination) < .001f)
            hasWaypoint = false;
        if (Time.time < nextAttack)
            return;
        var guard = owner.FindNearbyGuard(transform.position);
        if (guard != null && owner.Navigation.HasSight(transform.position, guard.transform.position))
        {
            nextAttack = Time.time + 1;
            animationView?.PlayAction(guard.transform.position - transform.position);
            guard.Damage(1);
            return;
        }

        if (!atBarrier || Vector2.Distance(transform.position, destination) > .05f)
            return;
        nextAttack = Time.time + 1;
        animationView?.PlayAction(owner.Perimeter.center - (Vector2)transform.position);
        owner.HitBarricade(transform.position);
    }

    public void Damage(float amount)
    {
        health -= Mathf.Max(0, amount);
        if (health <= 0)
            owner.Remove(this);
    }
}
