using UnityEngine;

/// <summary>
/// Kale ağzının arkasındaki tetik kutusu. Top çizgiyi tamamen geçince gol sayılır.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("Kendi kalemiz mi (buraya giren top kendi kalemize gol)")]
    public bool isOwnGoal;

    [Tooltip("Şutta otomatik nişan için kale ağzının ortası")]
    public Vector3 aimPoint;

    public Vector3 AimPoint => aimPoint;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Ball>() == null) return;
        if (MatchManager.Instance) MatchManager.Instance.GoalScored(this);
    }
}
