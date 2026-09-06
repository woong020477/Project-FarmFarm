// 역할: 저장 데이터와 작물 설정에서 공유하는 작물 식별자. 기존 숫자 값을 유지해야 한다.
namespace Enum
{
    public enum CropType
    {
        Carrot = 0,
        // Value 1 is retired. Do not reuse it: existing saves used it for potato.
        Tomato = 2,
        Lettuce = 3,
        Radish = 4,
        Strawberry = 5,
        Grain = 6,
        Turnip = 7,
        Cotton = 8,
        Onion = 9,
        Cauliflower = 10,
        Corn = 11,
        ChiliPepper = 12,
        Grapes = 13,
        PricklyPear = 14,
        Coffee = 15,
        Zucchini = 16,
        Pumpkin = 17,
        Pineapple = 18,
        Watermelon = 19
    }
}
