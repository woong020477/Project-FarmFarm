// 역할: 공유 방호 피해 이벤트를 받아 피격 위치의 시각 효과를 재생한다.
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class BarricadeImpact : MonoBehaviour
{
    [SerializeField]
    private Tilemap sandbags;
    [SerializeField]
    private Sprite debrisSprite;
    [SerializeField]
    private Color flashColor = new(1, .32f, .12f, 1);
    [Min(.05f)]
    [SerializeField]
    private float flashSeconds = .22f;
    private Defense owner;
    private sealed class Flash
    {
        public Vector3Int cell;
        public Color original;
        public TileFlags flags;
        public float until;
        public bool active;
    }

    private sealed class Debris
    {
        public SpriteRenderer view;
        public Vector2 velocity;
        public float remaining, total;
    }

    private readonly Flash[] flashes = new Flash[24];
    private readonly Debris[] debris = new Debris[64];
    private readonly LineRenderer[] rings = new LineRenderer[6];
    private readonly float[] ringLife = new float[6];
    private Material ringMaterial;
    private int nextRing;
    private int nextDebris, nextFlash;
    public int ActiveDebris { get; private set; }

    private void Awake()
    {
        owner = GetComponent<Defense>();
        for (int i = 0; i < flashes.Length; i++)
            flashes[i] = new Flash();
        for (int i = 0; i < debris.Length; i++)
        {
            var go = new GameObject("ImpactPixel");
            go.transform.SetParent(transform, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = debrisSprite;
            sprite.sortingOrder = 60;
            sprite.enabled = false;
            debris[i] = new Debris
            {
                view = sprite
            };
        }

        ringMaterial = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < rings.Length; i++)
        {
            var go = new GameObject("ImpactRing");
            go.transform.SetParent(transform, false);
            var ring = go.AddComponent<LineRenderer>();
            ring.sharedMaterial = ringMaterial;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 20;
            ring.widthMultiplier = .07f;
            ring.sortingOrder = 61;
            ring.enabled = false;
            rings[i] = ring;
        }
    }

    private void OnEnable()
    {
        if (owner == null)
            owner = GetComponent<Defense>();
        owner.BarricadeHit += Show;
    }

    private void OnDisable()
    {
        if (owner != null)
            owner.BarricadeHit -= Show;
        foreach (var f in flashes)
            if (f != null && f.active)
                Restore(f);
        foreach (var d in debris)
            if (d != null)
            {
                d.remaining = 0;
                d.view.enabled = false;
            }

        for (int i = 0; i < rings.Length; i++)
        {
            ringLife[i] = 0;
            if (rings[i] != null)
                rings[i].enabled = false;
        }
    }

    public void Show(Vector2 position)
    {
        if (sandbags == null)
            return;
        var center = sandbags.WorldToCell(position);
        Vector3 hit = position;
        float nearest = float.MaxValue;
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
            {
                var cell = center + new Vector3Int(x, y);
                if (!sandbags.HasTile(cell))
                    continue;
                float distance = Vector2.SqrMagnitude((Vector2)sandbags.GetCellCenterWorld(cell) - position);
                if (distance < nearest)
                {
                    nearest = distance;
                    hit = sandbags.GetCellCenterWorld(cell);
                }

                if (distance > 6)
                    continue;
                Flash flash = null;
                foreach (var f in flashes)
                    if (f.active && f.cell == cell)
                    {
                        flash = f;
                        break;
                    }

                if (flash == null)
                {
                    flash = flashes[nextFlash++ % flashes.Length];
                    if (flash.active)
                        Restore(flash);
                    flash.cell = cell;
                    flash.original = sandbags.GetColor(cell);
                    flash.flags = sandbags.GetTileFlags(cell);
                    flash.active = true;
                    sandbags.RemoveTileFlags(cell, TileFlags.LockColor);
                }

                flash.until = Time.time + flashSeconds;
                sandbags.SetColor(cell, flashColor);
            }

        for (int i = 0; i < 10; i++)
        {
            var d = debris[nextDebris++ % debris.Length];
            float angle = i * Mathf.PI * .2f;
            d.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1.1f, 2.8f);
            d.remaining = d.total = Random.Range(.28f, .5f);
            d.view.transform.position = hit;
            d.view.transform.localScale = Vector3.one * Random.Range(.09f, .18f);
            d.view.color = i < 4 ? new Color(1, .75f, .3f) : new Color(.8f, .64f, .4f);
            d.view.enabled = true;
        }

        int index = nextRing++ % rings.Length;
        ringLife[index] = .35f;
        rings[index].transform.position = hit;
        rings[index].enabled = true;
        UpdateRing(index);
    }

    private void Update()
    {
        foreach (var f in flashes)
            if (f.active && Time.time >= f.until)
                Restore(f);
        ActiveDebris = 0;
        foreach (var d in debris)
        {
            if (d.remaining <= 0)
                continue;
            d.remaining -= Time.deltaTime;
            d.velocity += Vector2.down * (4 * Time.deltaTime);
            d.view.transform.position += (Vector3)(d.velocity * Time.deltaTime);
            var color = d.view.color;
            color.a = Mathf.Clamp01(d.remaining / d.total);
            d.view.color = color;
            d.view.enabled = d.remaining > 0;
            if (d.view.enabled)
                ActiveDebris++;
        }

        for (int i = 0; i < rings.Length; i++)
            if (ringLife[i] > 0)
            {
                ringLife[i] -= Time.deltaTime;
                UpdateRing(i);
            }
    }

    private void UpdateRing(int index)
    {
        float progress = 1 - Mathf.Clamp01(ringLife[index] / .35f);
        var ring = rings[index];
        ring.enabled = ringLife[index] > 0;
        ring.startColor = ring.endColor = new Color(1, .55f, .15f, 1 - progress);
        for (int p = 0; p < 20; p++)
        {
            float a = p * Mathf.PI * .1f;
            ring.SetPosition(p, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * (.3f + progress * .75f));
        }
    }

    private void OnDestroy()
    {
        if (ringMaterial != null)
            Destroy(ringMaterial);
    }

    private void Restore(Flash f)
    {
        if (sandbags != null)
        {
            sandbags.SetColor(f.cell, f.original);
            sandbags.SetTileFlags(f.cell, f.flags);
        }

        f.active = false;
    }
}
