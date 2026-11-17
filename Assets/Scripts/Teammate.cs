using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Basit takım arkadaşı: yakına gelen topu kontrol eder, kısa bekler ve
/// futbolcunun koştuğu yere pası geri verir. Pas denemek için var.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Teammate : MonoBehaviour
{
    public static readonly List<Teammate> All = new List<Teammate>();

    public Ball ball;
    public PlayerController passTarget;
    public float receiveRadius = 1.1f;
    public float holdTime = 0.8f;
    public float chaseRadius = 7f;
    public float moveSpeed = 5f;

    Rigidbody rb;
    Collider bodyCollider;
    Vector3 home;
    float holdTimer;
    float noControlUntil;
    float expectUntil;

    bool HasBall => ball != null && ball.ControlledBy == this;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        home = transform.position;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Pas geldiğini bilsin: biraz daha geniş alanda topu karşılamaya çıkar.</summary>
    public void ExpectPass() { expectUntil = Time.time + 3f; }

    public void SetHome(Vector3 p) { home = p; }

    void FixedUpdate()
    {
        if (ball == null) return;
        float dt = Time.fixedDeltaTime;
        Vector3 toBall = (ball.Position - rb.position).Flat();
        float dist = toBall.magnitude;
        Vector3 moveTarget = home;
        Vector3 face = toBall;

        if (HasBall)
        {
            moveTarget = rb.position; // dur
            Vector3 toPlayer = passTarget ? (passTarget.transform.position - rb.position).Flat() : Vector3.forward;
            face = toPlayer;

            // Topu ayağın önünde tut
            Vector3 fwd = rb.rotation * Vector3.forward;
            Vector3 hold = rb.position + fwd * 0.6f;
            Vector3 err = (hold - ball.Position).Flat();
            ball.Rb.SetVelocity(new Vector3(err.x * 10f, Mathf.Min(ball.Rb.GetVelocity().y, 0f), err.z * 10f));

            holdTimer -= dt;
            if (holdTimer <= 0f && passTarget != null) PassBack();
        }
        else
        {
            bool free = ball.ControlledBy == null && ball.Position.y < 0.8f;
            if (free && Time.time >= noControlUntil && dist < receiveRadius)
            {
                ball.ControlledBy = this;
                holdTimer = holdTime;
            }
            else if (free && Time.time >= noControlUntil &&
                     (dist < chaseRadius || (Time.time < expectUntil && dist < chaseRadius * 2f)))
            {
                // Topa doğru koş; gelen topun biraz önüne
                moveTarget = ball.Position + ball.Rb.GetVelocity().Flat() * 0.3f;
            }
        }

        // Hareket
        Vector3 v = rb.GetVelocity();
        Vector3 to = (moveTarget - rb.position).Flat();
        Vector3 want = to.magnitude > 0.3f ? to.normalized * Mathf.Min(moveSpeed, to.magnitude * 3f) : Vector3.zero;
        Vector3 h = Vector3.MoveTowards(v.Flat(), want, 25f * dt);
        rb.SetVelocity(new Vector3(h.x, v.y, h.z));

        if (face.sqrMagnitude > 0.01f)
        {
            Quaternion q = Quaternion.LookRotation(face.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, q, 540f * dt));
        }

        // Top ayaktayken gövdeye takılmasın
        var ballCol = ball.GetComponent<Collider>();
        if (bodyCollider && ballCol)
            Physics.IgnoreCollision(bodyCollider, ballCol, HasBall || Time.time < noControlUntil);
    }

    void PassBack()
    {
        // Futbolcunun koştuğu yerin biraz önüne
        Vector3 lead = passTarget.transform.position + passTarget.Velocity.Flat() * 0.6f;
        Vector3 to = (lead - ball.Position).Flat();
        float d = Mathf.Max(0.5f, to.magnitude);
        float speed = PlayerController.PassSpeedFor(d, ball, 6f, 20f);
        Vector3 vel = to / d * speed;
        vel.y = 0.4f;
        noControlUntil = Time.time + 0.6f;
        ball.Kick(vel, this);
    }
}
