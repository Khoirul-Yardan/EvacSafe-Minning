using System;
using UnityEngine;
using UnityEngine.UI;
using static SafeMining.MiningUiKit;
using S = SafeMining.MiningUiStyle;
using I = SafeMining.MiningIcons;

namespace SafeMining
{
    // Pause and end-of-session panel. Reads simulation state only; actions go through the
    // same public MiningSimulation calls the previous modal used.
    public sealed class MiningResultPanel
    {
        sealed class SessionSnapshot
        {
            public bool adaptive, dataLoss, hadDisconnect, dataStale;
            public SessionState outcome;
            public float elapsed, exposure, maxResponseMs;
            public int contacts, reroutes, seed;
            public MiningMode mode;
            public HazardScenarioMode scenario;
            public HazardSource source;
            public SessionSnapshot(MiningSimulation s)
            {
                adaptive = s.Adaptive; outcome = s.State; elapsed = s.Elapsed; exposure = s.Exposure;
                maxResponseMs = s.MaxResponseMs; contacts = s.HazardContacts; reroutes = s.Reroutes; seed = s.ActiveSeed;
                mode = s.Mode; scenario = s.ActiveScenario; source = s.ActiveHazardSource;
                dataLoss = s.EdgeSession != null && s.EdgeSession.DataLoss;
                hadDisconnect = s.EdgeSession != null && s.EdgeSession.HadDisconnect;
                dataStale = s.EdgeSession != null && s.EdgeSession.DataStale;
            }
        }

        public GameObject Root { get; }
        public Button PrimaryButton => primary.button;

        readonly MiningSimulation simulation;
        readonly Action beforeRestart;
        SessionSnapshot comparison;
        bool showDetail;

        Image badge;
        GameObject pauseBars;
        Text badgeGlyph, title, subtitle, qualityText, reroutesText, contactsText, detailText, exportStatus, sensitivityValue, fovValue, headBobValue;
        IconText modeChip, navigationChip, scenarioChip, sourceChip;
        ButtonView primary, secondary, exportLink, menuLink, detailToggle, headBob;
        GameObject qualityBadge, metricsRow, secondaryRow, comparisonTable, cameraRow;
        readonly Text[] metricValues = new Text[3];
        readonly Text[] adaptiveCells = new Text[5], staticCells = new Text[5];
        Text adaptiveHeader, staticHeader;

        public MiningResultPanel(Transform canvas, MiningSimulation simulation, Action beforeRestart)
        {
            this.simulation = simulation; this.beforeRestart = beforeRestart;
            var backdrop = Box(canvas, "Pause and result", S.Backdrop);
            Stretch(backdrop.rectTransform); backdrop.raycastTarget = true; // Blocks clicks to the HUD behind.
            Root = backdrop.gameObject;

            var card = Box(backdrop.transform, "Result card", S.Surface, S.Rounded).rectTransform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(640, 0);
            var column = Pad(card.gameObject, 28, 24, 16);
            column.padding.bottom = 20;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHeader(card);
            var chips = Row(card, "Session chips", 8);
            modeChip = Chip(chips, S.TextSecondary); navigationChip = Chip(chips, S.TextSecondary);
            scenarioChip = Chip(chips, S.TextSecondary); sourceChip = Chip(chips, S.Info);

            var quality = Box(card, "Data quality", S.Warning, S.Rounded);
            var qualityRow = quality.gameObject.AddComponent<HorizontalLayoutGroup>();
            qualityRow.padding = new RectOffset(12, 12, 8, 8); qualityRow.spacing = 8; qualityRow.childAlignment = TextAnchor.MiddleLeft;
            qualityRow.childControlWidth = qualityRow.childControlHeight = true; qualityRow.childForceExpandWidth = false;
            Icon(quality.transform, I.Warning, S.SizeCaption, S.OnWarning);
            qualityText = Label(quality.transform, "Quality note", "", S.Strong, S.SizeCaption, S.OnWarning);
            qualityText.GetComponent<LayoutElement>().flexibleWidth = 1;
            qualityBadge = quality.gameObject;

            BuildMetrics(card);
            BuildComparison(card);
            BuildDetail(card);
            BuildActions(card);
            BuildCameraSettings(card);
        }

        void BuildHeader(Transform card)
        {
            var header = Row(card, "Outcome", 14);
            badge = Box(header, "Outcome badge", S.Safe, S.Circle);
            Size(badge.gameObject, 52, 52, 0);
            badgeGlyph = Label(badge.transform, "Glyph", "", S.Icons, 28, S.OnSafe, TextAnchor.MiddleCenter);
            Stretch(badgeGlyph.rectTransform);
            // Solid bars: the outline pause glyph reads as "00" at this size.
            pauseBars = new GameObject("Pause bars", typeof(RectTransform)); pauseBars.transform.SetParent(badge.transform, false);
            foreach (float x in new[] { -6f, 6f })
            {
                var bar = Box(pauseBars.transform, "Bar", S.TextPrimary, S.Rounded).rectTransform;
                bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f); bar.anchoredPosition = new Vector2(x, 0); bar.sizeDelta = new Vector2(6, 22);
            }
            var text = Column(header, "Outcome text", 2);
            text.GetComponent<LayoutElement>().flexibleWidth = 1;
            title = Label(text, "Title", "", S.Heading, S.SizeTitle, S.TextPrimary);
            subtitle = Label(text, "Reason", "", S.Body, 18, S.TextSecondary);
        }

        void BuildMetrics(Transform card)
        {
            metricsRow = Row(card, "Main metrics", 10, TextAnchor.UpperLeft, true).gameObject;
            (string glyph, string name)[] tiles = { (I.Clock, "Waktu evakuasi"), (I.Distance, "Jarak tempuh"), (I.Exposure, "Paparan bahaya") };
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = Box(metricsRow.transform, "Metric " + tiles[i].name, S.SurfaceSunken, S.Rounded);
                Pad(tile.gameObject, 14, 10, 4);
                Size(tile.gameObject, 100, -1, 1); // Equal thirds, not text-sized.
                IconLabel(tile.transform, "Label", tiles[i].glyph, tiles[i].name, S.Body, S.SizeCaption, S.TextSecondary);
                metricValues[i] = Label(tile.transform, "Value", "", S.Numeric, S.SizeMetric, S.TextPrimary);
            }
            var row = Row(card, "Secondary metrics", 0, TextAnchor.MiddleLeft, true);
            reroutesText = IconLabel(row, "Reroutes", I.Route, "", S.Body, 18, S.TextSecondary).label;
            var contacts = Row(row, "Contacts", 6, TextAnchor.MiddleRight);
            contactsText = Label(contacts, "Hazard contacts", "", S.Body, 18, S.TextSecondary, TextAnchor.MiddleRight);
            secondaryRow = row.gameObject;
        }

        void BuildComparison(Transform card)
        {
            var table = Column(card, "Same seed comparison", 0);
            comparisonTable = table.gameObject;
            string[] rows = { "Hasil", "Waktu evakuasi", "Paparan bahaya", "Kontak dengan area bahaya", "Perubahan rute" };
            var head = TableRow(table, "Header");
            Label(head, "Metric", "", S.Body, S.SizeCaption, S.TextSecondary);
            adaptiveHeader = Cell(head, S.Strong, S.SizeCaption, S.TextSecondary);
            staticHeader = Cell(head, S.Strong, S.SizeCaption, S.TextSecondary);
            for (int i = 0; i < rows.Length; i++)
            {
                Divider(table);
                var row = TableRow(table, rows[i]);
                Label(row, "Metric", rows[i], S.Body, 18, S.TextSecondary);
                adaptiveCells[i] = Cell(row, S.Numeric, 18, S.TextPrimary);
                staticCells[i] = Cell(row, S.Numeric, 18, S.TextPrimary);
            }
        }

        void BuildDetail(Transform card)
        {
            Divider(card);
            detailToggle = Link(card, I.ChevronRight, "Lihat detail sistem", () => showDetail = !showDetail);
            detailText = Label(card, "System detail", "", S.Body, S.SizeCaption, S.TextSecondary);
        }

        void BuildActions(Transform card)
        {
            var buttons = Row(card, "Actions", 10, TextAnchor.MiddleLeft, true);
            primary = Primary(buttons, I.Restart, "", OnPrimary);
            secondary = Secondary(buttons, I.Compare, "", OnSecondary);
            secondary.button.GetComponent<LayoutElement>().flexibleWidth = 1.35f;
            primary.button.name = "Primary action"; secondary.button.name = "Secondary action"; // Labels change per state.

            var links = Row(card, "Links", 0, TextAnchor.MiddleLeft, true);
            exportLink = Link(links, I.Download, "Ekspor data", () => simulation.Export());
            menuLink = Link(links, I.Back, "Kembali ke menu", ReturnToMenu, TextAnchor.MiddleRight);
            exportStatus = Label(card, "Export status", "", S.Body, S.SizeCaption, S.TextSecondary);
        }

        void BuildCameraSettings(Transform card)
        {
            var section = Column(card, "FPP camera", 8);
            cameraRow = section.gameObject;
            Divider(section);
            IconLabel(section, "Heading", I.Camera, "Kamera FPP", S.Strong, 18, S.TextPrimary);
            var controls = Row(section, "Camera controls", 16, TextAnchor.UpperLeft, true);
            sensitivityValue = Setting(controls, "Mouse sensitivity",
                Secondary(null, null, "−", () => simulation.mouseSensitivity = Mathf.Max(.02f, simulation.mouseSensitivity - .01f), 38),
                Secondary(null, null, "+", () => simulation.mouseSensitivity = Mathf.Min(.3f, simulation.mouseSensitivity + .01f), 38));
            fovValue = Setting(controls, "Field of view",
                Secondary(null, null, "−", () => simulation.firstPersonFieldOfView = Mathf.Max(60, simulation.firstPersonFieldOfView - 2), 38),
                Secondary(null, null, "+", () => simulation.firstPersonFieldOfView = Mathf.Min(95, simulation.firstPersonFieldOfView + 2), 38));
            headBob = Secondary(null, null, "", () => simulation.headBobAmount = simulation.headBobAmount > 0 ? 0 : .012f, 38);
            headBobValue = Setting(controls, "Head bob", headBob);
        }

        // Caption above its buttons, so labels never have to fit inside a narrow button.
        static Text Setting(Transform parent, string name, params ButtonView[] buttons)
        {
            var group = Column(parent, name, 6);
            Size(group.gameObject, 100, -1, 1);
            var caption = Label(group, "Value", "", S.Body, S.SizeCaption, S.TextSecondary);
            var row = Row(group, "Buttons", 6, TextAnchor.MiddleLeft, true);
            foreach (var button in buttons)
            {
                button.surface.transform.SetParent(row, false);
                Size(button.surface.gameObject, 40, 38, 1); // Share the group's width instead of the default 100 minimum.
            }
            return caption;
        }

        public void Refresh(bool visible)
        {
            Root.SetActive(visible);
            if (!visible) return;
            var s = simulation;
            bool paused = s.State == SessionState.Paused, success = s.State == SessionState.Success;
            bool comparing = !paused && comparison != null && comparison.adaptive != s.Adaptive && comparison.seed == s.ActiveSeed &&
                comparison.mode == s.Mode && comparison.scenario == s.ActiveScenario && comparison.source == s.ActiveHazardSource;

            badge.gameObject.SetActive(!comparing);
            badge.color = paused ? S.SurfaceRaised : success ? S.Safe : S.Danger;
            badgeGlyph.text = paused ? "" : success ? I.Check : I.Cross;
            pauseBars.SetActive(paused);
            badgeGlyph.color = paused ? S.TextPrimary : success ? S.OnSafe : S.OnDanger;
            title.text = comparing ? "Perbandingan navigasi" : paused ? "Simulasi dijeda" : success ? "Evakuasi berhasil" : "Evakuasi terhalang";
            subtitle.text = comparing ? "Seed dan skenario sama. Baca hasil sebelum membandingkan waktu."
                : paused ? "Waktu simulasi berhenti. Lanjutkan kapan saja."
                : success ? "Pekerja mencapai zona aman " + (s.TargetExit + 1) : BlockedReason(s);

            SetChip(modeChip, s.Mode == MiningMode.Story ? I.Watch : I.Walk, s.Mode == MiningMode.Story ? "Mode Cerita" : "Mode FPP");
            SetChip(navigationChip, s.Adaptive ? I.Route : I.RouteFixed, s.Adaptive ? "Navigasi adaptif" : "Navigasi statis");
            SetChip(scenarioChip, ScenarioIcon(s.ActiveScenario), "Skenario " + MiningHUD.ScenarioName(s.ActiveScenario).ToLowerInvariant());
            SetChip(sourceChip, SourceIcon(s.ActiveHazardSource), MiningHUD.SourceName(s.ActiveHazardSource));

            string quality = QualityNote(s.EdgeSession != null && s.EdgeSession.DataLoss, s.EdgeSession != null && s.EdgeSession.DataStale,
                s.EdgeSession != null && s.EdgeSession.HadDisconnect);
            if (comparing && string.IsNullOrEmpty(quality)) quality = QualityNote(comparison.dataLoss, comparison.dataStale, comparison.hadDisconnect);
            qualityBadge.SetActive(!string.IsNullOrEmpty(quality)); qualityText.text = quality;

            metricsRow.SetActive(!comparing); secondaryRow.SetActive(!comparing); comparisonTable.SetActive(comparing);
            if (comparing) FillComparison(s);
            else
            {
                metricValues[0].text = Number(s.Elapsed.ToString("F1"), "s");
                metricValues[1].text = Number(s.Travelled.ToString("F0"), "m");
                metricValues[2].text = Number(s.Exposure.ToString("F1"), "s");
                metricValues[2].color = s.Exposure > 0 ? S.Warning : S.TextPrimary;
                reroutesText.text = "Perubahan rute  " + Strong(s.Reroutes.ToString());
                contactsText.text = "Kontak dengan area bahaya  " + Strong(s.HazardContacts.ToString());
            }

            detailToggle.label.text = showDetail ? "Sembunyikan detail sistem" : "Lihat detail sistem";
            detailToggle.icon.text = showDetail ? I.ChevronDown : I.ChevronRight;
            detailText.gameObject.SetActive(showDetail);
            detailText.text = "Seed " + s.ActiveSeed + "   ·   Sumber " + MiningHUD.SourceName(s.ActiveHazardSource) +
                (s.EdgeSession != null ? "   ·   Sesi " + s.EdgeSession.SessionId.Substring(0, 8) : "") +
                "\nWaktu hitung rute maks. " + (comparing
                    ? "adaptif " + (comparison.adaptive ? comparison.maxResponseMs : s.MaxResponseMs).ToString("F3") + " ms, statis " +
                        (comparison.adaptive ? s.MaxResponseMs : comparison.maxResponseMs).ToString("F3") + " ms"
                    : s.MaxResponseMs.ToString("F3") + " ms") +
                ". Ini waktu komputasi lokal, bukan latensi jaringan.";

            primary.label.text = paused ? "Lanjutkan" : "Ulangi skenario";
            primary.icon.text = paused ? I.Play : I.Restart;
            secondary.label.text = paused ? "Kembali ke menu" : s.Adaptive ? "Bandingkan dengan statis" : "Bandingkan dengan adaptif";
            secondary.icon.text = paused ? I.Back : I.Compare;
            secondary.button.gameObject.SetActive(!comparing); // The comparison is already on screen.
            menuLink.button.gameObject.SetActive(!paused);
            exportLink.label.text = paused ? "Ekspor data sementara" : "Ekspor data";
            bool exported = !string.IsNullOrEmpty(s.ExportStatus);
            exportStatus.gameObject.SetActive(exported);
            exportStatus.text = s.ExportStatus;
            exportStatus.color = exported && s.ExportStatus.StartsWith("Ekspor gagal") ? S.Danger : S.TextSecondary;

            cameraRow.SetActive(paused && s.Mode == MiningMode.FirstPerson);
            sensitivityValue.text = "Sensitivitas mouse  " + Strong(s.mouseSensitivity.ToString("F2"));
            fovValue.text = "Sudut pandang  " + Strong(s.firstPersonFieldOfView.ToString("F0") + "°");
            headBobValue.text = "Ayunan kamera";
            headBob.label.text = s.headBobAmount > 0 ? "Aktif" : "Mati";
        }

        internal static string ScenarioIcon(HazardScenarioMode scenario)
            => scenario == HazardScenarioMode.Scripted ? I.Controlled : scenario == HazardScenarioMode.NoHazards ? I.NoHazard : I.Shuffle;

        internal static string SourceIcon(HazardSource source)
            => source == HazardSource.MqttEdgeSimulation ? I.Broadcast : source == HazardSource.LocalEdgeSimulation ? I.Cpu : I.Schedule;

        static void SetChip(IconText chip, string glyph, string label) { chip.icon.text = glyph; chip.label.text = label; }

        void FillComparison(MiningSimulation s)
        {
            bool firstAdaptive = comparison.adaptive;
            var adaptive = firstAdaptive ? comparison : new SessionSnapshot(s);
            var baseline = firstAdaptive ? new SessionSnapshot(s) : comparison;
            adaptiveHeader.text = firstAdaptive ? "Adaptif" : "Adaptif (sesi ini)";
            staticHeader.text = firstAdaptive ? "Statis (sesi ini)" : "Statis";
            FillColumn(adaptiveCells, adaptive); FillColumn(staticCells, baseline);
        }

        static void FillColumn(Text[] cells, SessionSnapshot snapshot)
        {
            bool ok = snapshot.outcome == SessionState.Success;
            cells[0].text = ok ? "Berhasil" : "Terhalang"; cells[0].color = ok ? S.Safe : S.Danger; cells[0].font = S.Strong;
            cells[1].text = snapshot.elapsed.ToString("F1") + " s";
            cells[2].text = snapshot.exposure.ToString("F1") + " s"; cells[2].color = snapshot.exposure > 0 ? S.Warning : S.TextPrimary;
            cells[3].text = snapshot.contacts.ToString();
            cells[4].text = snapshot.reroutes.ToString();
        }

        void OnPrimary()
        {
            if (simulation.State == SessionState.Paused) simulation.TogglePause();
            else Restart(simulation.Adaptive, false);
        }

        void OnSecondary()
        {
            if (simulation.State == SessionState.Paused) { ReturnToMenu(); return; }
            comparison = new SessionSnapshot(simulation);
            Restart(!simulation.Adaptive, true);
        }

        void ReturnToMenu() { comparison = null; showDetail = false; simulation.Menu(); }

        void Restart(bool adaptive, bool keepComparison)
        {
            if (!keepComparison) comparison = null;
            simulation.scenarioSeed = simulation.ActiveSeed;
            simulation.scenarioMode = simulation.ActiveScenario;
            simulation.hazardSource = simulation.ActiveHazardSource;
            showDetail = false; beforeRestart?.Invoke();
            simulation.Begin(simulation.Mode, adaptive);
        }

        static string BlockedReason(MiningSimulation s)
        {
            switch (s.Phase)
            {
                case "Baseline terhalang": return "Rute statis tertutup longsor";
                case "Tidak ada rute aman": return "Tidak ada rute aman yang tersisa";
                case "Terpapar longsor": return "Longsor terjadi di posisi pekerja";
                default: return s.Dialogue;
            }
        }

        static string QualityNote(bool dataLoss, bool stale, bool hadDisconnect)
        {
            if (dataLoss) return "Data edge tidak lengkap. Ulangi sesi sebelum dipakai sebagai hasil eksperimen.";
            if (hadDisconnect) return "MQTT sempat terputus. Pisahkan hasil ini dari eksperimen normal.";
            if (stale) return "Data edge belum mutakhir. Tinjau hasil sebelum dipakai.";
            return "";
        }

        static string Number(string value, string unit)
            => value + "<size=" + S.SizeCaption + "><color=#9AA5AD> " + unit + "</color></size>";

        static string Strong(string value) => "<color=#E8ECEF>" + value + "</color>";

        static Transform TableRow(Transform parent, string name)
        {
            var row = Row(parent, name, 12);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 7, 7);
            return row;
        }

        static Text Cell(Transform row, Font font, int size, Color color)
        {
            var text = Label(row, "Value", "", font, size, color, TextAnchor.MiddleRight);
            Size(text.gameObject, 150, -1, 0);
            row.GetChild(0).GetComponent<LayoutElement>().flexibleWidth = 1;
            return text;
        }
    }
}
