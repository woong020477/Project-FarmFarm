// 역할: 인증/씬 전환의 클라이언트 공개 설정. 서버 비밀키를 보관하지 않는다.
using UnityEngine;

[CreateAssetMenu(menuName = "FarmFarm/Login Config")]
public sealed class LoginConfig : ScriptableObject
{
    [Tooltip("공개 OAuth 웹 클라이언트 ID. Client Secret은 입력하지 않습니다.")]
    public string googleClientId;
    public string connectionId = "google";
    public string farmScene = "Farm_Scene";
}
