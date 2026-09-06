// 역할: 도시 오브젝트의 프레임 기반 스프라이트 애니메이션.
using UnityEngine;

namespace FarmFarm.Exteriors
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ExteriorAnimation : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer view;
        [SerializeField]
        private Sprite[] frames;
        [Min(0.1f)]
        [SerializeField]
        private float framesPerSecond = 8;
        [SerializeField]
        private Vector2 travel;
        [Min(0.1f)]
        [SerializeField]
        private float travelPeriod = 8;
        [SerializeField]
        private float phase;
        private Vector3 origin;
        private float elapsed;
        private int currentFrame = -1;
        private bool originCaptured;
        public int CurrentFrame => currentFrame;

        public void Configure(SpriteRenderer renderer, Sprite[] sprites, float fps, Vector2 movement, float offset)
        {
            view = renderer;
            frames = sprites;
            framesPerSecond = fps;
            travel = movement;
            phase = offset;
            origin = transform.localPosition;
            originCaptured = true;
            Tick(0);
        }

        private void Awake()
        {
            origin = transform.localPosition;
            originCaptured = true;
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (view == null || frames == null || frames.Length == 0)
                return;
            if (!originCaptured)
            {
                origin = transform.localPosition;
                originCaptured = true;
            }

            elapsed += dt;
            int frame = Mathf.FloorToInt((elapsed + phase) * framesPerSecond) % frames.Length;
            if (frame != currentFrame)
            {
                currentFrame = frame;
                view.sprite = frames[frame];
            }

            if (travel != Vector2.zero)
            {
                float t = Mathf.PingPong((elapsed + phase) / travelPeriod, 1);
                transform.localPosition = origin + (Vector3)(travel * t);
                view.flipX = Mathf.Repeat((elapsed + phase) / travelPeriod, 2) >= 1;
            }
        }
    }
}
