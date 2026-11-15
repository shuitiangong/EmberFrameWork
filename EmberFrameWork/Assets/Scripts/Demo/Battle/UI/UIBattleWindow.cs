// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../../FrameWork/Runtime/Core/LICENSE.txt.
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using EmberFrameWork;

namespace GameLogic
{
    [Window(UILayer.UI, location: BattleSystem.ResourceRoot + "/UI/UIBattleWindow.prefab")]
    sealed class UIBattleWindow : UIWindow
    {
        #region 脚本工具生成的代码

        private Text m_textScore;
        private GameObject m_goOverView;
        private Button m_btnRestart;

        protected override void ScriptGenerator()
        {
            m_textScore = FindChildComponent<Text>("ScoreView/m_textScore");
            m_goOverView = FindChild("m_goOverView").gameObject;
            m_btnRestart = FindChildComponent<Button>("m_goOverView/m_btnRestart");
            m_btnRestart.onClick.AddListener(OnClickRestartBtn);
            FindChild("m_goOverView/m_btnHome").gameObject.SetActive(false);
        }

        #endregion

        protected override void RegisterEvent()
        {
            AddUIEvent<int>(ActorEventDefine.ScoreChange, OnScoreChange);
            AddUIEvent(ActorEventDefine.GameOver, OnGameOver);
        }

        protected override void OnRefresh()
        {
            m_textScore.text = "Score : 0";
            m_goOverView.SetActive(false);
        }

        #region 事件

        private void OnClickRestartBtn() => BattleSystem.Instance.LoadBattleAsync().Forget();

        #endregion

        private void OnScoreChange(int currentScores)
        {
            m_textScore.text = $"Score : {currentScores}";
        }

        private void OnGameOver()
        {
            m_goOverView.SetActive(true);
        }
    }
}
