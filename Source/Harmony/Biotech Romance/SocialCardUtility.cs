﻿using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using System.Text;
using System;

namespace BetterRomance.HarmonyPatches
{
    //Use min age setting instead of static 16
    [HarmonyPatch(typeof(SocialCardUtility), "CanDrawTryRomance")]
    public static class SocialCardUtility_CanDrawTryRomance
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Is(OpCodes.Ldc_R4, 16f))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.Call(typeof(SettingsUtilities), nameof(SettingsUtilities.MinAgeForSex));
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }

    //Adds a button for ordered hookups to the social card
    [HarmonyPatch(typeof(SocialCardUtility), nameof(SocialCardUtility.DrawRelationsAndOpinions))]
    public static class SocialCardUtility_DrawRelationsAndOpinions
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo EndGroup = AccessTools.Method(typeof(Widgets), nameof(Widgets.EndGroup));
            MethodInfo CanDrawTryRomance = AccessTools.Method(typeof(SocialCardUtility), "CanDrawTryRomance");

            foreach (CodeInstruction code in instructions)
            {
                if (code.Calls(EndGroup))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.Call(typeof(SocialCardUtility_DrawRelationsAndOpinions), "SocialCardHelper");
                }
                yield return code;
                if (code.Calls(CanDrawTryRomance))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call(typeof(HookupUtility), nameof(HookupUtility.CanDrawTryHookup));
                    yield return new CodeInstruction(OpCodes.Or);
                }
            }
        }

        private static void SocialCardHelper(Pawn pawn, Rect rect)
        {
            Vector2 ButtonSize = (Vector2)AccessTools.Field(typeof(SocialCardUtility), "RoleChangeButtonSize").GetValue(null);
            if (HookupUtility.CanDrawTryHookup(pawn))
            {
                bool romanceDrawn = (bool)AccessTools.Method(typeof(SocialCardUtility), "CanDrawTryRomance").Invoke(null, new object[] { pawn });
                //Adjust x position based on if romance button is present
                float width = romanceDrawn ? rect.width - 150f - ButtonSize.x - 5f : rect.width - 150f - +10f;
                DrawTryHookup(new Rect(width, rect.height - ButtonSize.y, ButtonSize.x, ButtonSize.y), pawn);
            }
        }

        private static void DrawTryHookup(Rect buttonRect, Pawn pawn)
        {
            Color color = GUI.color;
            bool isTryHookupOnCooldown = pawn.CheckForPartnerComp().IsOrderedHookupOnCooldown;
            AcceptanceReport canDoHookup = HookupUtility.HookupEligible(pawn, initiator: true, forOpinionExplanation: false);
            List<FloatMenuOption> list = canDoHookup.Accepted ? HookupOptions(pawn) : null;
            GUI.color = (!canDoHookup.Accepted || list.NullOrEmpty() || isTryHookupOnCooldown) ? ColoredText.SubtleGrayColor : Color.white;
            if (Widgets.ButtonText(buttonRect, "WBR.TryHookupButtonLabel".Translate() + "..."))
            {
                if (isTryHookupOnCooldown)
                {
                    int numTicks = pawn.CheckForPartnerComp().orderedHookupTick - Find.TickManager.TicksGame;
                    Messages.Message("WBR.CantHookupInitiateMessageCooldown".Translate(pawn, numTicks.ToStringTicksToPeriod()), MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
                if (!canDoHookup.Accepted)
                {
                    if (!canDoHookup.Reason.NullOrEmpty())
                    {
                        Messages.Message(canDoHookup.Reason, MessageTypeDefOf.RejectInput, historical: false);
                    }
                    return;
                }
                if (list.NullOrEmpty())
                {
                    Messages.Message("WBR.TryHookupNoOptsMessage".Translate(pawn), MessageTypeDefOf.RejectInput, historical: false);
                }
                else
                {
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            }
            GUI.color = color;
        }

        /// <summary>
        /// Generates a list of <see cref="Pawn"/>s that <paramref name="romancer"/> can try to hook up with
        /// </summary>
        /// <param name="romancer"></param>
        /// <returns></returns>
        private static List<FloatMenuOption> HookupOptions(Pawn romancer)
        {
            List<(float, FloatMenuOption)> eligibleList = new List<(float, FloatMenuOption)>();
            List<FloatMenuOption> ineligibleList = new List<FloatMenuOption>();

            foreach (Pawn p in RomanceUtilities.GetAllSpawnedHumanlikesOnMap(romancer.Map))
            {
                if (HookupUtility.HookupOption(romancer, p, out FloatMenuOption option, out float chance))
                {
                    eligibleList.Add((chance, option));
                }
                else if (option != null)
                {
                    ineligibleList.Add(option);
                }
            }
            return (from pair in eligibleList
                    orderby pair.Item1 descending
                    select pair.Item2).Concat(ineligibleList.OrderBy((FloatMenuOption opt) => opt.Label)).ToList();
        }
    }

    //Adds an explanation of ordered hookup acceptance chance to the social card tooltip
    [HarmonyPatch(typeof(SocialCardUtility), "GetPawnRowTooltip")]
    public static class SocialCardUtility_GetPawnRowTooltip
    {

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            MethodInfo RomanceExplanation = AccessTools.Method(typeof(SocialCardUtility), "RomanceExplanation");
            Label newLabel = ilg.DefineLabel();
            Label oldLabel = ilg.DefineLabel();
            LocalBuilder text = ilg.DeclareLocal(typeof(string));
            bool startFound = false;

            foreach (CodeInstruction code in instructions)
            {
                if (startFound && code.opcode == OpCodes.Brtrue_S)
                {
                    oldLabel = (Label)code.operand;
                    code.operand = newLabel;
                }

                yield return code;

                if (startFound && code.opcode == OpCodes.Pop)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1) { labels = new List<Label> { newLabel } };
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.LoadField(AccessTools.Inner(typeof(SocialCardUtility), "CachedSocialTabEntry"), "otherPawn");
                    yield return CodeInstruction.Call(typeof(SocialCardUtility_GetPawnRowTooltip), nameof(SocialCardUtility_GetPawnRowTooltip.HookupExplanation));
                    yield return new CodeInstruction(OpCodes.Stloc, text);
                    yield return new CodeInstruction(OpCodes.Ldloc, text);
                    yield return CodeInstruction.Call(typeof(GenText), nameof(GenText.NullOrEmpty));
                    yield return new CodeInstruction(OpCodes.Brtrue, oldLabel);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldloc, text);
                    yield return CodeInstruction.Call(typeof(StringBuilder), nameof(StringBuilder.AppendLine), parameters: new Type[] { typeof(string) });
                    yield return new CodeInstruction(OpCodes.Pop);

                    startFound = false;
                }

                if (code.Calls(RomanceExplanation))
                {
                    startFound = true;
                }
            }
        }

        public static string HookupExplanation(Pawn initiator, Pawn target)
        {
            if (!HookupUtility.CanDrawTryHookup(initiator))
            {
                return null;
            }
            AcceptanceReport ar = HookupUtility.HookupEligiblePair(initiator, target, forOpinionExplanation: true);
            if (!ar.Accepted && ar.Reason.NullOrEmpty())
            {
                return null;
            }
            if (!ar.Accepted)
            {
                return "WBR.HookupChanceCant".Translate() + (" (" + ar.Reason + ")\n");
            }
            StringBuilder text = new StringBuilder();
            float chance = HookupUtility.HookupSuccessChance(target, initiator, ordered: true, forTooltip: true);
            text.AppendLine(("WBR.HookupChance".Translate() + (": " + chance.ToStringPercent())).Colorize(ColoredText.TipSectionTitleColor));
            text.Append(HookupUtility.HookupFactors(initiator, target));
            return text.ToString();
        }
    }

    //This is to prevent drawing the button since I had to co-opt the bool that usually prevents it
    [HarmonyPatch(typeof(SocialCardUtility), "DrawTryRomance")]
    public static class SocialCardUtility_DrawTryRomance
    {
        public static bool Prefix(Rect buttonRect, Pawn pawn)
        {
            return (bool)AccessTools.Method(typeof(SocialCardUtility), "CanDrawTryRomance").Invoke(null, new object[] { pawn });
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            Label newLabel = ilg.DefineLabel();
            Label oldLabel = ilg.DefineLabel();
            bool cooldownFound = false;
            MethodInfo Accepted = AccessTools.PropertyGetter(typeof(AcceptanceReport), nameof(AcceptanceReport.Accepted));

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];

                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "CantRomanceInitiateMessageCooldown")
                {
                    cooldownFound = true;
                }
                if (code.opcode == OpCodes.Brtrue_S && cooldownFound)
                {
                    oldLabel = (Label)code.operand;
                    code.operand = newLabel;
                    cooldownFound = false;
                }

                if (code.labels.Contains(oldLabel))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1)
                    {
                        labels= new List<Label> { newLabel }
                    };
                    yield return CodeInstruction.Call(typeof(RomanceUtilities), nameof(RomanceUtilities.IsAsexual));
                    yield return new CodeInstruction(OpCodes.Brfalse_S, oldLabel);

                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call(typeof(RomanceUtilities), nameof(RomanceUtilities.GetOrientation));
                    yield return new CodeInstruction(OpCodes.Ldc_I4_3);
                    yield return new CodeInstruction(OpCodes.Bne_Un_S, oldLabel);

                    yield return new CodeInstruction(OpCodes.Ldstr, "WBR.CantRomanceInitiateMessageAromantic");
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call(typeof(NamedArgument), "op_Implicit", new Type[] { typeof(Thing) });
                    yield return CodeInstruction.Call(typeof(TranslatorFormattedStringExtensions), nameof(TranslatorFormattedStringExtensions.Translate), new Type[] { typeof(string), typeof(NamedArgument) });
                    yield return CodeInstruction.Call(typeof(TaggedString), "op_Implicit", new Type[] {typeof(TaggedString)});
                    yield return CodeInstruction.LoadField(typeof(MessageTypeDefOf), nameof(MessageTypeDefOf.RejectInput));
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return CodeInstruction.Call(typeof(Messages), nameof(Messages.Message), new Type[] { typeof(string), typeof(MessageTypeDef), typeof(bool) });
                    yield return new CodeInstruction(OpCodes.Ret);
                }

                yield return code;
            }
        }
    }
}