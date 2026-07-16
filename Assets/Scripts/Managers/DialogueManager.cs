using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화 진행을 담당하는 싱글톤 매니저.
/// NPC 쪽에서 DialogueManager.Instance.StartDialogue(dialogueSO) 를 호출하면 바로 실행됨.
/// UI(패널, 텍스트, 이미지)는 인스펙터에서 직접 연결. (텍스트는 레거시 Text 사용)
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI 연결 (인스펙터에서 드래그)")]
    [SerializeField] private GameObject dialoguePanel;      // 대화창 전체 패널 (활성/비활성용)
    [SerializeField] private Text speakerNameText;           // 화자 이름 표시 (레거시 Text)
    [SerializeField] private Text dialogueText;              // 대사 표시 (레거시 Text)
    [SerializeField] private Image speakerPortraitImage;     // 말하는 캐릭터 이미지
    [SerializeField] private Button skipButton;              // 대화 스킵(전체 종료) 버튼

    [Header("옵션")]
    [SerializeField] private KeyCode advanceKey = KeyCode.Space; // 대사 넘기는 키 (마우스 클릭도 같이 지원)
    [SerializeField] private float typingSpeed = 0.03f;          // 글자 하나당 딜레이(초). 작을수록 빠름

    // 현재 진행 중인 대화 상태
    private List<DialogueLine> currentLines;
    private int currentIndex;
    private bool isDialogueActive;
    private bool isTyping;
    private Coroutine typingCoroutine;

    public bool IsDialogueActive => isDialogueActive;

    /// <summary>대화가 전부 끝났을 때 호출됨 (필요하면 외부에서 구독해서 사용)</summary>
    public event Action OnDialogueEnd;

    private void Awake()
    {
        // 싱글톤 처리
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (skipButton != null)
            skipButton.onClick.AddListener(ForceEndDialogue);
    }

    private void Update()
    {
        if (!isDialogueActive) return;

        // 스페이스나 마우스 좌클릭으로 다음 대사 진행 / 타이핑 스킵
        if (Input.GetKeyDown(advanceKey) || Input.GetMouseButtonDown(0))
        {
            AdvanceDialogue();
        }
    }

    /// <summary>
    /// NPC에서 이 함수를 바로 호출해서 대화를 시작한다.
    /// 예: DialogueManager.Instance.StartDialogue(myDialogueSO);
    /// </summary>
    public void StartDialogue(DialogueSO dialogueData)
    {
        if (isDialogueActive)
        {
            // 이미 대화 진행 중이면 무시
            return;
        }

        if (dialogueData == null || dialogueData.lines == null || dialogueData.lines.Count == 0)
        {
            Debug.LogWarning("[DialogueManager] 빈 대화 데이터가 전달됨");
            return;
        }

        currentLines = dialogueData.lines;
        currentIndex = 0;
        isDialogueActive = true;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        ShowLine(currentIndex);
    }

    /// <summary>
    /// 클릭/스페이스 입력 시 호출됨.
    /// - 타이핑 중이면: 타이핑을 멈추고 해당 라인 전체 텍스트를 바로 표시.
    /// - 타이핑이 끝난 상태면: 다음 라인으로 넘어감(마지막이면 대화 종료).
    /// </summary>
    private void AdvanceDialogue()
    {
        if (isTyping)
        {
            CompleteTypingImmediately();
            return;
        }

        currentIndex++;

        if (currentIndex >= currentLines.Count)
        {
            EndDialogue();
            return;
        }

        ShowLine(currentIndex);
    }

    /// <summary>지정한 인덱스의 대사를 타이핑 효과로 표시 시작</summary>
    private void ShowLine(int index)
    {
        DialogueLine line = currentLines[index];

        if (speakerNameText != null)
            speakerNameText.text = line.speakerName;

        // 스프라이트가 지정된 경우에만 갱신 (비어있으면 이전 이미지 유지)
        if (speakerPortraitImage != null && line.portrait != null)
            speakerPortraitImage.sprite = line.portrait;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeLine(line.sentence));
    }

    /// <summary>한 글자씩 출력하는 타이핑 효과 코루틴</summary>
    private IEnumerator TypeLine(string sentence)
    {
        isTyping = true;

        if (dialogueText != null)
            dialogueText.text = string.Empty;

        for (int i = 0; i < sentence.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.text += sentence[i];

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        typingCoroutine = null;
    }

    /// <summary>타이핑 도중 넘기기 입력이 들어오면 해당 라인 텍스트를 즉시 전체 표시</summary>
    private void CompleteTypingImmediately()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (dialogueText != null)
            dialogueText.text = currentLines[currentIndex].sentence;

        isTyping = false;
    }

    /// <summary>대화 종료 처리</summary>
    private void EndDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isDialogueActive = false;
        isTyping = false;
        currentLines = null;
        currentIndex = 0;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        OnDialogueEnd?.Invoke();
    }

    /// <summary>
    /// 대화 전체를 강제 종료(스킵)한다. skipButton에 자동으로 연결되어 있음.
    /// </summary>
    public void ForceEndDialogue()
    {
        if (isDialogueActive)
            EndDialogue();
    }
}