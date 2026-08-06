using System;
using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Item;
using Mu3Library.Particle;

using Object = UnityEngine.Object;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Merge
{
    public class MergingCommand : BoardCommand
    {
        private bool _isValid =>
            _item01 != null &&
            _item02 != null &&
            _item01.Info != null &&
            _item02.Info != null &&
            _item01.Index >= 0 &&
            _item01.Index == _item02.Index;
        public bool IsValid => _isValid;

        private readonly BoardItem _item01;
        private readonly BoardItem _item02;

        private readonly List<ParticleHandler> _effects = new();

        private readonly Action _onStart;
        private readonly Action _onComplete;




        public MergingCommand(BoardItem item01, BoardItem item02, Action onStart = null, Action onComplete = null)
        {
            ClearEffects();

            _isRunning = false;
            _isCompleted = false;

            item01.SetMergeState(true);
            item02.SetMergeState(true);

            _item01 = item01;
            _item02 = item02;

            _onStart = onStart;
            _onComplete = onComplete;
        }

        public override void Run()
        {
            _isRunning = true;

            _onStart?.Invoke();

            void SetMergingEffect(BoardItem item)
            {
                if (item == null || item.Info == null || item.Info.MergingEffect == null)
                {
                    return;
                }

                var effect = Object.Instantiate(item.Info.MergingEffect);
                effect.transform.position = item.transform.position;
                effect.gameObject.SetActive(true);

                void OnCompleted()
                {
                    if (effect == null)
                    {
                        return;
                    }

                    effect.OnCompleted -= OnCompleted;
                    Object.Destroy(effect.gameObject);

                    foreach (var eft in _effects)
                    {
                        if (eft.IsPlaying)
                        {
                            return;
                        }
                    }

                    _isRunning = false;
                    _isCompleted = true;

                    _onComplete?.Invoke();
                }
                effect.OnCompleted += OnCompleted;

                effect.Play();

                _effects.Add(effect);
            }
            SetMergingEffect(_item01);
            SetMergingEffect(_item02);

            if (_effects.Count == 0)
            {
                _isRunning = false;
                _isCompleted = true;

                _onComplete?.Invoke();
            }
        }

        public override void Dispose()
        {
            ClearEffects();
        }

        private void ClearEffects()
        {
            foreach (var effect in _effects)
            {
                if (effect == null)
                {
                    continue;
                }

                effect.Stop();
                Object.Destroy(effect);
            }

            _effects.Clear();
        }
    }
}
