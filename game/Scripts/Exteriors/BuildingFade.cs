// 역할: 주체가 가려지는 건물 영역의 투명도를 조절하는 시각 효과.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmFarm.Exteriors
{
    public sealed class BuildingFade : MonoBehaviour
    {
        [Serializable]
        public sealed class Building
        {
            public string name;
            public Rect area;
            public Vector3Int[] cells;
            [NonSerialized]
            public float alpha = 1;
        }

        [SerializeField]
        private Tilemap canopy;
        [SerializeField, HideInInspector]
        private Transform player;
        [SerializeField]
        private Building[] buildings;
        [SerializeField]
        private Material revealMaterial;
        [Range(0, 1)]
        [SerializeField]
        private float hiddenAlpha = 0;
        [Min(0.05f)]
        [SerializeField]
        private float fadeSeconds = 0.8f;
        [Min(0.05f)]
        [SerializeField]
        private float feather = 0.75f;
        [Min(0)]
        [SerializeField]
        private float approachDistance = 0.6f;
        private sealed class Wave
        {
            public Vector2 center;
            public float progress;
        }

        private sealed class Region
        {
            public Building building;
            public TilemapRenderer renderer;
            public Bounds bounds;
            public MaterialPropertyBlock properties = new();
            public Dictionary<Transform, Wave> waves = new();
            public Vector4[] sources = new Vector4[GameManager.MaxDroneCount + 1];
        }

        private static readonly int SourcesId = Shader.PropertyToID("_RevealSources");
        private static readonly int CountId = Shader.PropertyToID("_RevealCount");
        private static readonly int FeatherId = Shader.PropertyToID("_RevealFeather");
        private static readonly int HiddenId = Shader.PropertyToID("_HiddenAlpha");
        private readonly List<Region> regions = new();
        private readonly List<Transform> actors = new();
        private readonly List<Transform> staleActors = new();
        private Transform[] previewActors;
        private TilemapRenderer sourceRenderer;
        private GameObject renderRoot;
        private bool sourceWasEnabled;
        public Building[] Buildings => buildings;
        public Tilemap Source => canopy;

        public void Configure(Tilemap map, Transform target, Building[] areas)
        {
            Release();
            canopy = map;
            player = target;
            buildings = areas;
        }

        public void SetTarget(Transform target)
        {
            player = target;
            previewActors = target == null ? Array.Empty<Transform>() : new[]
            {
                target
            };
        }

        public void SetPreviewActors(params Transform[] targets) => previewActors = targets;
        public void SetMaterial(Material material) => revealMaterial = material;
        private void LateUpdate() => Tick(Time.deltaTime);
        // Original tiles remain untouched; each region receives its own render copy.
        public void Initialize()
        {
            if (renderRoot != null || canopy == null || revealMaterial == null || buildings == null)
                return;
            sourceRenderer = canopy.GetComponent<TilemapRenderer>();
            if (sourceRenderer == null)
                return;
            sourceWasEnabled = sourceRenderer.enabled;
            renderRoot = new GameObject("BuildingReveal_RenderCopies");
            renderRoot.hideFlags = HideFlags.DontSave;
            renderRoot.transform.SetParent(canopy.transform, false);
            var claimed = new HashSet<Vector3Int>();
            foreach (var building in buildings)
            {
                if (building.cells == null || building.cells.Length == 0)
                    continue;
                var cells = new List<Vector3Int>();
                foreach (var cell in building.cells)
                    if (canopy.HasTile(cell) && claimed.Add(cell))
                        cells.Add(cell);
                if (cells.Count == 0)
                    continue;
                var map = CopyCells(building.name, cells, revealMaterial);
                var rect = building.area;
                var bounds = new Bounds(canopy.transform.TransformPoint(new Vector3(rect.center.x, rect.center.y)), Vector3.zero);
                bounds.Encapsulate(canopy.transform.TransformPoint(new Vector3(rect.xMin, rect.yMin)));
                bounds.Encapsulate(canopy.transform.TransformPoint(new Vector3(rect.xMax, rect.yMax)));
                regions.Add(new Region { building = building, renderer = map.GetComponent<TilemapRenderer>(), bounds = bounds });
            }

            var remainder = new List<Vector3Int>();
            foreach (var cell in canopy.cellBounds.allPositionsWithin)
                if (canopy.HasTile(cell) && !claimed.Contains(cell))
                    remainder.Add(cell);
            if (remainder.Count > 0)
                CopyCells("UnchangedDetails", remainder, sourceRenderer.sharedMaterial);
            sourceRenderer.enabled = false;
        }

        private Tilemap CopyCells(string label, List<Vector3Int> cells, Material material)
        {
            var go = new GameObject(label, typeof(Tilemap), typeof(TilemapRenderer));
            go.hideFlags = HideFlags.DontSave;
            go.layer = canopy.gameObject.layer;
            go.transform.SetParent(renderRoot.transform, false);
            var map = go.GetComponent<Tilemap>();
            map.tileAnchor = canopy.tileAnchor;
            map.orientation = canopy.orientation;
            map.orientationMatrix = canopy.orientationMatrix;
            map.color = canopy.color;
            var tiles = new TileBase[cells.Count];
            for (int i = 0; i < cells.Count; i++)
                tiles[i] = canopy.GetTile(cells[i]);
            map.SetTiles(cells.ToArray(), tiles);
            foreach (var p in cells)
            {
                map.SetTileFlags(p, TileFlags.None);
                map.SetColor(p, canopy.GetColor(p));
                map.SetTransformMatrix(p, canopy.GetTransformMatrix(p));
            }

            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            renderer.mode = sourceRenderer.mode;
            renderer.sortOrder = sourceRenderer.sortOrder;
            renderer.enabled = sourceWasEnabled;
            return map;
        }

        public void Tick(float dt)
        {
            Initialize();
            if (renderRoot == null)
                return;
            actors.Clear();
            if (previewActors != null)
            {
                foreach (var actor in previewActors)
                    AddActor(actor);
            }
            else if (GameManager.Instance != null)
            {
                var manager = GameManager.Instance;
                if (manager.Player != null)
                    AddActor(manager.Player.transform);
                foreach (var drone in manager.Drones)
                    if (drone != null)
                        AddActor(drone.transform);
            }
            else
                AddActor(player);
            foreach (var region in regions)
            {
                staleActors.Clear();
                foreach (var pair in region.waves)
                {
                    if (actors.Contains(pair.Key))
                        continue;
                    pair.Value.progress = Mathf.MoveTowards(pair.Value.progress, 0, dt / fadeSeconds);
                    if (pair.Value.progress <= 0)
                        staleActors.Add(pair.Key);
                }

                foreach (var actor in staleActors)
                    region.waves.Remove(actor);
                foreach (var actor in actors)
                {
                    var position = actor.position;
                    position.z = region.bounds.center.z;
                    bool near = region.bounds.SqrDistance(position) <= approachDistance * approachDistance;
                    if (!region.waves.TryGetValue(actor, out var wave))
                    {
                        if (!near || region.waves.Count >= region.sources.Length)
                            continue;
                        wave = new Wave
                        {
                            center = position
                        };
                        region.waves.Add(actor, wave);
                    }

                    if (near)
                        wave.center = position;
                    wave.progress = Mathf.MoveTowards(wave.progress, near ? 1 : 0, dt / fadeSeconds);
                }

                int count = 0;
                float maximumProgress = 0;
                foreach (var wave in region.waves.Values)
                {
                    if (wave.progress <= 0)
                        continue;
                    Vector2 extents = region.bounds.extents;
                    Vector2 offset = wave.center - (Vector2)region.bounds.center;
                    float reach = new Vector2(Mathf.Abs(offset.x) + extents.x, Mathf.Abs(offset.y) + extents.y).magnitude + feather;
                    region.sources[count++] = new Vector4(wave.center.x, wave.center.y, wave.progress * reach, 0);
                    maximumProgress = Mathf.Max(maximumProgress, wave.progress);
                }

                region.building.alpha = Mathf.Lerp(1, hiddenAlpha, maximumProgress);
                region.renderer.GetPropertyBlock(region.properties);
                region.properties.SetVectorArray(SourcesId, region.sources);
                region.properties.SetInt(CountId, count);
                region.properties.SetFloat(FeatherId, feather);
                region.properties.SetFloat(HiddenId, hiddenAlpha);
                region.renderer.SetPropertyBlock(region.properties);
            }
        }

        private void AddActor(Transform actor)
        {
            if (actor != null && actor.gameObject.activeInHierarchy && actors.Count < GameManager.MaxDroneCount + 1 && !actors.Contains(actor))
                actors.Add(actor);
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();
        public void Release()
        {
            if (renderRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(renderRoot);
                else
                    DestroyImmediate(renderRoot);
                if (sourceRenderer != null)
                    sourceRenderer.enabled = sourceWasEnabled;
            }

            renderRoot = null;
            regions.Clear();
            if (buildings != null)
                foreach (var building in buildings)
                    building.alpha = 1;
        }
    }
}
