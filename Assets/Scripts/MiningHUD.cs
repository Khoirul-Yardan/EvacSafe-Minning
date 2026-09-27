using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;

namespace SafeMining
{
    public class MiningHUD : MonoBehaviour
    {
        sealed class SessionComparison
        {
            public bool adaptive;
            public SessionState outcome;
            public float elapsed, exposure, maxResponseMs;
            public int contacts, reroutes, seed;
            public MiningMode mode;
            public HazardScenarioMode scenario;
            public HazardSource source;
            public bool dataLoss, hadDisconnect, dataStale;
            public SessionComparison(MiningSimulation simulation)
            {
                adaptive = simulation.Adaptive; outcome = simulation.State; elapsed = simulation.Elapsed;
                exposure = simulation.Exposure; maxResponseMs = simulation.MaxResponseMs;
                contacts = simulation.HazardContacts; reroutes = simulation.Reroutes; seed = simulation.ActiveSeed;
                mode = simulation.Mode; scenario = simulation.ActiveScenario; source = simulation.ActiveHazardSource;
                dataLoss = simulation.EdgeSession != null && simulation.EdgeSession.DataLoss;
                hadDisconnect = simulation.EdgeSession != null && simulation.EdgeSession.HadDisconnect;
                dataStale = simulation.EdgeSession != null && simulation.EdgeSession.DataStale;
            }
        }
        public MiningSimulation Simulation;
        readonly Color safetyAmber = new Color(.80f, .72f, .51f);
        readonly Color safeGreen = new Color(.49f, .79f, .63f);
        readonly Color muted = new Color(.68f, .71f, .70f);
        readonly Color panel = new Color(.045f, .052f, .055f, .89f);
        Font font;
        Transform canvas, menu, hud, modal, glasses;
        Text phase, mode, dialogue, direction, distance, metrics, hazard, timer, modalTitle, modalBody, exportStatus, scenarioLabel, detectorStatus, edgeFlow;
        Text controlHints, equipmentStatus, sourceBadge, sourceHint, menuStartLabel, modalPrimaryLabel, modalSecondaryLabel, modalExportLabel, modalCompareLabel;
        bool adaptive = true;
        bool showDiagnostics;
        bool wasMenu;
        bool wasModalVisible;
        SessionComparison comparison;
        MiningMode selectedMode = MiningMode.Story;
        HazardScenarioMode selectedScenario = HazardScenarioMode.Random;
        HazardSource selectedHazardSource = HazardSource.MqttEdgeSimulation;
        Button storyModeButton, fppModeButton, adaptiveButton, staticButton, randomScenarioButton, scriptedScenarioButton, noHazardsButton;
        Button localEdgeButton, mqttButton, legacyButton, startButton, diagnosticsButton;
        Button modalPrimaryButton, modalSecondaryButton, modalExportButton, modalCompareButton;
        RectTransform storyCard, fppCard, infoCard, diagnosticsPanel;
        GameObject cameraSettingsRoot;
        GameObject comparisonPanel;
        Text comparisonMeta;
        readonly Text[] comparisonAdaptiveValues = new Text[6];
        readonly Text[] comparisonStaticValues = new Text[6];
        Text storySelectionLabel, fppSelectionLabel;
        Image dangerWash;
        MineMapGraphic map;
        MiningEdgeSession observedEdgeSession;
        EdgeStatusMessage latestVibration;
        EdgeStatusMessage latestAppliedHazard;
        float warningThreshold, dangerThreshold;
        readonly Dictionary<string, EdgeStatusMessage> vibrationByDevice = new Dictionary<string, EdgeStatusMessage>();

        void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("UI Layer | SAFE-MINING HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.transform; canvas.SetParent(transform, false); go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            hud = Panel(canvas, "In-session HUD", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            BuildHUD(); BuildMenu(); BuildModal();
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null) eventSystem.SetSelectedGameObject(startButton.gameObject);
        }

        RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return r;
        }
        RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var card = Panel(parent, name, anchor, anchor, pos, size, panel);
            var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.38f, .40f, .38f, .5f); outline.effectDistance = new Vector2(1, -1); return card;
        }
        Text Label(Transform parent, string name, string value, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.text = value; text.font = font; text.fontSize = fontSize; text.color = color;
            text.alignment = alignment; text.supportRichText = true; text.raycastTarget = false; return text;
        }
        Button Button(Transform parent, string value, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var baseColor = primary ? new Color(.24f, .40f, .32f) : new Color(.14f, .17f, .18f);
            var r = Panel(parent, value, new Vector2(.5f, .5f), new Vector2(.5f, .5f), pos, size, baseColor);
            r.GetComponent<Image>().raycastTarget = true; var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = r.GetComponent<Image>(); button.onClick.AddListener(action);
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var colors = button.colors; colors.normalColor = baseColor; colors.highlightedColor = Color.Lerp(baseColor, Color.white, .16f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, .22f); colors.selectedColor = Color.Lerp(baseColor, Color.white, .08f); colors.disabledColor = new Color(.16f, .17f, .16f, .7f);
            colors.fadeDuration = .12f; button.colors = colors;
            Label(r, "Label", value, Vector2.zero, size - new Vector2(14, 8), 19, Color.white, TextAnchor.MiddleCenter);
            return button;
        }
        Button AnchoredButton(Transform parent, string value, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var baseColor = primary ? new Color(.24f, .40f, .32f) : new Color(.14f, .17f, .18f);
            var r = Panel(parent, value, anchor, anchor, pos, size, baseColor);
            r.GetComponent<Image>().raycastTarget = true;
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = r.GetComponent<Image>(); button.onClick.AddListener(action);
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var colors = button.colors; colors.normalColor = baseColor; colors.highlightedColor = Color.Lerp(baseColor, Color.white, .16f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, .22f); colors.selectedColor = Color.Lerp(baseColor, Color.white, .08f); colors.disabledColor = new Color(.16f, .17f, .16f, .7f);
            colors.fadeDuration = .12f; button.colors = colors;
            Label(r, "Label", value, Vector2.zero, size - new Vector2(14, 8), 17, Color.white, TextAnchor.MiddleCenter);
            return button;
        }
        Button CardButton(RectTransform card, UnityEngine.Events.UnityAction action)
        {
            var image = card.GetComponent<Image>(); image.raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            return button;
        }
        void StyleChoice(Button button, bool selected, bool isCard = false)
        {
            if (button == null) return;
            var baseColor = selected ? new Color(.16f, .20f, .16f, .98f) : new Color(.075f, .09f, .09f, .96f);
            var image = button.targetGraphic as Image;
            if (image != null) image.color = Color.white;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors; colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, selected ? .13f : .22f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, .22f);
            colors.selectedColor = Color.Lerp(baseColor, Color.white, .08f); colors.disabledColor = new Color(.11f, .12f, .11f, .72f);
            colors.fadeDuration = .12f; button.colors = colors;
            var outline = button.GetComponent<Outline>();
            if (outline == null) outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = selected ? safetyAmber : new Color(.38f, .40f, .38f, .36f);
            outline.effectDistance = selected ? new Vector2(1.4f, -1.4f) : new Vector2(1f, -1f);
            if (isCard) outline.useGraphicAlpha = false;
        }
        void BuildMenu()
        {
            menu = Panel(canvas, "Simulation modes", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.02f, .025f, .028f, .86f));
            Label(menu, "Title", "SAFE-MINING <color=#BDCAA9>EVAC</color>", new Vector2(0, 300), new Vector2(1100, 72), 54, Color.white, TextAnchor.MiddleCenter);
            Label(menu, "Subtitle", "Pilih cara latihan dan kondisi evakuasi.", new Vector2(0, 232), new Vector2(1100, 36), 23, muted, TextAnchor.MiddleCenter);

            storyCard = Card(menu, "Story mode", new Vector2(.5f, .5f), new Vector2(-282, 25), new Vector2(536, 238));
            storyModeButton = CardButton(storyCard, () => { selectedMode = MiningMode.Story; RefreshMenuChoices(); });
            Label(storyCard, "Mode type", "EVAKUASI OTOMATIS", new Vector2(0, 79), new Vector2(466, 26), 15, safetyAmber);
            Label(storyCard, "Heading", "Mode Cerita", new Vector2(0, 35), new Vector2(466, 54), 35, Color.white);
            Label(storyCard, "Body", "Amati pekerja, detektor, peringatan, dan perubahan rute. Cocok untuk memahami skenario.", new Vector2(0, -31), new Vector2(466, 70), 19, muted);
            storySelectionLabel = Label(storyCard, "Selection", "DIPILIH", new Vector2(0, -88), new Vector2(466, 24), 14, safetyAmber, TextAnchor.MiddleCenter);

            fppCard = Card(menu, "First person mode", new Vector2(.5f, .5f), new Vector2(282, 25), new Vector2(536, 238));
            fppModeButton = CardButton(fppCard, () => { selectedMode = MiningMode.FirstPerson; RefreshMenuChoices(); });
            Label(fppCard, "Mode type", "LATIHAN MANUAL", new Vector2(0, 79), new Vector2(466, 26), 15, safetyAmber);
            Label(fppCard, "Heading", "Mode FPP", new Vector2(0, 35), new Vector2(466, 54), 35, Color.white);
            Label(fppCard, "Body", "Kendalikan pekerja dengan WASD dan mouse. Shift untuk lari; F untuk lampu; G untuk kacamata.", new Vector2(0, -31), new Vector2(466, 70), 19, muted);
            fppSelectionLabel = Label(fppCard, "Selection", "PILIH MODE", new Vector2(0, -88), new Vector2(466, 24), 14, muted, TextAnchor.MiddleCenter);

            Label(menu, "Setup heading", "PENGATURAN LATIHAN", new Vector2(0, -115), new Vector2(1100, 26), 16, safetyAmber, TextAnchor.MiddleCenter);
            Label(menu, "Navigation label", "NAVIGASI", new Vector2(-365, -145), new Vector2(390, 22), 14, muted, TextAnchor.MiddleCenter);
            adaptiveButton = Button(menu, "Adaptif", new Vector2(-465, -178), new Vector2(188, 42), () => { adaptive = true; RefreshMenuChoices(); });
            staticButton = Button(menu, "Statis", new Vector2(-265, -178), new Vector2(188, 42), () => { adaptive = false; RefreshMenuChoices(); });
            Label(menu, "Scenario label", "SKENARIO", new Vector2(224, -145), new Vector2(510, 22), 14, muted, TextAnchor.MiddleCenter);
            randomScenarioButton = Button(menu, "Acak", new Vector2(45, -178), new Vector2(160, 42), () => SelectScenario(HazardScenarioMode.Random));
            scriptedScenarioButton = Button(menu, "Terkontrol", new Vector2(225, -178), new Vector2(160, 42), () => SelectScenario(HazardScenarioMode.Scripted));
            noHazardsButton = Button(menu, "Tanpa bahaya", new Vector2(405, -178), new Vector2(190, 42), () => SelectScenario(HazardScenarioMode.NoHazards));

            Label(menu, "Source label", "SUMBER DETEKSI SIMULASI", new Vector2(0, -222), new Vector2(900, 22), 14, muted, TextAnchor.MiddleCenter);
            localEdgeButton = Button(menu, "Edge lokal", new Vector2(-300, -253), new Vector2(270, 40), () => SelectHazardSource(HazardSource.LocalEdgeSimulation));
            mqttButton = Button(menu, "MQTT edge", new Vector2(0, -253), new Vector2(270, 40), () => SelectHazardSource(HazardSource.MqttEdgeSimulation));
            legacyButton = Button(menu, "Jadwal pembanding", new Vector2(300, -253), new Vector2(270, 40), () => SelectHazardSource(HazardSource.LegacyTimeline));
            sourceHint = Label(menu, "Source guidance", "", new Vector2(0, -282), new Vector2(1160, 22), 14, muted, TextAnchor.MiddleCenter);

            scenarioLabel = Label(menu, "Scenario seed", "", new Vector2(-80, -316), new Vector2(800, 28), 16, safetyAmber, TextAnchor.MiddleCenter);
            Button(menu, "Seed acak baru", new Vector2(460, -316), new Vector2(190, 40), () => { Simulation.NewRandomScenario(); RefreshMenuChoices(); });
            startButton = Button(menu, "Mulai simulasi", new Vector2(0, -370), new Vector2(600, 54), () => Simulation.Begin(selectedMode, adaptive), true);
            menuStartLabel = startButton.GetComponentInChildren<Text>();
            Label(menu, "Controls and data note", "FPP: WASD · mouse · Shift · F lampu · G kacamata · Esc jeda     /     Semua data dan sensor bersifat virtual", new Vector2(0, -424), new Vector2(1420, 28), 15, muted, TextAnchor.MiddleCenter);
            RefreshMenuChoices();
        }
        void SelectScenario(HazardScenarioMode scenario)
        {
            selectedScenario = Simulation.scenarioMode = scenario;
            RefreshMenuChoices();
        }
        void SelectHazardSource(HazardSource source)
        {
            selectedHazardSource = Simulation.hazardSource = source;
            RefreshMenuChoices();
        }
        void RefreshMenuChoices()
        {
            if (Simulation == null || menuStartLabel == null) return;
            StyleChoice(storyModeButton, selectedMode == MiningMode.Story, true);
            StyleChoice(fppModeButton, selectedMode == MiningMode.FirstPerson, true);
            storySelectionLabel.text = selectedMode == MiningMode.Story ? "DIPILIH" : "PILIH MODE";
            storySelectionLabel.color = selectedMode == MiningMode.Story ? safetyAmber : muted;
            fppSelectionLabel.text = selectedMode == MiningMode.FirstPerson ? "DIPILIH" : "PILIH MODE";
            fppSelectionLabel.color = selectedMode == MiningMode.FirstPerson ? safetyAmber : muted;
            StyleChoice(adaptiveButton, adaptive);
            StyleChoice(staticButton, !adaptive);
            StyleChoice(randomScenarioButton, Simulation.scenarioMode == HazardScenarioMode.Random);
            StyleChoice(scriptedScenarioButton, Simulation.scenarioMode == HazardScenarioMode.Scripted);
            StyleChoice(noHazardsButton, Simulation.scenarioMode == HazardScenarioMode.NoHazards);
            StyleChoice(localEdgeButton, Simulation.hazardSource == HazardSource.LocalEdgeSimulation);
            StyleChoice(mqttButton, Simulation.hazardSource == HazardSource.MqttEdgeSimulation);
            StyleChoice(legacyButton, Simulation.hazardSource == HazardSource.LegacyTimeline);
            menuStartLabel.text = selectedMode == MiningMode.Story ? "Mulai Mode Cerita" : "Mulai Mode FPP";
            scenarioLabel.text = "Skenario " + ScenarioName(Simulation.scenarioMode) + "   ·   Seed " + Simulation.scenarioSeed + "   ·   R mengulang seed ini";
            sourceHint.text = Simulation.hazardSource == HazardSource.MqttEdgeSimulation
                ? "Broker MQTT harus aktif. Jika belum tersedia, pilih Edge Lokal."
                : Simulation.hazardSource == HazardSource.LocalEdgeSimulation
                    ? "Edge Lokal berjalan tanpa broker MQTT. Sensor tetap dimodelkan secara virtual."
                    : "Jadwal pembanding lama; jalur ini tidak memakai sampel sensor virtual.";
        }
        static string ScenarioName(HazardScenarioMode scenario)
        {
            return scenario == HazardScenarioMode.Scripted ? "Terkontrol" : scenario == HazardScenarioMode.NoHazards ? "Tanpa bahaya" : "Acak";
        }
        static string SourceName(HazardSource source)
        {
            return source == HazardSource.MqttEdgeSimulation ? "MQTT edge"
                : source == HazardSource.LocalEdgeSimulation ? "Edge lokal" : "Jadwal pembanding";
        }
        void ApplyHudPresentation(MiningSimulation simulation)
        {
            bool detailed = simulation.Mode == MiningMode.Story || showDiagnostics;
            diagnosticsPanel.gameObject.SetActive(detailed);
            diagnosticsButton.gameObject.SetActive(simulation.Mode == MiningMode.FirstPerson);
            diagnosticsButton.GetComponentInChildren<Text>().text = showDiagnostics ? "Tutup detail sistem" : "Detail sistem";
            metrics.gameObject.SetActive(detailed);
            infoCard.sizeDelta = new Vector2(246, detailed ? 124 : 82);
        }
        void BuildHUD()
        {
            var banner = Card(hud, "Mission", new Vector2(0, 1), new Vector2(298, -79), new Vector2(540, 110));
            mode = Label(banner, "Mode", "", new Vector2(-24, 29), new Vector2(420, 24), 15, safetyAmber);
            phase = Label(banner, "Phase", "", new Vector2(-24, -3), new Vector2(420, 36), 22, Color.white);
            sourceBadge = Label(banner, "Virtual source status", "", new Vector2(-24, -35), new Vector2(440, 18), 13, muted);
            Button(banner, "Jeda · Esc", new Vector2(216, 29), new Vector2(102, 36), () => Simulation.TogglePause());

            diagnosticsPanel = Panel(hud, "Detailed system diagnostics", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            var flow = Card(diagnosticsPanel, "Sensor edge broker route", new Vector2(.5f, 1), new Vector2(96, -79), new Vector2(620, 110));
            Label(flow, "Pipeline heading", "ALUR DETEKSI · SENSOR VIRTUAL → RUTE", new Vector2(-95, 31), new Vector2(390, 25), 15, safetyAmber);
            edgeFlow = Label(flow, "Pipeline status", "Menunggu sesi simulasi", new Vector2(0, -13), new Vector2(588, 60), 16, Color.white);
            var radar = Card(hud, "Map", new Vector2(1, 1), new Vector2(-151, -200), new Vector2(246, 352));
            Label(radar, "Map label", "PETA EVAKUASI  /  N", new Vector2(0, 146), new Vector2(214, 28), 17, safetyAmber);
            var mapObject = new GameObject("Live mine topology", typeof(RectTransform), typeof(CanvasRenderer), typeof(MineMapGraphic)); mapObject.transform.SetParent(radar, false);
            var rect = mapObject.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(208, 250); rect.anchoredPosition = new Vector2(0, -1);
            map = mapObject.GetComponent<MineMapGraphic>(); map.Simulation = Simulation; map.raycastTarget = false;
            Label(radar, "Legend", "<color=#43F0B4>Aman</color>   <color=#FF6559>Bahaya</color>   <color=#FFFFFF>Anda</color>", new Vector2(0, -145), new Vector2(224, 27), 15, muted, TextAnchor.MiddleCenter);
            infoCard = Card(hud, "Elapsed time and metrics", new Vector2(1, 1), new Vector2(-151, -456), new Vector2(246, 124));
            timer = Label(infoCard, "Timer", "", new Vector2(0, 30), new Vector2(212, 37), 28, Color.white);
            metrics = Label(infoCard, "Metrics", "", new Vector2(0, -20), new Vector2(212, 57), 16, muted);
            var bottom = Card(hud, "Team dialogue", new Vector2(0, 0), new Vector2(342, 94), new Vector2(628, 132));
            Label(bottom, "Speaker", "RADIO TIM  /  PUSAT KENDALI", new Vector2(0, 40), new Vector2(582, 25), 16, safetyAmber);
            dialogue = Label(bottom, "Dialogue", "", new Vector2(0, -15), new Vector2(582, 82), 20, Color.white);
            var cutscene = Card(hud, "Landslide cutscene", new Vector2(0, 0), new Vector2(218, 304), new Vector2(380, 264));
            var cutsceneTitle = Label(cutscene, "Camera location", "", new Vector2(0, 111), new Vector2(352, 24), 16, new Color(1, .62f, .44f));
            var cutsceneStatus = Label(cutscene, "Camera queue", "", new Vector2(0, -114), new Vector2(352, 22), 14, muted);
            var feed = new GameObject("Live landslide view", typeof(RectTransform), typeof(RawImage));
            feed.transform.SetParent(cutscene, false);
            var feedRect = feed.GetComponent<RectTransform>(); feedRect.sizeDelta = new Vector2(352, 198);
            feed.GetComponent<RawImage>().raycastTarget = false;
            gameObject.AddComponent<MiningLandslideCutscene>().Initialize(Simulation, cutscene.gameObject,
                feed.GetComponent<RawImage>(), cutsceneTitle, cutsceneStatus);
            var guide = Card(hud, "AR direction", new Vector2(.5f, 0), new Vector2(190, 96), new Vector2(360, 136));
            Label(guide, "AR label", "PETUNJUK KE ZONA AMAN", new Vector2(0, 40), new Vector2(320, 25), 15, safetyAmber, TextAnchor.MiddleCenter);
            direction = Label(guide, "Direction", "", new Vector2(0, 0), new Vector2(330, 43), 27, safeGreen, TextAnchor.MiddleCenter);
            distance = Label(guide, "Distance", "", new Vector2(0, -41), new Vector2(330, 26), 17, muted, TextAnchor.MiddleCenter);
            var sensors = Card(diagnosticsPanel, "Detector network", new Vector2(0, 1), new Vector2(298, -191), new Vector2(540, 90));
            detectorStatus = Label(sensors, "Sensor status", "", Vector2.zero, new Vector2(500, 74), 18, safetyAmber);
            hazard = Label(hud, "Priority alert", "", new Vector2(0, 255), new Vector2(960, 78), 21, new Color(.91f, .51f, .34f), TextAnchor.MiddleCenter);
            controlHints = Label(hud, "Control hints", "", new Vector2(0, -426), new Vector2(1400, 26), 15, muted, TextAnchor.MiddleCenter);
            equipmentStatus = Label(hud, "FPP equipment", "", new Vector2(96, 296), new Vector2(620, 26), 15, muted, TextAnchor.MiddleCenter);
            diagnosticsButton = AnchoredButton(hud, "Detail sistem", new Vector2(.5f, 1), new Vector2(325, -48), new Vector2(220, 26), () =>
            { showDiagnostics = !showDiagnostics; ApplyHudPresentation(Simulation); });
            glasses = Panel(hud, "Safety glasses rim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 })
            {
                Vector2 anchor = new Vector2(sx < 0 ? 0 : 1, sy < 0 ? 0 : 1);
                Panel(glasses, "Lens horizontal rim", anchor, anchor, new Vector2(-sx * 76, -sy * 20), new Vector2(116, 3), new Color(safetyAmber.r, safetyAmber.g, safetyAmber.b, .65f));
                Panel(glasses, "Lens vertical rim", anchor, anchor, new Vector2(-sx * 20, -sy * 72), new Vector2(3, 105), new Color(safetyAmber.r, safetyAmber.g, safetyAmber.b, .65f));
            }
            Label(glasses, "Reticle", "·", Vector2.zero, new Vector2(24, 24), 24, new Color(.85f, .9f, .85f, .55f), TextAnchor.MiddleCenter);
            dangerWash = Panel(hud, "Danger proximity", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>();
            dangerWash.transform.SetAsFirstSibling();
        }
        void BuildModal()
        {
            modal = Panel(canvas, "Pause and result", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.01f, .025f, .04f, .9f));
            var card = Card(modal, "Result", new Vector2(.5f, .5f), new Vector2(0, 50), new Vector2(790, 640));
            modalTitle = Label(card, "Title", "", new Vector2(0, 238), new Vector2(710, 64), 36, safeGreen, TextAnchor.MiddleCenter);
            modalBody = Label(card, "Summary", "", new Vector2(0, 82), new Vector2(682, 218), 20, Color.white);
            modalPrimaryButton = Button(card, "Lanjutkan simulasi", new Vector2(-177, -55), new Vector2(326, 52), () =>
            { if (Simulation.State == SessionState.Paused) Simulation.TogglePause(); else StartSameSession(Simulation.Adaptive, false); }, true);
            modalPrimaryLabel = modalPrimaryButton.GetComponentInChildren<Text>();
            modalSecondaryButton = Button(card, "Kembali ke menu", new Vector2(177, -55), new Vector2(326, 52), ReturnToMenu);
            modalSecondaryLabel = modalSecondaryButton.GetComponentInChildren<Text>();
            modalCompareButton = Button(card, "Jalankan pembanding dengan seed sama", new Vector2(0, -123), new Vector2(682, 44), RunComparison);
            modalCompareLabel = modalCompareButton.GetComponentInChildren<Text>();
            modalExportButton = Button(card, "Ekspor hasil evaluasi", new Vector2(0, -177), new Vector2(682, 44), () => Simulation.Export());
            modalExportLabel = modalExportButton.GetComponentInChildren<Text>();
            exportStatus = Label(card, "Export location", "", new Vector2(0, -235), new Vector2(690, 50), 14, muted, TextAnchor.MiddleCenter);
            var comparison = Panel(card, "Same seed comparison", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 96), new Vector2(682, 240), Color.clear);
            comparisonPanel = comparison.gameObject;
            comparisonMeta = Label(comparison, "Comparison setup", "", new Vector2(0, 102), new Vector2(650, 20), 14, muted, TextAnchor.MiddleCenter);
            Label(comparison, "Adaptive column", "ADAPTIF", new Vector2(-5, 72), new Vector2(190, 22), 15, safetyAmber, TextAnchor.MiddleCenter);
            Label(comparison, "Static column", "STATIS", new Vector2(215, 72), new Vector2(180, 22), 15, muted, TextAnchor.MiddleCenter);
            string[] comparisonRows = { "Waktu evakuasi", "Paparan bahaya", "Kontak bahaya", "Perubahan rute", "Hasil", "Respons maks." };
            for (int i = 0; i < comparisonRows.Length; i++)
            {
                float y = 44 - i * 28;
                Label(comparison, "Metric " + i, comparisonRows[i], new Vector2(-215, y), new Vector2(190, 22), 16, muted);
                comparisonAdaptiveValues[i] = Label(comparison, "Adaptive value " + i, "—", new Vector2(-5, y), new Vector2(190, 22), 16, Color.white, TextAnchor.MiddleCenter);
                comparisonStaticValues[i] = Label(comparison, "Static value " + i, "—", new Vector2(215, y), new Vector2(180, 22), 16, Color.white, TextAnchor.MiddleCenter);
            }
            comparisonPanel.SetActive(false);
            var settings = Card(modal, "Camera settings", new Vector2(.5f, .5f), new Vector2(0, -354), new Vector2(790, 126));
            cameraSettingsRoot = settings.gameObject;
            Label(settings, "Settings label", "KENYAMANAN KAMERA FPP", new Vector2(0, 37), new Vector2(710, 26), 15, safetyAmber);
            Button(settings, "Mouse -", new Vector2(-292, -4), new Vector2(125, 38), () => Simulation.mouseSensitivity = Mathf.Max(.02f, Simulation.mouseSensitivity - .01f));
            Button(settings, "Mouse +", new Vector2(-153, -4), new Vector2(125, 38), () => Simulation.mouseSensitivity = Mathf.Min(.3f, Simulation.mouseSensitivity + .01f));
            Button(settings, "FOV -", new Vector2(18, -4), new Vector2(104, 38), () => Simulation.firstPersonFieldOfView = Mathf.Max(60, Simulation.firstPersonFieldOfView - 2));
            Button(settings, "FOV +", new Vector2(135, -4), new Vector2(104, 38), () => Simulation.firstPersonFieldOfView = Mathf.Min(95, Simulation.firstPersonFieldOfView + 2));
            Button(settings, "Ayunan", new Vector2(292, -4), new Vector2(125, 38), () => Simulation.headBobAmount = Simulation.headBobAmount > 0 ? 0 : .012f);
            cameraSettingsLabel = Label(settings, "Current camera settings", "", new Vector2(0, -42), new Vector2(710, 24), 15, muted, TextAnchor.MiddleCenter);
            cameraSettingsRoot.SetActive(false);
        }
        Text cameraSettingsLabel;
        void ReturnToMenu()
        {
            comparison = null;
            Simulation.Menu();
        }
        void RunComparison()
        {
            comparison = new SessionComparison(Simulation);
            StartSameSession(!Simulation.Adaptive, true);
        }
        void StartSameSession(bool useAdaptiveNavigation, bool keepComparison)
        {
            var s = Simulation;
            if (!keepComparison) comparison = null;
            s.scenarioSeed = s.ActiveSeed;
            s.scenarioMode = s.ActiveScenario;
            s.hazardSource = s.ActiveHazardSource;
            showDiagnostics = false;
            s.Begin(s.Mode, useAdaptiveNavigation);
        }
        string BuildSourceStatus(MiningSimulation simulation)
        {
            if (simulation.ActiveHazardSource == HazardSource.LegacyTimeline)
            {
                sourceBadge.color = muted;
                return "MODE PEMBANDING · JADWAL LEGACY";
            }
            if (simulation.ActiveHazardSource == HazardSource.LocalEdgeSimulation)
            {
                sourceBadge.color = safeGreen;
                return "DATA VIRTUAL · EDGE LOKAL";
            }
            var session = simulation.EdgeSession;
            if (session == null)
            {
                if (simulation.LastDetectorAlert.Contains("Konfigurasi edge gagal"))
                {
                    sourceBadge.color = new Color(.91f, .51f, .34f);
                    return "MQTT · konfigurasi edge gagal — periksa edge / broker";
                }
                sourceBadge.color = muted;
                return "DATA VIRTUAL · MQTT MENUNGGU SESI";
            }
            if (session.DataLoss || session.DataStale || session.TransportStatus.Contains("terputus"))
            {
                sourceBadge.color = new Color(.91f, .51f, .34f);
                return session.DataLoss
                    ? "MQTT · DATA HILANG — jeda, lalu pilih Edge Lokal"
                    : "MQTT · DATA BELUM MUTAKHIR — jeda, lalu pilih Edge Lokal";
            }
            sourceBadge.color = safeGreen;
            return "DATA VIRTUAL · " + session.TransportStatus.ToUpperInvariant();
        }
        string BuildPriorityAlert(MiningSimulation simulation, float nearestHazardDistance)
        {
            int closedIndex = -1, warningIndex = -1;
            float nearestClosed = float.PositiveInfinity, nearestWarning = float.PositiveInfinity;
            Vector3 actor = simulation.Actor.position;
            for (int i = 0; i < simulation.HazardLevels.Length; i++)
            {
                int level = simulation.HazardLevels[i];
                if (level < 1 || i >= simulation.HazardSites.Count) continue;
                Vector3 site = MineLayout.World(simulation.HazardSites[i]);
                float distance = Vector2.Distance(new Vector2(actor.x, actor.z), new Vector2(site.x, site.z));
                if (level == 2 && distance < nearestClosed) { nearestClosed = distance; closedIndex = i; }
                else if (level == 1 && distance < nearestWarning) { nearestWarning = distance; warningIndex = i; }
            }
            bool closed = closedIndex >= 0;
            int alertIndex = closed ? closedIndex : warningIndex;
            bool warning = !closed && warningIndex >= 0;
            if (alertIndex < 0 && nearestHazardDistance >= 13) return "";

            string headline = alertIndex >= 0
                ? "D" + (alertIndex + 1).ToString("00") + " · " + (closed ? "LORONG TERTUTUP" : "WASPADA") +
                    " · grid " + simulation.HazardSites[alertIndex].x + ":" + simulation.HazardSites[alertIndex].y
                : "BAHAYA DI DEKAT · " + nearestHazardDistance.ToString("F0") + " m";
            string route;
            if (simulation.TargetExit < 0)
                route = "Rute aman belum tersedia. Jeda simulasi dan tinjau peta.";
            else if (simulation.Adaptive)
                route = "Rute adaptif → Zona aman " + (simulation.TargetExit + 1) + " · " + simulation.RouteDistance.ToString("F0") + " m";
            else
                route = "Navigasi statis tetap pada rute awal. Periksa peta sebelum melanjutkan.";
            string color = closed ? "#D8755B" : warning ? "#E2BD69" : "#D8755B";
            return "<color=" + color + ">" + headline + "</color>\n" + route;
        }
        bool HasMatchingComparison(MiningSimulation simulation)
        {
            return comparison != null && comparison.adaptive != simulation.Adaptive &&
                comparison.seed == simulation.ActiveSeed && comparison.mode == simulation.Mode &&
                comparison.scenario == simulation.ActiveScenario && comparison.source == simulation.ActiveHazardSource;
        }
        static string OutcomeName(SessionState state)
        {
            return state == SessionState.Success ? "Berhasil" : state == SessionState.Blocked ? "Terhalang" : "Belum selesai";
        }
        static string DataQualityNote(bool dataLoss, bool stale, bool hadDisconnect)
        {
            if (dataLoss) return "DATA TIDAK LENGKAP";
            if (stale) return "DATA BELUM MUTAKHIR";
            if (hadDisconnect) return "MQTT SEMPAT TERPUTUS";
            return "";
        }
        void UpdateComparison(MiningSimulation simulation)
        {
            bool comparisonWasAdaptive = comparison.adaptive;
            float adaptiveTime = comparisonWasAdaptive ? comparison.elapsed : simulation.Elapsed;
            float staticTime = comparisonWasAdaptive ? simulation.Elapsed : comparison.elapsed;
            float adaptiveExposure = comparisonWasAdaptive ? comparison.exposure : simulation.Exposure;
            float staticExposure = comparisonWasAdaptive ? simulation.Exposure : comparison.exposure;
            int adaptiveContacts = comparisonWasAdaptive ? comparison.contacts : simulation.HazardContacts;
            int staticContacts = comparisonWasAdaptive ? simulation.HazardContacts : comparison.contacts;
            int adaptiveReroutes = comparisonWasAdaptive ? comparison.reroutes : simulation.Reroutes;
            int staticReroutes = comparisonWasAdaptive ? simulation.Reroutes : comparison.reroutes;
            SessionState adaptiveOutcome = comparisonWasAdaptive ? comparison.outcome : simulation.State;
            SessionState staticOutcome = comparisonWasAdaptive ? simulation.State : comparison.outcome;
            float adaptiveResponse = comparisonWasAdaptive ? comparison.maxResponseMs : simulation.MaxResponseMs;
            float staticResponse = comparisonWasAdaptive ? simulation.MaxResponseMs : comparison.maxResponseMs;
            string[] adaptiveValues = {
                adaptiveTime.ToString("F1") + " s", adaptiveExposure.ToString("F1") + " s", adaptiveContacts.ToString(),
                adaptiveReroutes.ToString(), OutcomeName(adaptiveOutcome), adaptiveResponse.ToString("F3") + " ms"
            };
            string[] staticValues = {
                staticTime.ToString("F1") + " s", staticExposure.ToString("F1") + " s", staticContacts.ToString(),
                staticReroutes.ToString(), OutcomeName(staticOutcome), staticResponse.ToString("F3") + " ms"
            };
            for (int i = 0; i < adaptiveValues.Length; i++)
            {
                comparisonAdaptiveValues[i].text = adaptiveValues[i];
                comparisonStaticValues[i].text = staticValues[i];
            }
            string firstQuality = DataQualityNote(comparison.dataLoss, comparison.dataStale, comparison.hadDisconnect);
            string secondQuality = DataQualityNote(simulation.EdgeSession != null && simulation.EdgeSession.DataLoss,
                simulation.EdgeSession != null && simulation.EdgeSession.DataStale,
                simulation.EdgeSession != null && simulation.EdgeSession.HadDisconnect);
            comparisonMeta.text = "Seed " + simulation.ActiveSeed + " · " + ScenarioName(simulation.ActiveScenario) + " · " +
                (simulation.Mode == MiningMode.Story ? "Cerita" : "FPP") + " · " + SourceName(simulation.ActiveHazardSource);
            if (!string.IsNullOrEmpty(firstQuality) || !string.IsNullOrEmpty(secondQuality))
            {
                comparisonMeta.text += " · " + (string.IsNullOrEmpty(firstQuality) ? "" : firstQuality) +
                    (!string.IsNullOrEmpty(firstQuality) && !string.IsNullOrEmpty(secondQuality) ? " / " : "") + secondQuality;
                comparisonMeta.color = new Color(.91f, .62f, .43f);
            }
            else comparisonMeta.color = muted;
        }
        void Update()
        {
            if (menu == null) return;
            var s = Simulation;
            ObserveEdgeSession(s.EdgeSession);
            bool isMenu = s.State == SessionState.Menu;
            menu.gameObject.SetActive(isMenu);
            hud.gameObject.SetActive(s.State != SessionState.Menu);
            bool showResult = s.State == SessionState.Paused || s.State == SessionState.Success || s.State == SessionState.Blocked;
            modal.gameObject.SetActive(showResult);
            if (isMenu && !wasMenu)
            {
                if (s.SessionRevision > 0)
                {
                    selectedMode = s.Mode; adaptive = s.Adaptive;
                    selectedScenario = s.ActiveScenario; selectedHazardSource = s.ActiveHazardSource;
                    s.scenarioMode = selectedScenario; s.hazardSource = selectedHazardSource;
                }
                else
                {
                    selectedScenario = s.scenarioMode; selectedHazardSource = s.hazardSource;
                }
                showDiagnostics = false;
                RefreshMenuChoices();
                var eventSystem = EventSystem.current;
                if (eventSystem != null) eventSystem.SetSelectedGameObject(startButton.gameObject);
            }
            if (showResult && !wasModalVisible)
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null) eventSystem.SetSelectedGameObject(modalPrimaryButton.gameObject);
            }
            else if (!showResult && wasModalVisible && !isMenu)
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            }
            else if (!isMenu && !showResult && wasMenu)
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            }
            wasMenu = isMenu;
            wasModalVisible = showResult;
            if (isMenu)
            {
                float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
                float menuScale = Mathf.Clamp(aspect / 1.67f, .78f, 1f);
                menu.localScale = Vector3.one * menuScale;
                return;
            }

            ApplyHudPresentation(s);
            mode.text = (s.Mode == MiningMode.Story ? "MODE CERITA" : "MODE FPP") + "  ·  " + (s.Adaptive ? "NAVIGASI ADAPTIF" : "NAVIGASI STATIS");
            sourceBadge.text = BuildSourceStatus(s);
            controlHints.text = s.Mode == MiningMode.Story ? "Esc  Jeda / lanjut     R  Ulangi skenario yang sama" : "WASD  Bergerak     Mouse  Lihat     Shift  Lari     F  Lampu helm     G  Kacamata     Esc  Jeda     R  Ulang";
            equipmentStatus.text = s.Mode == MiningMode.FirstPerson ? "LAMPU HELM " + (s.HeadlampEnabled ? "ON" : "OFF") + "   /   NAVIGASI AR " + (s.GlassesEnabled ? "ON" : "OFF") : "";
            detectorStatus.text = "JARINGAN DETEKTOR  /  " + s.Detectors.Length + " titik\n" + s.LastDetectorAlert;
            edgeFlow.text = BuildEdgeFlow(s);
            phase.text = s.Phase;
            dialogue.text = s.Dialogue;
            bool hasGlasses = s.Mode == MiningMode.Story || s.GlassesEnabled;
            direction.text = hasGlasses ? s.DirectionHint() : "KACAMATA NONAKTIF";
            distance.text = hasGlasses && s.TargetExit >= 0 ? "Zona aman " + (s.TargetExit + 1) + "  /  " + s.RouteDistance.ToString("F0") + " m sepanjang rute" : "Menunggu rute menuju zona aman";
            timer.text = s.Elapsed.ToString("F1") + " s";
            metrics.text = (s.Adaptive ? "ADAPTIF" : "STATIS / BASELINE") + "  |  " + s.Reroutes + " perubahan\nRespons hitung " + s.ResponseMs.ToString("F2") + " ms";
            float danger = s.NearestHazardDistance();
            hazard.text = BuildPriorityAlert(s, danger);
            dangerWash.color = new Color(1, .08f, .01f, Mathf.Clamp01((8 - danger) / 8) * .18f);
            glasses.gameObject.SetActive(s.Mode == MiningMode.FirstPerson && s.GlassesEnabled);
            map.SetVerticesDirty();
            if (showResult)
            {
                cameraSettingsLabel.text = "Mouse " + s.mouseSensitivity.ToString("F2") + "   /   FOV " + s.firstPersonFieldOfView.ToString("F0") + "°   /   Ayunan " + (s.headBobAmount > 0 ? "ON" : "OFF");
                bool paused = s.State == SessionState.Paused;
                bool success = s.State == SessionState.Success;
                bool terminal = !paused;
                bool showComparison = terminal && HasMatchingComparison(s);
                modalTitle.text = showComparison ? "PERBANDINGAN HASIL" : paused ? "SIMULASI DIJEDA" : success ? "EVAKUASI BERHASIL" : "EVAKUASI TERHALANG";
                modalTitle.color = showComparison ? safetyAmber : paused ? safetyAmber : success ? safeGreen : new Color(.91f, .51f, .34f);
                modalPrimaryLabel.text = paused ? "Lanjutkan simulasi" : "Ulangi skenario · seed sama";
                modalSecondaryLabel.text = paused ? "Akhiri & kembali ke menu" : "Kembali ke menu";
                modalExportLabel.text = paused ? "Ekspor sesi sementara" : "Ekspor hasil evaluasi";
                modalCompareLabel.text = s.Adaptive ? "Jalankan baseline statis · seed sama" : "Jalankan navigasi adaptif · seed sama";
                modalCompareButton.gameObject.SetActive(terminal);
                modalExportButton.gameObject.SetActive(true);
                modalExportButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, paused ? -145 : -177);
                exportStatus.rectTransform.anchoredPosition = new Vector2(0, paused ? -202 : -235);
                cameraSettingsRoot.SetActive(paused && s.Mode == MiningMode.FirstPerson);
                modalBody.gameObject.SetActive(!showComparison);
                comparisonPanel.SetActive(showComparison);
                string scenarioInfo = (s.Mode == MiningMode.Story ? "Mode Cerita" : "Mode FPP") + "  /  " +
                    (s.Adaptive ? "Navigasi adaptif" : "Baseline statis") + "  /  " + ScenarioName(s.ActiveScenario) + "  /  Seed " + s.ActiveSeed;
                string dataQuality = s.EdgeSession == null ? "" : s.EdgeSession.DataLoss ? "\n\nDATA TIDAK LENGKAP · periksa status edge sebelum membandingkan." :
                    s.EdgeSession.HadDisconnect ? "\n\nKoneksi MQTT sempat terputus · periksa log edge hasil ekspor." :
                    s.EdgeSession.DataStale ? "\n\nData edge belum mutakhir · hasil perlu ditinjau." : "";
                modalBody.text = (paused ? scenarioInfo + "\n\nSesi dijeda. Ekspor sekarang akan berstatus sementara.\n\n" : scenarioInfo + "\n\n") +
                    "Waktu simulasi       " + s.Elapsed.ToString("F1") + " detik" +
                    "\nJarak tempuh          " + s.Travelled.ToString("F1") + " m" +
                    "\nPaparan bahaya       " + s.Exposure.ToString("F1") + " detik / " + s.HazardContacts + " kontak" +
                    "\nPerubahan rute        " + s.Reroutes + " kali" +
                    "\nRespons hitung maks.  " + s.MaxResponseMs.ToString("F3") + " ms" + dataQuality;
                exportStatus.text = string.IsNullOrEmpty(s.ExportStatus)
                    ? paused ? "Ekspor sesi jeda bersifat sementara. Respons = komputasi lokal, bukan latensi jaringan."
                        : "Ekspor sesi ini sebelum pembanding untuk menyimpan data. Respons = komputasi lokal, bukan latensi jaringan."
                    : s.ExportStatus;
                if (showComparison) UpdateComparison(s);
            }
        }

        void ObserveEdgeSession(MiningEdgeSession session)
        {
            if (observedEdgeSession == session) return;
            if (observedEdgeSession != null)
            {
                observedEdgeSession.VibrationSampled -= OnVibrationSampled;
                observedEdgeSession.HazardApplied -= OnHazardApplied;
            }
            observedEdgeSession = session;
            latestVibration = null;
            vibrationByDevice.Clear();
            latestAppliedHazard = null;
            if (observedEdgeSession != null)
            {
                var settings = observedEdgeSession.Settings;
                warningThreshold = settings.warningThreshold;
                dangerThreshold = settings.dangerThreshold;
                observedEdgeSession.VibrationSampled += OnVibrationSampled;
                observedEdgeSession.HazardApplied += OnHazardApplied;
            }
        }

        void OnVibrationSampled(EdgeStatusMessage sample) { vibrationByDevice[sample.deviceId] = sample; }
        void OnHazardApplied(EdgeStatusMessage message) { latestAppliedHazard = message; }

        string BuildEdgeFlow(MiningSimulation simulation)
        {
            var session = simulation.EdgeSession;
            if (simulation.State != SessionState.Menu && simulation.ActiveHazardSource == HazardSource.LegacyTimeline)
            {
                edgeFlow.color = muted;
                return "JADWAL LEGACY > BAHAYA > RUTE\nSumber pembanding; tidak memakai sensor virtual atau MQTT.";
            }
            latestVibration = null;
            foreach (var sampled in vibrationByDevice.Values)
                if (latestVibration == null || sampled.vibrationNormalized > latestVibration.vibrationNormalized)
                    latestVibration = sampled;
            if (session == null || latestVibration == null)
            {
                edgeFlow.color = muted;
                return "Sensor virtual menunggu sesi dimulai.\nNilai getaran ditampilkan sebagai skala simulasi (0–1), bukan satuan sensor fisik.";
            }

            var sample = latestVibration;
            string reading = sample.deviceId + "  GETARAN SIM " + sample.vibrationNormalized.ToString("F2") +
                "  |  AMBANG " + warningThreshold.ToString("F2") + "/" + dangerThreshold.ToString("F2");
            int edgeLevel = sample.level < 0 ? latestAppliedHazard?.level ?? -1 : sample.level;
            string level = edgeLevel < 0 ? "MENUNGGU KEPUTUSAN" : LevelName(edgeLevel);
            string transport = session.Source == HazardSource.MqttEdgeSimulation
                ? session.TransportStatus
                : "EDGE LOKAL (tanpa MQTT)";
            string applied = latestAppliedHazard == null
                ? "menunggu status dari edge"
                : latestAppliedHazard.deviceId + " " + LevelName(latestAppliedHazard.level) + " diterapkan";
            string route = simulation.TargetExit >= 0 ? "EXIT " + (simulation.TargetExit + 1) : "mencari exit";
            edgeFlow.color = edgeLevel == 2 ? new Color(1f, .48f, .40f) : edgeLevel == 1 ? new Color(1f, .78f, .38f) : edgeLevel < 0 ? muted : Color.white;
            return reading + "\nEDGE " + level + "  >  " + transport + "  >  " + applied + "  >  RUTE " + route;
        }

        static string LevelName(int level)
        {
            return level == 2 ? "TERTUTUP" : level == 1 ? "WASPADA" : "NORMAL";
        }

        void OnDestroy()
        {
            if (observedEdgeSession == null) return;
            observedEdgeSession.VibrationSampled -= OnVibrationSampled;
            observedEdgeSession.HazardApplied -= OnHazardApplied;
        }
    }

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
            foreach (var cell in Simulation.Cells) Rect(vh, Origin(cell), new Vector2(scale * .93f, scale * .93f), new Color(.16f, .27f, .31f));
            if (Simulation.GlassesEnabled || Simulation.Mode == MiningMode.Story)
                foreach (var cell in Simulation.Route) Rect(vh, Origin(cell), Vector2.one * (scale * .45f), new Color(.05f, .85f, .58f));
            foreach (var site in Simulation.HazardSites) Rect(vh, Origin(site), Vector2.one * scale * .35f, new Color(.25f, .75f, 1));
            for (int i = 0; i < Simulation.HazardSites.Count; i++)
                if (Simulation.HazardLevels[i] > 0) Rect(vh, Origin(Simulation.HazardSites[i]), Vector2.one * scale * .9f, Simulation.HazardLevels[i] == 2 ? new Color(1, .18f, .12f) : new Color(1, .65f, .08f));
            foreach (var exit in MineLayout.Exits) Rect(vh, Origin(exit), Vector2.one * scale * .85f, new Color(.3f, 1, .67f));
            if (Simulation.Actor == null) return;
            Vector3 p = Simulation.Actor.position;
            Vector2 location = r.center + (new Vector2(p.x, p.z) / MineLayout.CellSize - center) * scale;
            float a = -Simulation.Actor.eulerAngles.y * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)); Vector2 right = new Vector2(forward.y, -forward.x);
            int index = vh.currentVertCount;
            vh.AddVert(location + forward * 7, Color.white, Vector2.zero); vh.AddVert(location - forward * 4 - right * 4, Color.white, Vector2.zero); vh.AddVert(location - forward * 4 + right * 4, Color.white, Vector2.zero);
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
