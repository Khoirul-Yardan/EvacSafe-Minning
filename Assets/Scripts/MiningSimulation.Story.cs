using System.Collections.Generic;
using UnityEngine;

namespace SafeMining
{
    public enum StoryStage { Walking, Tremor, Checking, Confirmed, Mapping, Ready }

    public partial class MiningSimulation
    {
        public StoryStage StoryStep => storyStage;
        public bool StoryHolding => Mode == MiningMode.Story && storyStage != StoryStage.Walking;
        public float StoryTremor => StoryHolding ? Mathf.Clamp01((storyLastTremor + 1 - Elapsed) * 2) : 0;
        StoryStage storyStage;
        MiningStoryCues storyCues;
        readonly Dictionary<int, float> storyPending = new Dictionary<int, float>();
        readonly HashSet<int> storyClosed = new HashSet<int>();
        float storyStarted, storyDeadline, storyShotsUntil, storyLastTremor = -10;
        bool storyPlanRequested, storyHazardChanged;
        string storyLocations = "";

        void ResetStory()
        {
            storyStage = StoryStage.Walking;
            storyPending.Clear(); storyClosed.Clear(); storyLocations = "";
            storyStarted = storyDeadline = storyShotsUntil = 0; storyLastTremor = -10;
            storyPlanRequested = storyHazardChanged = false;
            if (leftLeg != null) leftLeg.localRotation = rightLeg.localRotation = leftArm.localRotation = rightArm.localRotation = Quaternion.identity;
            storyCues?.ResetCues();
        }

        void HoldStory(int index, float sampleTime)
        {
            if (Mode != MiningMode.Story || State != SessionState.Running || storyClosed.Contains(index)) return;
            storyPending[index] = sampleTime;
            storyLastTremor = Elapsed;
            if (!StoryHolding)
            {
                storyStarted = Elapsed; storyShotsUntil = Elapsed;
                storyLocations = ""; storyHazardChanged = false;
                storyStage = StoryStage.Tremor;
                storyCues?.ShowNearbySigns();
                LogEvent("story_stop", "environmental_vibration");
            }
            else if (storyStage == StoryStage.Mapping || storyStage == StoryStage.Ready || storyStage == StoryStage.Confirmed)
                storyStage = StoryStage.Checking;
            storyPlanRequested = false;
            leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
            leftArm.localRotation = Quaternion.Euler(-35, 0, -15);
            rightArm.localRotation = Quaternion.Euler(-65, 0, 20);
        }

        void ObserveStoryVibration(EdgeStatusMessage sample)
        {
            if (Mode != MiningMode.Story || State != SessionState.Running ||
                !int.TryParse(sample.deviceId?.Substring(1), out int device)) return;
            int index = device - 1;
            if (index < 0 || index >= HazardLevels.Length) return;
            var settings = EdgeSession.Settings;
            if (sample.vibrationNormalized < settings.warningThreshold) return;
            storyCues?.Observe(index);
            HoldStory(index, sample.simulationTimeS);
        }

        void ObserveStoryHazard(int index, int level)
        {
            if (Mode != MiningMode.Story) return;
            if (level > 0)
            {
                HoldStory(index, Elapsed);
                storyCues?.Observe(index);
            }
            storyHazardChanged = true;
        }

        void ObserveStoryReport(EdgeStatusMessage report)
        {
            if (Mode != MiningMode.Story || !int.TryParse(report.deviceId?.Substring(1), out int device)) return;
            ResolveStoryReport(device - 1, report.level, report.simulationTimeS);
        }

        void ResolveStoryReport(int index, int level, float reportTime)
        {
            if (!StoryHolding || !storyPending.TryGetValue(index, out float lastHigh)) return;
            // A normal snapshot preceding the latest tremor is not an all-clear.
            float clearDuration = EdgeSession != null ? EdgeSession.Settings.clearDuration : 0;
            if (level == 1 || (level == 0 && reportTime + .0001f < lastHigh + clearDuration)) return;
            storyPending.Remove(index);
            if (level == 2 && storyClosed.Add(index))
            {
                storyShotsUntil = Mathf.Max(Elapsed, storyShotsUntil) + MiningLandslideCutscene.ShotSeconds;
                string location = "D" + (index + 1).ToString("00") + " (grid " + HazardSites[index].x + ":" + HazardSites[index].y + ")";
                storyLocations += (storyLocations.Length == 0 ? "" : ", ") + location;
                LogEvent("story_location_confirmed", location);
            }
        }

        void TickStory()
        {
            if (Mode != MiningMode.Story || State != SessionState.Running) return;
            if (!StoryHolding)
            {
                if (Elapsed >= 4 && Phase == "01 / Briefing")
                {
                    Phase = "01 / Berjalan di lorong";
                    Dialogue = "Pekerja: Kita berjalan menuju zona aman. Amati lantai, pipa, dan dinding. Bila ada getaran, berhenti untuk memeriksa laporan edge device.";
                }
                return;
            }
            bool dataReady = EdgeSession == null || (!EdgeSession.DataLoss && !EdgeSession.DataStale);
            if (Elapsed - storyStarted < 1.8f)
            {
                Phase = "02 / Getaran - berhenti";
                Dialogue = "Pekerja: Lantai lorong bergetar, pipa berderak, debu dan retakan tampak di dinding. Berhenti dulu! Tanda ini belum memastikan lokasi longsor.";
                return;
            }
            if (storyPending.Count > 0 || !dataReady)
            {
                storyStage = StoryStage.Checking;
                Phase = "03 / Memeriksa edge device";
                Dialogue = !dataReady
                    ? "Pekerja tetap diam. Laporan edge belum mutakhir; tunggu koneksi dan data perangkat. Jalur belum dinyatakan aman."
                    : "Pekerja: Periksa laporan perangkat di setiap lorong. Status waspada belum memastikan longsor; tunggu konfirmasi lokasi atau laporan normal stabil.";
                if (storyLocations.Length > 0) Dialogue += " Tertutup: " + storyLocations + ".";
                return;
            }
            if (storyStage == StoryStage.Tremor || storyStage == StoryStage.Checking)
            {
                storyStage = StoryStage.Confirmed;
                storyDeadline = Mathf.Max(Elapsed + 2, storyShotsUntil + .1f);
                LogEvent("story_check_complete", storyLocations.Length > 0 ? "closure_confirmed" : "stable_clear");
            }
            if (storyStage == StoryStage.Confirmed)
            {
                Phase = "04 / Hasil pemeriksaan";
                Dialogue = storyLocations.Length > 0
                    ? "Edge mengonfirmasi longsor di " + storyLocations + ". Lihat kamera lokasi. Pekerja tetap diam; lorong merah akan dikeluarkan dari jalur adaptif."
                    : "Edge melaporkan kondisi normal stabil. Tidak ada longsor baru yang terkonfirmasi. Pekerja tetap diam saat jalur diperiksa.";
                if (Elapsed < storyDeadline) return;
                storyStage = StoryStage.Mapping; storyDeadline = Elapsed + 1.5f;
            }
            if (storyStage == StoryStage.Mapping)
            {
                Phase = "05 / Memetakan jalur";
                Dialogue = Adaptive
                    ? "Sistem menghitung jalur dari posisi pekerja yang berhenti, menghindari lorong tertutup. Tunggu hasil pemetaan sebelum bergerak."
                    : "Pembanding statis mempertahankan jalur awal. Sistem memeriksa apakah jalur itu terhalang; tidak mencari jalur pengganti.";
                if (Elapsed < storyDeadline) return;
                if (!storyPlanRequested)
                {
                    storyPlanRequested = true;
                    LogEvent("story_plan_requested", "after_edge_check");
                    if (Adaptive)
                    {
                        Plan(storyHazardChanged);
                        EdgeSession?.TraceNavigation("story_replan", "confirmed_world_revision=" + hazardRevision);
                    }
                    else if (UsesEdgePlanner && EdgeRouteSession.NeedsRequest) Plan(false);
                }
                if (NavigationWaiting || State != SessionState.Running) return;
                if (!Adaptive && Route.Exists(cell => Blocked.Contains(cell)))
                {
                    State = SessionState.Blocked; TerminalReason = "static_route"; Phase = "Baseline terhalang";
                    Dialogue = "Pemeriksaan selesai: longsor menutup rute awal. Pekerja tetap berhenti karena navigasi statis tidak memetakan jalur pengganti.";
                    LogEvent("blocked", "static_route"); SetCursor(); return;
                }
                storyStage = StoryStage.Ready; storyDeadline = Elapsed + 1.5f;
                Phase = "06 / Jalur siap";
                Dialogue = "Pekerja: Pemeriksaan selesai. Jalur ke zona aman " + (TargetExit + 1) + " sudah tersedia. Kita lanjut mengikuti petunjuk.";
            }
            if (storyStage == StoryStage.Ready && Elapsed >= storyDeadline)
            {
                storyStage = StoryStage.Walking; Phase = "07 / Melanjutkan evakuasi";
                LogEvent("story_resume", "route_ready");
            }
        }
    }
}
