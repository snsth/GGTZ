using UnityEngine;
using UnityEngine.AI;

public class VoiceDebugHUD : MonoBehaviour
{
    public VoiceInputWindows voice;
    public VoiceCommandRouter router;
    public BossRollAbility roll;
    public NavMeshAgent agent;

    public KeyCode toggleKey = KeyCode.F1;
    public bool visible = true;
    public int fontSize = 14;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
        if (!agent && roll) agent = roll.GetComponent<NavMeshAgent>();
    }

    void OnGUI()
    {
        if (!visible) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };

        string status = voice ? voice.Status : "n/a";
        string hypo = voice ? voice.LastHypothesis : "";
        string res = voice ? voice.LastText : "";
        string raw = router ? router.LastRaw : "";
        string action = router ? router.LastAction.ToString() : "n/a";
        string score = router ? router.LastScore.ToString("0.00") : "n/a";
        string rolling = roll ? roll.IsRolling.ToString() : "n/a";
        string lastTrig = roll ? roll.LastRollTrigger : "n/a";
        string agEnabled = agent ? agent.enabled.ToString() : "n/a";
        string agStopped = agent ? agent.isStopped.ToString() : "n/a";

        string s =
            "[Voice]\n" +
            "- Status: " + status + "\n" +
            "- Hypothesis: " + hypo + "\n" +
            "- Result: " + res + "\n" +
            "[Match]\n" +
            "- LastRaw: " + raw + "\n" +
            "- Action: " + action + " (score=" + score + ")\n" +
            "[Roll]\n" +
            "- IsRolling: " + rolling + "  LastTrigger: " + lastTrig + "\n" +
            "[Agent]\n" +
            "- enabled: " + agEnabled + "  isStopped: " + agStopped + "\n" +
            "\n(F1 to toggle)";

        GUI.Box(new Rect(10, 10, 520, 220), s, style);
    }
}
