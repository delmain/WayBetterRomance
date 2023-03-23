using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Verse;

namespace BetterRomance.HarmonyPatches
{
    public static class TweaksGalore_Patches
    {
        public static void PatchTweaksGalore(this Harmony harmony)
        {
            var tgIsSexualityTrait = typeof(TweaksGalore.Patch_PawnGenerator_GenerateTraits).GetMethod(nameof(TweaksGalore.Patch_PawnGenerator_GenerateTraits.IsSexualityTrait));
            var prefixIsSexualityTrait = typeof(TG_PawnGenerator_GenerateTraits_IsSexualityTrait).GetMethod(nameof(TG_PawnGenerator_GenerateTraits_IsSexualityTrait.Prefix));
            harmony.Patch(tgIsSexualityTrait, prefix: new HarmonyMethod(prefixIsSexualityTrait));

            LogUtil.Message("TweaksGalore patches applied.");

            var timer = new System.Threading.Timer(
                e => TimerCheck(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(100));
            curTime.Restart();
        }

        public static void WritePawnTraits(string method, Pawn pawn)
        {
            var message = $"pawn: {pawn.ToStringSafe()} | traits: {string.Join(",", (pawn?.story?.traits?.allTraits ?? new List<Trait>()).Select(t => t.Label))}";
            WritePawnMessage(method, pawn, message);
        }

        public static void WritePawnMessage(string method, Pawn pawn, string message)
        {
            if (pawn == null)
                return;

            var sb = cacheDict.GetOrAdd(pawn, p => new StringBuilder());
            sb.AppendLine($"WBR: {method} - {message}");

            timerDict.AddOrUpdate(pawn, curTime.ElapsedMilliseconds, (p, prev) => curTime.ElapsedMilliseconds);

        }

        private static ConcurrentDictionary<Pawn, StringBuilder> cacheDict = new ConcurrentDictionary<Pawn, StringBuilder>();
        private static ConcurrentDictionary<Pawn, long> timerDict = new ConcurrentDictionary<Pawn, long>();
        private static Stopwatch curTime = new Stopwatch();
        private static void TimerCheck()
        {
            List<Pawn> toBeRemoved = null;
            foreach (var kvp in timerDict.ToList())
            {
                if (kvp.Value < (curTime.ElapsedMilliseconds - 1000)) // More than a second ago
                {
                    var pawn = kvp.Key;

                    if (toBeRemoved == null)
                        toBeRemoved = new List<Pawn>();
                    toBeRemoved.Add(pawn);

                    if (cacheDict.TryRemove(pawn, out StringBuilder sb))
                    {
                        Log.Message(sb.ToString());
                        sb.Clear();
                    }
                    else
                        Log.Error($"WBR: Trying to clear message cache for {pawn.ToStringSafe()} but TryRemove failed");
                }
            }
            foreach (var remove in toBeRemoved)
            {
                if (!timerDict.TryRemove(remove, out _))
                    Log.Error($"WBR: Trying to clear timer cache for {remove.ToStringSafe()} but TryRemove failed");
            }
        }
    }

    public static class TG_PawnGenerator_GenerateTraits_IsSexualityTrait
    {
        public static bool Prefix(Trait trait, ref bool __result)
        {
            var ret = RomanceUtilities.OrientationTraits.Contains(trait.def);
            LogUtil.Message($"TG_PawnGenerator_GenerateTraits_IsSexualityTrait Prefix - trait: {trait.Label} | isSexualityTrait: {ret}");
            __result = ret;
            return false;
        }
    }
}
