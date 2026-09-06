// 역할: 교차로 신호 상태와 신호등 표시를 관리한다.
using UnityEngine;

namespace FarmFarm.Exteriors
{
    public sealed class TrafficSignal : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer[] horizontalLights;
        [SerializeField]
        private SpriteRenderer[] verticalLights;
        [SerializeField]
        private Sprite red;
        [SerializeField]
        private Sprite yellow;
        [SerializeField]
        private Sprite green;
        [Min(1)]
        [SerializeField]
        private float greenSeconds = 8f;
        [Min(0.1f)]
        [SerializeField]
        private float yellowSeconds = 2f;
        [Min(0.1f)]
        [SerializeField]
        private float clearanceSeconds = 1.5f;
        [SerializeField]
        private float phaseOffset;
        private int displayedPhase = -1;
        private TrafficVehicle occupant;
        public Vector2 Center => transform.position;
        public TrafficVehicle Occupant => occupant;

        public void Configure(SpriteRenderer[] horizontal, SpriteRenderer[] vertical, Sprite redSprite, Sprite yellowSprite, Sprite greenSprite, float offset)
        {
            horizontalLights = horizontal;
            verticalLights = vertical;
            red = redSprite;
            yellow = yellowSprite;
            green = greenSprite;
            phaseOffset = offset;
            Refresh(0);
        }

        public int Phase(float time)
        {
            float half = greenSeconds + yellowSeconds + clearanceSeconds;
            float t = Mathf.Repeat(time + phaseOffset, half * 2f);
            int axis = t >= half ? 3 : 0;
            t %= half;
            return axis + (t < greenSeconds ? 0 : t < greenSeconds + yellowSeconds ? 1 : 2);
        }

        public bool CanEnter(bool horizontal, float time) => Phase(time) == (horizontal ? 0 : 3);
        public bool Reserve(TrafficVehicle vehicle, bool horizontal, float time)
        {
            if (occupant == vehicle)
                return true;
            if (occupant != null || !CanEnter(horizontal, time))
                return false;
            occupant = vehicle;
            return true;
        }

        public void Release(TrafficVehicle vehicle)
        {
            if (occupant == vehicle)
                occupant = null;
        }

        public void ResetSignal()
        {
            occupant = null;
            displayedPhase = -1;
        }

        public void Refresh(float time)
        {
            int phase = Phase(time);
            if (phase == displayedPhase)
                return;
            displayedPhase = phase;
            foreach (var light in horizontalLights)
                if (light != null)
                    light.sprite = phase == 0 ? green : phase == 1 ? yellow : red;
            foreach (var light in verticalLights)
                if (light != null)
                    light.sprite = phase == 3 ? green : phase == 4 ? yellow : red;
        }

        private void OnDisable()
        {
            displayedPhase = -1;
        }
    }
}
