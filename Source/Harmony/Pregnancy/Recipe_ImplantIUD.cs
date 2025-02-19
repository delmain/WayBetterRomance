﻿using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection.Emit;

namespace BetterRomance.HarmonyPatches
{
    [HarmonyPatch(typeof(Recipe_ImplantIUD), "AvailableOnNow")]
    public static class Recipe_ImplantIUD_AvailableOnNow
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Is(OpCodes.Ldc_I4, 16))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return CodeInstruction.Call(typeof(SettingsUtilities), nameof(SettingsUtilities.MinAgeToHaveChildren), new Type[] { typeof(Pawn) });
                    yield return new CodeInstruction(OpCodes.Conv_I4);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }

}
