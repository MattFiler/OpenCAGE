////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2009, Daniel Kollmann
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted
// provided that the following conditions are met:
//
// - Redistributions of source code must retain the above copyright notice, this list of conditions
//   and the following disclaimer.
//
// - Redistributions in binary form must reproduce the above copyright notice, this list of
//   conditions and the following disclaimer in the documentation and/or other materials provided
//   with the distribution.
//
// - Neither the name of Daniel Kollmann nor the names of its contributors may be used to endorse
//   or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
// IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY
// WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
////////////////////////////////////////////////////////////////////////////////////////////////////

/*
 * 
 * LegendPlugin was created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using Brainiac.Design;
using LegendPlugin.Properties;

namespace LegendPlugin
{
	public class LegendPlugin : Plugin
	{
		public LegendPlugin()
		{
			// register resource manager
			AddResourceManager(Resources.ResourceManager);


            /*
             * GROUPS
            */
            NodeGroup actions = new NodeGroup(Resources.NodeGroupActions, NodeIcon.Action, null);
			_nodeGroups.Add(actions);

			NodeGroup conditions= new NodeGroup(Resources.NodeGroupConditions, NodeIcon.Condition, null);
			_nodeGroups.Add(conditions);

			NodeGroup decorators= new NodeGroup(Resources.NodeGroupDecorators, NodeIcon.Decorator, null);
			_nodeGroups.Add(decorators);

			NodeGroup selectors= new NodeGroup(Resources.NodeGroupSelectors, NodeIcon.Selector, null);
			_nodeGroups.Add(selectors);

			NodeGroup sequences= new NodeGroup(Resources.NodeGroupSequences, NodeIcon.Sequence, null);
			_nodeGroups.Add(sequences);


            /*
             * ACTIONS
            */
            //actions.Items.Add(typeof(Nodes.ActionAbortMeleeAttack));
            actions.Items.Add(typeof(Nodes.ActionAffectedByFlameThrower));
            actions.Items.Add(typeof(Nodes.ActionAffectedByFlameThrowerInVent));
            //actions.Items.Add(typeof(Nodes.ActionAlienWonScareTest));
            actions.Items.Add(typeof(Nodes.ActionApplyPrimaryDamageControlResponse));
            actions.Items.Add(typeof(Nodes.ActionAssert));
            actions.Items.Add(typeof(Nodes.ActionBackstageAlienResponse));
            actions.Items.Add(typeof(Nodes.ActionBackstageAreaSweep));
            actions.Items.Add(typeof(Nodes.ActionBreakout));
            actions.Items.Add(typeof(Nodes.ActionBrokenCover));
            actions.Items.Add(typeof(Nodes.ActionChangeCover));
            actions.Items.Add(typeof(Nodes.ActionDead));
            actions.Items.Add(typeof(Nodes.ActionDebugMenuLinkItem));
            //actions.Items.Add(typeof(Nodes.ActionDebugString));
            actions.Items.Add(typeof(Nodes.ActionDespawn));
            actions.Items.Add(typeof(Nodes.ActionDoneSystematicSearch));
            actions.Items.Add(typeof(Nodes.ActionExpireTimer));
            actions.Items.Add(typeof(Nodes.ActionFail));
            //actions.Items.Add(typeof(Nodes.ActionFakeSense));
            actions.Items.Add(typeof(Nodes.ActionForceIdle));
            //actions.Items.Add(typeof(Nodes.ActionForceSearch));
            actions.Items.Add(typeof(Nodes.ActionGetOutOfTheWay));
            actions.Items.Add(typeof(Nodes.ActionHitTargetAndRun));
            actions.Items.Add(typeof(Nodes.ActionIdle));
            actions.Items.Add(typeof(Nodes.ActionIdleInCover));
            actions.Items.Add(typeof(Nodes.ActionIdleTime));
            actions.Items.Add(typeof(Nodes.ActionIdleTimeFacingSuspiciousItem));
            actions.Items.Add(typeof(Nodes.ActionIdleTimeFacingTarget));
            actions.Items.Add(typeof(Nodes.ActionIdleTimeFacingTargetMostRecentSensedPosition));
            actions.Items.Add(typeof(Nodes.ActionIdleTimeFacingTargetOutsideCombatArea));
            //actions.Items.Add(typeof(Nodes.ActionIdleTimeFacingTargetSensedPosition));
            actions.Items.Add(typeof(Nodes.ActionListeningInConvo));
            actions.Items.Add(typeof(Nodes.ActionMakeAggressive));
            actions.Items.Add(typeof(Nodes.ActionMeleeAttack));
            actions.Items.Add(typeof(Nodes.ActionMoveInDirection));
            actions.Items.Add(typeof(Nodes.ActionMoveThroughTarget));
            actions.Items.Add(typeof(Nodes.ActionMoveToAttackTarget));
            actions.Items.Add(typeof(Nodes.ActionMoveToBackstageViaVentClosestToAlien));
            actions.Items.Add(typeof(Nodes.ActionMoveToConvo));
            actions.Items.Add(typeof(Nodes.ActionMoveToCover));
            actions.Items.Add(typeof(Nodes.ActionMoveToFrontStageViaFlankedVentClosestToPlayer));
            actions.Items.Add(typeof(Nodes.ActionMoveToMostRecentSensedPosition));
            actions.Items.Add(typeof(Nodes.ActionMoveToNearestStandingPointToTarget));
            actions.Items.Add(typeof(Nodes.ActionMoveToObjective));
            actions.Items.Add(typeof(Nodes.ActionMoveToTarget));
            actions.Items.Add(typeof(Nodes.ActionMoveWithGamepad));
            actions.Items.Add(typeof(Nodes.ActionNotifySquad));
            //actions.Items.Add(typeof(Nodes.ActionPauseSenses));
            actions.Items.Add(typeof(Nodes.ActionPerformAmbush));
            actions.Items.Add(typeof(Nodes.ActionPerformRole));
            actions.Items.Add(typeof(Nodes.ActionPlayerController));
            //actions.Items.Add(typeof(Nodes.ActionPlayTree));
            actions.Items.Add(typeof(Nodes.ActionPlayTreeAndFaceTarget));
            actions.Items.Add(typeof(Nodes.ActionRangedAim));
            actions.Items.Add(typeof(Nodes.ActionRangedShoot));
            actions.Items.Add(typeof(Nodes.ActionRequestCover));
            //actions.Items.Add(typeof(Nodes.ActionResetSearchJobs));
            actions.Items.Add(typeof(Nodes.ActionScript));
            actions.Items.Add(typeof(Nodes.ActionSetFrameFlag));
            actions.Items.Add(typeof(Nodes.ActionSetGaugeAmount));
            actions.Items.Add(typeof(Nodes.ActionSetLogicCharacterFlags));
            //actions.Items.Add(typeof(Nodes.ActionSetMenaceManager));
            actions.Items.Add(typeof(Nodes.ActionSetWithdrawState));
            actions.Items.Add(typeof(Nodes.ActionSpeakingInConvo));
            actions.Items.Add(typeof(Nodes.ActionStartTimer));
            actions.Items.Add(typeof(Nodes.ActionStartTimerRandom));
            actions.Items.Add(typeof(Nodes.ActionSuccess));
            actions.Items.Add(typeof(Nodes.ActionSuspectTargetResponse));
            actions.Items.Add(typeof(Nodes.ActionSuspend));
            actions.Items.Add(typeof(Nodes.ActionSuspiciousItemDoneStage));
            actions.Items.Add(typeof(Nodes.ActionSuspiciousItemMoveTo));
            actions.Items.Add(typeof(Nodes.ActionSuspiciousItemReaction));
            actions.Items.Add(typeof(Nodes.ActionSwitchToNextTarget));
            //actions.Items.Add(typeof(Nodes.ActionTakeStep));
            actions.Items.Add(typeof(Nodes.ActionThreatAware));
            actions.Items.Add(typeof(Nodes.ActionThreatEscalation));
            actions.Items.Add(typeof(Nodes.ActionTriggerSound));
            actions.Items.Add(typeof(Nodes.ActionWeaponEquip));
            

            /*
             * CONDITIONS
            */
            conditions.Items.Add(typeof(Nodes.ConditionDebugMenuLinkTest));
            conditions.Items.Add(typeof(Nodes.ConditionRequiresPrimaryDamageControlResponse));
            conditions.Items.Add(typeof(Nodes.ConditionIsCorpseTrap));
            conditions.Items.Add(typeof(Nodes.ConditionIsDead));
            conditions.Items.Add(typeof(Nodes.ConditionHaveTarget));
            conditions.Items.Add(typeof(Nodes.ConditionIsEnemyOfTarget));
            conditions.Items.Add(typeof(Nodes.ConditionIsCharacterClass));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsWithinDistance));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsWithinDistanceOfAlien));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsWithinAggroRadius));
            conditions.Items.Add(typeof(Nodes.ConditionIsPerformingRoleOrCouldPerformRole));
            //conditions.Items.Add(typeof(Nodes.ConditionMostRecentSenseActivationHasBeenAbove));
            conditions.Items.Add(typeof(Nodes.ConditionHasMotivation));
            conditions.Items.Add(typeof(Nodes.ConditionHasScript));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsOnlyAccessibleCrouching));
            conditions.Items.Add(typeof(Nodes.ConditionHasValidRouteToNearTarget));
            conditions.Items.Add(typeof(Nodes.ConditionShouldFollow));
            //conditions.Items.Add(typeof(Nodes.ConditionPlayerIsAnEnemy));
            conditions.Items.Add(typeof(Nodes.ConditionPlayerIsInExploitableArea));
            conditions.Items.Add(typeof(Nodes.ConditionShouldSuspend));
            conditions.Items.Add(typeof(Nodes.ConditionHaveNextTarget));
            conditions.Items.Add(typeof(Nodes.ConditionIsSenseActivationAbove));
            conditions.Items.Add(typeof(Nodes.ConditionIsAnySenseActivationAbove));
            //conditions.Items.Add(typeof(Nodes.ConditionHasSenseActivationBeenAbove));
            conditions.Items.Add(typeof(Nodes.ConditionHasAnySenseBeenAbove));
            //conditions.Items.Add(typeof(Nodes.ConditionWasSenseThresholdLastIncreaseActivation));
            conditions.Items.Add(typeof(Nodes.ConditionAngleToTargetLessThan));
            conditions.Items.Add(typeof(Nodes.ConditionHasMeleeAttackAvailableOrIsAttacking));
            conditions.Items.Add(typeof(Nodes.ConditionHasMeleeAttackAvailable));
            //conditions.Items.Add(typeof(Nodes.ConditionHasMeleeCounterAttackAvailable));
            //conditions.Items.Add(typeof(Nodes.ConditionHasMeleeBlockAvailable));
            //conditions.Items.Add(typeof(Nodes.ConditionTargetIsUsingMeleeAttack));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsTargetingMe));
            conditions.Items.Add(typeof(Nodes.ConditionLastTimeTargetShotAtMe));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsPlayer));
            conditions.Items.Add(typeof(Nodes.ConditionIsGaugeAmountAbove));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsWithinDistanceThreshold));
            //conditions.Items.Add(typeof(Nodes.ConditionTargetIsInWeaponRange));
            conditions.Items.Add(typeof(Nodes.ConditionAllowedToAttackTarget));
            conditions.Items.Add(typeof(Nodes.ConditionAllowedToPursueTarget));
            conditions.Items.Add(typeof(Nodes.ConditionHasAlertnessState));
            //conditions.Items.Add(typeof(Nodes.ConditionHasAggroLevel));
            conditions.Items.Add(typeof(Nodes.ConditionIsInVent));
            //conditions.Items.Add(typeof(Nodes.ConditionIsCrouching));
            conditions.Items.Add(typeof(Nodes.ConditionIsInCrawlSpace));
            conditions.Items.Add(typeof(Nodes.ConditionScriptedWithdrawRequested));
            conditions.Items.Add(typeof(Nodes.ConditionRangeTestForScriptedWithdrawal));
            conditions.Items.Add(typeof(Nodes.ConditionShouldUseCover));
            conditions.Items.Add(typeof(Nodes.ConditionHasAWeapon));
            conditions.Items.Add(typeof(Nodes.ConditionCurrentWeaponNeedsReloading));
            conditions.Items.Add(typeof(Nodes.ConditionCurrentWeaponIsEquipped));
            conditions.Items.Add(typeof(Nodes.ConditionHasTimerExpired));
            conditions.Items.Add(typeof(Nodes.ConditionLastTimeSensed));
            conditions.Items.Add(typeof(Nodes.ConditionLastTimeSquadNotified));
            conditions.Items.Add(typeof(Nodes.ConditionTargetsWeaponHasProperty));
            conditions.Items.Add(typeof(Nodes.ConditionLogicCharacterFlags));
            conditions.Items.Add(typeof(Nodes.ConditionTargetLogicCharacterFlags));
            conditions.Items.Add(typeof(Nodes.ConditionIsInCombatArea));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsInCombatArea));
            conditions.Items.Add(typeof(Nodes.ConditionObjectiveIsInCombatArea));
            conditions.Items.Add(typeof(Nodes.ConditionHasObjective));
            conditions.Items.Add(typeof(Nodes.ConditionObjectiveIsWithinDistance));
            //conditions.Items.Add(typeof(Nodes.ConditionHasGroupAwarenessState));
            //conditions.Items.Add(typeof(Nodes.ConditionCheckHealthState));
            conditions.Items.Add(typeof(Nodes.ConditionWithdrawState));
            conditions.Items.Add(typeof(Nodes.ConditionAngleNPCToTargetsAimLessThan));
            conditions.Items.Add(typeof(Nodes.ConditionIsInTargetsWeaponRange));
            conditions.Items.Add(typeof(Nodes.ConditionVisualSomeRaysThroughGlass));
            conditions.Items.Add(typeof(Nodes.ConditionAngleFromTargetAgainstTargetCameraDirnLessThan));
            conditions.Items.Add(typeof(Nodes.ConditionTargetsWeaponHasAmmo));
            conditions.Items.Add(typeof(Nodes.ConditionHasValidRouteToTarget));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsWithinRoutingDistance));
            conditions.Items.Add(typeof(Nodes.ConditionTargetIsWithinDistanceUnobscured));
            conditions.Items.Add(typeof(Nodes.ConditionTargetNearestStandPointIsWithinDistance));
            conditions.Items.Add(typeof(Nodes.ConditionHasSearchedMostRecentSensedPosition));
            conditions.Items.Add(typeof(Nodes.ConditionIsBranchActive));
            //conditions.Items.Add(typeof(Nodes.ConditionCanTakeStep));
            conditions.Items.Add(typeof(Nodes.ConditionIsInCover));
            //conditions.Items.Add(typeof(Nodes.ConditionCanShootNow));
            conditions.Items.Add(typeof(Nodes.ConditionHasValidCoverToChangeTo));
            conditions.Items.Add(typeof(Nodes.ConditionIsCurrentCoverValid));
            conditions.Items.Add(typeof(Nodes.ConditionIsRequestingCover));
            //conditions.Items.Add(typeof(Nodes.ConditionIsCoverTooClose));
            conditions.Items.Add(typeof(Nodes.ConditionIsCoverExposed));
            conditions.Items.Add(typeof(Nodes.ConditionShouldProcessSuspiciousItem));
            conditions.Items.Add(typeof(Nodes.ConditionSuspiciousItemBTPriority));
            conditions.Items.Add(typeof(Nodes.ConditionSuspiciousItemShouldDoStage));
            conditions.Items.Add(typeof(Nodes.ConditionSuspiciousItemFirstGroupMember));
            conditions.Items.Add(typeof(Nodes.ConditionSuspiciousItemGroupMembersRoutingTo));
            conditions.Items.Add(typeof(Nodes.ConditionSuspiciousItemWaitForGroupRouting));
            conditions.Items.Add(typeof(Nodes.ConditionSuspiciousItemGroupAllowedToProgress));
            conditions.Items.Add(typeof(Nodes.ConditionIsPartOfNPCGroup));
            conditions.Items.Add(typeof(Nodes.ConditionSquadDoingEscalation));
            conditions.Items.Add(typeof(Nodes.ConditionSquadDoingSuspiciousWarning));
            conditions.Items.Add(typeof(Nodes.ConditionAllowedToSearch));
            //conditions.Items.Add(typeof(Nodes.ConditionAllowedToDoSuspiciousWarning));
            //conditions.Items.Add(typeof(Nodes.ConditionIsGaugeAmountBelow));
            conditions.Items.Add(typeof(Nodes.ConditionHasAnySenseBeenAboveWithinTime));
            conditions.Items.Add(typeof(Nodes.ConditionLastTimeSearchedWithinTime));
            conditions.Items.Add(typeof(Nodes.ConditionLastTimeWasAbleToShootTarget));
            conditions.Items.Add(typeof(Nodes.ConditionCanBreakout));
            conditions.Items.Add(typeof(Nodes.ConditionIsBackstage));
            conditions.Items.Add(typeof(Nodes.ConditionLastTimeSensedFloat));
            conditions.Items.Add(typeof(Nodes.ConditionCanBeControlledByGamepad));
            conditions.Items.Add(typeof(Nodes.ConditionNeedsToGetOutOfTheWay));
            conditions.Items.Add(typeof(Nodes.ConditionHasAmbushPoint));
            conditions.Items.Add(typeof(Nodes.ConditionHasKilltrap));
            conditions.Items.Add(typeof(Nodes.ConditionHasFlankedVentCloseToPlayer));
            conditions.Items.Add(typeof(Nodes.ConditionHasVentCloseToAlien));
            conditions.Items.Add(typeof(Nodes.ConditionAnotherAlienIsAttackingThisTarget));
            conditions.Items.Add(typeof(Nodes.ConditionIsTargetInDeepCrouch));
            conditions.Items.Add(typeof(Nodes.ConditionAlienIsAllowed));
            conditions.Items.Add(typeof(Nodes.ConditionSuspiciousItemIsWithinDistance));
            conditions.Items.Add(typeof(Nodes.ConditionEventAOccuredAfterB));
            conditions.Items.Add(typeof(Nodes.ConditionIsFrameFlagSet));
            conditions.Items.Add(typeof(Nodes.ConditionHasToken));
            conditions.Items.Add(typeof(Nodes.ConditionHasDoneSuspectResponseMoveTo));
            conditions.Items.Add(typeof(Nodes.ConditionHasDoneSuspectResponseWithinTime));
            conditions.Items.Add(typeof(Nodes.ConditionWasSenseThresholdLastIncreaseActivationAbove));
            //conditions.Items.Add(typeof(Nodes.ConditionGameIsDifficulty));
            conditions.Items.Add(typeof(Nodes.ConditionInConvo));
            conditions.Items.Add(typeof(Nodes.ConditionTalkingInConvo));
            conditions.Items.Add(typeof(Nodes.ConditionListeningInConvo));
            conditions.Items.Add(typeof(Nodes.ConditionConvoInteruppted));
            conditions.Items.Add(typeof(Nodes.ConditionConvoInterupptedByPlayer));
            conditions.Items.Add(typeof(Nodes.ConditionSomeoneJoinedConvo));
            conditions.Items.Add(typeof(Nodes.ConditionPlayerJoinedConvo));
            conditions.Items.Add(typeof(Nodes.ConditionInPositionForConvo));
            

			/*
             * DECORATORS
            */
            decorators.Items.Add(typeof(Nodes.DecoratorAggressionEscalation));
            decorators.Items.Add(typeof(Nodes.DecoratorAwarenessState));
            decorators.Items.Add(typeof(Nodes.DecoratorBranch));
            decorators.Items.Add(typeof(Nodes.DecoratorLockBestVents));
            //decorators.Items.Add(typeof(Nodes.DecoratorLoop));
            decorators.Items.Add(typeof(Nodes.DecoratorMood));
            decorators.Items.Add(typeof(Nodes.DecoratorPercentage));
            //decorators.Items.Add(typeof(Nodes.DecoratorSetSenseSet));
            decorators.Items.Add(typeof(Nodes.DecoratorSquadSearch));
            decorators.Items.Add(typeof(Nodes.DecoratorSuspiciousItemInProgress));
            decorators.Items.Add(typeof(Nodes.DecoratorTimer));
            

            /*
             * SELECTORS
            */
			selectors.Items.Add(typeof(Nodes.SelectorLinear));
            selectors.Items.Add(typeof(Nodes.SelectorPercentage));


            /*
             * SEQUENCES
            */
            sequences.Items.Add(typeof(Nodes.SequenceLinear));
            sequences.Items.Add(typeof(Nodes.SequenceStateless));


            // register all the file managers
            _fileManagers.Add( new FileManagerInfo(typeof(Brainiac.Design.FileManagers.FileManagerXML), "Behaviour XML (*.xml)|*.xml", ".xml") );

			// register all the exporters
			_exporters.Add( new ExporterInfo(typeof(Brainiac.Design.Exporters.ExporterCs), "C# Behavior Exporter (Assign Properties)", true, "C#Properties") );
			_exporters.Add( new ExporterInfo(typeof(Brainiac.Design.Exporters.ExporterCsUseParameters), "C# Behavior Exporter (Use Parameters)", true, "C#Parameters") );
		}
	}
}
