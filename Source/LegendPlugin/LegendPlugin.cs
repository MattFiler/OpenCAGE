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

using System;
using System.Collections.Generic;
using System.Text;
using Brainiac.Design;
using LegendPlugin.Properties;

namespace LegendPlugin
{
	/// <summary>
	/// The plugin for Project Hoshimi which is loaded when you start the editor.
	/// </summary>
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
            actions.Items.Add(typeof(Actions.ActionAbortMeleeAttack));
            actions.Items.Add(typeof(Actions.ActionAffectedByFlameThrower));
            actions.Items.Add(typeof(Actions.ActionAffectedByFlameThrowerInVent));
            actions.Items.Add(typeof(Actions.ActionAlienWonScareTest));
            /*
            actions.Items.Add(typeof(Actions.ActionApplyPrimaryDamageControlResponse));
            actions.Items.Add(typeof(Actions.ActionAssert));
            actions.Items.Add(typeof(Actions.ActionBackstageAlienResponse));
            actions.Items.Add(typeof(Actions.ActionBackstageAreaSweep));
            actions.Items.Add(typeof(Actions.ActionBreakout));
            actions.Items.Add(typeof(Actions.ActionBrokenCover));
            actions.Items.Add(typeof(Actions.ActionChangeCover));
            actions.Items.Add(typeof(Actions.ActionDead));
            actions.Items.Add(typeof(Actions.ActionDebugMenuLinkItem));
            actions.Items.Add(typeof(Actions.ActionDebugString));
            actions.Items.Add(typeof(Actions.ActionDespawn));
            actions.Items.Add(typeof(Actions.ActionDoneSystematicSearch));
            actions.Items.Add(typeof(Actions.ActionExpireTimer));
            actions.Items.Add(typeof(Actions.ActionFail));
            actions.Items.Add(typeof(Actions.ActionFakeSense));
            actions.Items.Add(typeof(Actions.ActionForceIdle));
            actions.Items.Add(typeof(Actions.ActionForceSearch));
            actions.Items.Add(typeof(Actions.ActionGetOutOfTheWay));
            actions.Items.Add(typeof(Actions.ActionHitTargetAndRun));
            actions.Items.Add(typeof(Actions.ActionIdle));
            actions.Items.Add(typeof(Actions.ActionIdleInCover));
            actions.Items.Add(typeof(Actions.ActionIdleTime));
            actions.Items.Add(typeof(Actions.ActionIdleTimeFacingSuspiciousItem));
            actions.Items.Add(typeof(Actions.ActionIdleTimeFacingTarget));
            actions.Items.Add(typeof(Actions.ActionIdleTimeFacingTargetMostRecentSensedPosition));
            actions.Items.Add(typeof(Actions.ActionIdleTimeFacingTargetOutsideCombatArea));
            actions.Items.Add(typeof(Actions.ActionIdleTimeFacingTargetSensedPosition));
            actions.Items.Add(typeof(Actions.ActionListeningInConvo));
            actions.Items.Add(typeof(Actions.ActionMakeAggressive));
            actions.Items.Add(typeof(Actions.ActionMeleeAttack));
            actions.Items.Add(typeof(Actions.ActionMoveInDirection));
            actions.Items.Add(typeof(Actions.ActionMoveThroughTarget));
            actions.Items.Add(typeof(Actions.ActionMoveToAttackTarget));
            actions.Items.Add(typeof(Actions.ActionMoveToBackstageViaVentClosestToAlien));
            actions.Items.Add(typeof(Actions.ActionMoveToConvo));
            actions.Items.Add(typeof(Actions.ActionMoveToCover));
            actions.Items.Add(typeof(Actions.ActionMoveToFrontStageViaFlankedVentClosestToPlayer));
            actions.Items.Add(typeof(Actions.ActionMoveToMostRecentSensedPosition));
            actions.Items.Add(typeof(Actions.ActionMoveToNearestStandingPointToTarget));
            actions.Items.Add(typeof(Actions.ActionMoveToObjective));
            actions.Items.Add(typeof(Actions.ActionMoveToTarget));
            actions.Items.Add(typeof(Actions.ActionMoveWithGamepad));
            actions.Items.Add(typeof(Actions.ActionNotifySquad));
            actions.Items.Add(typeof(Actions.ActionPauseSenses));
            actions.Items.Add(typeof(Actions.ActionPerformAmbush));
            actions.Items.Add(typeof(Actions.ActionPerformRole));
            actions.Items.Add(typeof(Actions.ActionPlayerController));
            actions.Items.Add(typeof(Actions.ActionPlayTree));
            actions.Items.Add(typeof(Actions.ActionPlayTreeAndFaceTarget));
            actions.Items.Add(typeof(Actions.ActionRangedAim));
            actions.Items.Add(typeof(Actions.ActionRangedShoot));
            actions.Items.Add(typeof(Actions.ActionRequestCover));
            actions.Items.Add(typeof(Actions.ActionResetSearchJobs));
            actions.Items.Add(typeof(Actions.ActionScript));
            actions.Items.Add(typeof(Actions.ActionSetFrameFlag));
            actions.Items.Add(typeof(Actions.ActionSetGaugeAmount));
            actions.Items.Add(typeof(Actions.ActionSetLogicCharacterFlags));
            actions.Items.Add(typeof(Actions.ActionSetMenaceManager));
            actions.Items.Add(typeof(Actions.ActionSetWithdrawState));
            actions.Items.Add(typeof(Actions.ActionSpeakingInConvo));
            actions.Items.Add(typeof(Actions.ActionStartTimer));
            actions.Items.Add(typeof(Actions.ActionStartTimerRandom));
            actions.Items.Add(typeof(Actions.ActionSuccess));
            actions.Items.Add(typeof(Actions.ActionSuspectTargetResponse));
            actions.Items.Add(typeof(Actions.ActionSuspend));
            actions.Items.Add(typeof(Actions.ActionSuspiciousItemDoneStage));
            actions.Items.Add(typeof(Actions.ActionSuspiciousItemMoveTo));
            actions.Items.Add(typeof(Actions.ActionSuspiciousItemReaction));
            actions.Items.Add(typeof(Actions.ActionSwitchToNextTarget));
            actions.Items.Add(typeof(Actions.ActionTakeStep));
            actions.Items.Add(typeof(Actions.ActionThreatAware));
            actions.Items.Add(typeof(Actions.ActionThreatEscalation));
            actions.Items.Add(typeof(Actions.ActionTriggerSound));
            actions.Items.Add(typeof(Actions.ActionWeaponEquip));
            */


            /*
             * CONDITIONS
            */
            conditions.Items.Add(typeof(Conditions.ConditionDebugMenuLinkTest));
            conditions.Items.Add(typeof(Conditions.ConditionRequiresPrimaryDamageControlResponse));
            conditions.Items.Add(typeof(Conditions.ConditionIsCorpseTrap));
            conditions.Items.Add(typeof(Conditions.ConditionIsDead));
            /*
            conditions.Items.Add(typeof(Conditions.ConditionHaveTarget));
            conditions.Items.Add(typeof(Conditions.ConditionIsEnemyOfTarget));
            conditions.Items.Add(typeof(Conditions.ConditionIsCharacterClass));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsWithinDistance));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsWithinDistanceOfAlien));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsWithinAggroRadius));
            conditions.Items.Add(typeof(Conditions.ConditionIsPerformingRoleOrCouldPerformRole));
            conditions.Items.Add(typeof(Conditions.ConditionMostRecentSenseActivationHasBeenAbove));
            conditions.Items.Add(typeof(Conditions.ConditionHasMotivation));
            conditions.Items.Add(typeof(Conditions.ConditionHasScript));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsOnlyAccessibleCrouching));
            conditions.Items.Add(typeof(Conditions.ConditionHasValidRouteToNearTarget));
            conditions.Items.Add(typeof(Conditions.ConditionShouldFollow));
            conditions.Items.Add(typeof(Conditions.ConditionPlayerIsAnEnemy));
            conditions.Items.Add(typeof(Conditions.ConditionPlayerIsInExploitableArea));
            conditions.Items.Add(typeof(Conditions.ConditionShouldSuspend));
            conditions.Items.Add(typeof(Conditions.ConditionHaveNextTarget));
            conditions.Items.Add(typeof(Conditions.ConditionIsSenseActivationAbove));
            conditions.Items.Add(typeof(Conditions.ConditionIsAnySenseActivationAbove));
            conditions.Items.Add(typeof(Conditions.ConditionHasSenseActivationBeenAbove));
            conditions.Items.Add(typeof(Conditions.ConditionHasAnySenseBeenAbove));
            conditions.Items.Add(typeof(Conditions.ConditionWasSenseThresholdLastIncreaseActivation));
            conditions.Items.Add(typeof(Conditions.ConditionAngleToTargetLessThan));
            conditions.Items.Add(typeof(Conditions.ConditionHasMeleeAttackAvailableOrIsAttacking));
            conditions.Items.Add(typeof(Conditions.ConditionHasMeleeAttackAvailable));
            conditions.Items.Add(typeof(Conditions.ConditionHasMeleeCounterAttackAvailable));
            conditions.Items.Add(typeof(Conditions.ConditionHasMeleeBlockAvailable));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsUsingMeleeAttack));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsTargetingMe));
            conditions.Items.Add(typeof(Conditions.ConditionLastTimeTargetShotAtMe));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsPlayer));
            conditions.Items.Add(typeof(Conditions.ConditionIsGaugeAmountAbove));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsWithinDistanceThreshold));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsInWeaponRange));
            conditions.Items.Add(typeof(Conditions.ConditionAllowedToAttackTarget));
            conditions.Items.Add(typeof(Conditions.ConditionAllowedToPursueTarget));
            conditions.Items.Add(typeof(Conditions.ConditionHasAlertnessState));
            conditions.Items.Add(typeof(Conditions.ConditionHasAggroLevel));
            conditions.Items.Add(typeof(Conditions.ConditionIsInVent));
            conditions.Items.Add(typeof(Conditions.ConditionIsCrouching));
            conditions.Items.Add(typeof(Conditions.ConditionIsInCrawlSpace));
            conditions.Items.Add(typeof(Conditions.ConditionScriptedWithdrawRequested));
            conditions.Items.Add(typeof(Conditions.ConditionRangeTestForScriptedWithdrawal));
            conditions.Items.Add(typeof(Conditions.ConditionShouldUseCover));
            conditions.Items.Add(typeof(Conditions.ConditionHasAWeapon));
            conditions.Items.Add(typeof(Conditions.ConditionCurrentWeaponNeedsReloading));
            conditions.Items.Add(typeof(Conditions.ConditionCurrentWeaponIsEquipped));
            */
            conditions.Items.Add(typeof(Conditions.ConditionHasTimerExpired));
            /*
            conditions.Items.Add(typeof(Conditions.ConditionLastTimeSensed));
            conditions.Items.Add(typeof(Conditions.ConditionLastTimeSquadNotified));
            conditions.Items.Add(typeof(Conditions.ConditionTargetsWeaponHasProperty));
            conditions.Items.Add(typeof(Conditions.ConditionLogicCharacterFlags));
            conditions.Items.Add(typeof(Conditions.ConditionTargetLogicCharacterFlags));
            conditions.Items.Add(typeof(Conditions.ConditionIsInCombatArea));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsInCombatArea));
            conditions.Items.Add(typeof(Conditions.ConditionObjectiveIsInCombatArea));
            conditions.Items.Add(typeof(Conditions.ConditionHasObjective));
            conditions.Items.Add(typeof(Conditions.ConditionObjectiveIsWithinDistance));
            conditions.Items.Add(typeof(Conditions.ConditionHasGroupAwarenessState));
            conditions.Items.Add(typeof(Conditions.ConditionCheckHealthState));
            conditions.Items.Add(typeof(Conditions.ConditionWithdrawState));
            conditions.Items.Add(typeof(Conditions.ConditionAngleNPCToTargetsAimLessThan));
            conditions.Items.Add(typeof(Conditions.ConditionIsInTargetsWeaponRange));
            conditions.Items.Add(typeof(Conditions.ConditionVisualSomeRaysThroughGlass));
            conditions.Items.Add(typeof(Conditions.ConditionAngleFromTargetAgainstTargetCameraDirnLessThan));
            conditions.Items.Add(typeof(Conditions.ConditionTargetsWeaponHasAmmo));
            conditions.Items.Add(typeof(Conditions.ConditionHasValidRouteToTarget));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsWithinRoutingDistance));
            conditions.Items.Add(typeof(Conditions.ConditionTargetIsWithinDistanceUnobscured));
            conditions.Items.Add(typeof(Conditions.ConditionTargetNearestStandPointIsWithinDistance));
            conditions.Items.Add(typeof(Conditions.ConditionHasSearchedMostRecentSensedPosition));
            conditions.Items.Add(typeof(Conditions.ConditionIsBranchActive));
            conditions.Items.Add(typeof(Conditions.ConditionCanTakeStep));
            conditions.Items.Add(typeof(Conditions.ConditionIsInCover));
            conditions.Items.Add(typeof(Conditions.ConditionCanShootNow));
            conditions.Items.Add(typeof(Conditions.ConditionHasValidCoverToChangeTo));
            conditions.Items.Add(typeof(Conditions.ConditionIsCurrentCoverValid));
            conditions.Items.Add(typeof(Conditions.ConditionIsRequestingCover));
            conditions.Items.Add(typeof(Conditions.ConditionIsCoverTooClose));
            conditions.Items.Add(typeof(Conditions.ConditionIsCoverExposed));
            conditions.Items.Add(typeof(Conditions.ConditionShouldProcessSuspiciousItem));
            conditions.Items.Add(typeof(Conditions.ConditionSuspiciousItemBTPriority));
            conditions.Items.Add(typeof(Conditions.ConditionSuspiciousItemShouldDoStage));
            conditions.Items.Add(typeof(Conditions.ConditionSuspiciousItemFirstGroupMember));
            conditions.Items.Add(typeof(Conditions.ConditionSuspiciousItemGroupMembersRoutingTo));
            conditions.Items.Add(typeof(Conditions.ConditionSuspiciousItemWaitForGroupRouting));
            conditions.Items.Add(typeof(Conditions.ConditionSuspiciousItemGroupAllowedToProgress));
            conditions.Items.Add(typeof(Conditions.ConditionIsPartOfNPCGroup));
            conditions.Items.Add(typeof(Conditions.ConditionSquadDoingEscalation));
            conditions.Items.Add(typeof(Conditions.ConditionSquadDoingSuspiciousWarning));
            conditions.Items.Add(typeof(Conditions.ConditionAllowedToSearch));
            conditions.Items.Add(typeof(Conditions.ConditionAllowedToDoSuspiciousWarning));
            conditions.Items.Add(typeof(Conditions.ConditionIsGaugeAmountBelow));
            conditions.Items.Add(typeof(Conditions.ConditionHasAnySenseBeenAboveWithinTime));
            conditions.Items.Add(typeof(Conditions.ConditionLastTimeSearchedWithinTime));
            conditions.Items.Add(typeof(Conditions.ConditionLastTimeWasAbleToShootTarget));
            conditions.Items.Add(typeof(Conditions.ConditionCanBreakout));
            conditions.Items.Add(typeof(Conditions.ConditionIsBackstage));
            conditions.Items.Add(typeof(Conditions.ConditionLastTimeSensedFloat));
            conditions.Items.Add(typeof(Conditions.ConditionCanBeControlledByGamepad));
            conditions.Items.Add(typeof(Conditions.ConditionNeedsToGetOutOfTheWay));
            conditions.Items.Add(typeof(Conditions.ConditionHasAmbushPoint));
            conditions.Items.Add(typeof(Conditions.ConditionHasKilltrap));
            conditions.Items.Add(typeof(Conditions.ConditionHasFlankedVentCloseToPlayer));
            conditions.Items.Add(typeof(Conditions.ConditionHasVentCloseToAlien));
            conditions.Items.Add(typeof(Conditions.ConditionAnotherAlienIsAttackingThisTarget));
            conditions.Items.Add(typeof(Conditions.ConditionIsTargetInDeepCrouch));
            conditions.Items.Add(typeof(Conditions.ConditionAlienIsAllowed));
            conditions.Items.Add(typeof(Conditions.ConditionSuspiciousItemIsWithinDistance));
            conditions.Items.Add(typeof(Conditions.ConditionEventAOccuredAfterB));
            conditions.Items.Add(typeof(Conditions.ConditionIsFrameFlagSet));
            conditions.Items.Add(typeof(Conditions.ConditionHasToken));
            conditions.Items.Add(typeof(Conditions.ConditionHasDoneSuspectResponseMoveTo));
            conditions.Items.Add(typeof(Conditions.ConditionHasDoneSuspectResponseWithinTime));
            conditions.Items.Add(typeof(Conditions.ConditionWasSenseThresholdLastIncreaseActivationAbove));
            conditions.Items.Add(typeof(Conditions.ConditionGameIsDifficulty));
            conditions.Items.Add(typeof(Conditions.ConditionInConvo));
            conditions.Items.Add(typeof(Conditions.ConditionTalkingInConvo));
            conditions.Items.Add(typeof(Conditions.ConditionListeningInConvo));
            conditions.Items.Add(typeof(Conditions.ConditionConvoInteruppted));
            conditions.Items.Add(typeof(Conditions.ConditionConvoInterupptedByPlayer));
            conditions.Items.Add(typeof(Conditions.ConditionSomeoneJoinedConvo));
            conditions.Items.Add(typeof(Conditions.ConditionPlayerJoinedConvo));
            conditions.Items.Add(typeof(Conditions.ConditionInPositionForConvo));


			/*
             * DECORATORS
            */
            //decorators.Items.Add(typeof(Decorators.DecoratorAggressionEscalation));


            /*
             * SELECTORS
            */
			//selectors.Items.Add(typeof(Selectors.SelectorLinear));


            /*
             * SEQUENCES
            */
            //sequences.Items.Add(typeof(Sequences.SequenceLinear));


            // register all the file managers
            _fileManagers.Add( new FileManagerInfo(typeof(Brainiac.Design.FileManagers.FileManagerXML), "Behaviour XML (*.xml)|*.xml", ".xml") );

			// register all the exporters
			_exporters.Add( new ExporterInfo(typeof(Brainiac.Design.Exporters.ExporterCs), "C# Behavior Exporter (Assign Properties)", true, "C#Properties") );
			_exporters.Add( new ExporterInfo(typeof(Brainiac.Design.Exporters.ExporterCsUseParameters), "C# Behavior Exporter (Use Parameters)", true, "C#Parameters") );
		}
	}
}
