using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SafeMining
{
    public class MiningHUD : MonoBehaviour
    {
        public MiningSimulation Simulation;
        readonly Color cyan = new Color(.16f, .85f, .91f);
        readonly Color green = new Color(.22f, 1, .66f);
        readonly Color muted = new Color(.58f, .69f, .74f);
        readonly Color panel = new Color(.025f, .062f, .086f, .94f);
        Font font;
        Transform canvas, menu, hud, modal, glasses;
        Text phase, mode, dialogue, direction, distance, metrics, hazard, timer, modalTitle, modalBody, exportStatus, navButtonLabel;
        bool adaptive = true;
        Image dangerWash;
        MineMapGraphic map;

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
            var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.18f, .38f, .46f, .7f); outline.effectDistance = new Vector2(1, -1); return card;
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
            var r = Panel(parent, value, new Vector2(.5f, .5f), new Vector2(.5f, .5f), pos, size, primary ? new Color(.08f, .62f, .56f) : new Color(.09f, .18f, .23f));
            r.GetComponent<Image>().raycastTarget = true; var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = r.GetComponent<Image>(); button.onClick.AddListener(action);
            Label(r, "Label", value, Vector2.zero, size - new Vector2(14, 8), 19, Color.white, TextAnchor.MiddleCenter);
            return button;
        }
        void BuildMenu()
        {
            menu = Panel(canvas, "Mode selection", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.015f, .035f, .055f, .92f));
            Label(menu, "Eyebrow", "PENS   /   VIRTUAL UNDERGROUND MINE", new Vector2(0, 330), new Vector2(1100, 35), 19, cyan);
            Label(menu, "Title", "SAFE-MINING <color=#43F0B4>EVAC</color>", new Vector2(0, 267), new Vector2(1100, 80), 57, Color.white);
            Label(menu, "Subtitle", "Kenali bahaya. Temukan jalan pulang.", new Vector2(0, 198), new Vector2(1100, 45), 27, muted);
            var story = Card(menu, "Story card", new Vector2(.5f, .5f), new Vector2(-285, -18), new Vector2(530, 325));
            Label(story, "Number", "01  /  PELAJARI SKENARIO", new Vector2(0, 119), new Vector2(466, 30), 17, cyan);
            Label(story, "Heading", "Mode Cerita", new Vector2(0, 63), new Vector2(466, 60), 37, Color.white);
            Label(story, "Body", "Ikuti pekerja tambang menjalani evakuasi otomatis. Amati peringatan, longsor, dan perubahan rute melalui dialog tim.", new Vector2(0, -17), new Vector2(466, 96), 21, muted);
            Button(story, "Mulai Mode Cerita  >", new Vector2(0, -112), new Vector2(466, 56), () => Simulation.Begin(MiningMode.Story, adaptive), true);
            var fpp = Card(menu, "FPP card", new Vector2(.5f, .5f), new Vector2(285, -18), new Vector2(530, 325));
            Label(fpp, "Number", "02  /  TENTUKAN PILIHANMU", new Vector2(0, 119), new Vector2(466, 30), 17, cyan);
            Label(fpp, "Heading", "Mode FPP", new Vector2(0, 63), new Vector2(466, 60), 37, Color.white);
            Label(fpp, "Body", "Kendalikan pekerja dari sudut pandang pertama. Gunakan kacamata AR untuk menemukan jalur aman dan hindari longsor.", new Vector2(0, -17), new Vector2(466, 96), 21, muted);
            Button(fpp, "Mulai Mode FPP  >", new Vector2(0, -112), new Vector2(466, 56), () => Simulation.Begin(MiningMode.FirstPerson, adaptive), true);
            var navigation = Button(menu, "Navigasi: ADAPTIF  |  klik untuk pembanding STATIS", new Vector2(0, -237), new Vector2(1100, 52), () =>
            { adaptive = !adaptive; navButtonLabel.text = adaptive ? "Navigasi: ADAPTIF  |  klik untuk pembanding STATIS" : "Navigasi: STATIS / BASELINE  |  jalur awal tetap"; });
            navButtonLabel = navigation.GetComponentInChildren<Text>();
            Label(menu, "Controls", "WASD / joystick: bergerak     Mouse / stick kanan: melihat     Shift: cepat     Esc: jeda", new Vector2(0, -309), new Vector2(1100, 36), 18, muted, TextAnchor.MiddleCenter);
            Label(menu, "Research", "Simulasi penelitian berdasarkan abstrak SAFE-MINING EVAC  |  kondisi dan sensor dimodelkan secara virtual", new Vector2(0, -370), new Vector2(1300, 30), 16, muted, TextAnchor.MiddleCenter);
        }
        void BuildHUD()
        {
            var banner = Card(hud, "Mission", new Vector2(0, 1), new Vector2(298, -79), new Vector2(540, 110));
            mode = Label(banner, "Mode", "", new Vector2(0, 28), new Vector2(496, 28), 16, cyan);
            phase = Label(banner, "Phase", "", new Vector2(0, -12), new Vector2(496, 48), 24, Color.white);
            var radar = Card(hud, "Map", new Vector2(1, 1), new Vector2(-151, -200), new Vector2(246, 352));
            Label(radar, "Map label", "PETA EVAKUASI  /  N", new Vector2(0, 146), new Vector2(214, 28), 17, cyan);
            var mapObject = new GameObject("Live mine topology", typeof(RectTransform), typeof(CanvasRenderer), typeof(MineMapGraphic)); mapObject.transform.SetParent(radar, false);
            var rect = mapObject.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(208, 250); rect.anchoredPosition = new Vector2(0, -1);
            map = mapObject.GetComponent<MineMapGraphic>(); map.Simulation = Simulation; map.raycastTarget = false;
            Label(radar, "Legend", "<color=#43F0B4>Aman</color>   <color=#FF6559>Bahaya</color>   <color=#FFFFFF>Anda</color>", new Vector2(0, -145), new Vector2(224, 27), 15, muted, TextAnchor.MiddleCenter);
            var info = Card(hud, "Metrics", new Vector2(1, 1), new Vector2(-151, -456), new Vector2(246, 124));
            timer = Label(info, "Timer", "", new Vector2(0, 30), new Vector2(212, 37), 28, Color.white);
            metrics = Label(info, "Metrics", "", new Vector2(0, -20), new Vector2(212, 57), 16, muted);
            var bottom = Card(hud, "Team dialogue", new Vector2(0, 0), new Vector2(342, 94), new Vector2(628, 132));
            Label(bottom, "Speaker", "RADIO TIM  /  PUSAT KENDALI", new Vector2(0, 40), new Vector2(582, 25), 16, cyan);
            dialogue = Label(bottom, "Dialogue", "", new Vector2(0, -15), new Vector2(582, 82), 20, Color.white);
            var guide = Card(hud, "AR direction", new Vector2(.5f, 0), new Vector2(190, 96), new Vector2(360, 136));
            Label(guide, "AR label", "SAFETY GLASSES / NAVIGASI", new Vector2(0, 40), new Vector2(320, 25), 15, cyan, TextAnchor.MiddleCenter);
            direction = Label(guide, "Direction", "", new Vector2(0, 0), new Vector2(330, 43), 27, green, TextAnchor.MiddleCenter);
            distance = Label(guide, "Distance", "", new Vector2(0, -41), new Vector2(330, 26), 17, muted, TextAnchor.MiddleCenter);
            hazard = Label(hud, "Hazard alert", "", new Vector2(0, 255), new Vector2(720, 55), 24, new Color(1, .45f, .3f), TextAnchor.MiddleCenter);
            Label(hud, "Control hints", "ESC  Jeda     TAB  Ganti mode & ulang     R  Ulang     G  Kacamata", new Vector2(0, -426), new Vector2(1000, 26), 15, muted, TextAnchor.MiddleCenter);
            Button(banner, "II", new Vector2(237, 30), new Vector2(36, 34), () => Simulation.TogglePause());
            glasses = Panel(hud, "Safety glasses rim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 })
            {
                Vector2 anchor = new Vector2(sx < 0 ? 0 : 1, sy < 0 ? 0 : 1);
                Panel(glasses, "Lens horizontal rim", anchor, anchor, new Vector2(-sx * 76, -sy * 20), new Vector2(116, 3), new Color(cyan.r, cyan.g, cyan.b, .65f));
                Panel(glasses, "Lens vertical rim", anchor, anchor, new Vector2(-sx * 20, -sy * 72), new Vector2(3, 105), new Color(cyan.r, cyan.g, cyan.b, .65f));
            }
            Label(glasses, "Reticle", "+", Vector2.zero, new Vector2(30, 30), 22, new Color(.5f, 1, 1, .65f), TextAnchor.MiddleCenter);
            dangerWash = Panel(hud, "Danger proximity", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>();
            dangerWash.transform.SetAsFirstSibling();
        }
        void BuildModal()
        {
            modal = Panel(canvas, "Pause and result", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.01f, .025f, .04f, .9f));
            var card = Card(modal, "Result", new Vector2(.5f, .5f), Vector2.zero, new Vector2(790, 625));
            modalTitle = Label(card, "Title", "", new Vector2(0, 237), new Vector2(710, 64), 38, green, TextAnchor.MiddleCenter);
            modalBody = Label(card, "Summary", "", new Vector2(0, 78), new Vector2(682, 220), 23, Color.white);
            Button(card, "Lanjut / Ulang", new Vector2(-177, -86), new Vector2(326, 54), () => { if (Simulation.State == SessionState.Paused) Simulation.TogglePause(); else Simulation.Begin(Simulation.Mode, Simulation.Adaptive); }, true);
            Button(card, "Pilih mode", new Vector2(177, -86), new Vector2(326, 54), () => Simulation.Menu());
            Button(card, "Ekspor hasil evaluasi (.csv)", new Vector2(0, -161), new Vector2(682, 52), () => Simulation.Export());
            exportStatus = Label(card, "Export location", "", new Vector2(0, -245), new Vector2(690, 91), 15, muted, TextAnchor.MiddleCenter);
        }
        void Update()
        {
            if (menu == null) return;
            var s = Simulation;
            menu.gameObject.SetActive(s.State == SessionState.Menu);
            hud.gameObject.SetActive(s.State != SessionState.Menu);
            bool showResult = s.State == SessionState.Paused || s.State == SessionState.Success || s.State == SessionState.Blocked;
            modal.gameObject.SetActive(showResult);
            mode.text = "SAFE-MINING EVAC  /  " + (s.Mode == MiningMode.Story ? "MODE CERITA" : "MODE FPP");
            phase.text = s.Phase;
            dialogue.text = s.Dialogue;
            bool hasGlasses = s.Mode == MiningMode.Story || s.GlassesEnabled;
            direction.text = hasGlasses ? s.DirectionHint() : "KACAMATA NONAKTIF";
            distance.text = hasGlasses && s.TargetExit >= 0 ? "Zona aman " + (s.TargetExit + 1) + "  /  " + s.RouteDistance.ToString("F0") + " m sepanjang rute" : "Tekan G untuk kacamata";
            timer.text = s.Elapsed.ToString("F1") + " s";
            metrics.text = (s.Adaptive ? "ADAPTIF" : "STATIS / BASELINE") + "  |  " + s.Reroutes + " perubahan\nRespons hitung " + s.ResponseMs.ToString("F2") + " ms";
            float danger = s.NearestHazardDistance();
            hazard.text = danger < 13 ? "!  ZONA BAHAYA  /  " + danger.ToString("F0") + " m  -  JAGA JARAK" : "";
            dangerWash.color = new Color(1, .08f, .01f, Mathf.Clamp01((8 - danger) / 8) * .18f);
            glasses.gameObject.SetActive(s.Mode == MiningMode.FirstPerson && s.GlassesEnabled);
            map.SetVerticesDirty();
            if (showResult)
            {
                modalTitle.text = s.State == SessionState.Success ? "EVAKUASI BERHASIL" : s.State == SessionState.Paused ? "SIMULASI DIJEDA" : "RUTE TERHALANG";
                modalBody.text = (s.Mode == MiningMode.Story ? "Mode Cerita" : "Mode FPP") + "  /  " + (s.Adaptive ? "Navigasi adaptif" : "Baseline statis") +
                    "\n\nWaktu simulasi       " + s.Elapsed.ToString("F1") + " detik" +
                    "\nJarak tempuh          " + s.Travelled.ToString("F1") + " m" +
                    "\nPaparan bahaya       " + s.Exposure.ToString("F1") + " detik / " + s.HazardContacts + " kontak" +
                    "\nPerubahan rute        " + s.Reroutes + " kali" +
                    "\nRespons hitung maks.  " + s.MaxResponseMs.ToString("F3") + " ms";
                exportStatus.text = string.IsNullOrEmpty(s.ExportStatus) ? "Paparan = waktu di perimeter bahaya. Respons = waktu komputasi lokal, bukan latensi jaringan.\nUlangi skenario dengan baseline statis untuk membandingkan hasil." : s.ExportStatus;
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public class MineMapGraphic : MaskableGraphic
    {
        public MiningSimulation Simulation;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Simulation == null || Simulation.Cells == null) return;
            Rect r = rectTransform.rect; float scale = Mathf.Min(r.width / 12, r.height / 18);
            Vector2 Origin(Vector2Int p) => new Vector2(r.center.x + p.x * scale, r.center.y + (p.y - 3) * scale);
            foreach (var cell in Simulation.Cells) Rect(vh, Origin(cell), new Vector2(scale * .93f, scale * .93f), new Color(.16f, .27f, .31f));
            if (Simulation.GlassesEnabled || Simulation.Mode == MiningMode.Story)
                foreach (var cell in Simulation.Route) Rect(vh, Origin(cell), Vector2.one * (scale * .45f), new Color(.05f, .85f, .58f));
            for (int i = 0; i < MineLayout.HazardCells.Length; i++)
                if (Simulation.HazardLevels[i] > 0) Rect(vh, Origin(MineLayout.HazardCells[i]), Vector2.one * scale * .9f, Simulation.HazardLevels[i] == 2 ? new Color(1, .18f, .12f) : new Color(1, .65f, .08f));
            foreach (var exit in MineLayout.Exits) Rect(vh, Origin(exit), Vector2.one * scale * .85f, new Color(.3f, 1, .67f));
            if (Simulation.Actor == null) return;
            Vector3 p = Simulation.Actor.position;
            Vector2 location = new Vector2(r.center.x + p.x / MineLayout.CellSize * scale, r.center.y + (p.z / MineLayout.CellSize - 3) * scale);
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
