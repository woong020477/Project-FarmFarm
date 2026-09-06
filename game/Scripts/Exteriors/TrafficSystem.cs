// 역할: 차량 생성과 도시 교통 경로/교차로 사용을 조정한다.
using System;
using UnityEngine;

namespace FarmFarm.Exteriors
{
    // Local decorative simulation: no physics collision, server calls or persistent economy state.
    public sealed class TrafficSystem : MonoBehaviour
    {
        [Serializable]
        public sealed class Lane
        {
            public Vector2 start;
            public Vector2 end;
            public TrafficSignal[] signals;
            public Vector2 Direction => (end - start).normalized;
            public float Length => Vector2.Distance(start, end);
        }

        [SerializeField]
        private Lane[] lanes;
        [SerializeField]
        private TrafficVehicle[] vehiclePrefabs;
        [SerializeField]
        private TrafficSignal[] signals;
        [SerializeField]
        private Transform vehicleParent;
        [Range(1, 8)]
        [SerializeField]
        private int vehiclesPerLane = 3;
        [Min(4.5f)]
        [SerializeField]
        private float followingDistance = 5.5f;
        [Min(0.1f)]
        [SerializeField]
        private float acceleration = 3f;
        [Min(5f)]
        [SerializeField]
        private float junctionClearance = 7f;
        private TrafficVehicle[] vehicles;
        private float clock;
        private float accumulator;
        public TrafficVehicle[] Vehicles => vehicles;
        public Lane[] Lanes => lanes;
        public float SimulationTime => clock;
        public int SignalStops { get; private set; }
        public int FollowingStops { get; private set; }

        public void Configure(Lane[] routes, TrafficVehicle[] prefabs, TrafficSignal[] intersections, Transform parent)
        {
            lanes = routes;
            vehiclePrefabs = prefabs;
            signals = intersections;
            vehicleParent = parent;
        }

        private void Start() => Initialize();
        public void Initialize()
        {
            if (vehicles != null)
                return;
            if (lanes == null || vehiclePrefabs == null || vehiclePrefabs.Length == 0)
            {
                enabled = false;
                return;
            }

            vehicles = new TrafficVehicle[lanes.Length * vehiclesPerLane];
            for (int l = 0; l < lanes.Length; l++)
            {
                Lane lane = lanes[l];
                for (int n = 0; n < vehiclesPerLane; n++)
                {
                    int i = l * vehiclesPerLane + n;
                    vehicles[i] = Instantiate(vehiclePrefabs[i % vehiclePrefabs.Length], vehicleParent);
                    vehicles[i].name = "Vehicle_" + i.ToString("D2");
                    float distance = n * (lane.Length / vehiclesPerLane) + 2;
                    // Never spawn inside a junction, even when a route's length changes.
                    foreach (var signal in lane.signals)
                    {
                        float center = Vector2.Dot(signal.Center - lane.start, lane.Direction);
                        if (Mathf.Abs(distance - center) < junctionClearance + 1)
                            distance = center + junctionClearance + 2;
                    }

                    vehicles[i].Place(l, lane.start, lane.Direction, distance);
                }
            }
        }

        private void Update()
        {
            accumulator += Mathf.Min(Time.deltaTime, 0.2f);
            while (accumulator >= 0.05f)
            {
                Step(0.05f);
                accumulator -= 0.05f;
            }
        }

        // The same fixed-step method is exercised by the editor simulation tests.
        public void Step(float dt)
        {
            if (vehicles == null || dt <= 0)
                return;
            dt = Mathf.Min(dt, 0.05f);
            clock += dt;
            foreach (var signal in signals)
                signal.Refresh(clock);
            foreach (var vehicle in vehicles)
            {
                if (vehicle == null)
                    continue;
                Lane lane = lanes[vehicle.Lane];
                if (vehicle.WaitingToSpawn)
                {
                    bool clear = true;
                    foreach (var other in vehicles)
                        if (other != vehicle && !other.WaitingToSpawn && other.Lane == vehicle.Lane && other.Distance < followingDistance)
                        {
                            clear = false;
                            break;
                        }

                    if (clear)
                        vehicle.Place(vehicle.Lane, lane.start, lane.Direction, 0);
                    continue;
                }

                float allowed = float.PositiveInfinity;
                foreach (var other in vehicles)
                    if (other != vehicle && !other.WaitingToSpawn && other.Lane == vehicle.Lane && other.Distance > vehicle.Distance)
                        allowed = Mathf.Min(allowed, other.Distance - vehicle.Distance - followingDistance);
                if (allowed < 0.05f)
                    FollowingStops++;
                foreach (var signal in lane.signals)
                {
                    float center = Vector2.Dot(signal.Center - lane.start, lane.Direction);
                    float entry = center - junctionClearance;
                    float exit = center + junctionClearance;
                    if (vehicle.Distance > exit)
                    {
                        signal.Release(vehicle);
                        continue;
                    }

                    if (vehicle.Distance > entry + 0.01f)
                        continue; // Already admitted; finish crossing even on red.
                    float toStop = entry - vehicle.Distance;
                    if (toStop > 3f)
                        continue;
                    bool outgoingClear = allowed > (exit - vehicle.Distance);
                    if (!outgoingClear || !signal.Reserve(vehicle, Mathf.Abs(lane.Direction.x) > 0.5f, clock))
                    {
                        allowed = Mathf.Min(allowed, toStop);
                        if (toStop < 0.05f)
                            SignalStops++;
                    }
                }

                float speed = Mathf.MoveTowards(vehicle.Speed, vehicle.CruiseSpeed, acceleration * dt);
                float step = Mathf.Max(0, Mathf.Min(speed * dt, allowed));
                vehicle.Advance(step, step / dt);
                if (vehicle.Distance >= lane.Length)
                {
                    foreach (var signal in lane.signals)
                        signal.Release(vehicle);
                    vehicle.FinishTrip();
                }
            }
        }

        private void OnDisable()
        {
            accumulator = 0;
        // Preserve reservations while paused/disabled: occupants may still be inside crossings.
        }
    }
}
