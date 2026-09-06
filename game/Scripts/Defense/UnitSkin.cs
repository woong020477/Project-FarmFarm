// 역할: 유닛 방향별 대기·이동·행동 프레임의 공유 에셋.
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "FarmFarm/Unit Skin")]
public sealed class UnitSkin : ScriptableObject
{
    [Serializable]
    public sealed class Facing
    {
        public Sprite[] idle = Array.Empty<Sprite>(), walk = Array.Empty<Sprite>(), action = Array.Empty<Sprite>();
    }

    public Facing[] directions = new Facing[4]; // Right, up, left, down; source sheet order.
    public float framesPerSecond = 10;
    [Min(0)]
    public float walkFramesPerSecond;
    [Min(0)]
    public float actionFramesPerSecond;
}
