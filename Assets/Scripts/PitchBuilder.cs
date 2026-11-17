using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sahneyi koddan kurar: saha + çizgiler, iki kale, top, futbolcu, iki takım arkadaşı,
/// kamera, ışık ve maç yöneticisi. Hiçbir hazır model/doku gerekmez.
/// Boş bir sahnede Play'e basılsa bile kendiliğinden çalışır.
/// </summary>
public class PitchBuilder : MonoBehaviour
{
    public const float Length = 70f;   // z ekseni (kale-kale)
    public const float Width = 45f;    // x ekseni
    public const float GoalWidth = 7.32f;
    public const float GoalHeight = 2.44f;
    public const float GoalDepth = 2f;

    static readonly Color Grass = new Color(0.20f, 0.52f, 0.22f);
    static readonly Color GrassLight = new Color(0.24f, 0.58f, 0.26f);
    static readonly Color LineWhite = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color NetGrey = new Color(0.85f, 0.85f, 0.88f);
    static readonly Color Board = new Color(0.10f, 0.18f, 0.40f);
    static readonly Color Shirt = new Color(0.10f, 0.35f, 0.85f);
    static readonly Color MateShirt = new Color(0.35f, 0.60f, 0.95f);
    static readonly Color Shorts = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color Skin = new Color(0.85f, 0.65f, 0.50f);
    static readonly Color Marker = new Color(1.00f, 0.85f, 0.10f);

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

        // --- Zemin ve çim şeritleri ---
        var ground = Box("Zemin", new Vector3(0, -0.5f, 0), new Vector3(Width + 20f, 1f, Length + 24f), Grass, true);
        RigidbodyCompat.SetSurface(ground.GetComponent<Collider>(), 0.6f, 0.6f);
        int stripes = 14;
        float sl = Length / stripes;
        for (int i = 0; i < stripes; i += 2)
            Box("Serit", new Vector3(0, 0.002f, -hl + sl * (i + 0.5f)), new Vector3(Width, 0.004f, sl), GrassLight, false);

        // --- Çizgiler ---
        Line(new Vector3(-hw, 0, -hl), new Vector3(hw, 0, -hl));
        Line(new Vector3(-hw, 0, hl), new Vector3(hw, 0, hl));
        Line(new Vector3(-hw, 0, -hl), new Vector3(-hw, 0, hl));
        Line(new Vector3(hw, 0, -hl), new Vector3(hw, 0, hl));
        Line(new Vector3(-hw, 0, 0), new Vector3(hw, 0, 0));
        Circle(Vector3.zero, 6f);
        Spot(Vector3.zero);

        GoalTrigger targetGoal = null;
        foreach (int s in new[] { -1, 1 })
        {
            float gl = s * hl;
            RectLines(gl, 28f, 11f); // ceza sahası
            RectLines(gl, 13f, 4f);  // kale alanı
            Spot(new Vector3(0, 0, gl - s * 8f));
            var g = BuildGoal(s, own: s < 0);
            if (s > 0) targetGoal = g;
        }

        // --- Saha çevresi panoları (top uzağa kaçmasın) ---
        float bx = hw + 6f, bz = hl + 7f;
        foreach (int s in new[] { -1, 1 })
        {
            Box("Pano", new Vector3(0, 0.6f, s * bz), new Vector3(2f * bx, 1.2f, 0.4f), Board, true);
            Box("Pano", new Vector3(s * bx, 0.6f, 0), new Vector3(0.4f, 1.2f, 2f * bz), Board, true);
        }

        // --- Top ---
        Vector3 ballStart = new Vector3(0, Ball.Radius, 0);
        var ball = BuildBall(ballStart);

        // --- Futbolcu ---
        Vector3 playerStart = new Vector3(0, 0, -1.2f);
        var playerGo = BuildFootballer("Futbolcu", playerStart, Shirt, true);
        var player = playerGo.AddComponent<PlayerController>();
        player.ball = ball;
        player.targetGoal = targetGoal;

        // --- Takım arkadaşları (pas için) ---
        foreach (var pos in new[] { new Vector3(-12f, 0, 10f), new Vector3(11f, 0, 18f) })
        {
            var mateGo = BuildFootballer("TakimArkadasi", pos, MateShirt, false);
            mateGo.transform.rotation = Quaternion.LookRotation(-pos.Flat().normalized);
            var mate = mateGo.AddComponent<Teammate>();
            mate.ball = ball;
            mate.passTarget = player;
            mate.SetHome(pos);
        }

        // --- Kamera ---
        Camera cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cg.AddComponent<Camera>();
            cg.AddComponent<AudioListener>();
        }
        cam.fieldOfView = 55f;
        cam.farClipPlane = 300f;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = playerGo.transform;
        follow.Snap();
        player.cameraTransform = cam.transform;

        // --- Işık ---
        if (FindAnyObjectByType<Light>() == null)
        {
            var lg = new GameObject("Gunes");
            var light = lg.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lg.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // --- Maç yöneticisi ---
        var mm = new GameObject("MacYoneticisi").AddComponent<MatchManager>();
        mm.ball = ball;
        mm.player = player;
        mm.ballStart = ballStart;
        mm.playerStart = playerStart;
        mm.outOfBounds = new Vector2(bx + 8f, bz + 8f);
    }

    // ================================================================ parçalar

    GoalTrigger BuildGoal(int side, bool own)
    {
        float z = side * Length / 2f;
        var g = new GameObject(own ? "Kale_Bizim" : "Kale_Rakip").transform;
        g.SetParent(root, false);

        const float pr = 0.06f;                 // direk yarıçapı
        float hx = GoalWidth / 2f + pr;          // direk merkezinin x'i
        float lineZ = z + side * pr;             // direkler çizginin üstünde

        foreach (int s in new[] { -1, 1 })
        {
            var post = Prim(PrimitiveType.Cylinder, "Direk", g, new Vector3(s * hx, GoalHeight / 2f, lineZ),
                new Vector3(2 * pr, GoalHeight / 2f, 2 * pr), Color.white, true);
            RigidbodyCompat.SetSurface(post.GetComponent<Collider>(), 0.5f, 0.3f);
        }
        var bar = Prim(PrimitiveType.Cylinder, "Ust_Direk", g, new Vector3(0, GoalHeight + pr, lineZ),
            new Vector3(2 * pr, hx + pr, 2 * pr), Color.white, true);
        bar.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        RigidbodyCompat.SetSurface(bar.GetComponent<Collider>(), 0.5f, 0.3f);

        // Ağlar (top içeride kalsın diye collider'lı, sekmesiz)
        float backZ = z + side * GoalDepth;
        var nets = new List<GameObject>
        {
            Box("Ag_Arka", new Vector3(0, GoalHeight / 2f, backZ), new Vector3(GoalWidth + 4 * pr, GoalHeight, 0.04f), NetGrey, true, g),
            Box("Ag_Ust", new Vector3(0, GoalHeight, z + side * GoalDepth / 2f), new Vector3(GoalWidth, 0.03f, GoalDepth), NetGrey, true, g),
        };
        foreach (int s in new[] { -1, 1 })
            nets.Add(Box("Ag_Yan", new Vector3(s * hx, GoalHeight / 2f, z + side * GoalDepth / 2f), new Vector3(0.04f, GoalHeight, GoalDepth), NetGrey, true, g));
        foreach (var n in nets) RigidbodyCompat.SetSurface(n.GetComponent<Collider>(), 0f, 0.9f);

        // Gol tetiği: top çizgiyi TAMAMEN geçince (ön yüz çizgi + top çapı)
        float front = 2f * Ball.Radius;
        var trig = new GameObject(own ? "GolTetik_Bizim" : "GolTetik_Rakip");
        trig.transform.SetParent(g, false);
        trig.transform.localPosition = new Vector3(0, GoalHeight / 2f - 0.05f, z + side * (front + (GoalDepth - front) / 2f));
        var bc = trig.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(GoalWidth - 0.05f, GoalHeight - 0.1f, GoalDepth - front - 0.06f);
        var gt = trig.AddComponent<GoalTrigger>();
        gt.isOwnGoal = own;
        gt.aimPoint = new Vector3(0, 1f, z);
        return gt;
    }

    Ball BuildBall(Vector3 pos)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        b.name = "Top";
        b.transform.position = pos;
        b.transform.localScale = Vector3.one * (Ball.Radius * 2f);
        b.GetComponent<Renderer>().sharedMaterial = Mat(Color.white);

        // Siyah yamalar: topun döndüğü görülsün
        Vector3[] patches = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        foreach (var p in patches)
        {
            var dot = Prim(PrimitiveType.Sphere, "Yama", b.transform, p * 0.36f, Vector3.one * 0.38f, new Color(0.1f, 0.1f, 0.1f), false);
            dot.transform.localPosition = p * 0.33f;
        }

        RigidbodyCompat.SetSurface(b.GetComponent<Collider>(), 0.6f, 0.5f);
        var rb = b.AddComponent<Rigidbody>();
        rb.mass = 0.43f;
        rb.SetDamping(0.08f, 0.02f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        return b.AddComponent<Ball>();
    }

    GameObject BuildFootballer(string name, Vector3 pos, Color shirt, bool controlled)
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

        var t = p.transform;
        Prim(PrimitiveType.Capsule, "Govde", t, new Vector3(0, 1.2f, 0), new Vector3(0.62f, 0.38f, 0.42f), shirt, false);
        Prim(PrimitiveType.Cube, "Sort", t, new Vector3(0, 0.78f, 0), new Vector3(0.52f, 0.25f, 0.36f), Shorts, false);
        Prim(PrimitiveType.Cube, "Bacak_Sol", t, new Vector3(-0.13f, 0.35f, 0), new Vector3(0.16f, 0.65f, 0.18f), Skin, false);
        Prim(PrimitiveType.Cube, "Bacak_Sag", t, new Vector3(0.13f, 0.35f, 0), new Vector3(0.16f, 0.65f, 0.18f), Skin, false);
        Prim(PrimitiveType.Sphere, "Kafa", t, new Vector3(0, 1.7f, 0), Vector3.one * 0.3f, Skin, false);
        // Göğüsteki şerit: hangi yöne baktığı belli olsun
        Prim(PrimitiveType.Cube, "Gogus", t, new Vector3(0, 1.25f, 0.2f), new Vector3(0.3f, 0.12f, 0.06f), Color.white, false);

        if (controlled)
        {
            // Ayak altında sarı halka: kontrol edilen oyuncu
            Prim(PrimitiveType.Cylinder, "Isaret", t, new Vector3(0, 0.01f, 0), new Vector3(1.1f, 0.005f, 1.1f), Marker, false);
        }
        return p;
    }

    // ================================================================ yardımcılar

    Material Mat(Color c)
    {
        if (materials.TryGetValue(c, out var m)) return m;
        if (baseMaterial == null)
        {
            // Aktif render pipeline'ın varsayılan materyali (Built-in ya da URP)
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
        var g = Box("Cizgi", (a + b) / 2f + Vector3.up * 0.006f, new Vector3(width, 0.004f, d.magnitude + width), LineWhite, false);
        g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    void Circle(Vector3 center, float radius, int segments = 48)
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
        Prim(PrimitiveType.Cylinder, "Nokta", root, p + Vector3.up * 0.006f, new Vector3(0.3f, 0.002f, 0.3f), LineWhite, false);
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
