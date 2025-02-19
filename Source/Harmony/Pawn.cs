﻿using System.Collections.Generic;
using System.Linq;
using Verse;
using HarmonyLib;
using System.Reflection.Emit;

namespace BetterRomance.HarmonyPatches
{

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Pawn_GetGizmos
    {
        //Make growth tier gizmos show up based on actual min age instead of static 13
        [HarmonyPatch(MethodType.Enumerator)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction instruction = codes[i];
                yield return instruction;
                if (instruction.Is(OpCodes.Callvirt, AccessTools.DeclaredPropertyGetter(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeBiologicalYears))))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return CodeInstruction.LoadField(typeof(Pawn), nameof(Pawn.ageTracker));
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.DeclaredPropertyGetter(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AdultMinAge)));
                    yield return new CodeInstruction(OpCodes.Conv_I4);
                    i++;
                }
            }
        }

        //Add dev gizmo to reset ordered hookup cooldown
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            foreach (Gizmo gizmo in values)
            {
                yield return gizmo;
            }
            Comp_PartnerList comp = __instance.TryGetComp<Comp_PartnerList>();
            if (comp != null && DebugSettings.ShowDevGizmos && comp.IsOrderedHookupOnCooldown)
            {
                Command_Action action = new Command_Action
                {
                    defaultLabel = "DEV: Reset ordered hookup cooldown",
                    action = delegate
                    {
                        comp.orderedHookupTick = -1;
                    }
                };
                yield return action;
            }
        }
    }
}
