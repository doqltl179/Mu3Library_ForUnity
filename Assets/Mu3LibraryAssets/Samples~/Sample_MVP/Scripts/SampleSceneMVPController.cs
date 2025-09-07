using UnityEngine;

namespace Mu3Library.Base.Sample.MVP
{
    public class SampleSceneMVPController : MonoBehaviour
    {



        private void Start()
        {
            WindowParams param = new WindowParams()
            {
                Model = new TestDefaultModel()
                {

                },
                BackgroundColor = new Color(0, 0, 0, 0),
            };
            MVPManager.Instance.Open<TestDefaultPresenter>(param);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                WindowParams param = new WindowParams()
                {
                    Model = new TestPopupModel()
                    {
                        Message = $"Called from 'SampleSceneMVPController::Update'",
                        OnClickConfirm = () =>
                        {
                            Debug.Log("'OnClickConfirm' defined from 'SampleSceneMVPController::Update'");
                        },
                    },
                    BackgroundInteractType = BackgroundInteractType.Confirm,
                };
                MVPManager.Instance.Open<TestPopupPresenter>(param);
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                WindowParams param = new WindowParams()
                {
                    Model = new TestSystemPopupModel()
                    {
                        Message = $"Called from 'SampleSceneMVPController::Update'",
                        OnClickConfirm = () =>
                        {
                            Debug.Log("'OnClickConfirm' defined from 'SampleSceneMVPController::Update'");
                        },
                        OnClickCancel = () =>
                        {
                            Debug.Log("'OnClickClose' defined from 'SampleSceneMVPController::Update'");
                        }
                    },
                    BackgroundColor = new Color(0, 0, 0, 235 / 255f),
                    BackgroundInteractType = BackgroundInteractType.Cancel,
                };
                MVPManager.Instance.Open<TestSystemPopupPresenter>(param);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                MVPManager.Instance.ResetPool();
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                MVPManager.Instance.CloseAllImmediately();
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                MVPManager.Instance.CloseAllImmediately(SortingLayer.Default);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                MVPManager.Instance.CloseAllImmediately(SortingLayer.Popup);
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                MVPManager.Instance.CloseAllImmediately(SortingLayer.SystemPopup);
            }
        }
    }
}