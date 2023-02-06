﻿using HarmonyLib;
using Verse;

namespace BetterRomance.HarmonyPatches
{
    //Orientation traits are now added with a new method, don't allow that method to run in order to use user settings
    //Still ending up with occasional duplicate traits
    [HarmonyPatch(typeof(PawnGenerator), "TryGenerateSexualityTraitFor")]
    public static class PawnGenerator_TryGenerateSexualityTraitFor
    {
        public static bool Prefix(Pawn pawn, bool allowGay)
        {
            //Just use my method instead
            pawn.EnsureTraits();
            //Do anything with the allowGay bool?
            return false;
        }
    }
}