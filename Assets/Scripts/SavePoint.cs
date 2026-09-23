using UnityEngine;
using TMPro;

// A physical save point placed in the giant's yard (see GiantHouseBuilder). Walk the giant up
// to it and press F to open the save-slot picker in Save mode. Shows a small floating hint
// while the giant is in range and the game isn't already paused by something else, so it's
// discoverable without needing an instruction screen.
public class SavePoint : MonoBehaviour
{
    public float interactRange = 8f;
    public KeyCode interactKey = KeyCode.F;

    private Transform giant;
    private GameObject hintObject;
    private SaveSlotPanel panel;

    void Start()
    {
        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj != null) giant = giantObj.transform;

        panel = FindObjectOfType<SaveSlotPanel>();
        BuildHint();
    }

    void Update()
    {
        if (giant == null) return;

        float dist = Vector3.Distance(giant.position, transform.position);
        bool inRange = dist <= interactRange;
        bool canInteract = inRange && Time.timeScale > 0f && GameFlowManager.Instance != null && GameFlowManager.Instance.HasGameStarted;

        if (hintObject != null)
        {
            hintObject.SetActive(canInteract);
            if (canInteract && Camera.main != null)
            {
                hintObject.transform.rotation = Quaternion.LookRotation(hintObject.transform.position - Camera.main.transform.position);
            }
        }

        if (canInteract && Input.GetKeyDown(interactKey) && panel != null)
        {
            panel.Open(SaveSlotPanel.Mode.Save);
        }
    }

    void BuildHint()
    {
        GameObject hintGo = new GameObject("SaveHint");
        hintGo.transform.SetParent(transform, false);
        hintGo.transform.localPosition = new Vector3(0f, 5.5f, 0f);

        Canvas canvas = hintGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rt = hintGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(8f, 2f);
        hintGo.transform.localScale = Vector3.one * 0.35f;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(hintGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        textRt.anchoredPosition = Vector2.zero;

        TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "F 키를 눌러 저장";
        text.fontSize = 10f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;

        hintObject = hintGo;
        hintObject.SetActive(false);
    }
}
