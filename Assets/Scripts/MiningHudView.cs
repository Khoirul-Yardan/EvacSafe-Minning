using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static SafeMining.MiningUiKit;
using S = SafeMining.MiningUiStyle;
using I = SafeMining.MiningIcons;

namespace SafeMining
{
    // In-session HUD. Read-only view of MiningSimulation and its edge session events.
    // Detection stages show only what Unity itself observes; decisions made inside the
    // Python edge are not visible here, so the MQTT path reports "status received" instead.
    public sealed class MiningHudView
    {
        public GameObject Root { get; }

        readonly MiningSimulation simulation;
        readonly MiningLandslideCutscene cutscene;
        bool showDetail;

        // Status card
        Text modeIcon, modeText, navigationIcon, navigationText, headlineIcon, headline, sourceIcon, sourceText;
        GameObject equipmentRow, detailLinkRoot;
        Text lampIcon, lampText, glassesIcon, glassesText;
        ButtonView detailLink;
        // Alert banner
        GameObject banner;
        Image bannerAccent;
        Text bannerIcon, bannerTitle, bannerRouteIcon, bannerRoute;
        // Detection
        GameObject detection, gauge, stagesRoot, detectionWarning;
        Text detectionTitle, vibrationValue, detectionIdle, detectionWarningText, latchNote;
        Image vibrationFill;
        RectTransform warningMark, dangerMark;
        readonly Text[] stageIcons = new Text[3], stageTexts = new Text[3];
        // Map and time
        Text timer, reroutes;
        MineMapGraphic map;
        // Cutscene
        Text cutsceneTitle, cutsceneStatus;
        // Radio, direction, hints
        Text dialogue, directionIcon, directionText, distanceText;
        GameObject storyHints, fppHints;
        // FPP overlays
        GameObject glassesRim;
        Image dangerWash;

        // Edge observation
        MiningEdgeSession observed;
        float warningThreshold = .45f, dangerThreshold = .75f;
        readonly Dictionary<int, float> vibration = new Dictionary<int, float>();
        readonly Dictionary<int, int> decided = new Dictionary<int, int>();
        readonly Dictionary<int, float> lastChange = new Dictionary<int, float>();
        int[] previousLevels = new int[0];

        public MiningHudView(Transform canvas, MiningSimulation simulation, MiningLandslideCutscene cutscene)
        {
            this.simulation = simulation; this.cutscene = cutscene;
            var root = new GameObject("In-session HUD", typeof(RectTransform));
            root.transform.SetParent(canvas, false); Stretch((RectTransform)root.transform);
            Root = root;

            dangerWash = Box(root.transform, "Danger proximity", Color.clear); Stretch(dangerWash.rectTransform);
            BuildGlassesRim(root.transform);

            var left = Corner(root.transform, "Left column", new Vector2(0, 1), new Vector2(24, -24));
            BuildStatus(left); BuildDetection(left);
            var right = Corner(root.transform, "Right column", new Vector2(1, 1), new Vector2(-24, -24));
            right.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;
            BuildMap(right); BuildCutscene(right);
            BuildBanner(root.transform);
            BuildRadio(root.transform); BuildDirection(root.transform); BuildHints(root.transform);
        }

        // ---------- construction ----------

        static Transform Corner(Transform parent, string name, Vector2 anchor, Vector2 offset)
        {
            var column = Column(parent, name, 12);
            var r = (RectTransform)column; r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = offset;
            column.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            var fit = column.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return column;
        }

        static Transform Card(Transform parent, string name, float width, int padX = 16, int padY = 12, float spacing = 6)
        {
            var card = Box(parent, name, S.Hex(0x161B1F, .94f), S.Rounded);
            Pad(card.gameObject, padX, padY, spacing);
            Size(card.gameObject, width, -1, 0); // Fixed width: children with flexible width must not widen the card.
            return card.transform;
        }

        static Transform Floating(Transform parent, string name, Vector2 anchor, Vector2 offset, float width)
        {
            var card = Card(parent, name, width);
            var r = (RectTransform)card; r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = offset; r.sizeDelta = new Vector2(width, 0);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card;
        }

        void BuildStatus(Transform column)
        {
            var card = Card(column, "Status", 380);
            var top = Row(card, "Mode", 6);
            modeIcon = Icon(top, "", S.SizeCaption, S.TextSecondary);
            modeText = Label(top, "Mode", "", S.Body, S.SizeCaption, S.TextSecondary);
            navigationIcon = Icon(top, "", S.SizeCaption, S.TextSecondary);
            navigationText = Label(top, "Navigation", "", S.Body, S.SizeCaption, S.TextSecondary);
            navigationText.GetComponent<LayoutElement>().flexibleWidth = 1;
            var pause = Secondary(top, null, "Jeda  Esc", () => simulation.TogglePause(), 28);
            pause.button.name = "Pause button"; Size(pause.surface.gameObject, 96, 28, 0);
            pause.label.fontSize = pause.icon.fontSize = 14;

            var head = Row(card, "Headline", 8);
            headlineIcon = Icon(head, "", 24, S.TextPrimary);
            headline = Label(head, "Headline", "", S.Heading, 26, S.TextPrimary);

            var source = Row(card, "Source", 6);
            sourceIcon = Icon(source, "", S.SizeCaption, S.Info);
            sourceText = Label(source, "Source", "", S.Body, S.SizeCaption, S.Info);

            equipmentRow = Row(card, "FPP equipment", 6).gameObject;
            lampIcon = Icon(equipmentRow.transform, I.Headlamp, S.SizeCaption, S.TextPrimary);
            lampText = Label(equipmentRow.transform, "Lamp", "", S.Body, S.SizeCaption, S.TextSecondary);
            Size(Box(equipmentRow.transform, "Gap", Color.clear).gameObject, 10, 1, 0);
            glassesIcon = Icon(equipmentRow.transform, I.Glasses, S.SizeCaption, S.TextPrimary);
            glassesText = Label(equipmentRow.transform, "Glasses", "", S.Body, S.SizeCaption, S.TextSecondary);

            detailLink = Link(card, I.ChevronRight, "Detail sistem", () => showDetail = !showDetail);
            detailLink.button.name = "System detail toggle";
            detailLinkRoot = detailLink.surface.gameObject;
        }

        void BuildDetection(Transform column)
        {
            var card = Card(column, "Detection", 380, 16, 12, 8);
            detection = card.gameObject;
            var head = Row(card, "Title", 6);
            Icon(head, I.Vibration, S.SizeCaption, S.TextSecondary);
            detectionTitle = Label(head, "Title", "", S.Strong, S.SizeCaption, S.TextSecondary);

            detectionIdle = Label(card, "Idle", "", S.Body, S.SizeCaption, S.TextSecondary);

            gauge = Column(card, "Vibration gauge", 4).gameObject;
            var labels = Row(gauge.transform, "Labels", 0, TextAnchor.MiddleLeft, true);
            Label(labels, "Name", "Getaran simulasi (0 sampai 1)", S.Body, S.SizeCaption, S.TextSecondary);
            vibrationValue = Label(labels, "Value", "", S.Numeric, S.SizeCaption, S.TextPrimary, TextAnchor.MiddleRight);
            var track = Box(gauge.transform, "Track", S.Line, S.Rounded);
            Size(track.gameObject, -1, 10, 1);
            vibrationFill = Box(track.transform, "Fill", S.Safe, S.Rounded);
            vibrationFill.GetComponent<LayoutElement>().ignoreLayout = true;
            var fill = vibrationFill.rectTransform; fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            warningMark = Mark(track.transform, S.TextPrimary); dangerMark = Mark(track.transform, S.Danger);
            latchNote = Label(gauge.transform, "Latch note", "Getaran sudah turun, lorong tetap tertutup sampai sesi diulang.", S.Body, 14, S.TextSecondary);

            stagesRoot = Column(card, "Stages", 4).gameObject;
            for (int i = 0; i < 3; i++)
            {
                var row = Row(stagesRoot.transform, "Stage " + (i + 1), 6);
                stageIcons[i] = Icon(row, "", S.SizeCaption, S.TextSecondary);
                stageTexts[i] = Label(row, "Text", "", S.Body, S.SizeCaption, S.TextSecondary);
                stageTexts[i].GetComponent<LayoutElement>().flexibleWidth = 1;
            }
            var warning = Row(card, "Data warning", 6);
            Icon(warning, I.Disconnected, S.SizeCaption, S.Warning);
            detectionWarningText = Label(warning, "Text", "", S.Body, S.SizeCaption, S.Warning);
            detectionWarning = warning.gameObject;
        }

        static RectTransform Mark(Transform track, Color color)
        {
            var mark = Box(track, "Threshold", color);
            mark.GetComponent<LayoutElement>().ignoreLayout = true;
            var r = mark.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0, .5f); r.sizeDelta = new Vector2(2, 16);
            return r;
        }

        void BuildMap(Transform column)
        {
            var card = Card(column, "Map", 320, 12, 12, 8);
            var head = Row(card, "Title", 6);
            Icon(head, I.Map, S.SizeCaption, S.TextSecondary);
            Label(head, "Title", "Peta evakuasi", S.Strong, S.SizeCaption, S.TextSecondary);
            var surface = Box(card, "Map surface", S.SurfaceSunken, S.Rounded);
            Size(surface.gameObject, -1, 230, 1);
            var mapObject = new GameObject("Live mine topology", typeof(RectTransform), typeof(CanvasRenderer), typeof(MineMapGraphic));
            mapObject.transform.SetParent(surface.transform, false);
            var r = (RectTransform)mapObject.transform; Stretch(r); r.offsetMin = Vector2.one * 8; r.offsetMax = -Vector2.one * 8;
            map = mapObject.GetComponent<MineMapGraphic>(); map.Simulation = simulation; map.raycastTarget = false;
            var legend = Row(card, "Legend", 10);
            IconLabel(legend, "Safe", I.Exit, "Aman", S.Body, 14, S.TextSecondary, 3).icon.color = S.Safe;
            IconLabel(legend, "Warning", I.Warning, "Waspada", S.Body, 14, S.TextSecondary, 3).icon.color = S.Warning;
            IconLabel(legend, "Closed", I.Closed, "Tertutup", S.Body, 14, S.TextSecondary, 3).icon.color = S.Danger;
            Divider(card);
            var time = Row(card, "Time", 0, TextAnchor.MiddleLeft, true);
            var clock = Row(time, "Clock", 6);
            Icon(clock, I.Clock, S.SizeCaption, S.TextSecondary);
            timer = Label(clock, "Timer", "", S.Numeric, 22, S.TextPrimary);
            reroutes = Label(time, "Reroutes", "", S.Body, 14, S.TextSecondary, TextAnchor.MiddleRight);
        }

        void BuildCutscene(Transform column)
        {
            var card = Card(column, "Landslide cutscene", 320, 12, 12, 8);
            var head = Row(card, "Title", 6);
            var live = Box(head, "Live dot", S.Danger, S.Circle); Size(live.gameObject, 10, 10, 0);
            Label(head, "Live", "LIVE", S.Strong, 14, S.Danger);
            Icon(head, I.Camera, S.SizeCaption, S.TextSecondary);
            cutsceneTitle = Label(head, "Location", "", S.Strong, S.SizeCaption, S.TextPrimary);
            var feed = new GameObject("Live landslide view", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            feed.transform.SetParent(card, false);
            Size(feed, -1, 166, 1); // 16:9 at the card's inner width. feed.GetComponent<RawImage>().raycastTarget = false;
            cutsceneStatus = Label(card, "Queue", "", S.Body, S.SizeCaption, S.TextSecondary);
            // The cutscene component keeps its own labels; they stay hidden so its logic is untouched.
            var hidden = new GameObject("Cutscene component labels", typeof(RectTransform));
            hidden.transform.SetParent(card, false); hidden.SetActive(false);
            var ownTitle = Label(hidden.transform, "Title", "", S.Body, 12, Color.clear);
            var ownStatus = Label(hidden.transform, "Status", "", S.Body, 12, Color.clear);
            cutscene.Initialize(simulation, card.gameObject, feed.GetComponent<RawImage>(), ownTitle, ownStatus);
        }

        void BuildBanner(Transform root)
        {
            var card = Box(root, "Hazard banner", S.Hex(0x161B1F, .96f), S.Rounded);
            var r = card.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, 1); r.anchoredPosition = new Vector2(0, -24);
            var layout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 20, 0, 0); layout.spacing = 14; layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bannerAccent = Box(card.transform, "Accent", S.Warning); Size(bannerAccent.gameObject, 5, -1, 0);
            bannerIcon = Icon(card.transform, "", 26, S.Warning);
            var text = Column(card.transform, "Text", 2);
            ((VerticalLayoutGroup)text.GetComponent<LayoutGroup>()).padding = new RectOffset(0, 0, 10, 10);
            bannerTitle = Label(text, "Title", "", S.Strong, S.SizeBody, S.TextPrimary);
            bannerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            var route = Row(text, "Route", 6);
            bannerRouteIcon = Icon(route, I.Route, S.SizeCaption, S.TextSecondary);
            bannerRoute = Label(route, "Route", "", S.Body, S.SizeCaption, S.TextSecondary);
            bannerRoute.horizontalOverflow = HorizontalWrapMode.Overflow;
            banner = card.gameObject;
        }

        void BuildRadio(Transform root)
        {
            var card = Floating(root, "Team radio", Vector2.zero, new Vector2(24, 24), 560);
            IconLabel(card, "Speaker", I.Radio, "Radio tim", S.Strong, S.SizeCaption, S.TextSecondary);
            dialogue = Label(card, "Dialogue", "", S.Body, S.SizeBody, S.TextPrimary);
        }

        void BuildDirection(Transform root)
        {
            var card = Floating(root, "Direction", new Vector2(.5f, 0), new Vector2(0, 24), 300);
            ((VerticalLayoutGroup)card.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperCenter;
            directionIcon = Label(card, "Arrow", "", S.Icons, 44, S.Safe, TextAnchor.MiddleCenter);
            directionText = Label(card, "Direction", "", S.Heading, 26, S.Safe, TextAnchor.MiddleCenter);
            var distance = Row(card, "Distance", 6, TextAnchor.MiddleCenter);
            Icon(distance, I.Exit, S.SizeCaption, S.TextSecondary);
            distanceText = Label(distance, "Distance", "", S.Body, S.SizeCaption, S.TextSecondary);
        }

        void BuildHints(Transform root)
        {
            storyHints = HintRow(root, ("Esc", "jeda"), ("R", "ulangi"));
            fppHints = HintRow(root, ("WASD", "gerak"), ("Shift", "lari"), ("F", "lampu"), ("G", "kacamata"), ("Esc", "jeda"), ("R", "ulangi"));
        }

        static GameObject HintRow(Transform root, params (string key, string action)[] hints)
        {
            var row = Row(root, "Control hints", 14);
            var r = (RectTransform)row; r.anchorMin = r.anchorMax = r.pivot = new Vector2(1, 0); r.anchoredPosition = new Vector2(-24, 28);
            var fit = row.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var hint in hints)
            {
                var item = Row(row, hint.action, 6);
                var cap = Box(item, "Keycap", S.Hex(0x1F262B, .92f), S.Rounded);
                var capLayout = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
                capLayout.padding = new RectOffset(7, 7, 2, 2); capLayout.childControlWidth = capLayout.childControlHeight = true;
                Label(cap.transform, "Key", hint.key, S.Numeric, 14, S.TextPrimary).horizontalOverflow = HorizontalWrapMode.Overflow;
                Label(item, "Action", hint.action, S.Body, 15, S.TextSecondary).horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return row.gameObject;
        }

        void BuildGlassesRim(Transform root)
        {
            glassesRim = new GameObject("Safety glasses rim", typeof(RectTransform));
            glassesRim.transform.SetParent(root, false); Stretch((RectTransform)glassesRim.transform);
            var rim = S.Hex(0x2FD27F, .35f);
            foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 })
            {
                var anchor = new Vector2(sx < 0 ? 0 : 1, sy < 0 ? 0 : 1);
                Bar(glassesRim.transform, anchor, new Vector2(-sx * 70, -sy * 12), new Vector2(100, 2), rim);
                Bar(glassesRim.transform, anchor, new Vector2(-sx * 12, -sy * 62), new Vector2(2, 90), rim);
            }
            var reticle = Box(glassesRim.transform, "Reticle", S.Hex(0xE8ECEF, .5f), S.Circle);
            reticle.rectTransform.anchorMin = reticle.rectTransform.anchorMax = new Vector2(.5f, .5f); reticle.rectTransform.sizeDelta = Vector2.one * 5;
        }

        static void Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var bar = Box(parent, "Rim", color); var r = bar.rectTransform;
            r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = position; r.sizeDelta = size;
        }

        // ---------- per frame ----------

        public void ResetDetail() => showDetail = false;

        public void Refresh()
        {
            var s = simulation;
            Observe(s.EdgeSession);
            TrackLevels(s);
            bool fpp = s.Mode == MiningMode.FirstPerson;

            modeIcon.text = fpp ? I.Walk : I.Watch; modeText.text = (fpp ? "FPP" : "Cerita") + " / " + MiningHUD.ScenarioName(s.ActiveScenario);
            navigationIcon.text = s.Adaptive ? I.Route : I.RouteFixed; navigationText.text = s.Adaptive ? "Adaptif" : "Statis";
            int focus = Focus(s);
            int level = focus >= 0 ? s.HazardLevels[focus] : 0;
            string id = focus >= 0 ? DeviceId(focus) : "";
            SetHeadline(level == 2 ? I.Closed : level == 1 ? I.Warning : s.NavigationWaiting ? I.Clock : s.Elapsed < 4 && !fpp ? I.Info : I.Route,
                level == 2 ? "Lorong tertutup · " + id : level == 1 ? "Waspada · " + id : s.NavigationWaiting ? "Menunggu rute edge" : s.Elapsed < 4 && !fpp ? "Briefing" : "Menuju zona aman",
                level == 2 ? S.Danger : level == 1 ? S.Warning : S.TextPrimary);
            SetSource(s);
            if (s.StoryHolding) SetHeadline(I.Stop, s.Phase, S.Warning);

            equipmentRow.SetActive(fpp);
            SetEquipment(lampIcon, lampText, s.HeadlampEnabled, "Lampu helm");
            SetEquipment(glassesIcon, glassesText, s.GlassesEnabled, "Kacamata AR");
            detailLinkRoot.SetActive(fpp);
            detailLink.label.text = showDetail ? "Sembunyikan detail sistem" : "Detail sistem";
            detailLink.icon.text = showDetail ? I.ChevronDown : I.ChevronRight;
            detection.SetActive(!fpp || showDetail);
            RefreshDetection(s, focus);

            RefreshBanner(s);
            timer.text = ((int)(s.Elapsed / 60)).ToString("00") + ":" + (s.Elapsed % 60).ToString("00.0");
            reroutes.text = s.Reroutes + " perubahan rute";
            map.SetVerticesDirty();

            int shot = cutscene.ActiveDeviceIndex;
            if (shot >= 0)
            {
                cutsceneTitle.text = "Longsor · " + DeviceId(shot);
                cutsceneStatus.text = cutscene.PendingCount > 0 ? cutscene.PendingCount + " lokasi lain menunggu tayang" : "Lorong tertutup, kamera lokasi";
            }

            dialogue.text = s.Dialogue;
            RefreshDirection(s);
            storyHints.SetActive(!fpp); fppHints.SetActive(fpp);
            glassesRim.SetActive(fpp && s.GlassesEnabled);
            float near = s.NearestHazardDistance();
            dangerWash.color = new Color(S.Danger.r, S.Danger.g, S.Danger.b, Mathf.Clamp01((8 - near) / 8) * .16f);
        }

        void SetHeadline(string glyph, string text, Color color) { headlineIcon.text = glyph; headlineIcon.color = color; headline.text = text; headline.color = color; }

        void SetSource(MiningSimulation s)
        {
            string glyph = MiningResultPanel.SourceIcon(s.ActiveHazardSource), text; Color color;
            var session = s.EdgeSession;
            if (s.ActiveHazardSource == HazardSource.LegacyTimeline) { text = "Jadwal pembanding, tanpa sensor virtual"; color = S.TextSecondary; }
            else if (s.ActiveHazardSource == HazardSource.LocalEdgeSimulation) { text = "Edge lokal, data virtual"; color = S.TextSecondary; }
            else if (session == null) { text = s.LastDetectorAlert.Contains("Konfigurasi edge gagal") ? "Konfigurasi edge gagal" : "MQTT menunggu sesi"; color = S.Warning; }
            else if (s.EdgeRouteSession != null && s.EdgeRouteSession.Failed) { glyph = I.Disconnected; text = "Navigasi edge gagal, ulangi sesi"; color = S.Danger; }
            else if (session.DataLoss) { glyph = I.Disconnected; text = "Data MQTT hilang, ulangi sesi"; color = S.Danger; }
            else if (session.DataStale || session.TransportStatus.Contains("terputus")) { glyph = I.Disconnected; text = "MQTT: data belum mutakhir"; color = S.Warning; }
            else { text = s.NavigationWaiting ? "MQTT · menunggu rute Python" : "MQTT · planner Python edge"; color = S.Info; }
            sourceIcon.text = glyph; sourceIcon.color = sourceText.color = color; sourceText.text = text;
        }

        static void SetEquipment(Text icon, Text label, bool on, string name)
        {
            icon.color = on ? S.TextPrimary : S.LineStrong;
            label.text = name + (on ? " nyala" : " mati");
            label.color = on ? S.TextSecondary : S.LineStrong;
        }

        void RefreshDetection(MiningSimulation s, int focus)
        {
            var source = s.ActiveHazardSource;
            int count = s.HazardSites.Count;
            bool active = focus >= 0 && (s.HazardLevels[focus] > 0 || Vibration(focus) >= warningThreshold);
            detectionTitle.text = active ? "Deteksi · " + DeviceId(focus) : "Deteksi · " + count + " detektor";
            detectionIdle.gameObject.SetActive(!active);
            detectionIdle.text = source == HazardSource.LegacyTimeline
                ? "Jadwal pembanding menetapkan status langsung, tanpa sensor virtual."
                : "Semua detektor normal. Getaran dipantau 20 kali per detik.";
            gauge.SetActive(active && source != HazardSource.LegacyTimeline);
            stagesRoot.SetActive(active);

            var session = s.EdgeSession;
            bool stale = session != null && (session.DataLoss || session.DataStale || session.TransportStatus.Contains("terputus"));
            detectionWarning.SetActive(source == HazardSource.MqttEdgeSimulation && stale);
            detectionWarningText.text = session != null && session.DataLoss ? "Data hilang. Status tidak diterapkan lagi, ulangi sesi."
                : "Broker belum terhubung atau data terlambat. Status terakhir dipertahankan.";
            if (!active) return;

            float value = Vibration(focus);
            int applied = s.HazardLevels[focus];
            vibrationValue.text = value.ToString("0.00");
            vibrationValue.color = vibrationFill.color = value >= dangerThreshold ? S.Danger : value >= warningThreshold ? S.Warning : S.Safe;
            vibrationFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            warningMark.anchorMin = warningMark.anchorMax = new Vector2(warningThreshold, .5f);
            dangerMark.anchorMin = dangerMark.anchorMax = new Vector2(dangerThreshold, .5f);
            latchNote.gameObject.SetActive(applied == 2 && value < dangerThreshold);

            string levelName = applied == 2 ? "tertutup" : "waspada";
            if (source == HazardSource.MqttEdgeSimulation)
            {
                Stage(0, !stale, stale ? "Sampel getaran belum sampai ke broker" : "Sampel getaran dikirim ke broker MQTT");
                Stage(1, applied > 0, applied > 0 ? "Status " + levelName + " diterima dari edge Python" : "Menunggu keputusan edge Python");
            }
            else if (source == HazardSource.LocalEdgeSimulation)
            {
                int decision = decided.TryGetValue(focus, out int d) ? d : 0;
                Stage(0, true, "Sampel getaran dibaca edge lokal");
                Stage(1, decision > 0, decision > 0 ? "Edge lokal memutuskan " + (decision == 2 ? "tertutup" : "waspada") : "Edge lokal menunggu durasi ambang terpenuhi");
            }
            else
            {
                Stage(0, true, "Jadwal pembanding mencapai waktu kejadian");
                Stage(1, applied > 0, "Status " + levelName + " ditetapkan jadwal");
            }
            bool navigationFailed = s.EdgeRouteSession != null && s.EdgeRouteSession.Failed;
            string routeStatus = s.StoryHolding ? "; pekerja berhenti, pemeriksaan jalur berlangsung" : !s.Adaptive ? "; rute statis tetap" : navigationFailed ? "; navigasi edge gagal" :
                s.NavigationWaiting ? "; menunggu rute edge" : "; rute terbaru diterapkan";
            Stage(2, applied > 0 && !s.StoryHolding && !navigationFailed && (!s.Adaptive || !s.NavigationWaiting), applied > 0
                ? "Diterapkan: " + levelName + routeStatus
                : "Belum diterapkan ke lorong");
        }

        void Stage(int i, bool done, string text)
        {
            stageIcons[i].text = done ? I.Safe : I.Clock;
            stageIcons[i].color = done ? S.Safe : S.TextSecondary;
            stageTexts[i].text = text;
            stageTexts[i].color = done ? S.TextPrimary : S.TextSecondary;
        }

        void RefreshBanner(MiningSimulation s)
        {
            int closed = -1, warning = -1; float nearClosed = float.PositiveInfinity, nearWarning = float.PositiveInfinity;
            Vector3 actor = s.Actor.position;
            for (int i = 0; i < s.HazardLevels.Length && i < s.HazardSites.Count; i++)
            {
                if (s.HazardLevels[i] < 1) continue;
                Vector3 site = MineLayout.World(s.HazardSites[i]);
                float d = Vector2.Distance(new Vector2(actor.x, actor.z), new Vector2(site.x, site.z));
                if (s.HazardLevels[i] == 2 && d < nearClosed) { nearClosed = d; closed = i; }
                else if (s.HazardLevels[i] == 1 && d < nearWarning) { nearWarning = d; warning = i; }
            }
            int index = closed >= 0 ? closed : warning;
            banner.SetActive(index >= 0);
            if (index < 0) return;
            bool isClosed = closed >= 0;
            var color = isClosed ? S.Danger : S.Warning;
            bannerAccent.color = bannerIcon.color = color;
            bannerIcon.text = isClosed ? I.Closed : I.Warning;
            bannerTitle.text = DeviceId(index) + (isClosed ? " lorong tertutup longsor" : " waspada, getaran meningkat");
            if (s.EdgeRouteSession != null && s.EdgeRouteSession.Failed) { bannerRouteIcon.text = I.Stop; bannerRoute.text = "Layanan navigasi tidak tersedia"; }
            else if (s.StoryHolding) { bannerRouteIcon.text = I.Stop; bannerRoute.text = s.Phase + " / pekerja tetap diam"; }
            else if (s.NavigationWaiting) { bannerRouteIcon.text = I.Clock; bannerRoute.text = "Menunggu rute terbaru dari edge"; }
            else if (s.TargetExit < 0) { bannerRouteIcon.text = I.Stop; bannerRoute.text = "Tidak ada rute aman yang tersisa"; }
            else if (s.Adaptive) { bannerRouteIcon.text = I.Route; bannerRoute.text = "Rute ke zona aman " + (s.TargetExit + 1) + " · " + s.RouteDistance.ToString("F0") + " m"; }
            else { bannerRouteIcon.text = I.RouteFixed; bannerRoute.text = "Navigasi statis tetap di rute awal"; }
        }

        void RefreshDirection(MiningSimulation s)
        {
            bool glasses = s.Mode == MiningMode.Story || s.GlassesEnabled;
            string hint = glasses ? s.DirectionHint() : "";
            string glyph, text; Color color = S.Safe;
            switch (hint)
            {
                case "LURUS": glyph = I.Straight; text = "Lurus"; break;
                case "BELOK KIRI": glyph = I.TurnLeft; text = "Belok kiri"; break;
                case "BELOK KANAN": glyph = I.TurnRight; text = "Belok kanan"; break;
                case "PUTAR BALIK": glyph = I.UTurn; text = "Putar balik"; break;
                case "TIDAK ADA RUTE AMAN": glyph = I.Stop; text = "Tidak ada rute aman"; color = S.Danger; break;
                case "RUTE TERHALANG - BERHENTI": glyph = I.Stop; text = "Rute terhalang, berhenti"; color = S.Danger; break;
                default: glyph = I.Glasses; text = glasses ? hint : "Kacamata AR mati"; color = S.TextSecondary; break;
            }
            directionIcon.text = glyph; directionIcon.color = directionText.color = color; directionText.text = text;
            distanceText.text = !glasses ? "Tekan G untuk menyalakan petunjuk"
                : s.StoryHolding ? "Jalur ditahan sampai pemeriksaan selesai"
                : !s.NavigationWaiting && s.TargetExit >= 0 ? "Zona aman " + (s.TargetExit + 1) + " · " + s.RouteDistance.ToString("F0") + " m" : "Menunggu rute";
        }

        // ---------- detection focus ----------

        // The device that matters now: closed over warning over rising vibration, newest change first.
        int Focus(MiningSimulation s)
        {
            int best = -1; float bestScore = 0;
            for (int i = 0; i < s.HazardLevels.Length; i++)
            {
                float score = s.HazardLevels[i] * 10 + (Vibration(i) >= warningThreshold ? 5 : 0);
                if (score <= 0) continue;
                score += (lastChange.TryGetValue(i, out float t) ? t : 0) * .001f;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        float Vibration(int device) => vibration.TryGetValue(device, out float v) ? v : 0;

        void TrackLevels(MiningSimulation s)
        {
            if (previousLevels.Length != s.HazardLevels.Length) previousLevels = new int[s.HazardLevels.Length];
            for (int i = 0; i < previousLevels.Length; i++)
                if (previousLevels[i] != s.HazardLevels[i]) { previousLevels[i] = s.HazardLevels[i]; lastChange[i] = s.Elapsed; }
        }

        void Observe(MiningEdgeSession session)
        {
            if (observed == session) return;
            Unsubscribe();
            observed = session; vibration.Clear(); decided.Clear(); lastChange.Clear(); showDetail = false;
            if (session == null) return;
            var settings = session.Settings;
            warningThreshold = settings.warningThreshold; dangerThreshold = settings.dangerThreshold;
            session.VibrationSampled += OnSample;
            session.EdgeStateChanged += OnDecision;
        }

        public void Unsubscribe()
        {
            if (observed == null) return;
            observed.VibrationSampled -= OnSample;
            observed.EdgeStateChanged -= OnDecision;
        }

        void OnSample(EdgeStatusMessage sample) { int i = Index(sample.deviceId); if (i >= 0) vibration[i] = sample.vibrationNormalized; }

        // Local edge: fired on the edge decision. MQTT: fired when the received status is applied.
        void OnDecision(EdgeStatusMessage message) { int i = Index(message.deviceId); if (i >= 0) decided[i] = message.level; }

        static int Index(string deviceId) => deviceId != null && deviceId.Length > 1 && int.TryParse(deviceId.Substring(1), out int n) ? n - 1 : -1;

        static string DeviceId(int index) => "D" + (index + 1).ToString("00");
    }
}
