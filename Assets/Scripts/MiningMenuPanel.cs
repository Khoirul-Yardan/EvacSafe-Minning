using UnityEngine;
using UnityEngine.UI;
using static SafeMining.MiningUiKit;
using S = SafeMining.MiningUiStyle;
using I = SafeMining.MiningIcons;

namespace SafeMining
{
    // Start menu. Writes the chosen scenario and hazard source to MiningSimulation before Begin,
    // exactly like the previous menu; mode and navigation are passed to Begin.
    public sealed class MiningMenuPanel
    {
        sealed class ModeCard { public Button button; public Image border, fill; public Text icon, title, body; }

        public GameObject Root { get; }
        public Button StartButton => start.button;

        readonly MiningSimulation simulation;
        readonly System.Action<MiningMode, bool> begin;
        MiningMode mode = MiningMode.Story;
        bool adaptive = true;
        ModeCard storyCard, fppCard;
        Segmented navigation, scenario, source;
        Text navigationHelp, scenarioHelp, sourceHelp, sourceHelpIcon;
        ButtonView start;
        GameObject fppControls;
        readonly RectTransform content;

        public MiningMenuPanel(Transform canvas, MiningSimulation simulation, System.Action<MiningMode, bool> begin)
        {
            this.simulation = simulation; this.begin = begin;
            var backdrop = Box(canvas, "Simulation modes", S.Hex(0x0B0D0F, .97f));
            Stretch(backdrop.rectTransform); backdrop.raycastTarget = true;
            Root = backdrop.gameObject;

            var row = Row(backdrop.transform, "Menu layout", 56, TextAnchor.MiddleCenter);
            content = (RectTransform)row;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            row.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildIntro(row);
            BuildSettings(row);
            Refresh();
        }

        void BuildIntro(Transform row)
        {
            var intro = Column(row, "Introduction", 14);
            Size(intro.gameObject, 440);
            Label(intro, "Title", "SAFE-MINING <color=#2FD27F>EVAC</color>", S.Heading, 58, S.TextPrimary);
            Label(intro, "Description", "Simulasi evakuasi tambang bawah tanah dengan deteksi longsor oleh edge dan navigasi adaptif.",
                S.Body, S.SizeBody, S.TextSecondary);
            var legend = Column(intro, "Status legend", 8);
            ((VerticalLayoutGroup)legend.GetComponent<LayoutGroup>()).padding = new RectOffset(0, 0, 14, 14);
            IconLabel(legend, "Safe", I.Safe, "Aman dan rute evakuasi", S.Body, 18, S.TextSecondary).icon.color = S.Safe;
            IconLabel(legend, "Warning", I.Warning, "Waspada, getaran meningkat", S.Body, 18, S.TextSecondary).icon.color = S.Warning;
            IconLabel(legend, "Closed", I.Closed, "Lorong tertutup longsor", S.Body, 18, S.TextSecondary).icon.color = S.Danger;
            IconLabel(intro, "Virtual data note", I.Info, "Sensor dan data bersifat virtual.", S.Body, S.SizeCaption, S.TextSecondary);
        }

        void BuildSettings(Transform row)
        {
            var card = Box(row, "Settings card", S.Surface, S.Rounded);
            Size(card.gameObject, 720);
            var column = Pad(card.gameObject, 28, 26, 20);

            var modeSection = Section(card.transform, "Mode");
            var cards = Row(modeSection, "Mode cards", 12, TextAnchor.UpperLeft, true);
            cards.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true; // Both cards share the taller height.
            storyCard = BuildModeCard(cards, I.Watch, "Cerita", "Pekerja berjalan otomatis. Amati deteksi dan perubahan rute.", MiningMode.Story);
            fppCard = BuildModeCard(cards, I.Walk, "FPP", "Kamu yang mengendalikan pekerja dari sudut pandangnya.", MiningMode.FirstPerson);

            var navigationSection = Section(card.transform, "Navigasi");
            navigation = Segments(navigationSection, "Navigation", new[] { (I.Route, "Adaptif"), (I.RouteFixed, "Statis") },
                i => { adaptive = i == 0; Refresh(); });
            navigationHelp = Help(navigationSection);

            var scenarioSection = Section(card.transform, "Skenario");
            var reroll = Link(scenarioSection.GetChild(0), I.Dice, "Acak ulang", () => { simulation.NewRandomScenario(); Refresh(); }, TextAnchor.MiddleRight);
            Size(reroll.button.gameObject, 140, 26, 0);
            scenario = Segments(scenarioSection, "Scenario", new[] { (I.Shuffle, "Acak"), (I.Controlled, "Terkontrol"), (I.NoHazard, "Tanpa bahaya") },
                i => { simulation.scenarioMode = (HazardScenarioMode)i; Refresh(); });
            scenarioHelp = Help(scenarioSection);

            var sourceSection = Section(card.transform, "Sumber deteksi");
            HazardSource[] order = { HazardSource.MqttEdgeSimulation, HazardSource.LocalEdgeSimulation, HazardSource.LegacyTimeline };
            source = Segments(sourceSection, "Hazard source", new[] { (I.Broadcast, "MQTT edge"), (I.Cpu, "Edge lokal"), (I.Schedule, "Jadwal lama") },
                i => { simulation.hazardSource = order[i]; Refresh(); });
            var help = IconLabel(sourceSection, "Source help", I.Info, "", S.Body, S.SizeCaption, S.TextSecondary);
            sourceHelp = help.label; sourceHelpIcon = help.icon;
            sourceHelp.GetComponent<LayoutElement>().flexibleWidth = 1;

            start = Primary(card.transform, I.Play, "", () => begin(mode, adaptive), 54);
            start.button.name = "Start simulation"; // Stable name for editor validation runners.
            fppControls = IconLabel(card.transform, "FPP controls", I.Keyboard,
                "WASD bergerak · Mouse melihat · Shift lari · F lampu · G kacamata · Esc jeda", S.Body, S.SizeCaption, S.TextSecondary).root;
        }

        static Transform Section(Transform parent, string title)
        {
            var section = Column(parent, title, 8);
            var header = Row(section, "Header", 0, TextAnchor.MiddleLeft, true);
            Label(header, "Title", title, S.Strong, S.SizeCaption, S.TextSecondary).GetComponent<LayoutElement>().flexibleWidth = 1;
            return section;
        }

        static Text Help(Transform parent) => Label(parent, "Help", "", S.Body, S.SizeCaption, S.TextSecondary);

        ModeCard BuildModeCard(Transform parent, string glyph, string title, string body, MiningMode value)
        {
            var card = new ModeCard { border = Box(parent, title + " mode", S.Line, S.Rounded) };
            card.fill = Box(card.border.transform, "Fill", S.Surface, S.Rounded);
            card.fill.GetComponent<LayoutElement>().ignoreLayout = true;
            Stretch(card.fill.rectTransform);
            var layout = card.border.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14); layout.spacing = 12; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false;
            card.icon = Icon(card.border.transform, glyph, 26, S.TextSecondary);
            var text = Column(card.border.transform, "Text", 2);
            text.GetComponent<LayoutElement>().flexibleWidth = 1;
            card.title = Label(text, "Title", title, S.Strong, S.SizeBody, S.TextPrimary);
            card.body = Label(text, "Body", body, S.Body, S.SizeCaption, S.TextSecondary);
            Size(card.border.gameObject, 100, -1, 1);
            card.button = card.border.gameObject.AddComponent<Button>();
            card.button.targetGraphic = card.border; card.border.raycastTarget = true;
            card.button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            card.button.onClick.AddListener(() => { mode = value; Refresh(); });
            return card;
        }

        void StyleModeCard(ModeCard card, bool selected)
        {
            // Selected: 2 px light outline and raised fill. Unselected: hairline border, flat.
            SetColors(card.button, selected ? S.TextPrimary : S.Line, selected ? S.TextPrimary : S.LineStrong);
            float inset = selected ? 2 : 1;
            card.fill.rectTransform.offsetMin = Vector2.one * inset; card.fill.rectTransform.offsetMax = -Vector2.one * inset;
            card.fill.color = selected ? S.SurfaceRaised : S.Surface;
            card.icon.color = selected ? S.TextPrimary : S.TextSecondary;
            card.title.color = selected ? S.TextPrimary : S.TextSecondary;
        }

        // Called when the menu opens: adopt the last session's choices so "back to menu" keeps them.
        public void Open()
        {
            if (simulation.SessionRevision > 0)
            {
                mode = simulation.Mode; adaptive = simulation.Adaptive;
                if (!simulation.LearningSession)
                { simulation.scenarioMode = simulation.ActiveScenario; simulation.hazardSource = simulation.ActiveHazardSource; }
            }
            Refresh();
        }

        public void Refresh()
        {
            StyleModeCard(storyCard, mode == MiningMode.Story);
            StyleModeCard(fppCard, mode == MiningMode.FirstPerson);
            navigation.Select(adaptive ? 0 : 1);
            navigationHelp.text = adaptive
                ? "Rute dihitung ulang dari posisi pekerja setiap kali status bahaya berubah."
                : "Rute awal dipertahankan sebagai pembanding, tanpa perencanaan ulang.";
            scenario.Select((int)simulation.scenarioMode);
            scenarioHelp.text = simulation.scenarioMode == HazardScenarioMode.Scripted ? "Urutan tetap: D01 lalu D02. Cocok untuk demo berulang."
                : simulation.scenarioMode == HazardScenarioMode.NoHazards ? "Tanpa longsor otomatis, sebagai kontrol pembanding."
                : "Skenario tantangan: bahaya pertama diprioritaskan pada rute awal yang memiliki alternatif. Seed " + simulation.scenarioSeed + ".";
            var active = simulation.hazardSource;
            source.Select(active == HazardSource.MqttEdgeSimulation ? 0 : active == HazardSource.LocalEdgeSimulation ? 1 : 2);
            sourceHelp.text = active == HazardSource.MqttEdgeSimulation ? "Broker dan Python di Docker menjalankan klasifikasi serta perencanaan rute. Simulasi biasa memakai pilihan ini."
                : active == HazardSource.LocalEdgeSimulation ? "Edge berjalan di dalam Unity tanpa broker. Sensor tetap virtual."
                : "Jadwal pembanding lama, tanpa sensor virtual dan tanpa MQTT.";
            sourceHelpIcon.color = active == HazardSource.MqttEdgeSimulation ? S.Info : S.TextSecondary;
            start.label.text = mode == MiningMode.Story ? "Mulai Mode Cerita" : "Mulai Mode FPP";
            fppControls.SetActive(mode == MiningMode.FirstPerson);
        }

        // Keeps the two-column layout inside narrow aspect ratios (e.g. 4:3 projectors).
        public void FitToScreen()
        {
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
            content.localScale = Vector3.one * Mathf.Clamp(aspect / 1.72f, .72f, 1f);
        }
    }
}
