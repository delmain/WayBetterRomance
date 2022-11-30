using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BetterRomance
{
    public static class RomanceUtilities
    {
        /// <summary>
        /// Builds a list of up to five other pawns that <paramref name="pawn"/> finds suitable for the given activity. Looks at romance chance factor for hookups and opinion for dates.
        /// </summary>
        /// <param name="pawn">The pawn who is looking</param>
        /// <param name="hookup">Whether this list is for a hookup or a date</param>
        /// <returns>A list of pawns, with love relations first, then descending order by the secondary factor.</returns>
        public static List<Pawn> FindAttractivePawns(Pawn pawn, bool hookup = true)
        {
            List<Pawn> result = new List<Pawn>();
            //Removed asexual check, it instead goes in the joy givers that generate jobs that need this
            //Put existing partners in the list
            if (LovePartnerRelationUtility.HasAnyLovePartner(pawn))
            {
                foreach (Pawn p in GetAllLoveRelationPawns(pawn, false, true))
                {
                    //Skip pawns they share a bed with except for dates
                    if (!DoWeShareABed(pawn, p) || !hookup)
                    {
                        result.Add(p);
                    }
                }
            }
            //Stop here if non-spouse lovin' is not allowed
            if (!new HistoryEvent(HistoryEventDefOf.GotLovin_NonSpouse, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() && hookup)
            {
                return result;
            }
            //Then grab some attractive pawns
            while (result.Count < 5)
            {
                //For a hookup we need to start with a non-zero number, or pawns that they actually don't find attractive end up on the list
                //For a date we can start at zero since we're looking at opinion instead of romance factor
                float num = hookup ? 0.15f : 0f;
                Pawn tempPawn = null;
                foreach (Pawn p in pawn.Map.mapPawns.FreeColonistsSpawned)
                {
                    //Skip them if they're already in the list, or they share a bed if it's a hookup
                    if (result.Contains(p) || pawn == p || (DoWeShareABed(pawn, p) && hookup))
                    {
                        continue;
                    }
                    //Also skip if slave status is not the same for both pawns for hookups only
                    else if (pawn.IsSlave != p.IsSlave && hookup)
                    {
                        continue;
                    }
                    //For hookup check romance factor, for date check opinion
                    else if ((pawn.relations.SecondaryRomanceChanceFactor(p) > num && hookup) || (pawn.relations.OpinionOf(p) > num && !hookup))
                    {
                        //This will skip people who recently turned them down
                        Thought_Memory memory = pawn.needs.mood.thoughts.memories.Memories.Find(delegate (Thought_Memory x)
                        {
                            if (x.def == (hookup ? RomanceDefOf.RebuffedMyHookupAttempt : RomanceDefOf.RebuffedMyDateAttempt))
                            {
                                return x.otherPawn == p;
                            }
                            return false;
                        });
                        //Need to also check opinion against setting for a hookup
                        if ((memory == null && pawn.relations.OpinionOf(p) > pawn.MinOpinionForHookup()) || !hookup)
                        {
                            //romance factor for hookup, opinion for date
                            num = hookup ? pawn.relations.SecondaryRomanceChanceFactor(p) : pawn.relations.OpinionOf(p);
                            tempPawn = p;
                        }
                    }
                }
                if (tempPawn == null)
                {
                    break;
                }
                result.Add(tempPawn);
            }

            return result;
        }

        public static bool DoWeShareABed(Pawn pawn, Pawn other)
        {
            return pawn.ownership.OwnedBed != null && pawn.ownership.OwnedBed.OwnersForReading.Contains(other);
        }

        /// <summary>
        /// Determines if an interaction between <paramref name="pawn"/> and <paramref name="target"/> would be cheating from <paramref name="pawn"/>'s point of view. Includes a list of pawns that would think they are being cheated on.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="target"></param>
        /// <param name="cheaterList">A list of pawns who will think that <paramref name="pawn"/> cheated on them, regardless of what <paramref name="pawn"/> thinks</param>
        /// <returns>True or False</returns>
        public static bool IsThisCheating(Pawn pawn, Pawn target, out List<Pawn> cheaterList)
        {
            //This has to happen to get passed out
            cheaterList = new List<Pawn>();
            //Are they in a relationship?
            if (target != null && LovePartnerRelationUtility.LovePartnerRelationExists(pawn, target))
            {
                return false;
            }
            foreach (Pawn p in GetAllLoveRelationPawns(pawn, false, false))
            {
                //If the pawns have different ideos, I think this will check if the partner would feel cheated on per their ideo and settings
                if (!new HistoryEvent(pawn.GetHistoryEventForLoveRelationCountPlusOne(), p.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() && p.CaresAboutCheating())
                {
                    cheaterList.Add(p);
                }
            }
            //The cheater list is for use later, initiator will only look at their ideo and settings to decide if they're cheating
            if (new HistoryEvent(pawn.GetHistoryEventForLoveRelationCountPlusOne(), pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() || !pawn.CaresAboutCheating())
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if interaction between <paramref name="pawn"/> and <paramref name="target"/> is not cheating and is allowed by ideo. Otherwise, finds the partner they would feel the worst about cheating on and decides based on opinion.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="target"></param>
        /// <param name="cheatOn">The pawn they feel worst about cheating on</param>
        /// <returns></returns>
        public static bool WillPawnContinue(Pawn pawn, Pawn target, out Pawn cheatOn)
        {
            cheatOn = null;
            if (IsThisCheating(pawn, target, out List<Pawn> cheatedOnList))
            {
                if (!cheatedOnList.NullOrEmpty())
                {
                    //At this point, both the pawn and a non-zero number of partners consider this cheating
                    //If they are faithful, don't do it
                    if (pawn.story.traits.HasTrait(RomanceDefOf.Faithful))
                    {
                        return false;
                    }
                    //Don't allow if user has turned cheating off
                    if (BetterRomanceMod.settings.cheatChance == 0f)
                    {
                        return false;
                    }
                    //This should find the person they would feel the worst about cheating on
                    //With the philanderer map differences, I think this is the best way
                    float opinionFactor = 99999f;
                    foreach (Pawn p in cheatedOnList)
                    {
                        float opinion = pawn.relations.OpinionOf(p);
                        float tempOpinionFactor;
                        if (pawn.story.traits.HasTrait(RomanceDefOf.Philanderer))
                        {
                            tempOpinionFactor = pawn.Map == p.Map
                                ? Mathf.InverseLerp(70f, 15f, opinion)
                                : Mathf.InverseLerp(100f, 50f, opinion);
                        }
                        else
                        {
                            tempOpinionFactor = Mathf.InverseLerp(30f, -80f, opinion);
                        }
                        if (tempOpinionFactor < opinionFactor)
                        {
                            opinionFactor = tempOpinionFactor;
                            cheatOn = p;
                        }
                    }
                    if (Rand.Value *(BetterRomanceMod.settings.cheatChance/100f) < opinionFactor)
                    {
                        return false;
                    }
                }
                //Pawn thinks they are cheating, even though no partners will be upset
                //This can happen with the no spouses mod, which is a bit weird
                //Letting this continue for now, might change later
            }
            return true;
        }

        /// <summary>
        /// Determines if <paramref name="target"/> agrees to a hookup with <paramref name="asker"/>. Takes cheating into account.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="asker"></param>
        /// <returns>True or False</returns>
        public static bool IsHookupAppealing(Pawn target, Pawn asker)
        {
            if (target.relations.OpinionOf(asker) < target.MinOpinionForHookup())
            {
                return false;
            }
            //Asexual pawns below a certain rating will only agree to sex with existing partners
            if (target.IsAsexual() && target.AsexualRating() < 0.5f)
            {
                if (!LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
                {
                    return false;
                }
                //Otherwise their rating is already factored in via secondary romance chance factor
            }
            if (WillPawnContinue(target, asker, out _))
            {
                //It's either not cheating or they have decided to cheat
                float romanceFactor = target.relations.SecondaryRomanceChanceFactor(asker);
                if (!LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
                {
                    romanceFactor /= 1.5f;
                }
                float opinionFactor = 1f;
                //Decrease if opinion is negative
                opinionFactor *= Mathf.InverseLerp(-100f, 0f, target.relations.OpinionOf(asker));
                //Increase if opinion is positive, but on a lesser scale to above
                opinionFactor *= GenMath.LerpDouble(0, 100f, 1f, 1.5f, target.relations.OpinionOf(asker));
                return Rand.Range(0.05f, 1f) < (romanceFactor * opinionFactor);
            }
            return false;
        }

        /// <summary>
        /// Determines if <paramref name="target"/> accepts a date with <paramref name="asker"/>.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="asker"></param>
        /// <returns>True or false</returns>
        public static bool IsDateAppealing(Pawn target, Pawn asker)
        {
            //Always agree with an existing partner
            if (LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
            {
                return true;
            }
            if (WillPawnContinue(target, asker, out _))
            {
                //Definitely not cheating, or they decided to cheat
                //Same math as agreeing to a hookup but no asexual check
                float num = 0f;
                num += target.relations.SecondaryRomanceChanceFactor(asker) / 1.5f;
                num *= Mathf.InverseLerp(-100f, 0f, target.relations.OpinionOf(asker));
                return Rand.Range(0.05f, 1f) < num;
            }
            return false;
        }

        public static bool IsHangoutAppealing(Pawn target, Pawn asker)
        {
            //Always agree with an existing partner?
            if (LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
            {
                return true;
            }
            //Just looking at opinion
            float num = Mathf.InverseLerp(-100f, 0f, target.relations.OpinionOf(asker));
            return Rand.Range(0.05f, 1f) < num;

        }

        /// <summary>
        /// Will <paramref name="pawn"/> participate in a hookup. Checks settings and asexuality rating.
        /// </summary>
        /// <param name="pawn">The pawn in question</param>
        /// <returns>True or False</returns>
        public static bool WillPawnTryHookup(Pawn pawn)
        {
            //Sex repulsed asexual pawns will never agree to sex
            if (pawn.IsAsexual() && pawn.AsexualRating() < 0.2f)
            {
                return false;
            }
            //Is the race/pawnkind allowed to have hookups?
            if (!pawn.HookupAllowed())
            {
                return false;
            }
            //If their ideo prohibits all lovin', do not allow
            if (!new HistoryEvent(HistoryEventDefOf.SharedBed, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                return false;
            }
            //Check against canLovinTick
            if (Find.TickManager.TicksGame < pawn.mindState.canLovinTick)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Determines if <paramref name="pawn"/> is available for an activity.
        /// </summary>
        /// <param name="pawn"></param>
        /// <returns><see langword="false"/> if <paramref name="pawn"/> is close to needing to eat/sleep, there's enemies nearby, they're drafted, in labor, in a mental break, or they're doing a job that should not be interrupted.</returns>
        public static bool IsPawnFree(Pawn pawn)
        {
            if (PawnUtility.WillSoonHaveBasicNeed(pawn) || PawnUtility.EnemiesAreNearby(pawn) || pawn.Drafted )
            {
                return false;
            }
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.PregnancyLabor) || pawn.health.hediffSet.HasHediff(HediffDefOf.PregnancyLaborPushing))
            {
                return false;
            }
            if (pawn.mindState.mentalStateHandler.InMentalState)
            {
                return false;
            }
            return !DontInterruptJobs.Contains(pawn.CurJob.def);
        }

        private static readonly List<JobDef> DontInterruptJobs = new List<JobDef>
        {
            //Incapacitated
            JobDefOf.Wait_Downed,
            JobDefOf.Vomit,
            JobDefOf.Deathrest,
            JobDefOf.ExtinguishSelf,
            JobDefOf.Flee,
            JobDefOf.FleeAndCower,
            //Ceremonies
            JobDefOf.MarryAdjacentPawn,
            JobDefOf.SpectateCeremony,
            JobDefOf.GiveSpeech,
            JobDefOf.BestowingCeremony,
            JobDefOf.PrepareSkylantern,
            JobDefOf.PrisonerExecution,
            JobDefOf.Sacrifice,
            JobDefOf.Scarify,
            JobDefOf.Blind,
            //Emergency work
            JobDefOf.BeatFire,
            JobDefOf.Arrest,
            JobDefOf.Capture,
            JobDefOf.EscortPrisonerToBed,
            JobDefOf.Rescue,
            JobDefOf.CarryToBiosculpterPod,
            JobDefOf.BringBabyToSafety,
            //Medical work
            JobDefOf.TakeToBedToOperate,
            JobDefOf.TakeWoundedPrisonerToBed,
            JobDefOf.TendPatient,
            JobDefOf.FeedPatient,
            //Romance
            RomanceDefOf.DoLovinCasual,
            JobDefOf.Lovin,
            RomanceDefOf.JobDateLead,
            RomanceDefOf.JobDateFollow,
            RomanceDefOf.JobHangoutLead,
            RomanceDefOf.JobHangoutFollow,
            JobDefOf.TryRomance,
            //Ordered jobs
            JobDefOf.LayDown,
            JobDefOf.ReleasePrisoner,
            JobDefOf.UseCommsConsole,
            JobDefOf.EnterTransporter,
            JobDefOf.EnterCryptosleepCasket,
            JobDefOf.EnterBiosculpterPod,
            JobDefOf.TradeWithPawn,
            JobDefOf.ApplyTechprint,

            JobDefOf.SocialFight,
            JobDefOf.Breastfeed,
        };

        /// <summary>
        /// Grabs the first non-spouse love partner of the opposite gender. For use in generating parents.
        /// </summary>
        /// <param name="pawn"></param>
        /// <returns></returns>
        public static Pawn GetFirstLoverOfOppositeGender(Pawn pawn)
        {
            foreach (Pawn lover in GetNonSpouseLovers(pawn, true))
            {
                if (pawn.gender.Opposite() == lover.gender)
                {
                    return lover;
                }
            }
            return null;
        }

        /// <summary>
        /// Generates a list of love partners that does not include spouses or fiances
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="includeDead"></param>
        /// <returns></returns>
        public static List<Pawn> GetNonSpouseLovers(Pawn pawn, bool includeDead)
        {
            List<Pawn> list = new List<Pawn>();
            if (!pawn.RaceProps.IsFlesh)
            {
                return list;
            }
            List<DirectPawnRelation> relations = pawn.relations.DirectRelations;
            foreach (DirectPawnRelation rel in relations)
            {
                if (rel.def == PawnRelationDefOf.Lover && (includeDead || !rel.otherPawn.Dead))
                {
                    list.Add(rel.otherPawn);
                }
                else if (SettingsUtilities.LoveRelations.Contains(rel.def) && (includeDead || !rel.otherPawn.Dead))
                {
                    list.Add(rel.otherPawn);
                }
            }
            return list;
        }

        /// <summary>
        /// Generates a list of pawns that are in love relations with <paramref name="pawn"/>. Pawns are only listed once, even if they are in more than one love relation.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="includeDead">Whether dead pawns are added to the list</param>
        /// <param name="onMap">Whether pawns must be on the same map to be added to the list</param>
        /// <returns>A list of pawns that in a love relation with <paramref name="pawn"/></returns>
        public static List<Pawn> GetAllLoveRelationPawns(Pawn pawn, bool includeDead, bool onMap)
        {
            List<Pawn> list = new List<Pawn>();
            if (!pawn.RaceProps.IsFlesh)
            {
                return list;
            }
            foreach (DirectPawnRelation rel in LovePartnerRelationUtility.ExistingLovePartners(pawn, includeDead))
            {
                if (!list.Contains(rel.otherPawn))
                {
                    if (pawn.Map == rel.otherPawn.Map)
                    {
                        list.Add(rel.otherPawn);
                    }
                    else if (!onMap)
                    {
                        list.Add(rel.otherPawn);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Finds the most liked pawn with a specific <paramref name="relation"/> to <paramref name="pawn"/>. Any direct relation will work, no implied relations.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="relation"></param>
        /// <param name="allowDead"></param>
        /// <returns></returns>
        public static Pawn GetMostLikedOfRel(Pawn pawn, PawnRelationDef relation, bool allowDead)
        {
            List<DirectPawnRelation> list = pawn.relations.DirectRelations;
            Pawn result = null;
            int num = 0;
            foreach (DirectPawnRelation rel in list)
            {
                if (rel.def == relation && (rel.otherPawn.Dead || allowDead))
                {
                    if (pawn.relations.OpinionOf(rel.otherPawn) > num)
                    {
                        num = pawn.relations.OpinionOf(rel.otherPawn);
                        result = rel.otherPawn;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// A rating to use for determining sex aversion for asexual pawns. Seed is based on pawn's ID, so it will always return the same number for a given pawn.
        /// </summary>
        /// <param name="pawn"></param>
        /// <returns>float between 0 and 1</returns>
        public static float AsexualRating(this Pawn pawn)
        {
            Rand.PushState();
            Rand.Seed = pawn.thingIDNumber;
            float rating = Rand.Range(0f, 1f);
            Rand.PopState();
            return rating;
        }

        public static Orientation GetOrientation(this Pawn pawn)
        {
            if (pawn.story != null && pawn.story.traits != null)
            {
                TraitSet traits = pawn.story.traits;
                if (traits.HasTrait(TraitDefOf.Gay) || traits.HasTrait(RomanceDefOf.HomoAce))
                {
                    return Orientation.Homo;
                }
                else if (traits.HasTrait(RomanceDefOf.Straight) || traits.HasTrait(RomanceDefOf.HeteroAce))
                {
                    return Orientation.Hetero;
                }
                else if (traits.HasTrait(TraitDefOf.Bisexual) || traits.HasTrait(RomanceDefOf.BiAce))
                {
                    return Orientation.Bi;
                }
                else if (traits.HasTrait(TraitDefOf.Asexual))
                {
                    return Orientation.None;
                }
            }
            return Orientation.None;
        }

        public static bool IsAsexual(this Pawn pawn)
        {
            if (pawn.story != null && pawn.story.traits != null)
            {
                TraitSet traits = pawn.story.traits;
                if (traits.HasTrait(TraitDefOf.Asexual) || traits.HasTrait(RomanceDefOf.BiAce) || traits.HasTrait(RomanceDefOf.HeteroAce) || traits.HasTrait(RomanceDefOf.HomoAce))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Generates points for the lovin age curve based on age settings
        /// </summary>
        /// <returns>List<CurvePoint></returns>
        public static SimpleCurve GetLovinCurve(this Pawn pawn)
        {
            float minAge = pawn.MinAgeForSex();
            float maxAge = pawn.MaxAgeForSex();
            float declineAge = pawn.DeclineAtAge();
            List<CurvePoint> points = new List<CurvePoint>
            {
                new CurvePoint(minAge, 1.5f),
                new CurvePoint((declineAge / 5) + minAge, 1.5f),
                new CurvePoint(declineAge, 4f),
                new CurvePoint((maxAge / 4) + declineAge, 12f),
                new CurvePoint(maxAge, 36f)
            };
            return new SimpleCurve(points);
        }

        public static readonly List<TraitDef> OrientationTraits = new List<TraitDef>()
        {
            TraitDefOf.Gay,
            TraitDefOf.Bisexual,
            RomanceDefOf.Straight,
            TraitDefOf.Asexual,
            RomanceDefOf.HeteroAce,
            RomanceDefOf.HomoAce,
            RomanceDefOf.BiAce,
        };
    }

    public enum Orientation
    {
        Homo,
        Hetero,
        Bi,
        None,
    }
}