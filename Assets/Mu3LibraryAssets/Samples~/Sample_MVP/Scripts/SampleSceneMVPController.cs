using UnityEngine;

namespace Mu3Library.Sample.MVP
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
            MVPManager.Instance.Open<TestDefaultView, TestDefaultPresenter>
            (
                SortingLayer.Default,
                param
            );
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
                MVPManager.Instance.Open<TestPopupView, TestPopupPresenter>
                (
                    SortingLayer.Popup,
                    param
                );
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
                MVPManager.Instance.Open<TestSystemPopupView, TestSystemPopupPresenter>
                (
                    SortingLayer.SystemPopup,
                    param
                );
            }
        }
    }
}