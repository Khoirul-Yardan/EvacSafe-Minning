using System;
using UnityEngine;
using UnityEngine.UI;
using static SafeMining.MiningUiKit;
using S = SafeMining.MiningUiStyle;

namespace SafeMining
{
    // Preserves the local briefing/pre-answer/reflection flow inside the shared UI system.
    public sealed class MiningLearningPanel
    {
        readonly MiningSimulation simulation;
        readonly Action started;
        readonly Text title, body, question, feedback;
        readonly ButtonView[] answers = new ButtonView[3];
        readonly ButtonView next, skip;
        MiningMode mode;
        bool adaptive, reviewing;
        int selected = -1;
        public GameObject Root { get; }
        public bool Visible => Root.activeSelf;
        public Button FirstAnswer => answers[0].button;

        public MiningLearningPanel(Transform canvas, MiningSimulation simulation, Action started)
        {
            this.simulation = simulation; this.started = started;
            var backdrop = Box(canvas, "Learning briefing and reflection", S.Backdrop);
            Stretch(backdrop.rectTransform); backdrop.raycastTarget = true; Root = backdrop.gameObject;
            var card = Box(backdrop.transform, "Learning card", S.Surface, S.Rounded).rectTransform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(760, 0);
            Pad(card.gameObject, 28, 24, 14);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            title = Label(card, "Learning title", "", S.Heading, 32, S.TextPrimary);
            body = Label(card, "Learning briefing", "", S.Body, S.SizeBody, S.TextSecondary);
            Divider(card);
            question = Label(card, "Learning question", "", S.Strong, S.SizeBody, S.TextPrimary);
            string[] choices = {
                "Waspada; cek informasi dan rute aman sebelum kondisi memburuk.",
                "Abaikan bila lorong tampak kosong; lanjutkan seperti biasa.",
                "Tunggu sampai indikator merah sebelum memeriksa jalur alternatif."
            };
            for (int i = 0; i < answers.Length; i++)
            {
                int choice = i;
                answers[i] = Secondary(card, null, choices[i], () => SelectAnswer(choice), 54);
                answers[i].button.name = "Learning answer " + i;
                answers[i].label.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            feedback = Label(card, "Learning feedback", "", S.Body, S.SizeCaption, S.TextSecondary);
            next = Primary(card, MiningIcons.Play, "", Continue);
            skip = Link(card, MiningIcons.ChevronRight, "", Skip);
            Root.SetActive(false);
        }
        public void ShowPre(MiningMode mode, bool adaptive)
        {
            this.mode = mode; this.adaptive = adaptive; reviewing = false;
            title.text = "Sebelum latihan";
            body.text = mode == MiningMode.Story
                ? "Kenali status detektor dan amati perubahan jalur. Pekerja berhenti untuk memeriksa edge sebelum melanjutkan. "
                : "Kenali status detektor dan petunjuk evakuasi. WASD bergerak, mouse melihat, Shift berlari. ";
            body.text += "Pilihan skenario tetap: " + MiningHUD.ScenarioName(simulation.scenarioMode) + ". Latihan terpandu memakai edge lokal dengan data virtual.";
            feedback.text = "Pilih jawaban awal. Jawaban akan dibandingkan dengan refleksi setelah latihan.";
            next.label.text = "Mulai latihan terpandu";
            skip.label.text = "Lewati dan mulai simulasi biasa";
            ResetChoices();
        }
        public void ShowPost()
        {
            reviewing = true; title.text = "Tinjau keputusanmu";
            body.text = (simulation.State == SessionState.Success ? "Kamu mencapai zona aman. " : "Sesi berakhir sebelum zona aman tercapai. ") +
                (simulation.HazardContacts == 0 ? "Tidak ada kontak model yang tercatat. " : "Ada kontak dengan area bahaya pada sesi ini. ") +
                "Kuning berarti waspada, merah berarti jalur tertutup. Periksa rute alternatif sebelum kondisi memburuk.";
            feedback.text = "Pilih jawaban refleksi untuk meninjau pelajaran dari sesi ini.";
            next.label.text = "Kembali ke ringkasan sesi"; skip.label.text = "Lewati refleksi";
            ResetChoices();
        }
        void ResetChoices()
        {
            selected = -1; next.button.interactable = false; feedback.color = S.TextSecondary;
            question.text = "Lampu detektor menyala kuning. Apa artinya dan apa respons aman?";
            foreach (var answer in answers) SetColors(answer.button, S.LineStrong, S.TextSecondary);
            Root.SetActive(true);
        }
        void SelectAnswer(int answer)
        {
            selected = answer; next.button.interactable = true;
            for (int i = 0; i < answers.Length; i++)
                SetColors(answers[i].button, i == answer ? S.Safe : S.LineStrong, i == answer ? S.Safe : S.TextSecondary);
            if (!reviewing)
            {
                feedback.text = "Jawaban awal tersimpan. Setelah latihan, kamu akan menjawab pertanyaan refleksi.";
                return;
            }
            bool correct = answer == 0;
            simulation.RecordLearningPostAnswer(correct);
            string before = simulation.LearningPreCorrect.HasValue
                ? simulation.LearningPreCorrect.Value ? "Sebelum latihan: jawaban tepat." : "Sebelum latihan: status kuning belum dikenali."
                : "";
            feedback.text = (correct ? "Tepat. " : "Belum tepat. ") +
                "Lampu kuning berarti waspada: periksa petunjuk dan rute aman sebelum kondisi memburuk.\n" + before;
            feedback.color = correct ? S.Safe : S.Warning;
        }
        void Continue()
        {
            if (reviewing) { Root.SetActive(false); return; }
            if (selected < 0) return;
            simulation.BeginNewSession(mode, adaptive, true); simulation.RecordLearningPreAnswer(selected == 0);
            Root.SetActive(false); started?.Invoke();
        }
        void Skip()
        {
            Root.SetActive(false);
            if (reviewing) return;
            simulation.BeginNewSession(mode, adaptive); started?.Invoke();
        }
    }
}
