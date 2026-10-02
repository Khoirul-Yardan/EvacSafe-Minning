using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using S = SafeMining.MiningUiStyle;

namespace SafeMining
{
    // Owns the UI canvas and switches between the menu, in-session HUD and pause/result panel.
    // Each screen lives in its own class; this component only routes state and input focus.
    public class MiningHUD : MonoBehaviour
    {
        public MiningSimulation Simulation;
        MiningMenuPanel menuPanel;
        MiningHudView hudView;
        MiningResultPanel resultPanel;
        MiningLearningPanel learningPanel;
        bool wasMenu, wasModalVisible, wasLearningVisible;

        void Start()
        {
            var go = new GameObject("UI Layer | SAFE-MINING HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.transform; canvas.SetParent(transform, false); go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            hudView = new MiningHudView(canvas, Simulation, gameObject.AddComponent<MiningLandslideCutscene>());
            learningPanel = new MiningLearningPanel(canvas, Simulation, () => { hudView.ResetDetail(); resultPanel.ClearComparison(); });
            menuPanel = new MiningMenuPanel(canvas, Simulation, learningPanel.ShowPre);
            resultPanel = new MiningResultPanel(canvas, Simulation, () => hudView.ResetDetail(), learningPanel.ShowPost);
            learningPanel.Root.transform.SetAsLastSibling();
            Select(menuPanel.StartButton);
        }

        internal static string ScenarioName(HazardScenarioMode scenario)
        {
            return scenario == HazardScenarioMode.Scripted ? "Terkontrol" : scenario == HazardScenarioMode.NoHazards ? "Tanpa bahaya" : "Acak";
        }

        internal static string SourceName(HazardSource source)
        {
            return source == HazardSource.MqttEdgeSimulation ? "MQTT edge"
                : source == HazardSource.LocalEdgeSimulation ? "Edge lokal" : "Jadwal pembanding";
        }

        void Update()
        {
            if (menuPanel == null) return;
            var s = Simulation;
            bool isMenu = s.State == SessionState.Menu;
            bool showResult = s.State == SessionState.Paused || s.State == SessionState.Success || s.State == SessionState.Blocked;
            bool learning = learningPanel.Visible;
            menuPanel.Root.SetActive(isMenu && !learning);
            hudView.Root.SetActive(!isMenu && !learning);
            resultPanel.Refresh(showResult && !learning);

            if (isMenu && !wasMenu) { hudView.ResetDetail(); menuPanel.Open(); Select(menuPanel.StartButton); }
            else if (showResult && !wasModalVisible && !learning) Select(resultPanel.PrimaryButton);
            else if (!isMenu && !showResult && (wasModalVisible || wasMenu)) Select(null);
            if (learning && !wasLearningVisible) Select(learningPanel.FirstAnswer);
            wasMenu = isMenu; wasModalVisible = showResult && !learning; wasLearningVisible = learning;

            if (isMenu) { menuPanel.FitToScreen(); return; }
            if (!learning) hudView.Refresh();
        }

        static void Select(Button button)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(button != null ? button.gameObject : null);
        }

        void OnDestroy() => hudView?.Unsubscribe();
    }

    // Live minimap. Colours follow MiningUiStyle so the map matches the HUD legend.
    [RequireComponent(typeof(CanvasRenderer))]
    public class MineMapGraphic : MaskableGraphic
    {
        public MiningSimulation Simulation;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Simulation == null || Simulation.Cells == null) return;
            Rect r = rectTransform.rect;
            if (Simulation.Cells.Count == 0) return;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var cell in Simulation.Cells)
            {
                minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y); maxY = Mathf.Max(maxY, cell.y);
            }
            var center = new Vector2((minX + maxX) * .5f, (minY + maxY) * .5f);
            float scale = Mathf.Min(r.width / (maxX - minX + 3), r.height / (maxY - minY + 3));
            Vector2 Origin(Vector2Int p) => r.center + ((Vector2)p - center) * scale;
            foreach (var cell in Simulation.Cells) Rect(vh, Origin(cell), new Vector2(scale * .93f, scale * .93f), S.Line);
            if (!Simulation.StoryHolding && !Simulation.NavigationWaiting && (Simulation.GlassesEnabled || Simulation.Mode == MiningMode.Story))
                foreach (var cell in Simulation.Route) Rect(vh, Origin(cell), Vector2.one * (scale * .45f), S.Safe);
            foreach (var site in Simulation.HazardSites) Rect(vh, Origin(site), Vector2.one * scale * .3f, S.TextSecondary);
            for (int i = 0; i < Simulation.HazardSites.Count; i++)
                if (Simulation.HazardLevels[i] > 0) Rect(vh, Origin(Simulation.HazardSites[i]), Vector2.one * scale * .9f, Simulation.HazardLevels[i] == 2 ? S.Danger : S.Warning);
            foreach (var exit in MineLayout.Exits) Rect(vh, Origin(exit), Vector2.one * scale * .85f, S.Safe);
            if (Simulation.Actor == null) return;
            Vector3 p = Simulation.Actor.position;
            Vector2 location = r.center + (new Vector2(p.x, p.z) / MineLayout.CellSize - center) * scale;
            float a = -Simulation.Actor.eulerAngles.y * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)); Vector2 right = new Vector2(forward.y, -forward.x);
            int index = vh.currentVertCount;
            vh.AddVert(location + forward * 8, S.TextPrimary, Vector2.zero); vh.AddVert(location - forward * 5 - right * 5, S.TextPrimary, Vector2.zero); vh.AddVert(location - forward * 5 + right * 5, S.TextPrimary, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
        }
        static void Rect(VertexHelper vh, Vector2 center, Vector2 size, Color color)
        {
            int i = vh.currentVertCount; Vector2 half = size / 2;
            vh.AddVert(center + new Vector2(-half.x, -half.y), color, Vector2.zero); vh.AddVert(center + new Vector2(-half.x, half.y), color, Vector2.zero);
            vh.AddVert(center + new Vector2(half.x, half.y), color, Vector2.zero); vh.AddVert(center + new Vector2(half.x, -half.y), color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
