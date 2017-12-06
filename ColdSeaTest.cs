using UnityEngine;
using Ink.Runtime;
using MovementEffects;
using RSGLib.UI;
using TMPro;
using System.Collections.Generic;
using DarkTonic.MasterAudio;
using DG.Tweening;
using RSGlib.UI;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace ColdSea
{

    //-----------------------------------------------------------------------------
    public class ColdSeaTest : MonoBehaviour
    {
        public const float QUICK_TIME_SECONDS = 5;

#pragma warning disable 649
        [SerializeField] private TextAsset m_inkJsonAsset;
        [SerializeField] private GameObject m_textPanel;
        [SerializeField] private TextMeshProUGUI m_textPrefab;
        [SerializeField] private GameObject m_separatorPrefab;
        [SerializeField] private GameObject m_progressBarPrefab;

        [SerializeField] private Button m_buttonPrefab;
        [SerializeField] private Button m_continueButtonPrefab;
        [SerializeField] private Button m_resetButtonPrefab;
        [SerializeField] private ScrollRect m_scrollView;
        [SerializeField] private ImageChanger m_imageChanger;

        [SerializeField] private Color m_resultColor;
        [SerializeField] private Color m_standardTextColor;

#pragma warning restore 649

        private const float MIN_SCROLL_SIZE = 0.15f;

        private Story m_story;
        private bool m_refreshView;
        private List<Button> m_choices;
        private int m_choiceFadeCount;
        private bool m_textFullyFaded;
        private int m_scrollEvents;
        private bool m_showContinueButton;
        private Button m_continueButton;
        private Button m_resetButton;
        private Color m_currentColor;
        private ProgressBar m_progressBar;
        private bool m_timedChoiceSelected;
        private bool m_timedChoiceActive;

        //-----------------------------------------------------------------------------
        private void Awake()
        {
            m_choices = new List<Button>();
        }

        //-----------------------------------------------------------------------------
        private void Start()
        {
            StartStory();
        }

        //-----------------------------------------------------------------------------
        private void StartStory()
        {
            m_story = new Story(m_inkJsonAsset.text);
            m_refreshView = true;
            m_textFullyFaded = false;
            m_timedChoiceSelected = false;
            m_choiceFadeCount = 0;
            m_scrollEvents = 0;
            m_currentColor = m_standardTextColor;
            m_scrollView.onValueChanged.AddListener(OnLimitScroll);
            Timing.RunCoroutine(RefreshView());
        }

        //-----------------------------------------------------------------------------
        private void OnLimitScroll(Vector2 _scroll)
        {
            if (_scroll.y < 1 - MIN_SCROLL_SIZE * m_scrollEvents)
                m_scrollView.verticalNormalizedPosition = 1 - MIN_SCROLL_SIZE * m_scrollEvents;
        }

        //-----------------------------------------------------------------------------
        private IEnumerator<float> RefreshView()
        {
            while (true)
            {
                if (!m_refreshView)
                {
                    yield return 0;
                    continue;
                }

                LockScrolling(true);
                RemoveChoices();
                while (m_choices.Count > 0)
                    yield return 0;

                m_refreshView = false;
                m_choiceFadeCount = 0;

                while (m_story.canContinue && !m_showContinueButton)
                {
                    m_textFullyFaded = false;
                    string text = m_story.Continue().Trim();

                    PreProcessStoryTags();
                    CreateContentView(text);
                    CheckForScrolling();
                    while (!m_textFullyFaded)
                        yield return Timing.WaitForSeconds(1.0f);

                    PostProcessStoryTags();
                    CheckForScrolling();
                    m_currentColor = m_standardTextColor;
                }

                if (m_story.currentChoices.Count > 0 && !m_showContinueButton)
                {
                    if(m_story.currentChoices.Count > 1)
                        AddSeparator();

                    for (int index = 0; index < m_story.currentChoices.Count; index++)
                    {
                        Choice choice = m_story.currentChoices[index];
                        CreateChoiceView(choice);
                        CheckForScrolling();
                    }
                    while (m_choiceFadeCount != m_choices.Count)
                        yield return 0;

                    if (m_timedChoiceActive)
                        CreateTimedChoice();

                }
                LockScrolling(false);
            }
        }

        //-----------------------------------------------------------------------------
        private void PreProcessStoryTags()
        {
            if (m_story.currentTags.Count == 0)
                return;

            if (m_story.currentTags.Contains("preEmptyLine"))
                CreateContentView("");

            if (m_story.currentTags.Contains("preSeparator"))
                AddSeparator();

            if (m_story.currentTags.Contains("result"))
            {
                CreateContentView("");
                AddSeparator();
                m_currentColor = m_resultColor;
            }
        }

        //-----------------------------------------------------------------------------
        private void PostProcessStoryTags()
        {
            if (m_story.currentTags.Count == 0)
                return;

            if (m_story.currentTags.Contains("emptyLine"))
                CreateContentView("");

            if (m_story.currentTags.Contains("separator"))
                AddSeparator();

            if (m_story.currentTags.Contains("continueButton"))
                AddContinueButton();

            if (m_story.currentTags.Contains("result"))
                AddResetButton();

            string imageTag = GetImageTagFromTags();
            if (!string.IsNullOrEmpty(imageTag))
                m_imageChanger.ShowImage(imageTag.Replace("image_", ""));

            string soundTag = GetSoundTagFromTags();
            if (!string.IsNullOrEmpty(soundTag))
                MasterAudio.PlaySoundAndForget(soundTag.Replace("sound_", ""));

            if (m_story.currentTags.Contains("timed"))
                m_timedChoiceActive = true;
        }

        //-----------------------------------------------------------------------------
        private string GetImageTagFromTags()
        {
            int count = m_story.currentTags.Count;
            for (int index = 0; index < count; index++)
            {
                string storyTag = m_story.currentTags[index];
                if (storyTag.Contains("image_"))
                    return storyTag;
            }
            return "";
        }

        //-----------------------------------------------------------------------------
        private string GetSoundTagFromTags()
        {
            int count = m_story.currentTags.Count;
            for(int index = 0; index < count; index++)
            {
                string storyTag = m_story.currentTags[index];
                if(storyTag.Contains("sound_"))
                    return storyTag;
            }
            return "";
        }


        //-----------------------------------------------------------------------------
        private void LockScrolling(bool _lock)
        {
            m_scrollView.vertical = !_lock;
        }

        //-----------------------------------------------------------------------------
        private void CheckForScrolling()
        {
            int count = m_scrollView.content.childCount;
            float panelSize = m_scrollView.GetComponent<RectTransform>().rect.height * 0.85f;
            float size = m_textPanel.GetComponent<RectTransform>().sizeDelta.y;
            float contentSize = 0;
            for (int index = 0; index < count; index++)
            {
                var child = m_scrollView.content.GetChild(index);
                if (child.gameObject.activeSelf)
                    contentSize += child.GetComponent<RectTransform>().rect.height;
            }

            float alreadyScrolled = (MIN_SCROLL_SIZE * m_scrollEvents) * size;
            if (contentSize - alreadyScrolled >= panelSize)
            {
                m_scrollEvents++;
                DOTween.To(() => m_scrollView.verticalNormalizedPosition, _x => m_scrollView.verticalNormalizedPosition = _x, 1 - MIN_SCROLL_SIZE * m_scrollEvents, 1)
                    .SetEase(Ease.Linear);
            }
        }

        //-----------------------------------------------------------------------------
        private void OnClickChoiceButton(Choice _choice)
        {
            m_story.ChooseChoiceIndex(_choice.index);
            m_refreshView = true;
            m_timedChoiceSelected = true;
        }

        //-----------------------------------------------------------------------------
        private void CreateContentView(string _text)
        {
            TextMeshProUGUI storyText = Instantiate(m_textPrefab);
            storyText.text = _text;
            storyText.color = m_currentColor;
            storyText.transform.SetParent(m_textPanel.transform, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_textPanel.GetComponent<RectTransform>());
            var textFade = storyText.GetComponent<RollingTextFade>();
            textFade.FadeIn(() => m_textFullyFaded = true);
            MasterAudio.PlaySoundAndForget("writing");
        }

        //-----------------------------------------------------------------------------
        private void CreateChoiceView(Choice _choice)
        {
            Button choiceButton = Instantiate(m_buttonPrefab);
            choiceButton.transform.SetParent(m_textPanel.transform, false);
            choiceButton.onClick.AddListener(() => OnClickChoiceButton(_choice));

            var choiceText = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
            choiceText.text = _choice.text;

            var hightLigther = choiceText.GetComponent<UITextHighlighter>();
            hightLigther.enabled = false;

            var textFade = choiceText.GetComponent<RollingTextFade>();
            textFade.FadeIn(() =>
            {
                m_choiceFadeCount++;
                hightLigther.enabled = true;
            });
            m_choices.Add(choiceButton);
        }

        //-----------------------------------------------------------------------------
        private void CreateTimedChoice()
        {
            m_timedChoiceSelected = false;
            m_progressBar = Instantiate(m_progressBarPrefab).GetComponent<ProgressBar>();
            m_progressBar.transform.SetParent(m_textPanel.transform, false);
            m_progressBar.AutoProgress(QUICK_TIME_SECONDS, AutomaticChoice);
        }

        //-----------------------------------------------------------------------------
        private void AutomaticChoice()
        {
            if (m_timedChoiceSelected || !m_timedChoiceActive)
                return;

            int choice = Random.Range(0, m_story.currentChoices.Count);
            m_timedChoiceActive = false;
            m_refreshView = true;
            m_story.ChooseChoiceIndex(choice);
        }

        //-----------------------------------------------------------------------------
        private void AddContinueButton()
        {
            m_continueButton = Instantiate(m_continueButtonPrefab);
            m_continueButton.transform.SetParent(m_textPanel.transform, false);
            m_continueButton.onClick.AddListener(OnClickContinueButton);
            m_showContinueButton = true;
        }

        //-----------------------------------------------------------------------------
        private void AddResetButton()
        {
            m_resetButton = Instantiate(m_resetButtonPrefab);
            m_resetButton.transform.SetParent(m_textPanel.transform, false);
            m_resetButton.onClick.AddListener(OnClickResetButton);
        }


        //-----------------------------------------------------------------------------
        private void OnClickResetButton()
        {
            m_scrollEvents = 0;
            RemoveAllChildren();
            m_story.ResetState();
            m_refreshView = true;
            m_scrollView.verticalNormalizedPosition = 1;
        }

        //-----------------------------------------------------------------------------
        private void OnClickContinueButton()
        {
            Destroy(m_continueButton.gameObject);
            m_showContinueButton = false;
            m_refreshView = true;
        }

        //-----------------------------------------------------------------------------
        private void AddSeparator()
        {
            GameObject separator = Instantiate(m_separatorPrefab);
            separator.transform.SetParent(m_textPanel.transform, false);
        }

        //-----------------------------------------------------------------------------
        private void RemoveAllChildren()
        {
            int childCount = m_textPanel.transform.childCount;
            for (int index = childCount - 1; index >= 0; --index)
            {
                var go = m_textPanel.transform.GetChild(index).gameObject;
                go.SetActive(false);
                Destroy(go);
            }
        }

        //-----------------------------------------------------------------------------
        private void RemoveChoices()
        {
            int count = m_choices.Count;
            for (int index = count - 1; index >= 0; --index)
            {
                var go = m_choices[index].gameObject;
                go.GetComponentInChildren<RollingTextFade>().FastFadeOut(() => RemoveChoice(go));
            }

            if (m_progressBar != null)
                m_progressBar.GetComponent<UIPanelFader>().FadeOut(()=>Destroy(m_progressBar));
        }

        //-----------------------------------------------------------------------------
        private void RemoveChoice(GameObject _go)
        {
            m_choices.Remove(_go.GetComponent<Button>());
            Destroy(_go);
        }

    }
}