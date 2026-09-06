// 역할: 주어진 차로를 따라 이동하며 선행 차량과 교차로 신호를 확인한다.
using UnityEngine;

namespace FarmFarm.Exteriors
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TrafficVehicle : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer view;
        [SerializeField]
        private Sprite[] cardinalSprites;
        [Min(0.1f)]
        [SerializeField]
        private float cruiseSpeed = 7f;
        public int Lane { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float CruiseSpeed => cruiseSpeed;
        public bool WaitingToSpawn { get; private set; }
        public int CompletedTrips { get; private set; }

        private Vector2 origin;
        private Vector2 direction;
        public void Configure(SpriteRenderer renderer, Sprite[] sprites, float speed)
        {
            view = renderer;
            cardinalSprites = sprites;
            cruiseSpeed = speed;
        }

        public void Place(int lane, Vector2 start, Vector2 heading, float distance)
        {
            Lane = lane;
            origin = start;
            direction = heading;
            Distance = distance;
            Speed = 0;
            WaitingToSpawn = false;
            view.enabled = true;
            int index = heading.x > 0.5f ? 0 : heading.y > 0.5f ? 1 : heading.x < -0.5f ? 2 : 3;
            view.sprite = cardinalSprites[index];
            UpdatePosition();
        }

        public void Advance(float distance, float speed)
        {
            Distance += distance;
            Speed = speed;
            UpdatePosition();
        }

        public void FinishTrip()
        {
            CompletedTrips++;
            WaitingToSpawn = true;
            Speed = 0;
            view.enabled = false;
        }

        private void UpdatePosition() => transform.position = origin + direction * Distance;
    }
}
