using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeMaker
{
    class Program
    {
        static void Main(string[] args)
        {
            //THIS PROGRAM MUST BE PLACED IN LEGENDPLUGIN BASE FOLDER


            //ACTION
            string[] actionArray = { "ActionAbortMeleeAttack", "ActionAffectedByFlameThrower", "ActionAffectedByFlameThrowerInVent", "ActionAlienWonScareTest", "ActionApplyPrimaryDamageControlResponse", "ActionAssert", "ActionBackstageAlienResponse", "ActionBackstageAreaSweep", "ActionBreakout", "ActionBrokenCover", "ActionChangeCover", "ActionDead", "ActionDebugMenuLinkItem", "ActionDebugString", "ActionDespawn", "ActionDoneSystematicSearch", "ActionExpireTimer", "ActionFail", "ActionFakeSense", "ActionForceIdle", "ActionForceSearch", "ActionGetOutOfTheWay", "ActionHitTargetAndRun", "ActionIdle", "ActionIdleInCover", "ActionIdleTime", "ActionIdleTimeFacingSuspiciousItem", "ActionIdleTimeFacingTarget", "ActionIdleTimeFacingTargetMostRecentSensedPosition", "ActionIdleTimeFacingTargetOutsideCombatArea", "ActionIdleTimeFacingTargetSensedPosition", "ActionListeningInConvo", "ActionMakeAggressive", "ActionMeleeAttack", "ActionMoveInDirection", "ActionMoveThroughTarget", "ActionMoveToAttackTarget", "ActionMoveToBackstageViaVentClosestToAlien", "ActionMoveToConvo", "ActionMoveToCover", "ActionMoveToFrontStageViaFlankedVentClosestToPlayer", "ActionMoveToMostRecentSensedPosition", "ActionMoveToNearestStandingPointToTarget", "ActionMoveToObjective", "ActionMoveToTarget", "ActionMoveWithGamepad", "ActionNotifySquad", "ActionPauseSenses", "ActionPerformAmbush", "ActionPerformRole", "ActionPlayerController", "ActionPlayTree", "ActionPlayTreeAndFaceTarget", "ActionRangedAim", "ActionRangedShoot", "ActionRequestCover", "ActionResetSearchJobs", "ActionScript", "ActionSetFrameFlag", "ActionSetGaugeAmount", "ActionSetLogicCharacterFlags", "ActionSetMenaceManager", "ActionSetWithdrawState", "ActionSpeakingInConvo", "ActionStartTimer", "ActionStartTimerRandom", "ActionSuccess", "ActionSuspectTargetResponse", "ActionSuspend", "ActionSuspiciousItemDoneStage", "ActionSuspiciousItemMoveTo", "ActionSuspiciousItemReaction", "ActionSwitchToNextTarget", "ActionTakeStep", "ActionThreatAware", "ActionThreatEscalation", "ActionTriggerSound", "ActionWeaponEquip" };

            foreach (string action in actionArray)
            {
                string actionBase = File.ReadAllText("Action/NodeMakerActionBase.cs");
                actionBase = actionBase.Replace("NodeMakerReplaceMe", action);
                if (!File.Exists("Action/" + action + ".cs")) {
                    File.WriteAllText("Action/"+action+".cs", actionBase);
                    Console.WriteLine("Generated file: Action/" + action + ".cs");
                }
                else
                {
                    string currFile = File.ReadAllText("Action/" + action + ".cs");
                    currFile = currFile.Replace("LegendPlugin.Actions", "LegendPlugin.Nodes");
                    File.WriteAllText("Action/" + action + ".cs", currFile);
                }
            }


            //CONDITION
            string[] conditionArray = { "ConditionDebugMenuLinkTest", "ConditionRequiresPrimaryDamageControlResponse", "ConditionIsCorpseTrap", "ConditionIsDead", "ConditionHaveTarget", "ConditionIsEnemyOfTarget", "ConditionIsCharacterClass", "ConditionTargetIsWithinDistance", "ConditionTargetIsWithinDistanceOfAlien", "ConditionTargetIsWithinAggroRadius", "ConditionIsPerformingRoleOrCouldPerformRole", "ConditionMostRecentSenseActivationHasBeenAbove", "ConditionHasMotivation", "ConditionHasScript", "ConditionTargetIsOnlyAccessibleCrouching", "ConditionHasValidRouteToNearTarget", "ConditionShouldFollow", "ConditionPlayerIsAnEnemy", "ConditionPlayerIsInExploitableArea", "ConditionShouldSuspend", "ConditionHaveNextTarget", "ConditionIsSenseActivationAbove", "ConditionIsAnySenseActivationAbove", "ConditionHasSenseActivationBeenAbove", "ConditionHasAnySenseBeenAbove", "ConditionWasSenseThresholdLastIncreaseActivation", "ConditionAngleToTargetLessThan", "ConditionHasMeleeAttackAvailableOrIsAttacking", "ConditionHasMeleeAttackAvailable", "ConditionHasMeleeCounterAttackAvailable", "ConditionHasMeleeBlockAvailable", "ConditionTargetIsUsingMeleeAttack", "ConditionTargetIsTargetingMe", "ConditionLastTimeTargetShotAtMe", "ConditionTargetIsPlayer", "ConditionIsGaugeAmountAbove", "ConditionTargetIsWithinDistanceThreshold", "ConditionTargetIsInWeaponRange", "ConditionAllowedToAttackTarget", "ConditionAllowedToPursueTarget", "ConditionHasAlertnessState", "ConditionHasAggroLevel", "ConditionIsInVent", "ConditionIsCrouching", "ConditionIsInCrawlSpace", "ConditionScriptedWithdrawRequested", "ConditionRangeTestForScriptedWithdrawal", "ConditionShouldUseCover", "ConditionHasAWeapon", "ConditionCurrentWeaponNeedsReloading", "ConditionCurrentWeaponIsEquipped", "ConditionHasTimerExpired", "ConditionLastTimeSensed", "ConditionLastTimeSquadNotified", "ConditionTargetsWeaponHasProperty", "ConditionLogicCharacterFlags", "ConditionTargetLogicCharacterFlags", "ConditionIsInCombatArea", "ConditionTargetIsInCombatArea", "ConditionObjectiveIsInCombatArea", "ConditionHasObjective", "ConditionObjectiveIsWithinDistance", "ConditionHasGroupAwarenessState", "ConditionCheckHealthState", "ConditionWithdrawState", "ConditionAngleNPCToTargetsAimLessThan", "ConditionIsInTargetsWeaponRange", "ConditionVisualSomeRaysThroughGlass", "ConditionAngleFromTargetAgainstTargetCameraDirnLessThan", "ConditionTargetsWeaponHasAmmo", "ConditionHasValidRouteToTarget", "ConditionTargetIsWithinRoutingDistance", "ConditionTargetIsWithinDistanceUnobscured", "ConditionTargetNearestStandPointIsWithinDistance", "ConditionHasSearchedMostRecentSensedPosition", "ConditionIsBranchActive", "ConditionCanTakeStep", "ConditionIsInCover", "ConditionCanShootNow", "ConditionHasValidCoverToChangeTo", "ConditionIsCurrentCoverValid", "ConditionIsRequestingCover", "ConditionIsCoverTooClose", "ConditionIsCoverExposed", "ConditionShouldProcessSuspiciousItem", "ConditionSuspiciousItemBTPriority", "ConditionSuspiciousItemShouldDoStage", "ConditionSuspiciousItemFirstGroupMember", "ConditionSuspiciousItemGroupMembersRoutingTo", "ConditionSuspiciousItemWaitForGroupRouting", "ConditionSuspiciousItemGroupAllowedToProgress", "ConditionIsPartOfNPCGroup", "ConditionSquadDoingEscalation", "ConditionSquadDoingSuspiciousWarning", "ConditionAllowedToSearch", "ConditionAllowedToDoSuspiciousWarning", "ConditionIsGaugeAmountBelow", "ConditionHasAnySenseBeenAboveWithinTime", "ConditionLastTimeSearchedWithinTime", "ConditionLastTimeWasAbleToShootTarget", "ConditionCanBreakout", "ConditionIsBackstage", "ConditionLastTimeSensedFloat", "ConditionCanBeControlledByGamepad", "ConditionNeedsToGetOutOfTheWay", "ConditionHasAmbushPoint", "ConditionHasKilltrap", "ConditionHasFlankedVentCloseToPlayer", "ConditionHasVentCloseToAlien", "ConditionAnotherAlienIsAttackingThisTarget", "ConditionIsTargetInDeepCrouch", "ConditionAlienIsAllowed", "ConditionSuspiciousItemIsWithinDistance", "ConditionEventAOccuredAfterB", "ConditionIsFrameFlagSet", "ConditionHasToken", "ConditionHasDoneSuspectResponseMoveTo", "ConditionHasDoneSuspectResponseWithinTime", "ConditionWasSenseThresholdLastIncreaseActivationAbove", "ConditionGameIsDifficulty", "ConditionInConvo", "ConditionTalkingInConvo", "ConditionListeningInConvo", "ConditionConvoInteruppted", "ConditionConvoInterupptedByPlayer", "ConditionSomeoneJoinedConvo", "ConditionPlayerJoinedConvo", "ConditionInPositionForConvo" };

            foreach (string condition in conditionArray)
            {
                string conditionBase = File.ReadAllText("Condition/NodeMakerConditionBase.cs");
                conditionBase = conditionBase.Replace("NodeMakerReplaceMe", condition);
                if (!File.Exists("Condition/" + condition + ".cs"))
                {
                    File.WriteAllText("Condition/" + condition + ".cs", conditionBase);
                    Console.WriteLine("Generated file: Condition/" + condition + ".cs");
                }
                else
                {
                    string currFile = File.ReadAllText("Condition/" + condition + ".cs");
                    currFile = currFile.Replace("LegendPlugin.Conditions", "LegendPlugin.Nodes");
                    File.WriteAllText("Condition/" + condition + ".cs", currFile);
                }
            }


            //DECORATORS
            string[] decoratorArray = { "DecoratorAggressionEscalation", "DecoratorAwarenessState", "DecoratorBranch", "DecoratorLockBestVents", "DecoratorLoop", "DecoratorMood", "DecoratorPercentage", "DecoratorSetSenseSet", "DecoratorSquadSearch", "DecoratorSuspiciousItemInProgress", "DecoratorTimer" };

            foreach (string decorator in decoratorArray)
            {
                string decoratorBase = File.ReadAllText("Decorator/NodeMakerDecoratorBase.cs");
                decoratorBase = decoratorBase.Replace("NodeMakerReplaceMe", decorator);
                if (!File.Exists("Decorator/" + decorator + ".cs"))
                {
                    File.WriteAllText("Decorator/" + decorator + ".cs", decoratorBase);
                    Console.WriteLine("Generated file: Decorator/" + decorator + ".cs");
                }
                else
                {
                    string currFile = File.ReadAllText("Decorator/" + decorator + ".cs");
                    currFile = currFile.Replace("LegendPlugin.Decorators", "LegendPlugin.Nodes");
                    File.WriteAllText("Decorator/" + decorator + ".cs", currFile);
                }
            }


            Console.ReadLine();
        }
    }
}
