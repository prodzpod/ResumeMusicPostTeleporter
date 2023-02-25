using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2;
using RoR2.WwiseUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ResumeMusicPostTeleporter
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class ResumeMusicPostTeleporterPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "ResumeMusicPostTeleporter";
        public const string PluginVersion = "1.0.2";
        public static ManualLogSource Log;
        internal static PluginInfo pluginInfo;
        public static ConfigFile Config;
        public static ConfigEntry<bool> UseTeleporterMusic;
        public static ConfigEntry<bool> SubstituteOtherMusic;
        public static List<string> ResumeMusicList;
        public static ConfigEntry<float> ResumeDelay;
        public static ConfigEntry<bool> NoTeleporterMusic;
        public static bool musicPlaying;
        public static float resumeDelay = 0;

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            Config = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);
            UseTeleporterMusic = Config.Bind("General", "Use Teleporter Music Instead", false, "If true, changes functionality to not stop teleporter music post charge.");
            SubstituteOtherMusic = Config.Bind("General", "Substitute Other Music for Blacklist", true, "If true, if the main/boss music is in the blacklist, resume the other one if possible.");
            ResumeMusicList = Config.Bind("General", "Resumable Music Blacklist", "", "Music to not resume, separated by comma. See logs to find currently playing song's internal name.").Value.Split(',').ToList().ConvertAll(x => x.Trim());
            ResumeDelay = Config.Bind("General", "Music Resume Delay", 5f, "Seconds to wait before resuming music.");
            NoTeleporterMusic = Config.Bind("General", "No Teleporter Music", false, "stage music forever");
            On.RoR2.MusicTrackDef.Play += (orig, self) => { orig(self); Log.LogDebug("Current Song: " + (self.cachedName ?? "None")); };
            On.RoR2.MusicTrackDef.Stop += (orig, self) => { orig(self); Log.LogDebug("Previous Song: " + (self.cachedName ?? "None")); };
            Stage.onStageStartGlobal += _ => { musicPlaying = false; resumeDelay = 0; };
            On.RoR2.MusicController.UpdateTeleporterParameters += (orig, self, teleporter, cameraTransform, targetBody) =>
            {
                orig(self, teleporter, cameraTransform, targetBody);
                if (!teleporter) return;
                if (musicPlaying) self.stBossStatus.valueId = CommonWwiseIds.alive;
            };
            MusicController.pickTrackHook += (MusicController self, ref MusicTrackDef track) =>
            {
                if (SceneCatalog.mostRecentSceneDef?.mainTrack != null && NoTeleporterMusic.Value)
                {
                    track = SceneCatalog.mostRecentSceneDef.mainTrack;
                    return;
                }
                if (!(TeleporterInteraction.instance?.isCharged ?? false)) return;
                resumeDelay += Time.deltaTime;
                if (resumeDelay < ResumeDelay.Value) return;
                List<MusicTrackDef> list = new() { SceneCatalog.mostRecentSceneDef.mainTrack, SceneCatalog.mostRecentSceneDef.bossTrack };
                if (UseTeleporterMusic.Value) list.Reverse();
                if (!SubstituteOtherMusic.Value) list.RemoveAt(1);
                MusicTrackDef newTrack = GetAvailableTrack(list);
                if (newTrack == null) return;
                if (track != newTrack) track = newTrack;
                musicPlaying = true;
            };
        }
        public static MusicTrackDef GetAvailableTrack(IEnumerable<MusicTrackDef> list)
        {
            foreach (var track in list)
            {
                if (track?.cachedName == null || ResumeMusicList.Contains(track.cachedName)) continue;
                return track;
            }
            return null;
        }
    }
}
