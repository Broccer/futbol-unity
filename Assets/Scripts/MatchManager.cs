using System.Collections;
using UnityEngine;

/// <summary>Skor, gol sonrası yeniden başlatma, saha dışı kontrolü ve ekran yazıları.</summary>
public class MatchManager : MonoBehaviour
{
    public static MatchManager Instance { get; private set; }

    public Ball ball;
    public PlayerController player;
    public Vector3 ballStart = new Vector3(0f, Ball.Radius, 0f);
    public Vector3 playerStart = new Vector3(0f, 0f, -1.2f);
    public Vector2 outOfBounds = new Vector2(36f, 50f); // |x|, |z| sınırı

    int goals;
    int ownGoals;
    int shots;
    string message;
    float messageUntil;
    bool resetting;
    GUIStyle bigStyle, smallStyle, boxStyle;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void RegisterShot() { shots++; }

    public void GoalScored(GoalTrigger goal)
    {
        if (resetting) return;
        if (goal.isOwnGoal) { ownGoals++; Show("Kendi kalene!", 2f); }
        else { goals++; Show("GOOOL!", 2f); }
        StartCoroutine(ResetAfter(2f));
    }

    void Update()
    {
        if (InputReader.ResetPressed()) ResetPositions();

        if (ball && !resetting)
        {
            Vector3 p = ball.Position;
            if (Mathf.Abs(p.x) > outOfBounds.x || Mathf.Abs(p.z) > outOfBounds.y || p.y < -3f)
            {
                Show("Top dışarıda", 1.2f);
                StartCoroutine(ResetAfter(1.2f));
            }
        }
    }

    void Show(string text, float seconds)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    IEnumerator ResetAfter(float seconds)
    {
        resetting = true;
        yield return new WaitForSeconds(seconds);
        ResetPositions();
        resetting = false;
    }

    public void ResetPositions()
    {
        if (ball) ball.ResetTo(ballStart);
        if (player) player.Teleport(playerStart, Quaternion.identity);
    }

    void OnGUI()
    {
        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bigStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            smallStyle.normal.textColor = Color.white;
            boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 6, 6) };
        }

        GUI.Box(new Rect(12, 12, 300, 40), $"Gol: {goals}    Şut: {shots}    Kendi kale: {ownGoals}", boxStyle);

        GUI.Label(new Rect(12, Screen.height - 30, Screen.width - 24, 24),
            "WASD/Oklar: koş   Shift: sprint   Space (basılı tut, bırak): şut   E: pas   R: sıfırla",
            smallStyle);

        if (player)
        {
            float c = player.Charge01;
            if (c > 0f)
            {
                var bg = new Rect(Screen.width / 2f - 150, Screen.height - 80, 300, 22);
                GUI.Box(bg, GUIContent.none);
                Color old = GUI.color;
                GUI.color = Color.Lerp(Color.green, Color.red, c);
                GUI.DrawTexture(new Rect(bg.x + 2, bg.y + 2, (bg.width - 4) * c, bg.height - 4), Texture2D.whiteTexture);
                GUI.color = old;
            }
            if (player.HasBall)
                GUI.Label(new Rect(Screen.width / 2f - 60, Screen.height - 110, 200, 24), "Top sende", smallStyle);
        }

        if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 100), message, bigStyle);
    }
}
