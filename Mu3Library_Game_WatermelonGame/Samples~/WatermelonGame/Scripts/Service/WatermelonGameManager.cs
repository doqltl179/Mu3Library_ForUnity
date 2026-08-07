
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
        private const string BoardConfigResourcePath = "Configs/BoardConfig";

        private BoardController m_controller;
        private BoardController m_subscribedController;
        private BoardConfig m_boardConfig;
        private bool m_hasLoadedBoardConfig;

        protected BoardController _controller
        {
            get
            {
                if (m_controller == null)
                {
                    m_controller = Object.FindFirstObjectByType<BoardController>(FindObjectsInactive.Exclude);
                }

                return m_controller;
            }
        }

        public event Action OnBoardPrepared;



        public virtual void Initialize()
        {
            TryGetController(out _);
        }

        public virtual void Dispose()
        {
            if (m_subscribedController == null)
            {
                return;
            }

            m_subscribedController.OnBoardPrepared -= OnBoardPreparedEvent;
            m_subscribedController = null;
        }

        public virtual void Prepare(BoardSnapshot snapshot = null)
        {
            if (!TryGetController(out BoardController controller) ||
                !TryGetBoardConfig(out BoardConfig config))
            {
                return;
            }

            controller.SetBoardConfig(config);
            controller.Prepare(snapshot);
        }

        public virtual void GameStart()
        {
            if (!TryGetController(out BoardController controller))
            {
                return;
            }

            controller.GameStart();
        }

        protected virtual void OnBoardPreparedEvent()
        {
            OnBoardPrepared?.Invoke();
        }

        protected virtual bool TryGetController(out BoardController controller)
        {
            controller = _controller;
            if (controller == null)
            {
                Debug.LogError("BoardController not found. WatermelonGameManager cannot prepare the board.");
                return false;
            }

            SubscribeController(controller);
            return true;
        }

        protected virtual bool TryGetBoardConfig(out BoardConfig config)
        {
            if (!m_hasLoadedBoardConfig)
            {
                m_hasLoadedBoardConfig = true;
                m_boardConfig = Resources.Load<BoardConfig>(BoardConfigResourcePath);

                if (m_boardConfig == null)
                {
                    Debug.LogError($"BoardConfig not found. Path: {BoardConfigResourcePath}");
                }
            }

            config = m_boardConfig;
            return config != null;
        }

        private void SubscribeController(BoardController controller)
        {
            if (m_subscribedController == controller)
            {
                return;
            }

            if (m_subscribedController != null)
            {
                m_subscribedController.OnBoardPrepared -= OnBoardPreparedEvent;
            }

            m_subscribedController = controller;
            m_subscribedController.OnBoardPrepared += OnBoardPreparedEvent;
        }
    }
}
