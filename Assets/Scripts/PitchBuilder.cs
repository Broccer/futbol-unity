using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sahneyi koddan kurar: tel örgülü mahalle sahası (asfalt), iki kale, top, iki futbolcu
/// (Mavi / Kırmızı), antrenman için iki takım arkadaşı, kamera, ışık ve maç yöneticisi.
/// Boş bir sahnede Play'e basılsa bile kendiliğinden çalışır.
/// </summary>
public class PitchBuilder : MonoBehaviour
{
    public const float Length = 40f;     // z ekseni (kale-kale)
    public const float Width = 24f;      // x ekseni
    public const float GoalWidth = 5f;
    public const float GoalHeight = 2.2f;
    public const float GoalDepth = 1.6f;
    public const float BoxWidth = 13f;
    public const float BoxDepth = 7.5f;
    public const float PenaltySpot = 6.5f;
    public const float WallHeight = 4f;

    static readonly Color Asphalt = new Color(0.22f, 0.23f, 0.25f);
    static readonly Color Pavement = new Color(0.45f, 0.44f, 0.42f);
    static readonly Color Court = new Color(0.17f, 0.36f, 0.30f);
    static readonly Color LinePaint = new Color(0.96f, 0.93f, 0.85f);
    static readonly Color Fence = new Color(0.16f, 0.30f, 0.22f);
    static readonly Color Kerb = new Color(0.62f, 0.60f, 0.56f);
    static readonly Color NetGrey = new Color(0.85f, 0.85f, 0.88f);
    static readonly Color GoalFrame = new Color(0.95f, 0.95f, 0.95f);
    public static readonly Color Blue = new Color(0.12f, 0.38f, 0.90f);
    public static readonly Color Red = new Color(0.88f, 0.18f, 0.16f);
    static readonly Color MateShirt = new Color(0.45f, 0.65f, 0.95f);
    static readonly Color Shorts = new Color(0.12f, 0.12f, 0.14f);
    static readonly Color Skin = new Color(0.85f, 0.65f, 0.50f);

    Transform root;
    Material baseMaterial;
    readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        if (FindAnyObjectByType<PitchBuilder>() != null) return;
        if (FindAnyObjectByType<PlayerController>() != null) return;
        new GameObject("SahaKurucu").AddComponent<PitchBuilder>();
    }

    void Awake()
    {
        if (built) return;
        built = true;
        Build();
    }

    void Build()
    {
        root = new GameObject("Saha").transform;
        float hl = Length / 2f, hw = Width / 2f;

        // --- Zemin: kaldırım + boyalı oyun alanı ---
        var ground = Box("Zemin", new Vector3(0, -0.5f, 0), new Vector3(Width + 40f, 1f, Length + 40f), Pavement, true);
        RigidbodyCompat.SetSurface(ground.GetComponent<Collider>(), 0.6f, 0.6f);
        Box("Asfalt", new Vector3(0, 0.001f, 0), new Vector3(Width + 2f, 0.002f, Length + 2f), Asphalt, false);
        Box("Saha_Boya", new Vector3(0, 0.002f, 0), new Vector3(Width, 0.002f, Length), Court, false);

        // --- Çizgiler ---
        Line(new Vector3(-hw, 0, -hl), new Vector3(hw, 0, -hl));
        Line(new Vector3(-hw, 0, hl), new Vector3(hw, 0, hl));
        Line(new Vector3(-hw, 0, -hl), new Vector3(-hw, 0, hl));
        Line(new Vector3(hw, 0, -hl), new Vector3(hw, 0, hl));
        Line(new Vector3(-hw, 0, 0), new Vector3(hw, 0, 0));
        Circle(Vector3.zero, 4f);
        Spot(Vector3.zero);

        var goals = new GoalTrigger[2];
        foreach (int s in new[] { -1, 1 })
        {
            float gl = s * hl;
            RectLines(gl, BoxWidth, BoxDepth);
            Spot(new Vector3(0, 0, gl - s * PenaltySpot));
            var g = BuildGoal(s);
            goals[g.defenderIndex] = g;
        }

        // --- Tel örgü: görünmez yüksek duvarlar (top kaçmasın), uzak kenarda görünür çit ---
        float wx = hw + 1.5f, wz = hl + 1.5f;
        foreach (int s in new[] { -1, 1 })
        {
            Wall(new Vector3(s * wx, WallHeight / 2f, 0), new Vector3(0.3f, WallHeight, 2f * wz + 0.3f));
            Wall(new Vector3(0, WallHeight / 2f, s * wz), new Vector3(2f * wx + 0.3f, WallHeight, 0.3f));
            // Kameraya yakın kenar (x+) alçak bordür, diğerleri çit görünümü
            if (s > 0)
                Box("Bordur", new Vector3(s * wx, 0.25f, 0), new Vector3(0.3f, 0.5f, 2f * wz), Kerb, false);
            else
                FenceVisual(new Vector3(s * wx, 0, 0), 2f * wz, true);
            FenceVisual(new Vector3(0, 0, s * wz), 2f * wx, false);
        }
        Box("Tavan", new Vector3(0, WallHeight + 4f, 0), new Vector3(2f * wx, 0.2f, 2f * wz), Fence, true).GetComponent<Renderer>().enabled = false;

        // --- Top ---
        Vector3 ballStart = new Vector3(0, Ball.Radius, 0);
        var ball = BuildBall(ballStart);

        // --- İki futbolcu ---
        var players = new PlayerController[2];
        for (int i = 0; i < 2; i++)
        {
            Color c = i == 0 ? Blue : Red;
            var go = BuildFootballer(i == 0 ? "Mavi" : "Kirmizi", new Vector3(0, 0, i == 0 ? -3f : 3f), c, true,
                out Transform visual, out Collider dive, out Renderer arrow);
            var pc = go.AddComponent<PlayerController>();
            pc.playerIndex = i;
            pc.displayName = i == 0 ? "Mavi" : "Kırmızı";
            pc.color = c;
            pc.ball = ball;
            pc.visual = visual;
            pc.diveCollider = dive;
            pc.aimArrow = arrow;
            pc.ownGoal = goals[i];
            pc.targetGoal = goals[1 - i];
            players[i] = pc;
        }

        // --- Antrenman için takım arkadaşları ---
        var mates = new List<Teammate>();
        foreach (var pos in new[] { new Vector3(-6f, 0, 6f), new Vector3(6f, 0, 12f) })
        {
            var mateGo = BuildFootballer("TakimArkadasi", pos, MateShirt, false, out _, out _, out _);
            mateGo.transform.rotation = Quaternion.LookRotation(-pos.Flat().normalized);
            var mate = mateGo.AddComponent<Teammate>();
            mate.ball = ball;
            mate.passTarget = players[0];
            mate.SetHome(pos);
            mates.Add(mate);
        }

        // --- Kamera ---
        Camera cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cg.AddComponent<Camera>();
            cg.AddComponent<AudioListener>();
        }
        cam.fieldOfView = 50f;
        cam.farClipPlane = 300f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.70f, 0.85f);
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.zLimit = hl - 11f;
        follow.SetSide(ball.transform, true);
        foreach (var p in players) p.cameraTransform = cam.transform;

        // --- Işık ---
        if (FindAnyObjectByType<Light>() == null)
        {
            var lg = new GameObject("Gunes");
            var light = lg.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lg.transform.rotation = Quaternion.Euler(55f, -40f, 0f);
        }

        // --- Maç yöneticisi ---
        var mm = new GameObject("MacYoneticisi").AddComponent<MatchManager>();
        mm.ball = ball;
        mm.players = players;
        mm.teammates = mates.ToArray();
        mm.goals = goals;
        mm.cam = follow;
        mm.ballStart = ballStart;
        mm.penaltySpotDistance = PenaltySpot;
        mm.outOfBounds = new Vector2(wx + 3f, wz + 3f);
    }

    // ================================================================ parçalar

    GoalTrigger BuildGoal(int side)
    {
        float z = side * Length / 2f;
        int defender = side < 0 ? 0 : 1; // Mavi -z kalesini, Kırmızı +z kalesini savunur
        var g = new GameObject(defender == 0 ? "Kale_Mavi" : "Kale_Kirmizi").transform;
        g.SetParent(root, false);

        const float pr = 0.06f;
        float hx = GoalWidth / 2f + pr;
        float lineZ = z + side * pr;

        foreach (int s in new[] { -1, 1 })
        {
            var post = Prim(PrimitiveType.Cylinder, "Direk", g, new Vector3(s * hx, GoalHeight / 2f, lineZ),
                new Vector3(2 * pr, GoalHeight / 2f, 2 * pr), GoalFrame, true);
            RigidbodyCompat.SetSurface(post.GetComponent<Collider>(), 0.5f, 0.3f);
        }
        var bar = Prim(PrimitiveType.Cylinder, "Ust_Direk", g, new Vector3(0, GoalHeight + pr, lineZ),
            new Vector3(2 * pr, hx + pr, 2 * pr), GoalFrame, true);
        bar.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        RigidbodyCompat.SetSurface(bar.GetComponent<Collider>(), 0.5f, 0.3f);

        float backZ = z + side * GoalDepth;
        var nets = new List<GameObject>
        {
            Box("Ag_Arka", new Vector3(0, GoalHeight / 2f, backZ), new Vector3(GoalWidth + 4 * pr, GoalHeight, 0.04f), NetGrey, true, g),
            Box("Ag_Ust", new Vector3(0, GoalHeight, z + side * GoalDepth / 2f), new Vector3(GoalWidth, 0.03f, GoalDepth), NetGrey, true, g),
        };
        foreach (int s in new[] { -1, 1 })
            nets.Add(Box("Ag_Yan", new Vector3(s * hx, GoalHeight / 2f, z + side * GoalDepth / 2f), new Vector3(0.04f, GoalHeight, GoalDepth), NetGrey, true, g));
        foreach (var n in nets) RigidbodyCompat.SetSurface(n.GetComponent<Collider>(), 0f, 0.9f);

        float front = 2f * Ball.Radius;
        var trig = new GameObject("GolTetik");
        trig.transform.SetParent(g, false);
        trig.transform.localPosition = new Vector3(0, GoalHeight / 2f - 0.05f, z + side * (front + (GoalDepth - front) / 2f));
        var bc = trig.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(GoalWidth - 0.05f, GoalHeight - 0.1f, GoalDepth - front - 0.06f);
        var gt = trig.AddComponent<GoalTrigger>();
        gt.defenderIndex = defender;
        gt.side = side;
        gt.lineZ = z;
        gt.goalHalfWidth = GoalWidth / 2f;
        gt.goalHeight = GoalHeight;
        gt.boxHalfWidth = BoxWidth / 2f;
        gt.boxDepth = BoxDepth;
        gt.aimPoint = new Vector3(0, 1f, z);
        return gt;
    }

    Ball BuildBall(Vector3 pos)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        b.name = "Top";
        b.transform.position = pos;
        b.transform.localScale = Vector3.one * (Ball.Radius * 2f);
        b.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.98f, 0.95f, 0.30f));

        Vector3[] patches = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        foreach (var p in patches)
            Prim(PrimitiveType.Sphere, "Yama", b.transform, p * 0.33f, Vector3.one * 0.38f, new Color(0.1f, 0.1f, 0.1f), false);

        RigidbodyCompat.SetSurface(b.GetComponent<Collider>(), 0.6f, 0.5f);
        var rb = b.AddComponent<Rigidbody>();
        rb.mass = 0.43f;
        rb.SetDamping(0.08f, 0.02f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        return b.AddComponent<Ball>();
    }

    GameObject BuildFootballer(string name, Vector3 pos, Color shirt, bool controlled,
        out Transform visual, out Collider diveCollider, out Renderer arrow)
    {
        var p = new GameObject(name);
        p.transform.SetParent(root, false);
        p.transform.position = pos;

        var col = p.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.height = 1.8f;
        col.radius = 0.35f;
        RigidbodyCompat.SetSurface(col, 0f, 0f);

        var rb = p.AddComponent<Rigidbody>();
        rb.mass = 75f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.SetDamping(0f, 0f);

        // Görsel gövde ayrı bir çocukta: plonjon/kaymada yatırılır
        visual = new GameObject("Gorsel").transform;
        visual.SetParent(p.transform, false);
        Prim(PrimitiveType.Capsule, "Govde", visual, new Vector3(0, 1.2f, 0), new Vector3(0.62f, 0.38f, 0.42f), shirt, false);
        Prim(PrimitiveType.Cube, "Sort", visual, new Vector3(0, 0.78f, 0), new Vector3(0.52f, 0.25f, 0.36f), Shorts, false);
        Prim(PrimitiveType.Cube, "Bacak_Sol", visual, new Vector3(-0.13f, 0.35f, 0), new Vector3(0.16f, 0.65f, 0.18f), Skin, false);
        Prim(PrimitiveType.Cube, "Bacak_Sag", visual, new Vector3(0.13f, 0.35f, 0), new Vector3(0.16f, 0.65f, 0.18f), Skin, false);
        Prim(PrimitiveType.Sphere, "Kafa", visual, new Vector3(0, 1.7f, 0), Vector3.one * 0.3f, Skin, false);
        Prim(PrimitiveType.Cube, "Gogus", visual, new Vector3(0, 1.25f, 0.2f), new Vector3(0.3f, 0.12f, 0.06f), Color.white, false);

        diveCollider = null;
        arrow = null;
        if (controlled)
        {
            // Plonjonda uzanan gövde/kollar: yana doğru geniş kutu (başta kapalı)
            var diveGo = new GameObject("Plonjon");
            diveGo.transform.SetParent(p.transform, false);
            var bc = diveGo.AddComponent<BoxCollider>();
            bc.center = new Vector3(0.95f, 0.6f, 0f);
            bc.size = new Vector3(1.9f, 0.6f, 0.5f);
            bc.enabled = false;
            diveCollider = bc;

            // Ayak altında takım renginde halka
            Prim(PrimitiveType.Cylinder, "Isaret", p.transform, new Vector3(0, 0.01f, 0), new Vector3(1.1f, 0.005f, 1.1f), shirt, false);

            // Şut nişan oku (kökte, gövdeyle yatmasın)
            var a = Prim(PrimitiveType.Cube, "NisanOku", p.transform, Vector3.zero, Vector3.one, Color.green, false);
            a.transform.SetParent(null, true); // dünya konumunda serbest dursun
            a.transform.SetParent(root, true);
            arrow = a.GetComponent<Renderer>();
            a.SetActive(false);
        }
        return p;
    }

    void Wall(Vector3 pos, Vector3 size)
    {
        var w = Box("Duvar", pos, size, Fence, true);
        w.GetComponent<Renderer>().enabled = false;
        RigidbodyCompat.SetSurface(w.GetComponent<Collider>(), 0.55f, 0.2f);
    }

    /// <summary>Çit görünümü: direkler + iki yatay boru + ince örgü paneli.</summary>
    void FenceVisual(Vector3 center, float length, bool alongZ)
    {
        Vector3 axis = alongZ ? Vector3.forward : Vector3.right;
        int posts = Mathf.CeilToInt(length / 4f);
        for (int i = 0; i <= posts; i++)
        {
            Vector3 p = center + axis * (-length / 2f + i * length / posts);
            Box("Cit_Direk", p + Vector3.up * WallHeight / 2f, new Vector3(0.1f, WallHeight, 0.1f), Fence, false);
        }
        Vector3 railScale = alongZ ? new Vector3(0.06f, 0.06f, length) : new Vector3(length, 0.06f, 0.06f);
        Box("Cit_Boru", center + Vector3.up * WallHeight, railScale, Fence, false);
        Box("Cit_Boru", center + Vector3.up * 1.0f, railScale, Fence, false);
        Vector3 panel = alongZ ? new Vector3(0.02f, WallHeight - 0.2f, length) : new Vector3(length, WallHeight - 0.2f, 0.02f);
        Box("Cit_Orgu", center + Vector3.up * (WallHeight / 2f), panel, new Color(0.30f, 0.42f, 0.36f), false);
    }

    // ================================================================ yardımcılar

    Material Mat(Color c)
    {
        if (materials.TryGetValue(c, out var m)) return m;
        // Derlemede shader'ın atılmaması için Resources'taki malzeme esas alınır (yoksa pembe görünür)
        if (baseMaterial == null) baseMaterial = Resources.Load<Material>("FutbolBase");
        if (baseMaterial == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMaterial = tmp.GetComponent<Renderer>().sharedMaterial;
            DestroyImmediate(tmp);
        }
        m = new Material(baseMaterial) { color = c };
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        materials[c] = m;
        return m;
    }

    GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Color c, bool keepCollider)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent ? parent : root, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        g.GetComponent<Renderer>().sharedMaterial = Mat(c);
        if (!keepCollider) DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    GameObject Box(string name, Vector3 pos, Vector3 scale, Color c, bool collider, Transform parent = null)
    {
        return Prim(PrimitiveType.Cube, name, parent, pos, scale, c, collider);
    }

    void Line(Vector3 a, Vector3 b, float width = 0.12f)
    {
        Vector3 d = b - a;
        var g = Box("Cizgi", (a + b) / 2f + Vector3.up * 0.006f, new Vector3(width, 0.004f, d.magnitude + width), LinePaint, false);
        g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    void Circle(Vector3 center, float radius, int segments = 40)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Line(center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius,
                 center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius);
        }
    }

    void Spot(Vector3 p)
    {
        Prim(PrimitiveType.Cylinder, "Nokta", root, p + Vector3.up * 0.006f, new Vector3(0.3f, 0.002f, 0.3f), LinePaint, false);
    }

    void RectLines(float goalLineZ, float width, float depth)
    {
        float s = Mathf.Sign(goalLineZ);
        float inner = goalLineZ - s * depth;
        float hw = width / 2f;
        Line(new Vector3(-hw, 0, inner), new Vector3(hw, 0, inner));
        Line(new Vector3(-hw, 0, goalLineZ), new Vector3(-hw, 0, inner));
        Line(new Vector3(hw, 0, goalLineZ), new Vector3(hw, 0, inner));
    }
}
