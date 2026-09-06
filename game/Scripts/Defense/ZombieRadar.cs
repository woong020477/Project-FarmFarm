// 역할: 좀비의 상대 방향을 화면의 아이콘과 안내 화살표로 표시한다.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ZombieRadar : MonoBehaviour
{
    [Serializable]
    public sealed class Marker
    {
        public RectTransform root, arrow;
        public Image portrait;
        public TMP_Text count;
    }

    [SerializeField]
    private RectTransform area;
    [SerializeField]
    private Marker[] markers = new Marker[8];
    private readonly Zombie[] nearest = new Zombie[8];
    private readonly int[] counts = new int[8];
    private readonly float[] distances = new float[8];
    public int VisibleMarkers { get; private set; }

    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        var manager = GameManager.Instance;
        if (manager == null || manager.Defense == null || manager.CameraController == null)
            return;
        var camera = manager.CameraController.View;
        for (int i = 0; i < 8; i++)
        {
            nearest[i] = null;
            counts[i] = 0;
            distances[i] = float.MaxValue;
        }

        foreach (var zombie in manager.Defense.Zombies)
        {
            if (zombie == null || !zombie.Alive)
                continue;
            Vector3 point = camera.WorldToViewportPoint(zombie.transform.position);
            if (point.z > 0 && point.x > .08f && point.x < .92f && point.y > .14f && point.y < .86f)
                continue;
            Vector2 delta = new Vector2((point.x - .5f) * camera.aspect, point.y - .5f);
            int sector = ((Mathf.RoundToInt(Mathf.Atan2(delta.y, delta.x) * 4 / Mathf.PI) % 8) + 8) % 8;
            counts[sector]++;
            float distance = delta.sqrMagnitude;
            if (distance < distances[sector])
            {
                distances[sector] = distance;
                nearest[sector] = zombie;
            }
        }

        VisibleMarkers = 0;
        for (int i = 0; i < 8; i++)
        {
            var marker = markers[i];
            if (marker == null || marker.root == null)
                continue;
            bool show = nearest[i] != null;
            marker.root.gameObject.SetActive(show);
            if (!show)
                continue;
            VisibleMarkers++;
            Vector3 point = camera.WorldToViewportPoint(nearest[i].transform.position);
            Vector2 delta = new Vector2((point.x - .5f) * area.rect.width, (point.y - .5f) * area.rect.height);
            Vector2 half = area.rect.size * .5f - Vector2.one * 26;
            float scale = Mathf.Min(half.x / Mathf.Max(.001f, Mathf.Abs(delta.x)), half.y / Mathf.Max(.001f, Mathf.Abs(delta.y)));
            marker.root.anchoredPosition = delta * scale;
            marker.arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            marker.arrow.anchoredPosition = delta.normalized * 22;
            marker.portrait.sprite = manager.Defense.ZombieIcon;
            marker.count.text = counts[i] > 1 ? counts[i].ToString() : string.Empty;
        }
    }

    private void OnDisable()
    {
        foreach (var m in markers)
            if (m?.root != null)
                m.root.gameObject.SetActive(false);
        VisibleMarkers = 0;
    }
}
