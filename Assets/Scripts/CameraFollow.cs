using UnityEngine;

/// <summary>
/// İki mod:
///  - Side: sahayı yandan (televizyon açısı) görür, takip edilen nesneyi z ekseninde izler.
///    Mavi sağa (+z), Kırmızı sola (-z) hücum eder.
///  - Fixed: sabit nokta (penaltı için atıcının arkası).
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public enum Mode { Side, Fixed }

    public Mode mode = Mode.Side;
    public Transform target;
    public float sideX = 23f;
    public float height = 15f;
    public float zLimit = 9f;
    public float smoothing = 4f;

    Vector3 fixedPos, fixedLook;

    public void SetSide(Transform follow, bool snap)
    {
        mode = Mode.Side;
        target = follow;
        if (snap) Apply(1f);
    }

    public void SetFixed(Vector3 position, Vector3 lookAt, bool snap)
    {
        mode = Mode.Fixed;
        fixedPos = position;
        fixedLook = lookAt;
        if (snap) Apply(1f);
    }

    void LateUpdate()
    {
        Apply(1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }

    void Apply(float k)
    {
        Vector3 pos, look;
        if (mode == Mode.Fixed)
        {
            pos = fixedPos;
            look = fixedLook;
        }
        else
        {
            float z = target ? Mathf.Clamp(target.position.z, -zLimit, zLimit) : 0f;
            pos = new Vector3(sideX, height, z);
            look = new Vector3(-1.5f, 0f, z);
        }
        transform.position = Vector3.Lerp(transform.position, pos, k);
        Quaternion want = Quaternion.LookRotation(look - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, k);
    }
}
