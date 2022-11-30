﻿using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace BetterRomance.HarmonyPatches
{
    //This stops asexual pawns from initiating lovin' while in bed with a partner
    [HarmonyPatch(typeof(JobGiver_DoLovin), "TryGiveJob")]
    public static class JobGiver_DoLovin_TryGiveJob
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn.IsAsexual())
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}