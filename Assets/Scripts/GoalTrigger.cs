using UnityEngine;

/// <summary>
/// Kale ağzının arkasındaki tetik kutusu. Top çizgiyi tamamen geçince gol sayılır.
/// Ceza sahası bilgisini de taşır (kaleci elle tutabilir mi?).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("Bu kaleyi savunan oyuncu (0 = Mavi, 1 = Kırmızı)")]
    public int defenderIndex;
    [Tooltip("+1: +z ucundaki kale, -1: -z ucundaki kale")]
    public int side = 1;
    public float lineZ;
    public float goalHalfWidth;
    public float goalHeight;
    public float boxHalfWidth;
    public float boxDepth;

    [Tooltip("Şutta otomatik nişan için kale ağzının ortası")]
    public Vector3 aimPoint;

    public Vector3 AimPoint => aimPoint;

    /// <summary>Nokta bu kalenin ceza sahasında mı (kale içi dahil)?</summary>
    public bool InPenaltyArea(Vector3 p)
    {
        float inward = (lineZ - p.z) * side;
        return inward >= -0.8f && inward <= boxDepth && Mathf.Abs(p.x) <= boxHalfWidth;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Ball>() == null) return;
        if (MatchManager.Instance) MatchManager.Instance.GoalScored(this);
    }
}
