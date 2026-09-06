// 역할: 에이전트의 고유 ID·위치·운반물 조회와 위치 복원을 위한 공통 계약.
using UnityEngine;

public interface IFarmAgent
{
    string PersistentId { get; }

    Vector3 Position { get; }

    CropCarrier CropCarrier { get; }

    void RestorePosition(Vector3 position);
}
