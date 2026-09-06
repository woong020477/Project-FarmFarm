// 역할: 도로 위 고가 구조물의 가림 영역을 보관한다. 별도의 물리적 2층 이동 경로는 만들지 않는다.
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmFarm.Exteriors
{
    // Visual grade separation. This does not change player movement or introduce
    // a second physical navigation floor; road traffic passes beneath the deck.
    public sealed class ExteriorBridge : MonoBehaviour
    {
        [SerializeField]
        private Rect underpass;
        [SerializeField]
        private Tilemap deck;
        public Rect Underpass => underpass;
        public Tilemap Deck => deck;

        public void Configure(Rect area, Tilemap tilemap)
        {
            underpass = area;
            deck = tilemap;
        }
    }
}
