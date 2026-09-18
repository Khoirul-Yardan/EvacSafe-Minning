using UnityEngine;

namespace SafeMining
{
    public class MiningRockfall : MonoBehaviour
    {
        Transform[] stones;
        Vector3[] restingPositions;
        float elapsed;
        void Awake()
        {
            stones = new Transform[transform.childCount]; restingPositions = new Vector3[stones.Length];
            for (int i = 0; i < stones.Length; i++) { stones[i] = transform.GetChild(i); restingPositions[i] = stones[i].localPosition; }
        }
        void OnEnable() { elapsed = 0; }
        void Update()
        {
            elapsed += Mathf.Min(Time.deltaTime, .05f);
            for (int i = 0; i < stones.Length; i++)
            {
                float t = Mathf.Clamp01((elapsed - i % 5 * .08f) / .85f);
                stones[i].localPosition = restingPositions[i] + Vector3.up * (2.7f * (1 - t * t));
            }
            // Keep the closed zone collider in place after the visual collapse settles.
        }
    }
}
