using UnityEngine;

/// <summary>Futbolcuyu arkadan-yukarıdan, rakip kaleye bakacak şekilde izler.</summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 10f, -11f);
    public float lookAhead = 4f;
    public float smoothing = 5f;

    public void Snap()
    {
        if (!target) return;
        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.forward * lookAhead + Vector3.up);
    }

    void LateUpdate()
    {
        if (!target) return;
        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target.position + offset, k);
        transform.LookAt(target.position + Vector3.forward * lookAhead + Vector3.up);
    }
}
