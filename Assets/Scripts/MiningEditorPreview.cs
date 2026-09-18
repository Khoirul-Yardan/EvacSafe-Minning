using UnityEngine;

namespace SafeMining
{
    // Saved documentation geometry is EditorOnly and never participates in gameplay.
    [DefaultExecutionOrder(-10000)]
    public class MiningEditorPreview : MonoBehaviour
    {
        public GameObject ceiling;
        public GameObject worker;
        public GameObject[] landslides;
        public Camera overviewCamera;
        public Camera storyCamera;
        public Camera fppCamera;
        public bool showLabels = true;

        void Awake()
        {
            if (Application.isPlaying) gameObject.SetActive(false);
        }
    }
}
