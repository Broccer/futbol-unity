using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyun akışı: menü, 2 kişilik maç (süre + skor), penaltı atışları (5'er + ani ölüm),
/// tek kişilik antrenman. Ekran yazıları OnGUI ile çizilir.
/// </summary>
public class MatchManager : MonoBehaviour
{
    public enum GameMode { None, Training, Match, Penalties }
    enum State { Menu, Playing, GoalPause, MatchOver, PenaltyReady, PenaltyInFlight, PenaltyResult, ShootoutOver }

    public static MatchManager Instance { get; private set; }

    [Header("Bağlantılar (PitchBuilder doldurur)")]
    public Ball ball;
    public PlayerController[] players = new PlayerController[2];
    public Teammate[] teammates = new Teammate[0];
    public GoalTrigger[] goals = new GoalTrigger[2];
    public CameraFollow cam;
    public Vector3 ballStart = new Vector3(0f, Ball.Radius, 0f);
    public float penaltySpotDistance = 6.5f;
    public Vector2 outOfBounds = new Vector2(20f, 30f);

    [Header("Maç")]
    public float matchMinutes = 3f;

    GameMode mode;
    State state;
    float stateTimer;
    readonly int[] score = new int[2];
    float timeLeft;
    int trainingGoals, trainingShots;
    bool matchWentToPenalties;

    // Penaltı serisi
    int shooter;
    readonly int[] penKicks = new int[2];
    readonly int[] penGoals = new int[2];
    readonly List<bool>[] penMarks = { new List<bool>(), new List<bool>() };
    float penaltySetupTime;
    int nextKickoff;

    string message;
    float messageUntil;
    GUIStyle bigStyle, midStyle, smallStyle, boxStyle, titleStyle, buttonStyle;

    PlayerController Shooter => players[shooter];
    PlayerController Keeper => players[1 - shooter];
    GoalTrigger PenaltyGoal => goals[1];

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start() { ShowMenu(); }

    // ================================================================ modlar

    void ShowMenu()
    {
        mode = GameMode.None;
        state = State.Menu;
        InputReader.Solo = false;
        SetActive(players[1], true);
        foreach (var t in teammates) SetActive(t, false);
        ball.ResetTo(ballStart);
        PlaceForKickoff(0, false);
        foreach (var p in players) p.SetRole(PlayerController.Role.Frozen);
        cam.SetSide(ball.transform, false);
    }

    public void StartTraining()
    {
        mode = GameMode.Training;
        state = State.Playing;
        InputReader.Solo = true;
        SetActive(players[1], false);
        foreach (var t in teammates) SetActive(t, true);
        trainingGoals = trainingShots = 0;
        var p = players[0];
        p.allowPass = true;
        p.ownGoal = goals[0];
        p.targetGoal = goals[1];
        p.SetRole(PlayerController.Role.Free);
        ResetTraining();
        cam.SetSide(p.transform, false);
        Show("Antrenman", 1.5f);
    }

    void ResetTraining()
    {
        ball.ResetTo(ballStart);
        players[0].Teleport(new Vector3(0, 0, -1.2f), Quaternion.identity);
    }

    public void StartMatch()
    {
        mode = GameMode.Match;
        InputReader.Solo = false;
        SetActive(players[1], true);
        foreach (var t in teammates) SetActive(t, false);
        score[0] = score[1] = 0;
        timeLeft = matchMinutes * 60f;
        matchWentToPenalties = false;
        for (int i = 0; i < 2; i++)
        {
            players[i].allowPass = false;
            players[i].ownGoal = goals[i];
            players[i].targetGoal = goals[1 - i];
        }
        cam.SetSide(ball.transform, false);
        Kickoff(Random.value < 0.5f ? 0 : 1);
    }

    void Kickoff(int kicker)
    {
        ball.ResetTo(ballStart);
        PlaceForKickoff(kicker, true);
        foreach (var p in players) p.SetRole(PlayerController.Role.Free);
        state = State.Playing;
        Show(players[kicker].displayName + " başlıyor", 1.2f);
    }

    /// <summary>Mavi +z'ye, Kırmızı -z'ye hücum eder. Başlama vuruşunu yapan topa yakın durur.</summary>
    void PlaceForKickoff(int kicker, bool _)
    {
        for (int i = 0; i < 2; i++)
        {
            float attack = i == 0 ? 1f : -1f;
            float dist = i == kicker ? 0.9f : 5f;
            players[i].Teleport(new Vector3(0, 0, -attack * dist), Quaternion.LookRotation(Vector3.forward * attack));
        }
    }

    public void StartPenalties(bool afterMatch)
    {
        mode = GameMode.Penalties;
        InputReader.Solo = false;
        SetActive(players[1], true);
        foreach (var t in teammates) SetActive(t, false);
        matchWentToPenalties = afterMatch;
        if (!afterMatch) score[0] = score[1] = 0;
        penKicks[0] = penKicks[1] = penGoals[0] = penGoals[1] = 0;
        penMarks[0].Clear();
        penMarks[1].Clear();
        foreach (var p in players) p.allowPass = false;
        shooter = 0;
        SetupPenalty();
    }

    void SetupPenalty()
    {
        var g = PenaltyGoal;
        float spotZ = g.lineZ - g.side * penaltySpotDistance;
        ball.ResetTo(new Vector3(0, Ball.Radius, spotZ));

        Shooter.Teleport(new Vector3(0, 0, spotZ - g.side * 1.1f), Quaternion.LookRotation(Vector3.forward * g.side));
        Shooter.targetGoal = g;
        Shooter.ownGoal = goals[0];
        Shooter.SetRole(PlayerController.Role.PenaltyShooter);

        Keeper.Teleport(new Vector3(0, 0, g.lineZ - g.side * 0.4f), Quaternion.LookRotation(Vector3.back * g.side));
        Keeper.ownGoal = g;
        Keeper.targetGoal = goals[0];
        Keeper.SetRole(PlayerController.Role.PenaltyKeeper);

        cam.SetFixed(new Vector3(0, 2.6f, spotZ - g.side * 6.5f), new Vector3(0, 1.1f, g.lineZ), true);
        penaltySetupTime = Time.time;
        state = State.PenaltyReady;
        Show($"{Shooter.displayName} atıyor — {Keeper.displayName} kalede", 2f);
    }

    void PenaltyOutcome(bool goal, string text)
    {
        if (state != State.PenaltyInFlight && state != State.PenaltyReady) return;
        penKicks[shooter]++;
        if (goal) penGoals[shooter]++;
        penMarks[shooter].Add(goal);
        Show(text, 1.8f);
        state = State.PenaltyResult;
        stateTimer = 0f;
        Keeper.SetRole(PlayerController.Role.Frozen);
        Shooter.SetRole(PlayerController.Role.Frozen);
    }

    bool ShootoutDecided()
    {
        int a = penGoals[0], b = penGoals[1], ka = penKicks[0], kb = penKicks[1];
        if (ka < 5 || kb < 5)
        {
            int ra = Mathf.Max(0, 5 - ka), rb = Mathf.Max(0, 5 - kb);
            return a + ra < b || b + rb < a;
        }
        return ka == kb && a != b; // ani ölüm: eşit atış sayısında fark varsa biter
    }

    // ================================================================ olaylar

    public void RegisterShot(int playerIndex)
    {
        if (mode == GameMode.Training) trainingShots++;
    }

    public void OnSave(PlayerController keeper)
    {
        if (mode == GameMode.Match && state == State.Playing) Show(keeper.displayName + " kurtardı!", 1f);
    }

    public void GoalScored(GoalTrigger goal)
    {
        switch (mode)
        {
            case GameMode.Training:
                if (state != State.Playing) return;
                trainingGoals++;
                Show("GOOOL!", 1.8f);
                state = State.GoalPause;
                stateTimer = 0f;
                break;

            case GameMode.Match:
                if (state != State.Playing) return;
                int scorer = 1 - goal.defenderIndex;
                score[scorer]++;
                Show($"GOOOL! {players[scorer].displayName}", 2.2f);
                nextKickoff = goal.defenderIndex; // yiyen başlar
                state = State.GoalPause;
                stateTimer = 0f;
                break;

            case GameMode.Penalties:
                if (state == State.PenaltyInFlight || state == State.PenaltyReady)
                    PenaltyOutcome(true, "GOL!");
                break;
        }
    }

    // ================================================================ döngü

    void Update()
    {
        if (state != State.Menu && InputReader.MenuPressed()) { ShowMenu(); return; }

        if (state == State.Menu)
        {
            int d = InputReader.DigitPressed();
            if (d == 1) StartMatch();
            else if (d == 2) StartPenalties(false);
            else if (d == 3) StartTraining();
            return;
        }

        stateTimer += Time.deltaTime;

        switch (state)
        {
            case State.Playing:
                if (mode == GameMode.Training && InputReader.ResetPressed()) ResetTraining();
                if (mode == GameMode.Match)
                {
                    timeLeft -= Time.deltaTime;
                    if (timeLeft <= 0f) EndMatch();
                }
                CheckOutOfBounds();
                break;

            case State.GoalPause:
                if (stateTimer > 2.2f)
                {
                    if (mode == GameMode.Training) { ResetTraining(); state = State.Playing; }
                    else Kickoff(nextKickoff);
                }
                break;

            case State.MatchOver:
                if (score[0] == score[1] && stateTimer > 3f) StartPenalties(true);
                break;

            case State.PenaltyReady:
                if (ball.LastKicker == (Component)Shooter && ball.LastKickTime >= penaltySetupTime)
                {
                    state = State.PenaltyInFlight;
                    stateTimer = 0f;
                }
                break;

            case State.PenaltyInFlight:
            {
                bool keeperTouched = ball.LastTouched == Keeper && ball.LastTouchTime >= penaltySetupTime;
                if (ball.ControlledBy == (Component)Keeper) PenaltyOutcome(false, "KURTARDI!");
                else if ((stateTimer > 0.6f && ball.Speed < 0.4f) || stateTimer > 3.5f || OutOfBounds())
                    PenaltyOutcome(false, keeperTouched ? "KURTARDI!" : "KAÇTI!");
                break;
            }

            case State.PenaltyResult:
                if (stateTimer > 2f)
                {
                    if (ShootoutDecided()) EndShootout();
                    else { shooter = 1 - shooter; SetupPenalty(); }
                }
                break;
        }
    }

    void EndMatch()
    {
        timeLeft = 0f;
        state = State.MatchOver;
        stateTimer = 0f;
        foreach (var p in players) p.SetRole(PlayerController.Role.Frozen);
        if (score[0] == score[1]) Show("Berabere! Penaltılara geçiliyor…", 3f);
        else Show($"Maç bitti — {players[score[0] > score[1] ? 0 : 1].displayName} kazandı!", 999f);
    }

    void EndShootout()
    {
        state = State.ShootoutOver;
        int w = penGoals[0] > penGoals[1] ? 0 : 1;
        Show($"{players[w].displayName} penaltılarla kazandı!", 999f);
    }

    bool OutOfBounds()
    {
        Vector3 p = ball.Position;
        return Mathf.Abs(p.x) > outOfBounds.x || Mathf.Abs(p.z) > outOfBounds.y || p.y < -3f;
    }

    void CheckOutOfBounds()
    {
        if (!OutOfBounds()) return;
        Show("Top dışarıda", 1.2f);
        if (mode == GameMode.Training) ResetTraining();
        else
        {
            // Son dokunanın rakibi başlar
            int last = ball.LastTouched ? ball.LastTouched.playerIndex : 0;
            Kickoff(1 - last);
        }
    }

    void Show(string text, float seconds)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    static void SetActive(Component c, bool on)
    {
        if (c && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    // ================================================================ ekran

    void EnsureStyles()
    {
        if (bigStyle != null) return;
        bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        bigStyle.normal.textColor = Color.white;
        midStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        midStyle.normal.textColor = Color.white;
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
        smallStyle.normal.textColor = Color.white;
        boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        titleStyle = new GUIStyle(midStyle) { fontSize = 44 };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fixedHeight = 52 };
    }

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    void OnGUI()
    {
        EnsureStyles();
        float W = Screen.width, H = Screen.height;

        if (state == State.Menu)
        {
            DrawMenu(W, H);
            return;
        }

        string blue = $"<color=#{Hex(players[0].color)}>{players[0].displayName.ToUpperInvariant()}</color>";
        string red = $"<color=#{Hex(players[1].color)}>{players[1].displayName.ToUpperInvariant()}</color>";

        if (mode == GameMode.Match || (mode == GameMode.Penalties && matchWentToPenalties))
        {
            int t = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
            string clock = mode == GameMode.Match ? $"   {t / 60:00}:{t % 60:00}" : "";
            GUI.Box(new Rect(W / 2f - 230, 10, 460, 48), $"{blue}  {score[0]} - {score[1]}  {red}{clock}", boxStyle);
        }
        if (mode == GameMode.Penalties)
        {
            float y = mode == GameMode.Penalties && matchWentToPenalties ? 64 : 10;
            GUI.Box(new Rect(W / 2f - 230, y, 460, 84), GUIContent.none);
            GUI.Label(new Rect(W / 2f - 220, y + 4, 440, 38), $"{blue}  {Marks(0)}   <b>{penGoals[0]}</b>", midStyle);
            GUI.Label(new Rect(W / 2f - 220, y + 42, 440, 38), $"{red}  {Marks(1)}   <b>{penGoals[1]}</b>", midStyle);
        }
        if (mode == GameMode.Training)
            GUI.Box(new Rect(12, 12, 280, 44), $"Gol: {trainingGoals}   Şut: {trainingShots}", boxStyle);

        // Oyuncuların başı üstünde güç çubuğu
        foreach (var p in players)
        {
            if (!p.gameObject.activeInHierarchy) continue;
            float c = p.Charge01;
            if (c <= 0f || Camera.main == null) continue;
            Vector3 sp = Camera.main.WorldToScreenPoint(p.transform.position + Vector3.up * 2.3f);
            if (sp.z < 0f) continue;
            var bg = new Rect(sp.x - 50, H - sp.y - 10, 100, 14);
            GUI.Box(bg, GUIContent.none);
            Color old = GUI.color;
            GUI.color = Color.Lerp(Color.green, Color.red, c);
            GUI.DrawTexture(new Rect(bg.x + 2, bg.y + 2, (bg.width - 4) * c, bg.height - 4), Texture2D.whiteTexture);
            GUI.color = old;
        }

        if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
            GUI.Label(new Rect(0, H * 0.22f, W, 90), message, bigStyle);

        // Alt bilgi
        string hint = mode switch
        {
            GameMode.Training => "WASD/Oklar koş · Shift sprint · Space şut (basılı tut) · E pas · F kayma · R sıfırla · Esc menü",
            GameMode.Penalties => "Atıcı: yön tuşunu basılı tutarak köşe seç (yukarı = üst köşe), şut tuşunu basılı tut-bırak. " +
                                  "Kaleci: sağ-sol hareket, plonjon tuşu (Mavi F · Kırmızı Sağ Ctrl). Esc menü",
            _ => "Mavi: WASD · Sol Shift · Space şut · F kayma/plonjon     Kırmızı: Oklar · Sağ Shift · Enter şut · Sağ Ctrl kayma/plonjon     Esc menü",
        };
        GUI.Label(new Rect(12, H - 44, W - 24, 40), hint, smallStyle);

        if (state == State.MatchOver && score[0] != score[1] || state == State.ShootoutOver)
        {
            float bx = W / 2f - 160;
            if (GUI.Button(new Rect(bx, H * 0.45f, 150, 52), "Rövanş", buttonStyle))
            {
                if (mode == GameMode.Penalties && !matchWentToPenalties) StartPenalties(false);
                else StartMatch();
            }
            if (GUI.Button(new Rect(bx + 170, H * 0.45f, 150, 52), "Menü", buttonStyle)) ShowMenu();
        }
    }

    string Marks(int i)
    {
        var sb = new System.Text.StringBuilder();
        int slots = Mathf.Max(5, penMarks[0].Count, penMarks[1].Count);
        for (int k = 0; k < slots; k++)
        {
            if (k < penMarks[i].Count)
                sb.Append(penMarks[i][k] ? "<color=#4CD964>●</color> " : "<color=#FF4D4D>●</color> ");
            else
                sb.Append("<color=#BBBBBB>○</color> ");
        }
        return sb.ToString();
    }

    void DrawMenu(float W, float H)
    {
        Color old = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
        GUI.color = old;

        float w = Mathf.Min(520f, W - 32f);
        float x = (W - w) / 2f;
        float y = Mathf.Max(20f, H * 0.12f);
        GUI.Label(new Rect(0, y, W, 60), "Mahalle Futbolu", titleStyle);
        y += 80;

        if (GUI.Button(new Rect(x, y, w, 52), "1 · Maç (2 oyuncu)", buttonStyle)) StartMatch();
        y += 62;
        if (GUI.Button(new Rect(x, y, w, 52), "2 · Penaltı atışları (2 oyuncu)", buttonStyle)) StartPenalties(false);
        y += 62;
        if (GUI.Button(new Rect(x, y, w, 52), "3 · Antrenman (tek oyuncu)", buttonStyle)) StartTraining();
        y += 70;

        GUI.Label(new Rect(x, y, 140, 32), "Maç süresi:", smallStyle);
        float[] options = { 2f, 3f, 5f };
        for (int i = 0; i < options.Length; i++)
        {
            bool sel = Mathf.Approximately(matchMinutes, options[i]);
            if (GUI.Toggle(new Rect(x + 120 + i * 90, y, 80, 28), sel, $" {options[i]:0} dk") && !sel) matchMinutes = options[i];
        }
        y += 44;

        string c0 = Hex(players[0].color), c1 = Hex(players[1].color);
        GUI.Label(new Rect(x, y, w, 140),
            $"<b><color=#{c0}>Mavi</color></b>: WASD koş · Sol Shift sprint · Space şut (basılı tut, bırak) · F kayma / plonjon\n" +
            $"<b><color=#{c1}>Kırmızı</color></b>: Oklar koş · Sağ Shift sprint · Enter şut · Sağ Ctrl kayma / plonjon\n" +
            "Kendi ceza sahanda plonjon tuşu kaleci atlayışı yapar ve topu elle tutarsın; dışarıda kayarak müdahale eder.\n" +
            "Gamepad: sol çubuk · RT sprint · B şut · X plonjon",
            smallStyle);
    }
}
