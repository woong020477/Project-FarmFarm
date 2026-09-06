// 역할: 서버 진행 응답과 퀘스트/용병 상태의 직렬화 모델. 필드명은 서버와 맞춰 유지한다.
using System;

[Serializable]
public sealed class QuestState
{
    public string id, itemId, title;
    public int amount, gold, experience;
    public bool reduced;
}

[Serializable]
public sealed class MercenaryState
{
    public string id;
    public int tier;
    public float health;
}

[Serializable]
public sealed class ProgressState
{
    public int version = 1, revision, plots = 1, drones = 1, hp = 10000, dailyCompleted, onlineSeconds;
    public long level = 1, experience, protectedUntil, serverTime, nextQuestAt;
    public bool recoveryRequired;
    public string[] receipts = Array.Empty<string>();
    public QuestState[] quests = Array.Empty<QuestState>();
    public MercenaryState[] mercenaries = Array.Empty<MercenaryState>();
}
