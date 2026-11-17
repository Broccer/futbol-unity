using UnityEngine;

/// <summary>
/// Top: yuvarlanma direnci, kimin kontrolünde olduğu ve vuruş.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    public const float Radius = 0.11f;

    [Tooltip("Yerde yuvarlanırken saniyede kaybettiği hız (m/s²)")]
    public float rollingResistance = 1.4f;

    public Rigidbody Rb { get; private set; }

    /// <summary>Topu şu an süren oyuncu (Futbolcu ya da takım arkadaşı). Boşta ise null.</summary>
    public Component ControlledBy { get; set; }
    public Component LastKicker { get; private set; }
    public float LastKickTime { get; private set; } = -10f;

    public bool IsGrounded => transform.position.y <= Radius + 0.04f;
    public Vector3 Position => Rb.position;

    void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        Rb.maxAngularVelocity = 200f; // varsayılan 7 rad/s, hızlı topun dönmesini keser
    }

    void FixedUpdate()
    {
        // Sürülen topun hızını oyuncu belirliyor; boştaki top çimde yavaşlar.
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

    /// <summary>Topa verilen hızla vurur. Yerdeki top kaymasın diye dönüşü de ayarlar.</summary>
    public void Kick(Vector3 velocity, Component kicker)
    {
        ControlledBy = null;
        LastKicker = kicker;
        LastKickTime = Time.time;
        Rb.WakeUp();
        Rb.SetVelocity(velocity);
        Rb.angularVelocity = RollingSpin(velocity);
    }

    /// <summary>Verilen yatay hızda kaymadan yuvarlanmak için gereken açısal hız.</summary>
    public static Vector3 RollingSpin(Vector3 velocity)
    {
        return Vector3.Cross(Vector3.up, velocity.Flat()) / Radius;
    }

    public void ResetTo(Vector3 position)
    {
        ControlledBy = null;
        Rb.SetVelocity(Vector3.zero);
        Rb.angularVelocity = Vector3.zero;
        Rb.position = position;
        transform.position = position;
    }
}
