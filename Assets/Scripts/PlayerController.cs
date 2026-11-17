using UnityEngine;

/// <summary>
/// Oyuncunun kontrol ettiği futbolcu:
///  - Kameraya göre koşma, sprint, ivmelenme ve sınırlı dönüş hızıyla yön değiştirme
///  - Yakındaki topu kontrol altına alma ve ayağının önünde sürme
///  - Basılı tutup bırakılan güçlü şut (kaleye hafif otomatik nişan)
///  - Baktığı yöndeki takım arkadaşına yerden pas
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Bağlantılar")]
    public Ball ball;
    public Transform cameraTransform;
    public GoalTrigger targetGoal;

    [Header("Koşu")]
    public float jogSpeed = 5.5f;
    public float sprintSpeed = 8.5f;
    [Tooltip("Top ayaktayken hız çarpanı")]
    public float dribbleSpeedFactor = 0.9f;
    public float acceleration = 28f;
    public float deceleration = 22f;
    [Tooltip("Derece/saniye")]
    public float turnSpeed = 720f;
    public float sprintTurnSpeed = 380f;

    [Header("Top sürme")]
    public float controlRadius = 1.1f;
    [Tooltip("Normal koşuda top ayaktan ne kadar önde dursun")]
    public float dribbleDistance = 0.6f;
    [Tooltip("Sprintte top biraz daha önüne itilir")]
    public float sprintDribbleDistance = 1.2f;
    public float dribbleStiffness = 12f;

    [Header("Vuruş")]
    public float kickRange = 1.4f;
    public float minShotSpeed = 12f;
    public float maxShotSpeed = 30f;
    public float maxChargeTime = 1.0f;
    [Tooltip("Kaleye bu açıdan yakın şutlar kaleye doğru hafifçe düzeltilir")]
    public float shotAssistAngle = 35f;
    public float passMinSpeed = 7f;
    public float passMaxSpeed = 22f;
    public float passConeAngle = 65f;

    Rigidbody rb;
    Collider bodyCollider;
    Vector3 moveDir;
    bool sprint;
    bool charging;
    float charge;
    bool shootQueued;
    float queuedPower;
    bool passQueued;
    float noControlUntil;
    float nextKickTime;
    bool ignoringBall;

    public bool HasBall => ball != null && ball.ControlledBy == this;
    public float Charge01 => charging ? Mathf.Clamp01(charge / maxChargeTime) : 0f;
    public Vector3 Velocity => rb.GetVelocity();
    Vector3 Forward => rb.rotation * Vector3.forward;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
    }

    void Update()
    {
        // --- Yön (kameraya göre) ---
        Vector2 m = InputReader.Move();
        Vector3 camF = cameraTransform ? cameraTransform.forward.Flat() : Vector3.forward;
        if (camF.sqrMagnitude < 0.0001f) camF = Vector3.forward;
        camF.Normalize();
        Vector3 camR = Vector3.Cross(Vector3.up, camF);
        moveDir = Vector3.ClampMagnitude(camF * m.y + camR * m.x, 1f);
        sprint = InputReader.Sprint() && moveDir.sqrMagnitude > 0.01f;

        // --- Şut gücü: basılı tut, bırakınca vur ---
        if (InputReader.ShootHeld())
        {
            if (!charging) { charging = true; charge = 0f; }
            charge += Time.deltaTime;
        }
        else if (charging)
        {
            charging = false;
            queuedPower = Mathf.Clamp01(charge / maxChargeTime);
            shootQueued = true;
        }

        if (InputReader.PassPressed()) passQueued = true;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // --- Koşma ---
        float maxSpeed = sprint ? sprintSpeed : jogSpeed;
        if (HasBall) maxSpeed *= dribbleSpeedFactor;
        if (charging) maxSpeed *= 0.6f; // şuta hazırlanırken yavaşla

        Vector3 v = rb.GetVelocity();
        Vector3 h = v.Flat();
        Vector3 target = moveDir * maxSpeed;
        float rate = moveDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        // Ters yöne dönüşte daha sert fren: ani yön değişimi hissi
        if (Vector3.Dot(h, target) < 0f) rate *= 1.4f;
        h = Vector3.MoveTowards(h, target, rate * dt);
        rb.SetVelocity(new Vector3(h.x, v.y, h.z));

        // --- Dönüş (sınırlı hız: sprintte ve topla daha geniş dönüş) ---
        if (moveDir.sqrMagnitude > 0.01f)
        {
            float ts = sprint ? sprintTurnSpeed : turnSpeed;
            if (HasBall) ts *= 0.8f;
            Quaternion want = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, want, ts * dt));
        }

        UpdateBallControl(h);

        if (shootQueued) { shootQueued = false; TryShoot(queuedPower); }
        if (passQueued) { passQueued = false; TryPass(); }
    }

    // ------------------------------------------------------------------ top sürme

    void UpdateBallControl(Vector3 playerVel)
    {
        if (ball == null) return;

        Vector3 toBall = (ball.Position - rb.position).Flat();
        float dist = toBall.magnitude;

        if (HasBall)
        {
            // Top kaçtıysa (çok uzak ya da havada) kontrol biter
            if (dist > controlRadius * 1.8f || ball.Position.y > 1.0f)
            {
                ball.ControlledBy = null;
            }
        }
        else if (ball.ControlledBy == null && Time.time >= noControlUntil)
        {
            Vector3 relVel = ball.Rb.GetVelocity().Flat() - playerVel;
            bool low = ball.Position.y < 0.6f;
            // Çok sert gelen top kontrol edilemez, sekip gider
            if (dist < controlRadius && low && relVel.magnitude < 18f)
            {
                ball.ControlledBy = this;
            }
        }

        SetIgnoreBall(HasBall || Time.time < noControlUntil);
        if (!HasBall) return;

        // Topu ayağın önündeki noktaya doğru yay gibi çek
        float d = sprint ? sprintDribbleDistance : dribbleDistance;
        Vector3 targetPos = rb.position + Forward * d;
        Vector3 err = (targetPos - ball.Position).Flat();
        Vector3 desired = playerVel + err * dribbleStiffness;
        desired = Vector3.ClampMagnitude(desired, sprintSpeed * 1.6f);

        Vector3 cur = ball.Rb.GetVelocity();
        ball.Rb.SetVelocity(new Vector3(desired.x, Mathf.Min(cur.y, 0f), desired.z));
        ball.Rb.angularVelocity = Ball.RollingSpin(desired);
    }

    // Top ayaktayken gövdeyle çarpışmasın (dönüşlerde takılmasın)
    void SetIgnoreBall(bool ignore)
    {
        if (ignore == ignoringBall || bodyCollider == null) return;
        var ballCol = ball.GetComponent<Collider>();
        if (ballCol == null) return;
        Physics.IgnoreCollision(bodyCollider, ballCol, ignore);
        ignoringBall = ignore;
    }

    // ------------------------------------------------------------------ vuruş

    bool CanKick()
    {
        if (ball == null || Time.time < nextKickTime) return false;
        if (HasBall) return true;
        float dist = (ball.Position - rb.position).Flat().magnitude;
        return dist < kickRange && ball.Position.y < 1.2f && ball.ControlledBy == null;
    }

    void TryShoot(float power)
    {
        if (!CanKick()) return;

        Vector3 dir = Forward.Flat().normalized;
        if (targetGoal != null)
        {
            Vector3 toGoal = (targetGoal.AimPoint - ball.Position).Flat();
            if (toGoal.sqrMagnitude > 0.01f && Vector3.Angle(dir, toGoal) < shotAssistAngle)
                dir = Vector3.Slerp(dir, toGoal.normalized, 0.6f);
        }

        float speed = Mathf.Lerp(minShotSpeed, maxShotSpeed, power);
        float loft = Mathf.Lerp(0.03f, 0.2f, power);
        Vector3 vel = (dir + Vector3.up * loft).normalized * speed;

        DoKick(vel);
        if (MatchManager.Instance) MatchManager.Instance.RegisterShot();
    }

    void TryPass()
    {
        if (!CanKick()) return;

        Vector3 fwd = Forward.Flat().normalized;
        Teammate best = null;
        float bestScore = float.MaxValue;
        foreach (var t in Teammate.All)
        {
            Vector3 to = (t.transform.position - rb.position).Flat();
            float ang = Vector3.Angle(fwd, to);
            if (ang > passConeAngle) continue;
            float score = ang + to.magnitude * 0.5f; // önce açı, sonra yakınlık
            if (score < bestScore) { bestScore = score; best = t; }
        }

        Vector3 vel;
        if (best != null)
        {
            Vector3 to = (best.transform.position - ball.Position).Flat();
            float d = to.magnitude;
            vel = to / d * PassSpeedFor(d, ball, passMinSpeed, passMaxSpeed);
            best.ExpectPass();
        }
        else
        {
            vel = fwd * 10f; // önünde kimse yoksa boşluğa pas
        }
        vel.y = 0.4f; // çimden hafif sekerek gitsin
        DoKick(vel);
    }

    /// <summary>Top hedefe ~3 m/s ile ulaşacak şekilde başlangıç hızı.</summary>
    public static float PassSpeedFor(float distance, Ball b, float min, float max)
    {
        float decel = b.rollingResistance * 1.3f;
        return Mathf.Clamp(Mathf.Sqrt(9f + 2f * decel * distance), min, max);
    }

    void DoKick(Vector3 vel)
    {
        // Top gövdenin içindeyse önce ayağın önüne al
        Vector3 rel = (ball.Position - rb.position).Flat();
        if (rel.magnitude < 0.45f)
        {
            Vector3 p = rb.position + Forward * 0.55f;
            p.y = Ball.Radius;
            ball.Rb.position = p;
        }

        noControlUntil = Time.time + 0.35f;
        nextKickTime = Time.time + 0.3f;
        SetIgnoreBall(true);
        ball.Kick(vel, this);
    }

    public void Teleport(Vector3 pos, Quaternion rot)
    {
        rb.SetVelocity(Vector3.zero);
        rb.angularVelocity = Vector3.zero;
        rb.position = pos;
        rb.rotation = rot;
        transform.SetPositionAndRotation(pos, rot);
        charging = false;
        shootQueued = passQueued = false;
    }
}
