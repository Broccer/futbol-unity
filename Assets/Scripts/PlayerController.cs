using UnityEngine;

/// <summary>
/// Oyuncunun kontrol ettiği futbolcu:
///  - Kameraya göre koşma, sprint, ivmelenme ve sınırlı dönüş hızıyla yön değiştirme
///  - Topu ayağının önünde sürme, rakibe yakın girip topu çalma
///  - Basılı tutup bırakılan şut: güç çubuğu + yerde nişan oku (yön)
///  - Kayma/plonjon tuşu: kendi ceza sahasında kaleci plonjonu, dışarıda kayarak müdahale
///  - Penaltıda atıcı (yön tuşlarıyla köşe seçer) ya da kaleci (çizgide sağa-sola) rolü
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public enum Role { Free, PenaltyShooter, PenaltyKeeper, Frozen }

    [Header("Kimlik")]
    public int playerIndex;
    public string displayName = "Mavi";
    public Color color = Color.blue;

    [Header("Bağlantılar")]
    public Ball ball;
    public Transform cameraTransform;
    public GoalTrigger targetGoal;
    public GoalTrigger ownGoal;
    public bool allowPass;
    public Transform visual;
    public Collider diveCollider;
    public Renderer aimArrow;

    public Role role = Role.Free;

    [Header("Koşu")]
    public float jogSpeed = 5.5f;
    public float sprintSpeed = 8.2f;
    public float dribbleSpeedFactor = 0.9f;
    public float acceleration = 28f;
    public float deceleration = 22f;
    public float turnSpeed = 720f;
    public float sprintTurnSpeed = 380f;

    [Header("Top sürme")]
    public float controlRadius = 1.1f;
    public float dribbleDistance = 0.6f;
    public float sprintDribbleDistance = 1.2f;
    public float dribbleStiffness = 12f;
    [Tooltip("Rakipteki topu bu mesafeden ve rakipten daha yakınsan çalarsın")]
    public float stealRadius = 0.8f;

    [Header("Şut")]
    public float kickRange = 1.4f;
    public float minShotSpeed = 11f;
    public float maxShotSpeed = 28f;
    public float maxChargeTime = 1.0f;
    public float shotAssistAngle = 30f;
    public float penaltyMinSpeed = 12f;
    public float penaltyMaxSpeed = 27f;
    public float passMinSpeed = 7f;
    public float passMaxSpeed = 22f;
    public float passConeAngle = 65f;

    [Header("Kalecilik / kayma")]
    public float diveSpeed = 7.5f;
    public float diveDuration = 0.55f;
    public float recoverTime = 0.4f;
    public float catchRadius = 1.0f;
    [Tooltip("Bundan sert gelen top tutulamaz, ancak gövdeyle çelinir")]
    public float catchMaxSpeed = 22f;

    Rigidbody rb;
    Collider bodyCollider;
    Vector3 moveDir;
    Vector2 rawMove;
    Vector3 camRight = Vector3.right;
    bool sprint;
    bool charging;
    float charge;
    bool shootQueued;
    float queuedPower;
    Vector2 queuedAim;
    bool passQueued;
    bool diveQueued;
    float noControlUntil;
    float nextKickTime;
    bool ignoringBall;
    float diveUntil = -10f, recoverUntil = -10f;
    Vector3 diveDir;
    bool keeperDiving;
    bool diveTouched;
    bool penaltyTaken;
    Material arrowMat;

    public bool HasBall => ball != null && ball.ControlledBy == this;
    public bool IsDiving => Time.time < diveUntil;
    bool Recovering => Time.time < recoverUntil;
    public float Charge01 => charging ? Mathf.Clamp01(charge / maxChargeTime) : 0f;
    public Vector3 Velocity => rb.GetVelocity();
    public bool PenaltyTaken => penaltyTaken;
    Vector3 Forward => rb.rotation * Vector3.forward;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
    }

    void Update()
    {
        if (role == Role.Frozen)
        {
            moveDir = Vector3.zero;
            sprint = charging = false;
            ShowArrow(false, Vector3.zero, 0f);
            return;
        }

        // --- Yön (kameraya göre) ---
        rawMove = InputReader.Move(playerIndex);
        Vector3 camF = cameraTransform ? cameraTransform.forward.Flat() : Vector3.forward;
        if (camF.sqrMagnitude < 0.0001f) camF = Vector3.forward;
        camF.Normalize();
        camRight = Vector3.Cross(Vector3.up, camF);
        moveDir = Vector3.ClampMagnitude(camF * rawMove.y + camRight * rawMove.x, 1f);
        sprint = InputReader.Sprint(playerIndex) && moveDir.sqrMagnitude > 0.01f;

        // --- Şut: basılı tut (güç), bırak (vur) ---
        bool canCharge = role != Role.PenaltyKeeper && !IsDiving;
        if (canCharge && InputReader.ShootHeld(playerIndex))
        {
            if (!charging) { charging = true; charge = 0f; }
            charge += Time.deltaTime;
        }
        else if (charging)
        {
            charging = false;
            queuedPower = Mathf.Clamp01(charge / maxChargeTime);
            queuedAim = rawMove;
            shootQueued = true;
        }

        if (allowPass && InputReader.PassPressed(playerIndex)) passQueued = true;
        if (InputReader.DivePressed(playerIndex)) diveQueued = true;

        // Nişan oku: serbest oyunda yön + güç görünür (penaltıda kaleci görmesin diye gizli)
        bool showArrow = charging && role == Role.Free && ball != null &&
                         (ball.Position - transform.position).Flat().magnitude < kickRange + 0.6f;
        ShowArrow(showArrow, showArrow ? ShotDirection() : Vector3.zero, Charge01);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (diveQueued) { diveQueued = false; TryDive(); }

        if (IsDiving)
        {
            DiveUpdate();
            return;
        }
        if (keeperDiving || (visual && visual.localRotation != Quaternion.identity)) EndDive();

        // --- Koşma ---
        Vector3 move = moveDir;
        if (role == Role.PenaltyShooter || role == Role.Frozen) move = Vector3.zero;
        if (role == Role.PenaltyKeeper) move = new Vector3(move.x, 0f, 0f); // sadece çizgi boyunca

        float maxSpeed = sprint ? sprintSpeed : jogSpeed;
        if (HasBall) maxSpeed *= dribbleSpeedFactor;
        if (charging) maxSpeed *= 0.6f;
        if (Recovering) maxSpeed *= 0.25f;
        if (role == Role.PenaltyKeeper) maxSpeed = 4.5f;

        Vector3 v = rb.GetVelocity();
        Vector3 h = v.Flat();
        Vector3 target = move * maxSpeed;
        float rate = move.sqrMagnitude > 0.01f ? acceleration : deceleration;
        if (Vector3.Dot(h, target) < 0f) rate *= 1.4f;
        h = Vector3.MoveTowards(h, target, rate * dt);

        if (role == Role.PenaltyKeeper && ownGoal != null)
        {
            // Kaleci çizgide kalır, direkler arasında
            float lineZ = ownGoal.lineZ - ownGoal.side * 0.4f;
            h.z = (lineZ - rb.position.z) * 8f;
            float lim = ownGoal.goalHalfWidth + 0.2f;
            if ((rb.position.x > lim && h.x > 0f) || (rb.position.x < -lim && h.x < 0f)) h.x = 0f;
        }
        rb.SetVelocity(new Vector3(h.x, v.y, h.z));

        // --- Yüz dönüşü ---
        Vector3 face = Vector3.zero;
        if (role == Role.PenaltyShooter && targetGoal) face = (targetGoal.AimPoint - rb.position).Flat();
        else if (role == Role.PenaltyKeeper && ball) face = (ball.Position - rb.position).Flat();
        else if (move.sqrMagnitude > 0.01f) face = move;
        if (face.sqrMagnitude > 0.01f)
        {
            float ts = sprint ? sprintTurnSpeed : turnSpeed;
            if (HasBall) ts *= 0.8f;
            Quaternion want = Quaternion.LookRotation(face.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, want, ts * dt));
        }

        if (role == Role.Free) UpdateBallControl(h);
        else SetIgnoreBall(false);
        TryCatch();

        if (shootQueued)
        {
            shootQueued = false;
            if (role == Role.PenaltyShooter) TryPenaltyShot(queuedPower, queuedAim);
            else if (role == Role.Free) TryShoot(queuedPower);
        }
        if (passQueued) { passQueued = false; if (role == Role.Free) TryPass(); }
    }

    // ------------------------------------------------------------------ top sürme

    void UpdateBallControl(Vector3 playerVel)
    {
        if (ball == null) return;

        Vector3 toBall = (ball.Position - rb.position).Flat();
        float dist = toBall.magnitude;

        if (HasBall)
        {
            if (dist > controlRadius * 1.8f || ball.Position.y > 1.0f) ball.ControlledBy = null;
        }
        else if (Time.time >= noControlUntil && ball.CanClaim(this) && !Recovering)
        {
            if (ball.ControlledBy == null)
            {
                Vector3 relVel = ball.Rb.GetVelocity().Flat() - playerVel;
                if (dist < controlRadius && ball.Position.y < 0.6f && relVel.magnitude < 18f)
                    TakeBall(0.2f);
            }
            else if (ball.ControlledBy is PlayerController other && other != this)
            {
                // Rakibin ayağındaki top: ondan daha yakınsan çalarsın
                float otherDist = (ball.Position - other.transform.position).Flat().magnitude;
                if (dist < stealRadius && dist < otherDist - 0.05f) TakeBall(0.6f);
            }
        }

        SetIgnoreBall(HasBall || Time.time < noControlUntil);
        if (!HasBall) return;

        float d = sprint ? sprintDribbleDistance : dribbleDistance;
        Vector3 targetPos = rb.position + Forward * d;
        Vector3 err = (targetPos - ball.Position).Flat();
        Vector3 desired = playerVel + err * dribbleStiffness;
        desired = Vector3.ClampMagnitude(desired, sprintSpeed * 1.6f);

        Vector3 cur = ball.Rb.GetVelocity();
        ball.Rb.SetVelocity(new Vector3(desired.x, Mathf.Min(cur.y, 0f), desired.z));
        ball.Rb.angularVelocity = Ball.RollingSpin(desired);
    }

    void TakeBall(float lockSeconds)
    {
        ball.ControlledBy = this;
        ball.Lock(this, lockSeconds);
        ball.RegisterTouch(this);
    }

    void SetIgnoreBall(bool ignore)
    {
        if (ignore == ignoringBall || bodyCollider == null || ball == null) return;
        var ballCol = ball.GetComponent<Collider>();
        if (ballCol == null) return;
        Physics.IgnoreCollision(bodyCollider, ballCol, ignore);
        ignoringBall = ignore;
    }

    // ------------------------------------------------------------------ şut / pas

    bool CanKick(out bool poke)
    {
        poke = false;
        if (ball == null || Time.time < nextKickTime || IsDiving) return false;
        if (HasBall) return true;
        float dist = (ball.Position - rb.position).Flat().magnitude;
        if (dist > kickRange || ball.Position.y > 1.2f || !ball.CanClaim(this)) return false;
        if (ball.ControlledBy == null) return true;
        if (ball.ControlledBy is PlayerController other && other != this && dist < 0.95f)
        {
            poke = true; // rakibin ayağından dürterek al
            return true;
        }
        return false;
    }

    /// <summary>Bakılan yön; kaleye yakınsa kaleye doğru hafif düzeltilmiş.</summary>
    Vector3 ShotDirection()
    {
        Vector3 dir = Forward.Flat().normalized;
        if (moveDir.sqrMagnitude > 0.01f) dir = moveDir.Flat().normalized; // basılı tutarken yön seçilebilir
        if (targetGoal != null && ball != null)
        {
            Vector3 toGoal = (targetGoal.AimPoint - ball.Position).Flat();
            if (toGoal.sqrMagnitude > 0.01f && Vector3.Angle(dir, toGoal) < shotAssistAngle)
                dir = Vector3.Slerp(dir, toGoal.normalized, 0.5f);
        }
        return dir;
    }

    void TryShoot(float power)
    {
        if (!CanKick(out bool poke)) return;
        if (poke) power = Mathf.Min(power, 0.3f);

        Vector3 dir = ShotDirection();
        float speed = Mathf.Lerp(minShotSpeed, maxShotSpeed, power);
        float loft = Mathf.Lerp(0.03f, 0.22f, power);
        Vector3 vel = (dir + Vector3.up * loft).normalized * speed;

        DoKick(vel);
        if (MatchManager.Instance) MatchManager.Instance.RegisterShot(playerIndex);
    }

    /// <summary>
    /// Penaltı: bırakırken basılı yön tuşu köşeyi seçer (sağ/sol), yukarı = üst köşe, aşağı = yerden.
    /// Güç arttıkça top hızlanır ama %80'in üstünde isabet bozulur.
    /// </summary>
    void TryPenaltyShot(float power, Vector2 aim2)
    {
        if (penaltyTaken || ball == null || targetGoal == null) return;
        if ((ball.Position - rb.position).Flat().magnitude > kickRange + 0.6f) return;

        float lateral = Mathf.Clamp(Vector3.Dot(camRight, Vector3.right) * aim2.x, -1f, 1f);
        float halfW = targetGoal.goalHalfWidth - 0.35f;
        float height = aim2.y > 0.3f ? targetGoal.goalHeight - 0.45f : aim2.y < -0.3f ? 0.25f : 0.9f;
        Vector3 aim = new Vector3(lateral * halfW, height, targetGoal.lineZ);

        float err = Mathf.Max(0f, power - 0.8f) * 6f; // aşırı güç = sapma
        Vector2 r = Random.insideUnitCircle * err;
        aim.x += r.x;
        aim.y = Mathf.Max(0.12f, aim.y + r.y * 0.7f);

        float speed = Mathf.Lerp(penaltyMinSpeed, penaltyMaxSpeed, power);
        Vector3 from = ball.Position;
        Vector3 flat = (aim - from).Flat();
        float d = flat.magnitude;
        float t = d / speed;
        float g = -Physics.gravity.y;
        float vy = (aim.y - from.y + 0.5f * g * t * t) / t;
        Vector3 vel = flat / d * speed + Vector3.up * vy;

        penaltyTaken = true;
        DoKick(vel);
    }

    void TryPass()
    {
        if (!CanKick(out _)) return;

        Vector3 fwd = Forward.Flat().normalized;
        Teammate best = null;
        float bestScore = float.MaxValue;
        foreach (var t in Teammate.All)
        {
            Vector3 to = (t.transform.position - rb.position).Flat();
            float ang = Vector3.Angle(fwd, to);
            if (ang > passConeAngle) continue;
            float score = ang + to.magnitude * 0.5f;
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
            vel = fwd * 10f;
        }
        vel.y = 0.4f;
        DoKick(vel);
    }

    public static float PassSpeedFor(float distance, Ball b, float min, float max)
    {
        float decel = b.rollingResistance * 1.3f;
        return Mathf.Clamp(Mathf.Sqrt(9f + 2f * decel * distance), min, max);
    }

    void DoKick(Vector3 vel)
    {
        Vector3 rel = (ball.Position - rb.position).Flat();
        if (rel.magnitude < 0.45f)
        {
            Vector3 p = rb.position + Forward * 0.55f;
            p.y = Mathf.Max(Ball.Radius, ball.Position.y);
            ball.Rb.position = p;
        }

        noControlUntil = Time.time + 0.35f;
        nextKickTime = Time.time + 0.3f;
        SetIgnoreBall(true);
        ball.Kick(vel, this);
    }

    // ------------------------------------------------------------------ plonjon / kayma

    bool InOwnBox => role == Role.PenaltyKeeper || (ownGoal != null && ownGoal.InPenaltyArea(rb.position));

    void TryDive()
    {
        if (IsDiving || Recovering || role == Role.PenaltyShooter || role == Role.Frozen || ball == null) return;

        Vector3 dir = moveDir.Flat();
        if (role == Role.PenaltyKeeper)
        {
            float sx = Mathf.Abs(dir.x) > 0.2f ? Mathf.Sign(dir.x) : Mathf.Sign(ball.Position.x - rb.position.x);
            dir = new Vector3(sx == 0f ? 1f : sx, 0f, 0f);
        }
        else if (dir.sqrMagnitude < 0.01f)
        {
            dir = (ball.Position - rb.position).Flat();
            if (dir.sqrMagnitude < 0.01f) dir = Forward.Flat();
        }
        dir.Normalize();

        if (HasBall) ball.ControlledBy = null;
        charging = false;
        diveDir = dir;
        diveTouched = false;
        keeperDiving = InOwnBox;
        diveUntil = Time.time + diveDuration;
        recoverUntil = diveUntil + recoverTime;

        if (keeperDiving)
        {
            // Sağ ekseni plonjon yönüne bakacak şekilde dön, yana yat
            rb.MoveRotation(Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up));
            if (visual) visual.localRotation = Quaternion.Euler(0f, 0f, -75f);
            if (diveCollider) diveCollider.enabled = true;
            rb.SetVelocity(dir * diveSpeed + Vector3.up * 1.5f);
        }
        else
        {
            // Ayak önde kayarak müdahale
            rb.MoveRotation(Quaternion.LookRotation(dir, Vector3.up));
            if (visual) visual.localRotation = Quaternion.Euler(-65f, 0f, 0f);
            float s = Mathf.Max(rb.GetVelocity().Flat().magnitude, diveSpeed);
            rb.SetVelocity(dir * s);
        }
    }

    void DiveUpdate()
    {
        if (!keeperDiving && !diveTouched && ball != null)
        {
            Vector3 feet = rb.position + diveDir * 0.5f;
            Vector3 toBall = (ball.Position - feet).Flat();
            if (toBall.magnitude < 0.8f && ball.Position.y < 0.6f)
            {
                diveTouched = true;
                noControlUntil = Time.time + 0.3f;
                ball.Kick(diveDir * 7f + Vector3.up * 0.6f, this);
            }
        }
        // Kayarken yavaşla
        Vector3 v = rb.GetVelocity();
        Vector3 h = Vector3.MoveTowards(v.Flat(), Vector3.zero, (keeperDiving ? 6f : 9f) * Time.fixedDeltaTime);
        rb.SetVelocity(new Vector3(h.x, v.y, h.z));
        TryCatch();
    }

    void EndDive()
    {
        keeperDiving = false;
        if (visual) visual.localRotation = Quaternion.identity;
        if (diveCollider) diveCollider.enabled = false;
    }

    /// <summary>Kendi ceza sahasında elle tutma.</summary>
    void TryCatch()
    {
        if (ball == null || role == Role.PenaltyShooter || role == Role.Frozen || HasBall) return;
        if (!InOwnBox || !ball.CanClaim(this)) return;
        if (ball.ControlledBy != null && ball.ControlledBy != (Component)this) return; // ayaktaki top elle alınmaz
        if (ball.LastKicker == (Component)this && Time.time - ball.LastKickTime < 0.5f) return;

        Vector3 right = rb.rotation * Vector3.right;
        Vector3 hands = keeperDiving
            ? rb.position + right * 1.0f + Vector3.up * 0.6f
            : rb.position + Vector3.up * 1.0f + Forward * 0.3f;
        float reach = keeperDiving ? catchRadius * 1.3f : catchRadius;

        // Ayakta: diz altı toplar zaten ayakla kontrol edilir (penaltı kalecisi hariç)
        if (!keeperDiving && role != Role.PenaltyKeeper && ball.Position.y < 0.35f) return;
        if ((ball.Position - hands).magnitude > reach) return;
        if (ball.Speed > catchMaxSpeed) return; // çok sert: sadece gövde çeler

        ball.Rb.SetVelocity(Vector3.zero);
        ball.Rb.angularVelocity = Vector3.zero;
        Vector3 p = rb.position + Forward.Flat().normalized * dribbleDistance;
        p.y = Ball.Radius;
        ball.Rb.position = p;
        TakeBall(1.0f);
        if (MatchManager.Instance) MatchManager.Instance.OnSave(this);
    }

    // ------------------------------------------------------------------ yardımcılar

    void ShowArrow(bool show, Vector3 dir, float power)
    {
        if (aimArrow == null) return;
        if (aimArrow.gameObject.activeSelf != show) aimArrow.gameObject.SetActive(show);
        if (!show) return;
        if (arrowMat == null) arrowMat = aimArrow.material;
        float len = 1f + 3f * power;
        Transform t = aimArrow.transform;
        t.position = transform.position + dir * (0.6f + len / 2f) + Vector3.up * 0.03f;
        t.rotation = Quaternion.LookRotation(dir, Vector3.up);
        t.localScale = new Vector3(0.18f, 0.02f, len);
        arrowMat.color = Color.Lerp(new Color(0.3f, 1f, 0.3f), new Color(1f, 0.25f, 0.2f), power);
    }

    public void SetRole(Role r)
    {
        role = r;
        penaltyTaken = false;
        charging = shootQueued = passQueued = diveQueued = false;
    }

    public void Teleport(Vector3 pos, Quaternion rot)
    {
        EndDive();
        diveUntil = recoverUntil = -10f;
        rb.SetVelocity(Vector3.zero);
        rb.angularVelocity = Vector3.zero;
        rb.position = pos;
        rb.rotation = rot;
        transform.SetPositionAndRotation(pos, rot);
        charging = shootQueued = passQueued = diveQueued = false;
        penaltyTaken = false;
        noControlUntil = 0f;
        SetIgnoreBall(false);
    }
}
