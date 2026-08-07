
using System;
using Mu3Library.DI;
using Mu3Library.Game.WatermelonGame.Board;
using Mu3Library.Game.WatermelonGame.Board.Config;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Sample.Game.WatermelonGame.Service
{
    public class WatermelonGameManager : IWatermelonGameManager, IWatermelonGameManagerEventBus, IInitializable, IDisposable
    {
        private BoardController m_controller;
        protected BoardController _controller =>
            m_controller ??=
            Object.FindFirstObjectByType<BoardController>(FindObjectsInactive.Exclude);

        public event Action OnBoardPrepared;
        public event Action OnGameStarted;
        public event Action OnGameEnded;

        public event Action<int> OnScoreChanged;



        public virtual void Initialize()
        {
            _controller.OnBoardPrepared += OnBoardPreparedEvent;
            _controller.OnGameStarted += OnGameStartedEvent;
            _controller.OnGameEnded += OnGameEndedEvent;
            _controller.OnScoreChanged += OnScoreChangedEvent;
        }

        public virtual void Dispose()
        {
            _controller.OnBoardPrepared -= OnBoardPreparedEvent;
            _controller.OnGameStarted -= OnGameStartedEvent;
            _controller.OnGameEnded -= OnGameEndedEvent;
            _controller.OnScoreChanged -= OnScoreChangedEvent;
        }

        public virtual void GameStart()
        {
            _controller.GameStart();
        }

        public virtual void Prepare(BoardSnapshot snapshot = null)
        {
            const string configPath = "Configs/BoardConfig";
            var config = Resources.Load<BoardConfig>(configPath);

            if (config == null)
            {
                Debug.LogError($"Config not foudn. path: {configPath}");
            }
            else
            {
                _controller.SetBoareConfig(config);
            }

            _controller.Prepare(snapshot);
        }

        protected virtual void OnScoreChangedEvent(int score)
        {
            OnScoreChanged?.Invoke(score);
        }

        protected virtual void OnGameEndedEvent()
        {
            OnGameEnded?.Invoke();
        }

        protected virtual void OnGameStartedEvent()
        {
            OnGameStarted?.Invoke();
        }

        protected virtual void OnBoardPreparedEvent()
        {
            OnBoardPrepared?.Invoke();
        }
    }
}
