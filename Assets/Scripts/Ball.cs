using UnityEngine;

/// <summary>
/// Top: yuvarlanma direnci, kimin kontrolünde olduğu, kısa süreli "kapma kilidi" ve vuruş.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    public const float Radius = 0.11f;

    [Tooltip("Yerde yuvarlanırken saniyede kaybettiği hız (m/s²)")]
    public float rollingResistance = 1.3f;

    public Rigidbody Rb { get; private set; }

    /// <summary>Topu şu an süren oyuncu (Futbolcu ya da takım arkadaşı). Boşta ise null.</summary>
    public Component ControlledBy { get; set; }
    public Component LastKicker { get; private set; }
    public float LastKickTime { get; private set; } = -10f;
    public PlayerController LastTouched { get; private set; }
    public float LastTouchTime { get; private set; } = -10f;

    Component lockOwner;
    float lockUntil;

    public bool IsGrounded => transform.position.y <= Radius + 0.04f;
    public Vector3 Position => Rb.position;
    public float Speed => Rb.GetVelocity().magnitude;

    void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        Rb.maxAngularVelocity = 200f; // varsayılan 7 rad/s, hızlı topun dönmesini keser
    }

    void FixedUpdate()
    {
        if (ControlledBy == null && IsGrounded)
        {
            Vector3 v = Rb.GetVelocity();
            Vector3 h = v.Flat();
            float s = h.magnitude;
            if (s > 0.0001f)
            {
                float ns = Mathf.Max(0f, s - rollingResistance * Time.fixedDeltaTime);
                h *= ns / s;
                Rb.SetVelocity(new Vector3(h.x, v.y, h.z));
            }
        }
    }

    void OnCollisionEnter(Collision c)
    {
        var pc = c.collider.GetComponentInParent<PlayerController>();
        if (pc != null) RegisterTouch(pc);
    }

    public void RegisterTouch(PlayerController pc)
    {
        LastTouched = pc;
        LastTouchTime = Time.time;
    }

    /// <summary>Belirli süre boyunca topu yalnızca 'owner' kontrol edebilir (çalım/kurtarış sonrası geri kapmayı önler).</summary>
    public void Lock(Component owner, float seconds)
    {
        lockOwner = owner;
        lockUntil = Time.time + seconds;
    }

    public bool CanClaim(Component c) => Time.time >= lockUntil || c == lockOwner;

    public void Kick(Vector3 velocity, Component kicker)
    {
        ControlledBy = null;
        LastKicker = kicker;
        LastKickTime = Time.time;
        if (kicker is PlayerController pc) RegisterTouch(pc);
        Lock(kicker, 0.25f);
        Rb.WakeUp();
        Rb.SetVelocity(velocity);
        Rb.angularVelocity = RollingSpin(velocity);
    }

    public static Vector3 RollingSpin(Vector3 velocity)
    {
        return Vector3.Cross(Vector3.up, velocity.Flat()) / Radius;
    }

    public void ResetTo(Vector3 position)
    {
        ControlledBy = null;
        LastKicker = null;
        LastTouched = null;
        lockOwner = null;
        lockUntil = 0f;
        Rb.SetVelocity(Vector3.zero);
        Rb.angularVelocity = Vector3.zero;
        Rb.position = position;
        transform.position = position;
    }
}
