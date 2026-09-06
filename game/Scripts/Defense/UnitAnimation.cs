// 역할: 방향과 이동/행동 상태에 따라 프레임 스프라이트를 선택한다.
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class UnitAnimation : MonoBehaviour
{
    [SerializeField]
    private UnitSkin skin;
    private SpriteRenderer view;
    private Vector3 previous;
    private int facing = 3;
    private float actionUntil, actionStart;
    private float locomotionTime, lastMovement;
    private bool wasMoving;
    public bool IsActing => Time.time < actionUntil;
    private float ActionRate => skin.actionFramesPerSecond > 0 ? skin.actionFramesPerSecond : skin.framesPerSecond;
    public float ActionDuration => skin == null ? 0 : skin.directions[facing].action.Length / Mathf.Max(1, ActionRate);

    private void Awake()
    {
        view = GetComponent<SpriteRenderer>();
        previous = transform.position;
    }

    public void Configure(UnitSkin value)
    {
        skin = value;
        view = GetComponent<SpriteRenderer>();
        previous = transform.position;
        Render(false);
    }

    public void Face(Vector2 direction)
    {
        if (direction.sqrMagnitude < .0001f)
            return;
        facing = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? (direction.x > 0 ? 0 : 2) : (direction.y > 0 ? 1 : 3);
    }

    public void PlayAction(Vector2 direction)
    {
        Face(direction);
        actionStart = Time.time;
        actionUntil = Time.time + ActionDuration;
        Render(false);
    }

    private void LateUpdate()
    {
        Vector2 delta = transform.position - previous;
        previous = transform.position;
        bool moved = delta.sqrMagnitude > .000001f;
        if (moved)
        {
            lastMovement = Time.time;
            if (!IsActing)
                Face(delta);
        }

        // A render frame without a physics step is not an idle transition.
        bool moving = moved || wasMoving && Time.time - lastMovement < .075f;
        if (moving != wasMoving)
            locomotionTime = 0;
        else
            locomotionTime += Time.deltaTime;
        wasMoving = moving;
        Render(moving);
    }

    private void Render(bool moving)
    {
        if (skin == null || view == null)
            return;
        var d = skin.directions[facing];
        var frames = IsActing && d.action.Length > 0 ? d.action : moving && d.walk.Length > 0 ? d.walk : d.idle;
        if (frames.Length == 0)
            return;
        float elapsed = IsActing ? Time.time - actionStart : locomotionTime;
        float rate = IsActing ? ActionRate : moving && skin.walkFramesPerSecond > 0 ? skin.walkFramesPerSecond : skin.framesPerSecond;
        view.sprite = frames[Mathf.FloorToInt(elapsed * Mathf.Max(1, rate)) % frames.Length];
    }
}
