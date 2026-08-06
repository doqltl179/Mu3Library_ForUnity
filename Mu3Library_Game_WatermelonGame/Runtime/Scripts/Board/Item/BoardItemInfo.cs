using Mu3Library.ObjectPool;
using Mu3Library.Particle;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    [System.Serializable]
    public class BoardItemInfo : CreateArguments
    {
        [SerializeField] private Sprite _sprite;
        public Sprite Sprite => _sprite;

        [SerializeField] private ParticleHandler _mergingEffect;
        public ParticleHandler MergingEffect => _mergingEffect;

        [SerializeField] private ParticleHandler _mergedEffect;
        public ParticleHandler MergedEffect => _mergedEffect;
    }
}
