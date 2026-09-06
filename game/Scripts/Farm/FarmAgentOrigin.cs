// 역할: 애니메이션과 운반물 크기에 영향을 받지 않는 농사 행동 기준점을 제공한다.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FarmAgentOrigin : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer bodySprite;
    [SerializeField]
    private bool animatedBody;
    public Vector3 Position => !animatedBody && bodySprite != null ? bodySprite.bounds.center : transform.position;

    private void Awake() => Initialize();
    public void Initialize()
    {
        // Only the body's renderer: carried crops must never move the action origin.
        if (bodySprite == null)
            bodySprite = GetComponent<SpriteRenderer>();
    }
}
