using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public GameObject mainMenuPanel;
    public GameObject startButton;
    public GameObject quitButton;

    [Header("Character Select")]
    public GameObject characterSelectPanel;
    public TextMeshProUGUI[] charNameTexts = new TextMeshProUGUI[4];
    public TextMeshProUGUI[] charAttrTexts = new TextMeshProUGUI[4];
    public TextMeshProUGUI[] charSkillTexts = new TextMeshProUGUI[4];
    public Image[] charPreviewImages = new Image[4];
    public GameObject[] charSlots = new GameObject[4];
    public TextMeshProUGUI p1StatusText;
    public TextMeshProUGUI p2StatusText;
    public TextMeshProUGUI instructionsText;

    [Header("Background Select")]
    public GameObject bgSelectPanel;
    public GameObject[] bgSlots = new GameObject[12];
    public Image[] bgPreviewImages = new Image[12];
    public TextMeshProUGUI[] bgNameTexts = new TextMeshProUGUI[12];
    public TextMeshProUGUI bgP1StatusText;
    public TextMeshProUGUI bgP2StatusText;
    public TextMeshProUGUI bgInstructionsText;

    [Header("Title Animation")]
    public float bounceSpeed = 2f;
    public float bounceHeight = 10f;

    [Header("Button Effects")]
    public float hoverScale = 1.08f;
    public float pressScale = 0.95f;
    public float animDuration = 0.1f;

    // Selection state
    public int player1CharacterIndex = 0;
    public int player2CharacterIndex = 1;
    public int selectedBgIndex = 0;

    private Vector3 titleStartPos;
    private RectTransform titleRt;
    private enum MenuState { MainMenu, CharSelect, BgSelect }
    private MenuState state = MenuState.MainMenu;
    private bool p1Confirmed = false;
    private bool p2Confirmed = false;

    void Start()
    {
        Time.timeScale = 1f;
        if (titleText != null)
        {
            titleRt = titleText.GetComponent<RectTransform>();
            if (titleRt != null) titleStartPos = titleRt.anchoredPosition;
            else titleStartPos = titleText.transform.position;
        }
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (bgSelectPanel != null) bgSelectPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // 运行时绑定按钮点击（编辑器脚本中使用 lambda 创建的场景，
        // lambda 无法序列化，场景加载后点击事件会丢失）
        if (startButton != null)
        {
            Button btn = startButton.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(OnStartButton);
            SetupButtonHover(startButton);
        }
        if (quitButton != null)
        {
            Button btn = quitButton.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(OnQuitButton);
            SetupButtonHover(quitButton);
        }
    }

    void Update()
    {
        // Title bounce (only on main menu)
        if (titleRt != null && state == MenuState.MainMenu)
        {
            float yOffset = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            titleRt.anchoredPosition = titleStartPos + new Vector3(0, yOffset, 0);
        }

        switch (state)
        {
            case MenuState.MainMenu:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    OnStartButton();
                break;
            case MenuState.CharSelect:
                UpdateCharacterSelection();
                break;
            case MenuState.BgSelect:
                UpdateBackgroundSelection();
                break;
        }
    }

    // === 按钮悬停/缩放动画效果 ===

    void SetupButtonHover(GameObject btnObj)
    {
        EventTrigger trigger = btnObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btnObj.AddComponent<EventTrigger>();

        // 移除已有监听避免重复绑定
        trigger.triggers.Clear();

        // PointerEnter → 放大
        EventTrigger.Entry enter = new EventTrigger.Entry();
        enter.eventID = EventTriggerType.PointerEnter;
        enter.callback.AddListener((data) => { OnButtonPointerEnter(btnObj); });
        trigger.triggers.Add(enter);

        // PointerExit → 恢复
        EventTrigger.Entry exit = new EventTrigger.Entry();
        exit.eventID = EventTriggerType.PointerExit;
        exit.callback.AddListener((data) => { OnButtonPointerExit(btnObj); });
        trigger.triggers.Add(exit);

        // PointerDown → 按压
        EventTrigger.Entry down = new EventTrigger.Entry();
        down.eventID = EventTriggerType.PointerDown;
        down.callback.AddListener((data) => { OnButtonPointerDown(btnObj); });
        trigger.triggers.Add(down);

        // PointerUp → 恢复（如果没有进入悬停状态则回到原始大小）
        EventTrigger.Entry up = new EventTrigger.Entry();
        up.eventID = EventTriggerType.PointerUp;
        up.callback.AddListener((data) => { OnButtonPointerUp(btnObj); });
        trigger.triggers.Add(up);
    }

    void OnButtonPointerEnter(GameObject btn)
    {
        StopButtonAnim(btn);
        StartCoroutine(ScaleButton(btn, hoverScale));
    }

    void OnButtonPointerExit(GameObject btn)
    {
        StopButtonAnim(btn);
        StartCoroutine(ScaleButton(btn, 1f));
    }

    void OnButtonPointerDown(GameObject btn)
    {
        StopButtonAnim(btn);
        StartCoroutine(ScaleButton(btn, pressScale));
    }

    void OnButtonPointerUp(GameObject btn)
    {
        StopButtonAnim(btn);
        // 松开后回到悬停大小（鼠标仍在按钮上）或原始大小
        StartCoroutine(ScaleButton(btn, hoverScale));
    }

    void StopButtonAnim(GameObject btn)
    {
        Coroutine existing = GetButtonAnim(btn);
        if (existing != null) StopCoroutine(existing);
    }

    // 用字典存储每个按钮的协程引用
    private Dictionary<GameObject, Coroutine> buttonAnims = new Dictionary<GameObject, Coroutine>();

    Coroutine GetButtonAnim(GameObject btn)
    {
        if (buttonAnims.TryGetValue(btn, out Coroutine c)) return c;
        return null;
    }

    IEnumerator ScaleButton(GameObject btn, float targetScale)
    {
        buttonAnims[btn] = null; // 清除旧引用
        Transform tf = btn.transform;
        Vector3 from = tf.localScale;
        Vector3 to = new Vector3(targetScale, targetScale, 1f);

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / animDuration;
            tf.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }
        tf.localScale = to;

        if (buttonAnims.ContainsKey(btn) && buttonAnims[btn] == null)
            buttonAnims.Remove(btn);
    }

    // === Main Menu ===

    public void OnStartButton()
    {
        state = MenuState.CharSelect;
        p1Confirmed = false;
        p2Confirmed = false;

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(true);
        if (bgSelectPanel != null) bgSelectPanel.SetActive(false);

        UpdateCharSelectionUI();
    }

    public void OnQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // === Character Selection ===

    void UpdateCharacterSelection()
    {
        // P1: W/S to browse, F to confirm
        if (!p1Confirmed)
        {
            if (Input.GetKeyDown(KeyCode.W))
                SelectCharacter(0, player1CharacterIndex - 1);
            if (Input.GetKeyDown(KeyCode.S))
                SelectCharacter(0, player1CharacterIndex + 1);
            if (Input.GetKeyDown(KeyCode.F))
                p1Confirmed = true;
        }

        // P2: Up/Down to browse, Num0 to confirm
        if (!p2Confirmed)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
                SelectCharacter(1, player2CharacterIndex - 1);
            if (Input.GetKeyDown(KeyCode.DownArrow))
                SelectCharacter(1, player2CharacterIndex + 1);
            if (Input.GetKeyDown(KeyCode.Keypad0))
                p2Confirmed = true;
        }

        UpdateCharSelectionUI();

        if (p1Confirmed && p2Confirmed)
            OnConfirmCharacterSelection();
    }

    void SelectCharacter(int playerIndex, int charIndex)
    {
        charIndex = Mathf.Clamp(charIndex, 0, CharacterDatabase.Count - 1);
        if (playerIndex == 0) player1CharacterIndex = charIndex;
        else player2CharacterIndex = charIndex;
    }

    void UpdateCharSelectionUI()
    {
        for (int i = 0; i < CharacterDatabase.Count; i++)
        {
            if (charSlots[i] == null) continue;

            CharacterData data = CharacterDatabase.Get(i);
            bool p1Selected = (i == player1CharacterIndex);
            bool p2Selected = (i == player2CharacterIndex);

            // Slot highlight
            Image slotImg = charSlots[i].GetComponent<Image>();
            if (slotImg != null)
            {
                if (p1Selected && p2Selected)
                    slotImg.color = Color.white;
                else if (p1Selected)
                    slotImg.color = p1Confirmed ? Color.red : new Color(1f, 0.4f, 0.4f);
                else if (p2Selected)
                    slotImg.color = p2Confirmed ? Color.blue : new Color(0.4f, 0.4f, 1f);
                else
                    slotImg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            }

            // Name highlight
            if (charNameTexts[i] != null)
            {
                if (p1Selected || p2Selected)
                    charNameTexts[i].color = data.characterColor;
                else
                    charNameTexts[i].color = data.characterColor * 0.5f;
            }
        }

        // Status text
        if (p1StatusText != null)
        {
            CharacterData p1d = CharacterDatabase.Get(player1CharacterIndex);
            p1StatusText.text = p1Confirmed
                ? "P1 Confirmed"
                : $"P1: {p1d.displayName}  [F] Confirm";
            p1StatusText.color = p1Confirmed ? Color.red : new Color(1f, 0.7f, 0.7f);
        }
        if (p2StatusText != null)
        {
            CharacterData p2d = CharacterDatabase.Get(player2CharacterIndex);
            p2StatusText.text = p2Confirmed
                ? "P2 Confirmed"
                : $"P2: {p2d.displayName}  [Num0] Confirm";
            p2StatusText.color = p2Confirmed ? Color.blue : new Color(0.7f, 0.7f, 1f);
        }

        if (instructionsText != null)
        {
            instructionsText.text = p1Confirmed && p2Confirmed
                ? "Selecting..."
                : "P1: [W/S] Browse  [F] Confirm    P2: [Up/Down] Browse  [Num0] Confirm";
        }
    }

    void OnConfirmCharacterSelection()
    {
        PlayerPrefs.SetInt("P1Character", player1CharacterIndex);
        PlayerPrefs.SetInt("P2Character", player2CharacterIndex);

        // Move to background selection
        state = MenuState.BgSelect;
        p1Confirmed = false;
        p2Confirmed = false;
        selectedBgIndex = 0;

        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (bgSelectPanel != null) bgSelectPanel.SetActive(true);

        UpdateBgSelectionUI();
    }

    // === Background Selection ===

    void UpdateBackgroundSelection()
    {
        // P1: W/S to browse, F to confirm
        if (!p1Confirmed)
        {
            if (Input.GetKeyDown(KeyCode.W))
                SelectBackground(selectedBgIndex - 4);
            if (Input.GetKeyDown(KeyCode.S))
                SelectBackground(selectedBgIndex + 4);
            if (Input.GetKeyDown(KeyCode.A))
                SelectBackground(selectedBgIndex - 1);
            if (Input.GetKeyDown(KeyCode.D))
                SelectBackground(selectedBgIndex + 1);
            if (Input.GetKeyDown(KeyCode.F))
                p1Confirmed = true;
        }

        // P2: Arrow keys to browse, Num0 to confirm
        if (!p2Confirmed)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
                SelectBackground(selectedBgIndex - 4);
            if (Input.GetKeyDown(KeyCode.DownArrow))
                SelectBackground(selectedBgIndex + 4);
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                SelectBackground(selectedBgIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow))
                SelectBackground(selectedBgIndex + 1);
            if (Input.GetKeyDown(KeyCode.Keypad0))
                p2Confirmed = true;
        }

        UpdateBgSelectionUI();

        if (p1Confirmed && p2Confirmed)
            OnConfirmBackgroundSelection();
    }

    void SelectBackground(int index)
    {
        selectedBgIndex = Mathf.Clamp(index, 0, BackgroundDatabase.Count - 1);
    }

    void UpdateBgSelectionUI()
    {
        for (int i = 0; i < BackgroundDatabase.Count && i < bgSlots.Length; i++)
        {
            if (bgSlots[i] == null) continue;

            bool isSelected = (i == selectedBgIndex);

            Image slotImg = bgSlots[i].GetComponent<Image>();
            if (slotImg != null)
            {
                if (isSelected)
                    slotImg.color = new Color(1f, 1f, 0.5f, 0.9f);
                else
                    slotImg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            }

            if (bgNameTexts[i] != null)
            {
                BackgroundData bg = BackgroundDatabase.Get(i);
                bgNameTexts[i].color = isSelected ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }
        }

        BackgroundData selectedBg = BackgroundDatabase.Get(selectedBgIndex);

        if (bgP1StatusText != null)
        {
            bgP1StatusText.text = p1Confirmed
                ? "P1 Confirmed"
                : $"[F] Confirm: {selectedBg.displayName}";
            bgP1StatusText.color = p1Confirmed ? Color.red : new Color(1f, 0.7f, 0.7f);
        }
        if (bgP2StatusText != null)
        {
            bgP2StatusText.text = p2Confirmed
                ? "P2 Confirmed"
                : $"[Num0] Confirm: {selectedBg.displayName}";
            bgP2StatusText.color = p2Confirmed ? Color.blue : new Color(0.7f, 0.7f, 1f);
        }

        if (bgInstructionsText != null)
        {
            bgInstructionsText.text = p1Confirmed && p2Confirmed
                ? "Entering game..."
                : "P1: [WASD] Browse  [F] Confirm    P2: [Arrows] Browse  [Num0] Confirm";
        }
    }

    void OnConfirmBackgroundSelection()
    {
        PlayerPrefs.SetInt("SelectedBackground", selectedBgIndex);
        SceneManager.LoadScene("GameScene");
    }
}
