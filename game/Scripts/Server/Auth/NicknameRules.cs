// 역할: 닉네임의 문자 종류와 길이를 검증하는 순수 규칙.
public static class NicknameRules
{
    public const string Hint = "한글 3~16자 또는 영문 4~20자\n숫자·공백·특수문자·한영 혼용 불가";
    public static bool IsValid(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 20)
            return false;
        bool korean = true, english = true;
        foreach (char c in value)
        {
            korean &= c >= '가' && c <= '힣';
            english &= (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        return (korean && value.Length >= 3 && value.Length <= 16) || (english && value.Length >= 4);
    }
}
