#include "BT_DEBUG.h"
#include "DEBUG_MENU.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <detours.h>

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <deque>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <tuple>
#include <unordered_map>
#include <vector>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds); the layouts
	are the same in all three. See BT_DEBUG.h for what is followed and how.
*/
namespace
{
	// ---- The game ----

	// The pool every running node's record comes from (one pool for every character): the records follow a header, 1782
	// of them 128 bytes apart. A record pointer is only read if it is one of them (a stale one still points into the pool,
	// so reading it cannot fault).
	constexpr uintptr_t kNodeRecordPool = 0x012B1C20;
	constexpr uint32_t kPoolRecords = 0x1BE0;
	constexpr uint32_t kPoolRecordCount = 1782;
	constexpr uint32_t kPoolRecordSize = 128;

	// The game's clock: the time now in seconds (a double), which stands still while the game is paused
	constexpr uintptr_t kGameTime = 0x012410A8;

	// The vtable entries replaced, and what each holds in the shipped game: the debug text, assert and debug switch
	// actions' start (all three the game's own "succeed at once"), and the debug switch test's test (its default, always)
	constexpr uintptr_t kDebugTextStartSlot = 0x00EECE7C;
	constexpr uintptr_t kAssertStartSlot = 0x00EC596C;
	constexpr uintptr_t kSwitchItemStartSlot = 0x00EC5554;
	constexpr uintptr_t kSwitchTestSlot = 0x00EE4C50;
	constexpr uintptr_t kSucceedAtStart = 0x002D2270;
	constexpr uintptr_t kSwitchTestShipped = 0x002EF6F0;

	// A character's running tree: the tree it runs (a pointer: the tree's root node at +4 of it), the character it is for
	// (a checked pointer: the character, then its unique id), the two paths (each a pointer to a list of node records,
	// root first: the first, then one past the last), its outer branch (a branch type, see kBranchNames), and how many
	// nodes of each branch type its path goes through
	constexpr uint32_t kTreeTemplate = 0x04;
	constexpr uint32_t kTemplateRoot = 0x04;
	constexpr uint32_t kTreeCharacter = 0x0C;
	constexpr uint32_t kTreeCharacterId = 0x10;
	constexpr uint32_t kTreeActivePath = 0x44;
	constexpr uint32_t kTreeWaitingPath = 0x48;
	constexpr uint32_t kTreeOuterBranch = 0x54;
	constexpr uint32_t kTreeBranchCounts = 0x5C;
	constexpr int kBranchTypes = 71;

	// A running node's record: how it is going (see StatusName), what it is used for (1 the active path, 2 the waiting
	// one, 3 a search), the node, the child it runs (in the node's own order of its children) and the child it chose (a
	// condition: 0 its child for false, 1 its child for true)
	constexpr uint32_t kRecordStatus = 0x04;
	constexpr uint32_t kRecordUse = 0x08;
	constexpr uint32_t kRecordNode = 0x10;
	constexpr uint32_t kRecordChild = 0x1C;
	constexpr uint32_t kRecordChosenChild = 0x1D;
	constexpr uint32_t kStatusFailed = 8;
	constexpr uint32_t kStatusSucceeded = 9;

	// Node fields: a branch node's type; the debug text's text (a string object: the characters themselves, or a pointer
	// to them once there are 16 or more, then the length and the room); the assert's message (300 characters); the
	// switch test's default and the switch action's
	constexpr uint32_t kBranchNodeType = 0x08;
	constexpr uint32_t kDebugText = 0x08;
	constexpr uint32_t kDebugTextLength = 0x18;
	constexpr uint32_t kDebugTextRoom = 0x1C;
	constexpr uint32_t kAssertText = 0x08;
	constexpr uint32_t kAssertTextSize = 300;
	constexpr uint32_t kTestDefault = 0x14;
	constexpr uint32_t kItemDefault = 0x08;

	// A character's fields: its debug name (64 characters) and unique id
	constexpr uint32_t kCharacterName = 0x10;
	constexpr uint32_t kNameLength = 0x40;
	constexpr uint32_t kCharacterId = 0x50;

	// A tag of the packed tree file: a pointer to its name, then its flags (the number of attributes in the low byte;
	// bits 8 and 9 a word more each after the attributes; bit 10 set when a sibling follows it; any bit from 11 up set
	// when it has children), then the attributes (a pointer to a name and one to a value each), the extra words, and a
	// pointer to its first child if it has any. Its next sibling starts straight after it.
	constexpr uint32_t kTagFlags = 0x04;
	constexpr uint32_t kTagAttributes = 0x08;
	constexpr uint32_t kTagHasSibling = 0x400;
	constexpr uint32_t kTagHasChildren = 0xFFFFF800;

	// The class each node object names itself by (the second entry of its vtable returns it) and the behaviour tree
	// editor's name for that class, sorted by the first for a binary search. Two editor classes share their parent's
	// object class (HasValidRouteToNearTarget, TargetIsWithinDistanceThreshold), and show as the parent.
	struct ClassName { const char* engine; const char* editor; };
	constexpr ClassName kClassNames[] = {
		{ "ATOMIC_ABORT_MELEE_ATTACK", "ActionAbortMeleeAttack" },
		{ "ATOMIC_AFFECTED_BY_FLAME_THROWER", "ActionAffectedByFlameThrower" },
		{ "ATOMIC_AFFECTED_BY_FLAME_THROWER_IN_VENT", "ActionAffectedByFlameThrowerInVent" },
		{ "ATOMIC_ALIEN_WON_SCARE_TEST", "ActionAlienWonScareTest" },
		{ "ATOMIC_APPLY_PRIMARY_DAMAGE_CONTROL_RESPONSE", "ActionApplyPrimaryDamageControlResponse" },
		{ "ATOMIC_BACKSTAGE_ALIEN_RESPONSE", "ActionBackstageAlienResponse" },
		{ "ATOMIC_BACKSTAGE_AREA_SWEEP", "ActionBackstageAreaSweep" },
		{ "ATOMIC_BREAKOUT", "ActionBreakout" },
		{ "ATOMIC_BROKEN_COVER", "ActionBrokenCover" },
		{ "ATOMIC_CHANGE_COVER", "ActionChangeCover" },
		{ "ATOMIC_DEAD", "ActionDead" },
		{ "ATOMIC_DEBUG_ASSERT", "ActionAssert" },
		{ "ATOMIC_DEBUG_FAIL", "ActionDebugMenuLinkItem" },
		{ "ATOMIC_DEBUG_STRING", "ActionDebugString" },
		{ "ATOMIC_DESPAWN", "ActionDespawn" },
		{ "ATOMIC_DONE_SYSTEMATIC_SEARCH", "ActionDoneSystematicSearch" },
		{ "ATOMIC_EXPIRE_TIMER", "ActionExpireTimer" },
		{ "ATOMIC_FAIL", "ActionFail" },
		{ "ATOMIC_FAKE_SENSE", "ActionFakeSense" },
		{ "ATOMIC_FORCE_IDLE", "ActionForceIdle" },
		{ "ATOMIC_FORCE_SEARCH", "ActionForceSearch" },
		{ "ATOMIC_GET_OUT_OF_THE_WAY", "ActionGetOutOfTheWay" },
		{ "ATOMIC_HIT_TARGET_AND_RUN", "ActionHitTargetAndRun" },
		{ "ATOMIC_IDLE", "ActionIdle" },
		{ "ATOMIC_IDLE_IN_COVER", "ActionIdleInCover" },
		{ "ATOMIC_IDLE_TIME", "ActionIdleTime" },
		{ "ATOMIC_IDLE_TIME_FACING_SUSPICIOUS_ITEM", "ActionIdleTimeFacingSuspiciousItem" },
		{ "ATOMIC_IDLE_TIME_FACING_TARGET", "ActionIdleTimeFacingTarget" },
		{ "ATOMIC_IDLE_TIME_FACING_TARGET_MOST_RECENT_SENSED_POSITION", "ActionIdleTimeFacingTargetMostRecentSensedPosition" },
		{ "ATOMIC_IDLE_TIME_FACING_TARGET_OUTSIDE_COMBAT_AREA", "ActionIdleTimeFacingTargetOutsideCombatArea" },
		{ "ATOMIC_IDLE_TIME_FACING_TARGET_SENSED_POSITION", "ActionIdleTimeFacingTargetSensedPosition" },
		{ "ATOMIC_ListeningInConvo", "ActionListeningInConvo" },
		{ "ATOMIC_MAKE_AGGRESSIVE", "ActionMakeAggressive" },
		{ "ATOMIC_MELEE_ATTACK_TARGET", "ActionMeleeAttack" },
		{ "ATOMIC_MOVE_IN_DIRECTION", "ActionMoveInDirection" },
		{ "ATOMIC_MOVE_THROUGH_TARGET", "ActionMoveThroughTarget" },
		{ "ATOMIC_MOVE_TO_ATTACK_TARGET", "ActionMoveToAttackTarget" },
		{ "ATOMIC_MOVE_TO_BACKSTAGE_VIA_VENT_CLOSEST_TO_ALIEN", "ActionMoveToBackstageViaVentClosestToAlien" },
		{ "ATOMIC_MOVE_TO_COVER", "ActionMoveToCover" },
		{ "ATOMIC_MOVE_TO_FRONTSTAGE_VIA_FLANKED_VENT_CLOSEST_TO_PLAYER", "ActionMoveToFrontStageViaFlankedVentClosestToPlayer" },
		{ "ATOMIC_MOVE_TO_MOST_RECENT_SENSED_POSITION", "ActionMoveToMostRecentSensedPosition" },
		{ "ATOMIC_MOVE_TO_NEAREST_STANDING_POINT_TO_TARGET", "ActionMoveToNearestStandingPointToTarget" },
		{ "ATOMIC_MOVE_TO_OBJECTIVE", "ActionMoveToObjective" },
		{ "ATOMIC_MOVE_TO_TARGET", "ActionMoveToTarget" },
		{ "ATOMIC_MOVE_WITH_GAMEPAD", "ActionMoveWithGamepad" },
		{ "ATOMIC_MoveToConvo", "ActionMoveToConvo" },
		{ "ATOMIC_NOTIFY_SQUAD", "ActionNotifySquad" },
		{ "ATOMIC_PAUSE_SENSES", "ActionPauseSenses" },
		{ "ATOMIC_PERFORM_AMBUSH", "ActionPerformAmbush" },
		{ "ATOMIC_PERFORM_ROLE", "ActionPerformRole" },
		{ "ATOMIC_PLAYER_CONTROLLER", "ActionPlayerController" },
		{ "ATOMIC_PLAY_TREE", "ActionPlayTree" },
		{ "ATOMIC_PLAY_TREE_AND_FACE_TARGET", "ActionPlayTreeAndFaceTarget" },
		{ "ATOMIC_RANGED_AIM", "ActionRangedAim" },
		{ "ATOMIC_RANGED_SHOOT", "ActionRangedShoot" },
		{ "ATOMIC_REQUEST_COVER", "ActionRequestCover" },
		{ "ATOMIC_RESET_SEARCH_JOBS", "ActionResetSearchJobs" },
		{ "ATOMIC_SCRIPT", "ActionScript" },
		{ "ATOMIC_SET_FRAME_FLAG", "ActionSetFrameFlag" },
		{ "ATOMIC_SET_GAUGE_AMOUNT", "ActionSetGaugeAmount" },
		{ "ATOMIC_SET_LOGIC_CHARACTER_FLAGS", "ActionSetLogicCharacterFlags" },
		{ "ATOMIC_SET_MENACE_MANAGER", "ActionSetMenaceManager" },
		{ "ATOMIC_SET_WITHDRAW_STATE", "ActionSetWithdrawState" },
		{ "ATOMIC_START_TIMER", "ActionStartTimer" },
		{ "ATOMIC_START_TIMER_RANDOM", "ActionStartTimerRandom" },
		{ "ATOMIC_SUCCESS", "ActionSuccess" },
		{ "ATOMIC_SUSPECT_TARGET_RESPONSE", "ActionSuspectTargetResponse" },
		{ "ATOMIC_SUSPEND", "ActionSuspend" },
		{ "ATOMIC_SUSPICIOUS_ITEM_DONE_STAGE", "ActionSuspiciousItemDoneStage" },
		{ "ATOMIC_SUSPICIOUS_ITEM_MOVE_TO", "ActionSuspiciousItemMoveTo" },
		{ "ATOMIC_SUSPICIOUS_ITEM_REACTION", "ActionSuspiciousItemReaction" },
		{ "ATOMIC_SWITCH_TO_NEXT_TARGET", "ActionSwitchToNextTarget" },
		{ "ATOMIC_SpeakingInConvo", "ActionSpeakingInConvo" },
		{ "ATOMIC_TAKE_STEP", "ActionTakeStep" },
		{ "ATOMIC_THREAT_AWARE", "ActionThreatAware" },
		{ "ATOMIC_THREAT_ESCALATION", "ActionThreatEscalation" },
		{ "ATOMIC_TRIGGER_SOUND", "ActionTriggerSound" },
		{ "ATOMIC_WEAPON_EQUIP", "ActionWeaponEquip" },
		{ "COMPOSITE_SELECTOR", "SelectorLinear" },
		{ "COMPOSITE_SELECTOR_PERCENTAGE", "SelectorPercentage" },
		{ "COMPOSITE_SEQUENCE", "SequenceLinear" },
		{ "COMPOSITE_SEQUENCE_STATELESS", "SequenceStateless" },
		{ "CONDITION_ALIEN_IS_ALLOWED", "ConditionAlienIsAllowed" },
		{ "CONDITION_ALLOWED_TO_ATTACK_TARGET", "ConditionAllowedToAttackTarget" },
		{ "CONDITION_ALLOWED_TO_DO_SUSPICIOUS_WARNING", "ConditionAllowedToDoSuspiciousWarning" },
		{ "CONDITION_ALLOWED_TO_PURSUE_TARGET", "ConditionAllowedToPursueTarget" },
		{ "CONDITION_ALLOWED_TO_SEARCH", "ConditionAllowedToSearch" },
		{ "CONDITION_ANGLE_FROM_TARGET_AGAINST_TARGET_CAMERA_DIRN_LESS_THAN", "ConditionAngleFromTargetAgainstTargetCameraDirnLessThan" },
		{ "CONDITION_ANGLE_NPC_TO_TARGETS_AIM_LESS_THAN", "ConditionAngleNPCToTargetsAimLessThan" },
		{ "CONDITION_ANGLE_TO_TARGET_LESS_THAN", "ConditionAngleToTargetLessThan" },
		{ "CONDITION_ANOTHER_ALIEN_IS_ATTACKING_THIS_TARGET", "ConditionAnotherAlienIsAttackingThisTarget" },
		{ "CONDITION_CAN_BE_CONTROLLED_BY_GAMEPAD", "ConditionCanBeControlledByGamepad" },
		{ "CONDITION_CAN_BREAKOUT", "ConditionCanBreakout" },
		{ "CONDITION_CAN_SHOOT", "ConditionCanShootNow" },
		{ "CONDITION_CAN_TAKE_STEP", "ConditionCanTakeStep" },
		{ "CONDITION_CHECK_HEALTH_STATE", "ConditionCheckHealthState" },
		{ "CONDITION_CURRENT_WEAPON_IS_EQUIPPED", "ConditionCurrentWeaponIsEquipped" },
		{ "CONDITION_CURRENT_WEAPON_NEEDS_RELOADING", "ConditionCurrentWeaponNeedsReloading" },
		{ "CONDITION_ConvoInterrupted", "ConditionConvoInteruppted" },
		{ "CONDITION_ConvoInterruptedByPlayer", "ConditionConvoInterupptedByPlayer" },
		{ "CONDITION_DEBUG_FAIL", "ConditionDebugMenuLinkTest" },
		{ "CONDITION_EVENT_A_OCCURED_AFTER_B", "ConditionEventAOccuredAfterB" },
		{ "CONDITION_GAME_IS_DIFFICULTY", "ConditionGameIsDifficulty" },
		{ "CONDITION_HAS_AGGRO_LEVEL", "ConditionHasAggroLevel" },
		{ "CONDITION_HAS_ALERTNESS_STATE", "ConditionHasAlertnessState" },
		{ "CONDITION_HAS_AMBUSH_POINT", "ConditionHasAmbushPoint" },
		{ "CONDITION_HAS_ANY_SENSE_BEEN_ABOVE", "ConditionHasAnySenseBeenAbove" },
		{ "CONDITION_HAS_ANY_SENSE_BEEN_ABOVE_WITHIN_TIME", "ConditionHasAnySenseBeenAboveWithinTime" },
		{ "CONDITION_HAS_A_WEAPON", "ConditionHasAWeapon" },
		{ "CONDITION_HAS_DONE_SUSPECT_REPONSE_MOVE_TO", "ConditionHasDoneSuspectResponseMoveTo" },
		{ "CONDITION_HAS_DONE_SUSPECT_REPONSE_WITHIN_TIME", "ConditionHasDoneSuspectResponseWithinTime" },
		{ "CONDITION_HAS_FLANKED_VENT_CLOSE_TO_PLAYER", "ConditionHasFlankedVentCloseToPlayer" },
		{ "CONDITION_HAS_GROUP_AWARENESS_STATE", "ConditionHasGroupAwarenessState" },
		{ "CONDITION_HAS_KILLTRAP", "ConditionHasKilltrap" },
		{ "CONDITION_HAS_MELEE_ATTACK_AVAILABLE", "ConditionHasMeleeAttackAvailable" },
		{ "CONDITION_HAS_MELEE_ATTACK_AVAILABLE_OR_IS_ATTACKING", "ConditionHasMeleeAttackAvailableOrIsAttacking" },
		{ "CONDITION_HAS_MELEE_BLOCK_AVAILABLE", "ConditionHasMeleeBlockAvailable" },
		{ "CONDITION_HAS_MELEE_COUNTERATTACK_AVAILABLE", "ConditionHasMeleeCounterAttackAvailable" },
		{ "CONDITION_HAS_MOTIVATION", "ConditionHasMotivation" },
		{ "CONDITION_HAS_OBJECTIVE", "ConditionHasObjective" },
		{ "CONDITION_HAS_SCRIPT", "ConditionHasScript" },
		{ "CONDITION_HAS_SEARCHED_MOST_RECENT_SENSED_POSITION", "ConditionHasSearchedMostRecentSensedPosition" },
		{ "CONDITION_HAS_SENSE_ACTIVATION_BEEN_ABOVE", "ConditionHasSenseActivationBeenAbove" },
		{ "CONDITION_HAS_TIMER_EXPIRED", "ConditionHasTimerExpired" },
		{ "CONDITION_HAS_TOKEN", "ConditionHasToken" },
		{ "CONDITION_HAS_VALID_COVER_TO_CHANGE_TO", "ConditionHasValidCoverToChangeTo" },
		{ "CONDITION_HAS_VENT_CLOSE_TO_ALIEN", "ConditionHasVentCloseToAlien" },
		{ "CONDITION_HAVE_NEXT_TARGET", "ConditionHaveNextTarget" },
		{ "CONDITION_HAVE_TARGET", "ConditionHaveTarget" },
		{ "CONDITION_HAVE_VALID_ROUTE_TO_NEAR_TARGET", "ConditionHasValidRouteToNearTarget" },
		{ "CONDITION_HAVE_VALID_ROUTE_TO_TARGET", "ConditionHasValidRouteToTarget" },
		{ "CONDITION_IS_ANY_SENSE_ACTIVATION_ABOVE", "ConditionIsAnySenseActivationAbove" },
		{ "CONDITION_IS_BACKSTAGE", "ConditionIsBackstage" },
		{ "CONDITION_IS_BRANCH_ACTIVE", "ConditionIsBranchActive" },
		{ "CONDITION_IS_CHARACTER_CLASS", "ConditionIsCharacterClass" },
		{ "CONDITION_IS_CORPSE_TRAP", "ConditionIsCorpseTrap" },
		{ "CONDITION_IS_COVER_EXPOSED", "ConditionIsCoverExposed" },
		{ "CONDITION_IS_COVER_TOO_CLOSE", "ConditionIsCoverTooClose" },
		{ "CONDITION_IS_CROUCHING", "ConditionIsCrouching" },
		{ "CONDITION_IS_CURRENT_COVER_VALID", "ConditionIsCurrentCoverValid" },
		{ "CONDITION_IS_DEAD", "ConditionIsDead" },
		{ "CONDITION_IS_ENEMY_OF_TARGET", "ConditionIsEnemyOfTarget" },
		{ "CONDITION_IS_FRAME_FLAG_SET", "ConditionIsFrameFlagSet" },
		{ "CONDITION_IS_GAUGE_AMOUNT_ABOVE", "ConditionIsGaugeAmountAbove" },
		{ "CONDITION_IS_GAUGE_AMOUNT_BELOW", "ConditionIsGaugeAmountBelow" },
		{ "CONDITION_IS_IN_COMBAT_AREA", "ConditionIsInCombatArea" },
		{ "CONDITION_IS_IN_COVER", "ConditionIsInCover" },
		{ "CONDITION_IS_IN_CRAWL_SPACE", "ConditionIsInCrawlSpace" },
		{ "CONDITION_IS_IN_TARGETS_WEAPON_RANGE", "ConditionIsInTargetsWeaponRange" },
		{ "CONDITION_IS_IN_VENT", "ConditionIsInVent" },
		{ "CONDITION_IS_PART_OF_NPC_GROUP", "ConditionIsPartOfNPCGroup" },
		{ "CONDITION_IS_PERFORMING_ROLE_OR_COULD_PERFORM_ROLE", "ConditionIsPerformingRoleOrCouldPerformRole" },
		{ "CONDITION_IS_REQUESTING_COVER", "ConditionIsRequestingCover" },
		{ "CONDITION_IS_SENSE_ACTIVATION_ABOVE", "ConditionIsSenseActivationAbove" },
		{ "CONDITION_IS_TARGET_IN_DEEP_CROUCH", "ConditionIsTargetInDeepCrouch" },
		{ "CONDITION_InConvo", "ConditionInConvo" },
		{ "CONDITION_InPositionForConvo", "ConditionInPositionForConvo" },
		{ "CONDITION_LAST_ABLE_TO_SHOOT_TARGET_WITHIN_TIME", "ConditionLastTimeWasAbleToShootTarget" },
		{ "CONDITION_LAST_TIME_SEARCHED_WITHIN_TIME", "ConditionLastTimeSearchedWithinTime" },
		{ "CONDITION_LAST_TIME_SENSED", "ConditionLastTimeSensed" },
		{ "CONDITION_LAST_TIME_SENSED_FLOAT", "ConditionLastTimeSensedFloat" },
		{ "CONDITION_LAST_TIME_SQUAD_NOTIFIED", "ConditionLastTimeSquadNotified" },
		{ "CONDITION_LAST_TIME_TARGET_SHOT_AT_ME", "ConditionLastTimeTargetShotAtMe" },
		{ "CONDITION_LOGIC_CHARACTER_FLAGS", "ConditionLogicCharacterFlags" },
		{ "CONDITION_ListeningInConvo", "ConditionListeningInConvo" },
		{ "CONDITION_MOST_RECENT_SENSE_HAS_BEEN_ABOVE", "ConditionMostRecentSenseActivationHasBeenAbove" },
		{ "CONDITION_NEEDS_TO_GET_OUT_OF_THE_WAY", "ConditionNeedsToGetOutOfTheWay" },
		{ "CONDITION_OBJECTIVE_IS_IN_COMBAT_AREA", "ConditionObjectiveIsInCombatArea" },
		{ "CONDITION_OBJECTIVE_IS_WITHIN_DISTANCE", "ConditionObjectiveIsWithinDistance" },
		{ "CONDITION_PLAYER_IS_AN_ENEMY", "ConditionPlayerIsAnEnemy" },
		{ "CONDITION_PLAYER_IS_IN_EXPLOITABLE_AREA", "ConditionPlayerIsInExploitableArea" },
		{ "CONDITION_PlayerJoinedConvo", "ConditionPlayerJoinedConvo" },
		{ "CONDITION_RANGE_TEST_FOR_SCRIPTED_WITHDRAWAL", "ConditionRangeTestForScriptedWithdrawal" },
		{ "CONDITION_REQUIRES_PRIMARY_DAMAGE_CONTROL_RESPONSE", "ConditionRequiresPrimaryDamageControlResponse" },
		{ "CONDITION_SCRIPTED_WITHDRAW_REQUESTED", "ConditionScriptedWithdrawRequested" },
		{ "CONDITION_SHOULD_FOLLOW", "ConditionShouldFollow" },
		{ "CONDITION_SHOULD_PROCESS_SUSPICIOUS_ITEM", "ConditionShouldProcessSuspiciousItem" },
		{ "CONDITION_SHOULD_SUSPEND", "ConditionShouldSuspend" },
		{ "CONDITION_SHOULD_USE_COVER", "ConditionShouldUseCover" },
		{ "CONDITION_SQUAD_DOING_ESCALATION", "ConditionSquadDoingEscalation" },
		{ "CONDITION_SQUAD_DOING_SUSPICIOUS_WARNING", "ConditionSquadDoingSuspiciousWarning" },
		{ "CONDITION_SUSPICIOUS_ITEM_BT_PRIORITY", "ConditionSuspiciousItemBTPriority" },
		{ "CONDITION_SUSPICIOUS_ITEM_FIRST_GROUP_MEMBER", "ConditionSuspiciousItemFirstGroupMember" },
		{ "CONDITION_SUSPICIOUS_ITEM_GROUP_ALLOWED_TO_PROGRESS", "ConditionSuspiciousItemGroupAllowedToProgress" },
		{ "CONDITION_SUSPICIOUS_ITEM_GROUP_MEMBERS_ROUTING_TO", "ConditionSuspiciousItemGroupMembersRoutingTo" },
		{ "CONDITION_SUSPICIOUS_ITEM_IS_WITHIN_DISTANCE", "ConditionSuspiciousItemIsWithinDistance" },
		{ "CONDITION_SUSPICIOUS_ITEM_SHOULD_DO_STAGE", "ConditionSuspiciousItemShouldDoStage" },
		{ "CONDITION_SUSPICIOUS_ITEM_WAIT_FOR_GROUP_ROUTING", "ConditionSuspiciousItemWaitForGroupRouting" },
		{ "CONDITION_SomeoneJoinedConvo", "ConditionSomeoneJoinedConvo" },
		{ "CONDITION_TARGETS_WEAPON_HAS_AMMO", "ConditionTargetsWeaponHasAmmo" },
		{ "CONDITION_TARGETS_WEAPON_HAS_PROPERTY", "ConditionTargetsWeaponHasProperty" },
		{ "CONDITION_TARGET_IS_IN_COMBAT_AREA", "ConditionTargetIsInCombatArea" },
		{ "CONDITION_TARGET_IS_IN_WEAPON_RANGE", "ConditionTargetIsInWeaponRange" },
		{ "CONDITION_TARGET_IS_ONLY_ACCESSIBLE_CROUCHING", "ConditionTargetIsOnlyAccessibleCrouching" },
		{ "CONDITION_TARGET_IS_PLAYER", "ConditionTargetIsPlayer" },
		{ "CONDITION_TARGET_IS_TARGETING_ME", "ConditionTargetIsTargetingMe" },
		{ "CONDITION_TARGET_IS_USING_MELEE_ATTACK", "ConditionTargetIsUsingMeleeAttack" },
		{ "CONDITION_TARGET_IS_WITHIN_AGGRO_RADIUS", "ConditionTargetIsWithinAggroRadius" },
		{ "CONDITION_TARGET_IS_WITHIN_DISTANCE", "ConditionTargetIsWithinDistance" },
		{ "CONDITION_TARGET_IS_WITHIN_DISTANCE_OF_ALIEN", "ConditionTargetIsWithinDistanceOfAlien" },
		{ "CONDITION_TARGET_IS_WITHIN_DISTANCE_THRESHOLD", "ConditionTargetIsWithinDistanceThreshold" },
		{ "CONDITION_TARGET_IS_WITHIN_DISTANCE_UNOBSCURED", "ConditionTargetIsWithinDistanceUnobscured" },
		{ "CONDITION_TARGET_IS_WITHIN_ROUTING_DISTANCE", "ConditionTargetIsWithinRoutingDistance" },
		{ "CONDITION_TARGET_LOGIC_CHARACTER_FLAGS", "ConditionTargetLogicCharacterFlags" },
		{ "CONDITION_TARGET_NEAREST_STAND_POINT_IS_WITHIN_DISTANCE", "ConditionTargetNearestStandPointIsWithinDistance" },
		{ "CONDITION_TalkingInConvo", "ConditionTalkingInConvo" },
		{ "CONDITION_VISUAL_SOME_RAYS_THROUGH_GLASS", "ConditionVisualSomeRaysThroughGlass" },
		{ "CONDITION_WAS_SENSE_THRESHOLD_LAST_INCREASE_ACTIVATION", "ConditionWasSenseThresholdLastIncreaseActivation" },
		{ "CONDITION_WAS_SENSE_THRESHOLD_LAST_INCREASE_ACTIVATION_ABOVE", "ConditionWasSenseThresholdLastIncreaseActivationAbove" },
		{ "CONDITION_WITHDRAW_STATE", "ConditionWithdrawState" },
		{ "DECORATOR_AGGRESSION_ESCALATION", "DecoratorAggressionEscalation" },
		{ "DECORATOR_AWARENESS_STATE", "DecoratorAwarenessState" },
		{ "DECORATOR_BRANCH", "DecoratorBranch" },
		{ "DECORATOR_LOCK_BEST_VENTS", "DecoratorLockBestVents" },
		{ "DECORATOR_LOOP", "DecoratorLoop" },
		{ "DECORATOR_MOOD", "DecoratorMood" },
		{ "DECORATOR_PERCENTAGE", "DecoratorPercentage" },
		{ "DECORATOR_SET_SENSE_SET", "DecoratorSetSenseSet" },
		{ "DECORATOR_SQUAD_SEARCH", "DecoratorSquadSearch" },
		{ "DECORATOR_SUSPICIOUS_ITEM_IN_PROGRESS", "DecoratorSuspiciousItemInProgress" },
		{ "DECORATOR_TIMER", "DecoratorTimer" },
	};

	// The branch types, by number (as the editor's BranchType attribute names them)
	constexpr const char* kBranchNames[kBranchTypes] = {
		"NONE", "CINEMATIC_BRANCH", "ATTACK_BRANCH", "AIM_BRANCH", "DESPAWN_BRANCH", "FOLLOW_BRANCH", "STANDARD_BRANCH",
		"SEARCH_BRANCH", "AREA_SWEEP_BRANCH", "BACKSTAGE_AREA_SWEEP_BRANCH", "SHOT_BRANCH", "SUSPECT_TARGET_RESPONSE_BRANCH",
		"THREAT_AWARE_BRANCH", "BACKSTAGE_AMBUSH_BRANCH", "IDLE_JOB_BRANCH", "USE_COVER_BRANCH", "ASSAULT_BRANCH",
		"MELEE_BRANCH", "RETREAT_BRANCH", "CLOSE_ON_TARGET_BRANCH", "MUTUAL_MELEE_ONLY_BRANCH", "VENT_MELEE_BRANCH",
		"ASSAULT_NOT_ALLOWED_BRANCH", "IN_VENT_BRANCH", "CLOSE_COMBAT_BRANCH", "DRAW_WEAPON_BRANCH", "PURSUE_TARGET_BRANCH",
		"RANGED_ATTACK_BRANCH", "RANGED_COMBAT_BRANCH", "PRIMARY_CONTROL_RESPONSE_BRANCH", "DEAD_BRANCH", "SCRIPT_BRANCH",
		"IDLE_BRANCH", "DOWN_BUT_NOT_OUT_BRANCH", "MELEE_BLOCK_BRANCH", "AGRESSIVE_BRANCH", "ALERT_BRANCH", "SHOOTING_BRANCH",
		"GRAPPLE_BREAK_BRANCH", "REACT_TO_WEAPON_FIRE_BRANCH", "IN_COVER_BRANCH", "SUSPICIOUS_ITEM_BRANCH_HIGH",
		"SUSPICIOUS_ITEM_BRANCH_MEDIUM", "SUSPICIOUS_ITEM_BRANCH_LOW", "AGGRESSION_ESCALATION_BRANCH", "STUN_DAMAGE_BRANCH",
		"BREAKOUT_BRANCH", "SUSPEND_BRANCH", "TARGET_IS_NPC_BRANCH", "PLAYER_HIDING_BRANCH", "ATTACK_CORE_BRANCH",
		"CORPSE_TRAP_BRANCH", "OBSERVE_TARGET_BRANCH", "TARGET_IN_CRAWLSPACE_BRANCH", "MB_SUSPICIOUS_ITEM_ATLEAST_MOVE_CLOSE_TO",
		"MB_THREAT_AWARE_ATTACK_TARGET_WITHIN_CLOSE_RANGE", "MB_THREAT_AWARE_ATTACK_TARGET_WITHIN_VERY_CLOSE_RANGE",
		"MB_THREAT_AWARE_ATTACK_TARGET_FLAMED_ME", "MB_THREAT_AWARE_ATTACK_WEAPON_NOT_AIMED", "MB_THREAT_AWARE_MOVE_TO_LOST_VISUAL",
		"MB_THREAT_AWARE_MOVE_TO_BEFORE_ANIM", "MB_THREAT_AWARE_MOVE_TO_AFTER_ANIM", "MB_THREAT_AWARE_MOVE_TO_FLANKED_VENT",
		"KILLTRAP_BRANCH", "PANIC_BRANCH", "BACKSTAGE_ALIEN_RESPONSE_BRANCH", "NPC_VS_ALIEN_BRANCH", "USE_COVER_VS_ALIEN_BRANCH",
		"IN_COVER_VS_ALIEN_BRANCH", "REPEATED_PATHFIND_FAILS_BRANCH", "ALL_SEARCH_VARIANTS_BRANCH",
	};

	// ---- Limits ----

	constexpr int kMaxDepth = 64;          // nodes kept of each path (deeper ones are cut, and the cut shown)
	constexpr int kMaxEvents = 32;         // events kept for each character
	constexpr int kMaxLabels = 64;         // distinct switch labels in one run (the shipped trees have 18)
	constexpr int kMaxSwitchNodes = 256;   // switch nodes in one level (the shipped trees have 22)
	constexpr int kMaxFileNodes = 8192;    // nodes numbered in one tree file (the largest shipped file has 213)
	constexpr int kMaxTagDepth = 256;      // how deep the numbering goes into one file's tags
	constexpr int kMaxAttributes = 24;     // attributes read from one node's tag
	constexpr size_t kMaxLogLines = 1000;
	constexpr ULONGLONG kLogAgainAfterMs = 1000; // a text from the same node and character is a new line after this gap
	constexpr ULONGLONG kOverlayTextMs = 4000;   // how long a debug text shows over its character
	constexpr ULONGLONG kForgetAfterMs = 10000;  // a character not run for this long is forgotten
	constexpr int kOverlayMost = 12;             // the most characters labelled at once (the nearest)
	constexpr float kOverlayHeight = 2.1f;       // the labels' height above a character's feet (m)

	// ---- What is kept ----

	// A node noted as the trees loaded
	struct NodeRecord
	{
		int file = -1;           // in NodeMap::files
		int index = -1;          // its number in that file, in document order (0 is the file's root); -1 unknown
		std::string editorClass; // the editor's class, without the plugin's prefix
		std::string name;        // its Name attribute
		std::string key;         // the attribute that says most about it: a branch type, a switch label, a debug text
		std::string params;      // its other attributes, short
		int label = -1;          // a debug switch: its label's slot
	};

	// A tree file as loaded: a top-level tree, or one a tree refers to (loaded again, in place, each time)
	struct FileRecord
	{
		std::string name;     // in upper case, without its extension
		int parent = -1;      // the file that refers to it (-1: a top-level tree)
		int parentIndex = -1; // the number of the referring node in that file
		int nodes = 0;        // its node tags
		std::string tree;     // the top-level tree it was loaded for
	};

	struct NodeMap
	{
		uint32_t generation = 0;
		std::unordered_map<const void*, NodeRecord> nodes;
		std::vector<FileRecord> files;
		std::unordered_map<const void*, int> roots; // a top-level tree's root node -> its file
	};

	// One node of a path, as its tree was last run
	struct Entry
	{
		const void* node = nullptr;          // only compared, or looked up in the node map
		const char* engineClass = nullptr;   // the class the node object names itself by (a string in the game's own image)
		uint8_t status = 0;
		uint8_t use = 0;
		uint8_t child = 0;
		uint8_t chosen = 0;
		uint8_t branch = 0xFF;               // a branch node's type (0xFF: not a branch node)
		bool valid = false;                  // the record was one of the pool's
	};

	struct Path
	{
		Entry entries[kMaxDepth];
		int count = 0;
		bool cut = false;
	};

	enum class EventKind : uint8_t { Succeeded, Failed, BranchStarted, BranchEnded, DebugText, Assert, SwitchReset };

	struct Event
	{
		EventKind kind = EventKind::Succeeded;
		uint8_t branch = 0;
		const void* node = nullptr;
		const char* engineClass = nullptr;
		ULONGLONG at = 0;
		char text[96] = {};
	};

	// A character's tree, as last run
	struct State
	{
		uint32_t id = 0;
		uint32_t generation = 0;
		const void* root = nullptr; // the root node of the tree it runs: names the tree
		Path active;
		Path waiting;
		uint8_t outer = 0;
		uint8_t before = 0;         // the outer branch it was in before this one (when it changed while followed)
		bool beforeKnown = false;
		uint8_t branches[kBranchTypes] = {};
		bool seen = false;
		ULONGLONG at = 0;
		Event events[kMaxEvents];
		uint32_t eventCount = 0;    // events ever noted (the newest is at (eventCount - 1) % kMaxEvents)
		char lastText[96] = {};     // its latest debug text or assert, and when it was last run through
		bool lastTextAssert = false;
		ULONGLONG lastTextAt = 0;
	};

	// A line of the tree log
	struct LogLine
	{
		uint64_t sequence = 0;
		uint32_t character = 0;
		std::string characterName;
		bool assert = false;
		std::string text;
		std::string where;     // the tree, file and node number (when the loading was followed)
		uint32_t count = 1;    // times run through since the line was logged
		ULONGLONG firstAt = 0;
		ULONGLONG lastAt = 0;
		double gameTime = -1.0;
	};

	// A debug switch's label, across levels (its slot stays the label's for the run)
	struct LabelInfo
	{
		std::string text;
		bool inLevel = false;       // in the running level's trees
		int defaultValue = -1;      // the test's default (the action's if there is no test): 0 or 1
		bool test = false, item = false;
		std::vector<std::string> where; // the files it is in (and their top-level trees)
	};

	struct SwitchNode
	{
		const void* node = nullptr;
		int label = -1;
		bool item = false;
	};

	// Whether the hooks are in (the replaced vtable entries work either way: they fall back on the game's own)
	std::atomic<bool> g_attached = false;
	std::atomic<uint32_t> g_generation = 1; // a level's trees: moves on at each load and close

	// The game's image (a vtable, a function or a class name must lie in it) and the record pool
	uintptr_t g_imageStart = 0, g_imageEnd = 0;
	uintptr_t g_poolStart = 0;
	const double* g_gameTime = nullptr;

	// The node map, built as the trees load and handed over when they finish (under g_mapMutex)
	std::mutex g_mapMutex;
	std::shared_ptr<const NodeMap> g_map;

	// The loading (the loading thread only, while g_loadingThread is it)
	struct Context
	{
		int file = -1;
		std::unordered_map<const void*, int> tags; // the file's node tags -> their numbers
		bool walked = false;
		int current = -1;                          // the number of the node being read
	};
	struct Building
	{
		std::shared_ptr<NodeMap> map;
		std::vector<Context> files;
		std::vector<SwitchNode> switches;
		bool failed = false;
	};
	Building g_building;
	std::atomic<DWORD> g_loadingThread = 0;

	// The switch nodes of the running level, for the replaced vtable entries (behind g_switchLock: shared to look up,
	// taken whole to hand over a level's)
	SRWLOCK g_switchLock = SRWLOCK_INIT;
	SwitchNode g_switchNodes[kMaxSwitchNodes];
	int g_switchNodeCount = 0;

	// The switches' ticks (a label's slot), whether they apply only to the character selected in the menu, and that
	// character's id (kept up to date by the menu each frame). Named for the live link: "tree_switch." and the label.
	std::atomic<bool> g_ticks[kMaxLabels];
	std::atomic<bool> g_selectedOnly = false;
	std::atomic<uint32_t> g_scopeCharacter = 0;
	std::mutex g_labelMutex;
	std::vector<LabelInfo> g_labels; // by slot

	// The labels over the characters
	std::atomic<bool> g_overlay = false;

	// The characters' trees (under g_stateMutex)
	std::mutex g_stateMutex;
	std::unordered_map<uint32_t, State> g_states;
	ULONGLONG g_sweptAt = 0;

	// The tree log (under g_logMutex): the lines, and the latest line's sequence for each node and character
	std::mutex g_logMutex;
	std::deque<LogLine> g_log;
	uint64_t g_nextSequence = 1;
	std::map<std::tuple<uint32_t, uint32_t, const void*>, uint64_t> g_logLatest;

	// The vtable entries as the game had them
	typedef void(__thiscall* t_start)(const void* node, void* character, void* record, void* tree, bool a, bool b);
	typedef int(__thiscall* t_condition_test)(const void* node, void* character, void* record);
	typedef const char*(__thiscall* t_class_name)(const void* node);
	struct Slot
	{
		uintptr_t* address = nullptr;
		uintptr_t original = 0;
	};
	Slot g_debugTextSlot, g_assertSlot, g_itemSlot, g_testSlot;
	bool g_hooked[4] = {};

	// ---- Small helpers ----

	bool InImage(uintptr_t address)
	{
		return address >= g_imageStart && address < g_imageEnd;
	}

	bool InImage(const void* address)
	{
		return InImage(reinterpret_cast<uintptr_t>(address));
	}

	bool IsPoolRecord(const uint8_t* record)
	{
		const uintptr_t at = reinterpret_cast<uintptr_t>(record);
		return g_poolStart && at >= g_poolStart && at < g_poolStart + kPoolRecordCount * kPoolRecordSize && (at - g_poolStart) % kPoolRecordSize == 0;
	}

	const char* BranchName(int branch)
	{
		return branch >= 0 && branch < kBranchTypes ? kBranchNames[branch] : "?";
	}

	const char* EditorClassOf(const char* engineClass)
	{
		if (!engineClass)
			return nullptr;
		const ClassName* begin = std::begin(kClassNames);
		const ClassName* end = std::end(kClassNames);
		const ClassName* found = std::lower_bound(begin, end, engineClass, [](const ClassName& c, const char* name) { return strcmp(c.engine, name) < 0; });
		return found != end && strcmp(found->engine, engineClass) == 0 ? found->editor : nullptr;
	}

	const char* StatusName(int status)
	{
		switch (status)
		{
		case 0: return "not started";
		case 1: return "starting";
		case 2: return "running";
		case 3: return "starting a child";
		case 4: return "running a child";
		case 5: return "main task done";
		case 6: return "main task done, running";
		case 7: return "shutting down";
		case 8: return "failed";
		case 9: return "succeeded";
		case 10: return "finished";
		default: return "?";
		}
	}

	ImU32 StatusColour(int status)
	{
		switch (status)
		{
		case 1: return IM_COL32(150, 180, 240, 255);
		case 2: case 3: case 4: case 5: case 6: return IM_COL32(240, 205, 90, 255);
		case 7: return IM_COL32(240, 150, 60, 255);
		case 8: return IM_COL32(240, 95, 95, 255);
		case 9: case 10: return IM_COL32(120, 225, 120, 255);
		default: return IM_COL32(190, 190, 190, 255);
		}
	}

	bool StartsWith(const std::string& text, const char* start)
	{
		return text.compare(0, strlen(start), start) == 0;
	}

	bool EqualsIgnoringCase(const char* a, const char* b)
	{
		return _stricmp(a, b) == 0;
	}

	// An attribute value as the editor writes an enum ("SST_GRACEFULL:0"), without the number
	std::string WithoutNumber(const char* value)
	{
		std::string text = value;
		const size_t colon = text.rfind(':');
		if (colon != std::string::npos && colon + 1 < text.size() && std::all_of(text.begin() + colon + 1, text.end(), [](char c) { return isdigit(static_cast<unsigned char>(c)) != 0; }))
			text.erase(colon);
		return text;
	}

	std::string FileNameOf(const char* name)
	{
		std::string text = name;
		const size_t slash = text.find_last_of("\\/");
		if (slash != std::string::npos)
			text.erase(0, slash + 1);
		const size_t dot = text.rfind('.');
		if (dot != std::string::npos)
			text.erase(dot);
		for (char& c : text)
			c = static_cast<char>(toupper(static_cast<unsigned char>(c)));
		return text;
	}

	std::shared_ptr<const NodeMap> PublishedMap()
	{
		std::lock_guard<std::mutex> lock(g_mapMutex);
		return g_map;
	}

	// ---- Guarded reads of the game (no objects with destructors in these) ----

	// Copies a C string from the game, at most size - 1 characters; empty if it faulted
	bool CopyText(const char* from, char* to, size_t size)
	{
		__try
		{
			size_t i = 0;
			for (; from && i + 1 < size && from[i]; i++)
				to[i] = from[i];
			to[i] = '\0';
			return from != nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			to[0] = '\0';
			return false;
		}
	}

	double ReadGameTime()
	{
		__try
		{
			return g_gameTime ? *g_gameTime : -1.0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1.0;
		}
	}

	// The tag helpers, used inside the guarded readers below
	uint32_t TagWords(uint32_t flags)
	{
		return 2 + 2 * (flags & 0xFF) + ((flags >> 8) & 1) + ((flags >> 9) & 1);
	}

	const uint8_t* FirstChild(const uint8_t* tag)
	{
		const uint32_t flags = *reinterpret_cast<const uint32_t*>(tag + kTagFlags);
		if (!(flags & kTagHasChildren))
			return nullptr;
		return *reinterpret_cast<const uint8_t* const*>(tag + 4 * TagWords(flags));
	}

	const uint8_t* NextSibling(const uint8_t* tag)
	{
		const uint32_t flags = *reinterpret_cast<const uint32_t*>(tag + kTagFlags);
		if (!(flags & kTagHasSibling))
			return nullptr;
		return tag + 4 * (TagWords(flags) + ((flags & kTagHasChildren) ? 1 : 0));
	}

	bool IsNodeTag(const uint8_t* tag)
	{
		const char* name = *reinterpret_cast<const char* const*>(tag);
		return name && _stricmp(name, "Node") == 0;
	}

	// Numbers a tree file's nodes as the editor's XML has them: every tag named Node under (and including) the file's
	// root, in document order. The tags are put in out (at most max); the count, or -1 if the file could not be read.
	int WalkNodeTags(const uint8_t* root, const uint8_t** out, int max)
	{
		static const uint8_t* parents[kMaxTagDepth];
		__try
		{
			int count = 0, depth = 0;
			const uint8_t* tag = root;
			for (;;)
			{
				if (IsNodeTag(tag))
				{
					if (count >= max)
						return count;
					out[count++] = tag;
				}
				const uint8_t* child = FirstChild(tag);
				if (child && depth < kMaxTagDepth)
				{
					parents[depth++] = tag;
					tag = child;
					continue;
				}
				// Onto the next sibling, or up to the parent's (the root's own siblings are other files' business)
				for (;;)
				{
					if (depth == 0)
						return count;
					const uint8_t* sibling = NextSibling(tag);
					if (sibling)
					{
						tag = sibling;
						break;
					}
					tag = parents[--depth];
				}
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	struct Attribute
	{
		char name[40];
		char value[256];
	};

	// A tag's attributes (at most max), each name and value cut to fit; -1 if the tag could not be read
	int ReadAttributes(const uint8_t* tag, Attribute* out, int max)
	{
		__try
		{
			const uint32_t flags = *reinterpret_cast<const uint32_t*>(tag + kTagFlags);
			const int count = static_cast<int>(flags & 0xFF);
			int read = 0;
			for (int i = 0; i < count && read < max; i++)
			{
				const char* const* pair = reinterpret_cast<const char* const*>(tag + kTagAttributes + i * 8);
				if (!pair[0] || !pair[1])
					continue;
				CopyText(pair[0], out[read].name, sizeof(out[read].name));
				CopyText(pair[1], out[read].value, sizeof(out[read].value));
				read++;
			}
			return read;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// The class a node object names itself by: the second entry of its vtable (it only returns a string of the game's).
	// Only on the thread running the node's tree, inside a guarded reader.
	const char* NodeClass(const uint8_t* node)
	{
		if (!node)
			return nullptr;
		const uintptr_t* vtable = *reinterpret_cast<const uintptr_t* const*>(node);
		if (!InImage(vtable) || !InImage(vtable[1]))
			return nullptr;
		const char* name = reinterpret_cast<t_class_name>(vtable[1])(node);
		return InImage(name) ? name : nullptr;
	}

	// A list of node records (first, one past the last) into a path, inside a guarded reader
	void ReadPath(const uint8_t* list, Path& path)
	{
		path.count = 0;
		path.cut = false;
		if (!list)
			return;
		const uint8_t* const* first = *reinterpret_cast<const uint8_t* const* const*>(list);
		const uint8_t* const* last = *reinterpret_cast<const uint8_t* const* const*>(list + 4);
		if (!first || last < first)
			return;
		const size_t count = static_cast<size_t>(last - first);
		path.cut = count > kMaxDepth;
		for (size_t i = 0; i < count && i < kMaxDepth; i++)
		{
			Entry& entry = path.entries[path.count++];
			entry = Entry();
			const uint8_t* record = first[i];
			if (!IsPoolRecord(record))
				continue;
			entry.valid = true;
			entry.status = static_cast<uint8_t>(*reinterpret_cast<const uint32_t*>(record + kRecordStatus));
			entry.use = static_cast<uint8_t>(*reinterpret_cast<const uint32_t*>(record + kRecordUse));
			entry.child = record[kRecordChild];
			entry.chosen = record[kRecordChosenChild];
			const uint8_t* node = *reinterpret_cast<const uint8_t* const*>(record + kRecordNode);
			entry.node = node;
			entry.engineClass = NodeClass(node);
			if (entry.engineClass && strcmp(entry.engineClass, "DECORATOR_BRANCH") == 0)
			{
				const uint32_t branch = *reinterpret_cast<const uint32_t*>(node + kBranchNodeType);
				entry.branch = branch < kBranchTypes ? static_cast<uint8_t>(branch) : 0xFF;
			}
		}
	}

	// What a character's tree run left (the thread that ran it)
	struct TreeRead
	{
		uint32_t id = 0;
		const void* root = nullptr;
		Path active;
		Path waiting;
		uint32_t outer = 0;
		uint8_t branches[kBranchTypes] = {};
	};

	bool ReadTree(const uint8_t* tree, TreeRead& out)
	{
		__try
		{
			const uint8_t* character = *reinterpret_cast<const uint8_t* const*>(tree + kTreeCharacter);
			const uint32_t id = *reinterpret_cast<const uint32_t*>(tree + kTreeCharacterId);
			if (!character || *reinterpret_cast<const uint32_t*>(character + kCharacterId) != id)
				return false;
			out.id = id;
			const uint8_t* treeTemplate = *reinterpret_cast<const uint8_t* const*>(tree + kTreeTemplate);
			out.root = treeTemplate ? *reinterpret_cast<const void* const*>(treeTemplate + kTemplateRoot) : nullptr;
			ReadPath(*reinterpret_cast<const uint8_t* const*>(tree + kTreeActivePath), out.active);
			ReadPath(*reinterpret_cast<const uint8_t* const*>(tree + kTreeWaitingPath), out.waiting);
			out.outer = *reinterpret_cast<const uint32_t*>(tree + kTreeOuterBranch);
			memcpy(out.branches, tree + kTreeBranchCounts, sizeof(out.branches));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The leading node of a tree whose leading action has just ended (the thread running it)
	struct LeadingRead
	{
		uint32_t id = 0;
		const void* node = nullptr;
		const char* engineClass = nullptr;
		uint32_t status = 0;
	};

	bool ReadLeading(const uint8_t* tree, LeadingRead& out)
	{
		__try
		{
			const uint8_t* character = *reinterpret_cast<const uint8_t* const*>(tree + kTreeCharacter);
			out.id = *reinterpret_cast<const uint32_t*>(tree + kTreeCharacterId);
			if (!character || *reinterpret_cast<const uint32_t*>(character + kCharacterId) != out.id)
				return false;
			const uint8_t* list = *reinterpret_cast<const uint8_t* const*>(tree + kTreeActivePath);
			if (!list)
				return false;
			const uint8_t* const* first = *reinterpret_cast<const uint8_t* const* const*>(list);
			const uint8_t* const* last = *reinterpret_cast<const uint8_t* const* const*>(list + 4);
			if (!first || last <= first)
				return false;
			const uint8_t* record = last[-1];
			if (!IsPoolRecord(record))
				return false;
			out.status = *reinterpret_cast<const uint32_t*>(record + kRecordStatus);
			const uint8_t* node = *reinterpret_cast<const uint8_t* const*>(record + kRecordNode);
			out.node = node;
			out.engineClass = NodeClass(node);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A debug text or assert being run (the thread running its tree): the character, and the text
	struct TextRead
	{
		uint32_t id = 0;
		char name[kNameLength + 1] = {};
		char text[256] = {};
	};

	bool ReadText(const uint8_t* node, const uint8_t* character, bool assert, TextRead& out)
	{
		__try
		{
			if (!character)
				return false;
			out.id = *reinterpret_cast<const uint32_t*>(character + kCharacterId);
			memcpy(out.name, character + kCharacterName, kNameLength);
			out.name[kNameLength] = '\0';
			if (assert)
			{
				const char* message = reinterpret_cast<const char*>(node + kAssertText);
				size_t i = 0;
				for (; i + 1 < sizeof(out.text) && i < kAssertTextSize && message[i]; i++)
					out.text[i] = message[i];
				out.text[i] = '\0';
			}
			else
			{
				const uint32_t length = *reinterpret_cast<const uint32_t*>(node + kDebugTextLength);
				const uint32_t room = *reinterpret_cast<const uint32_t*>(node + kDebugTextRoom);
				const char* text = room >= 16 ? *reinterpret_cast<const char* const*>(node + kDebugText) : reinterpret_cast<const char*>(node + kDebugText);
				size_t i = 0;
				for (; text && i + 1 < sizeof(out.text) && i < length; i++)
					out.text[i] = text[i];
				out.text[i] = '\0';
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	uint32_t ReadCharacterId(const uint8_t* character)
	{
		__try
		{
			return character ? *reinterpret_cast<const uint32_t*>(character + kCharacterId) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// A switch node's default (a byte of the node), or -1 if it could not be read
	int ReadDefault(const uint8_t* node, uint32_t offset)
	{
		__try
		{
			return node[offset] != 0 ? 1 : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// ---- The loading ----

	bool Capturing()
	{
		return g_loadingThread == GetCurrentThreadId() && !g_building.failed && g_building.map;
	}

	void BeginLoading()
	{
		try
		{
			g_generation++;
			{
				std::lock_guard<std::mutex> lock(g_mapMutex);
				g_map.reset();
			}
			AcquireSRWLockExclusive(&g_switchLock);
			g_switchNodeCount = 0;
			ReleaseSRWLockExclusive(&g_switchLock);
			{
				std::lock_guard<std::mutex> lock(g_stateMutex);
				g_states.clear();
			}
			// The switches start unticked at each level
			for (std::atomic<bool>& tick : g_ticks)
				tick = false;
			{
				std::lock_guard<std::mutex> lock(g_labelMutex);
				for (LabelInfo& label : g_labels)
				{
					label.inLevel = false;
					label.defaultValue = -1;
					label.test = label.item = false;
					label.where.clear();
				}
			}
			g_building = Building();
			g_building.map = std::make_shared<NodeMap>();
			g_building.map->generation = g_generation;
			g_loadingThread = GetCurrentThreadId();
		}
		catch (...)
		{
			g_loadingThread = 0;
		}
	}

	void EndLoading()
	{
		if (g_loadingThread != GetCurrentThreadId())
			return;
		g_loadingThread = 0;
		try
		{
			if (g_building.failed || !g_building.map)
			{
				DevTools::Log("Behaviour trees: the loading could not be followed; the debug menu shows node classes only");
				g_building = Building();
				return;
			}
			NodeMap& map = *g_building.map;
			// Each file's top-level tree
			for (FileRecord& file : map.files)
			{
				const FileRecord* top = &file;
				for (int guard = 0; top->parent >= 0 && guard < 64; guard++)
					top = &map.files[top->parent];
				file.tree = top->name;
			}
			const size_t nodes = map.nodes.size(), files = map.files.size(), trees = map.roots.size();
			const std::vector<SwitchNode> switches = std::move(g_building.switches);
			{
				std::lock_guard<std::mutex> lock(g_mapMutex);
				g_map = std::move(g_building.map);
			}
			AcquireSRWLockExclusive(&g_switchLock);
			g_switchNodeCount = 0;
			for (const SwitchNode& node : switches)
				if (g_switchNodeCount < kMaxSwitchNodes)
					g_switchNodes[g_switchNodeCount++] = node;
			ReleaseSRWLockExclusive(&g_switchLock);
			g_building = Building();
			DevTools::Log("Behaviour trees: %u top-level trees, %u files loaded, %u nodes named, %u debug switch nodes", static_cast<unsigned>(trees),
				static_cast<unsigned>(files), static_cast<unsigned>(nodes), static_cast<unsigned>(switches.size()));
		}
		catch (...)
		{
			g_building = Building();
		}
	}

	// A file starts loading: false if it is not followed (then nothing is noted until it finishes)
	bool PushFile(const char* name)
	{
		try
		{
			char text[128];
			CopyText(name, text, sizeof(text));
			FileRecord file;
			file.name = FileNameOf(text);
			if (!g_building.files.empty())
			{
				file.parent = g_building.files.back().file;
				file.parentIndex = g_building.files.back().current;
			}
			Context context;
			context.file = static_cast<int>(g_building.map->files.size());
			g_building.map->files.push_back(std::move(file));
			g_building.files.push_back(std::move(context));
			return true;
		}
		catch (...)
		{
			g_building.failed = true;
			return false;
		}
	}

	void PopFile(void* root)
	{
		try
		{
			if (g_building.files.empty())
				return;
			const int file = g_building.files.back().file;
			g_building.files.pop_back();
			if (g_building.files.empty() && root)
				g_building.map->roots[root] = file;
		}
		catch (...)
		{
			g_building.failed = true;
		}
	}

	// Before a node is read: the first in a file is its root, and the file's nodes are numbered from it. Gives the
	// node's number, and the number of the node being read before it (put back afterwards).
	bool BeforeNode(const void* tag, int& index, int& previous)
	{
		static const uint8_t* tags[kMaxFileNodes];
		try
		{
			if (g_building.files.empty())
				return false;
			Context& context = g_building.files.back();
			if (!context.walked)
			{
				context.walked = true;
				const int count = WalkNodeTags(static_cast<const uint8_t*>(tag), tags, kMaxFileNodes);
				for (int i = 0; i < count; i++)
					context.tags.emplace(tags[i], i);
				g_building.map->files[context.file].nodes = count;
			}
			const auto found = context.tags.find(tag);
			index = found != context.tags.end() ? found->second : -1;
			previous = context.current;
			context.current = index;
			return true;
		}
		catch (...)
		{
			g_building.failed = true;
			return false;
		}
	}

	// Notes a switch label at load: its slot (a new one named for the live link), or -1 when all are taken
	int NoteLabel(const std::string& text, bool item, int defaultValue, const std::string& where)
	{
		int slot = -1;
		bool added = false;
		{
			std::lock_guard<std::mutex> lock(g_labelMutex);
			for (size_t i = 0; i < g_labels.size(); i++)
				if (g_labels[i].text == text)
					slot = static_cast<int>(i);
			if (slot < 0)
			{
				if (g_labels.size() >= kMaxLabels)
					return -1;
				g_labels.emplace_back();
				g_labels.back().text = text;
				slot = static_cast<int>(g_labels.size() - 1);
				added = true;
			}
			LabelInfo& label = g_labels[slot];
			label.inLevel = true;
			if (item)
				label.item = true;
			else
				label.test = true;
			// The test's default is the switch's (an action only sets the tick back)
			if (defaultValue >= 0 && (!item || label.defaultValue < 0))
				label.defaultValue = defaultValue;
			if (std::find(label.where.begin(), label.where.end(), where) == label.where.end())
				label.where.push_back(where);
		}
		if (added)
			DEBUG_MENU::AddSwitch("tree_switch." + text, &g_ticks[slot]);
		return slot;
	}

	// After a node is read: noted, unless it already was (the editor's wrapper for a tree's root gives its wrapped node,
	// already noted by the read of it)
	void AfterNode(const void* tag, void* node, int index, int previous)
	{
		static Attribute attributes[kMaxAttributes];
		try
		{
			if (g_building.files.empty())
				return;
			Context& context = g_building.files.back();
			context.current = previous;
			if (!node || g_building.map->nodes.count(node))
				return;
			const int count = ReadAttributes(static_cast<const uint8_t*>(tag), attributes, kMaxAttributes);
			NodeRecord record;
			record.file = context.file;
			record.index = index;
			const char* label = nullptr;
			const char* defaultValue = nullptr;
			const char* keyName = nullptr;
			for (int i = 0; i < count; i++)
			{
				const Attribute& attribute = attributes[i];
				if (EqualsIgnoringCase(attribute.name, "Class"))
				{
					record.editorClass = attribute.value;
					for (const char* prefix : { "LegendPlugin.Nodes.", "Brainiac.Design.Nodes." })
						if (StartsWith(record.editorClass, prefix))
							record.editorClass.erase(0, strlen(prefix));
				}
				else if (EqualsIgnoringCase(attribute.name, "Name"))
					record.name = attribute.value;
				else if (EqualsIgnoringCase(attribute.name, "In_Game_Menu_Text"))
					label = attribute.value;
				else if (EqualsIgnoringCase(attribute.name, "DefaultBehaviour"))
					defaultValue = attribute.value;
				else if (!keyName && (EqualsIgnoringCase(attribute.name, "DebugString") || EqualsIgnoringCase(attribute.name, "AssertInfo")))
				{
					keyName = attribute.name;
					record.key = std::string("\"") + attribute.value + "\"";
				}
				else if (!keyName && EqualsIgnoringCase(attribute.name, "BranchType"))
				{
					keyName = attribute.name;
					record.key = WithoutNumber(attribute.value);
				}
			}
			// The rest, short
			for (int i = 0; i < count; i++)
			{
				const Attribute& attribute = attributes[i];
				if (!attribute.value[0] || EqualsIgnoringCase(attribute.name, "Class") || EqualsIgnoringCase(attribute.name, "Name") ||
					EqualsIgnoringCase(attribute.name, "In_Game_Menu_Text") || EqualsIgnoringCase(attribute.name, "DefaultBehaviour") ||
					(keyName && EqualsIgnoringCase(attribute.name, keyName)) || record.params.size() > 200)
					continue;
				if (!record.params.empty())
					record.params += ", ";
				record.params += std::string(attribute.name) + "=" + WithoutNumber(attribute.value);
			}
			if (label)
			{
				const bool item = StartsWith(record.editorClass, "Action");
				const int value = defaultValue ? (EqualsIgnoringCase(defaultValue, "true") ? 1 : 0) : -1;
				const FileRecord& file = g_building.map->files[context.file];
				const std::string& top = g_building.map->files[g_building.files.front().file].name;
				const std::string where = file.name == top ? file.name : file.name + " (" + top + ")";
				record.label = NoteLabel(label, item, value, where);
				record.key = std::string("switch \"") + label + "\"" + (value < 0 ? "" : value ? ", on by default" : ", off by default");
				if (record.label >= 0)
				{
					SwitchNode switchNode;
					switchNode.node = node;
					switchNode.label = record.label;
					switchNode.item = item;
					g_building.switches.push_back(switchNode);
				}
			}
			g_building.map->nodes.emplace(node, std::move(record));
		}
		catch (...)
		{
			g_building.failed = true;
		}
	}

	// ---- The characters' trees ----

	void PushEvent(State& state, EventKind kind, const void* node, const char* engineClass, uint8_t branch, const char* text, ULONGLONG now)
	{
		Event& event = state.events[state.eventCount % kMaxEvents];
		event.kind = kind;
		event.node = node;
		event.engineClass = engineClass;
		event.branch = branch;
		event.at = now;
		event.text[0] = '\0';
		if (text)
			strncpy_s(event.text, text, _TRUNCATE);
		state.eventCount++;
	}

	// The character's state, made afresh for a new level (under g_stateMutex)
	State& StateOf(uint32_t id, uint32_t generation)
	{
		State& state = g_states[id];
		if (state.id != id || state.generation != generation)
		{
			state = State();
			state.id = id;
			state.generation = generation;
		}
		return state;
	}

	void Store(const TreeRead& read)
	{
		const ULONGLONG now = GetTickCount64();
		const uint32_t generation = g_generation;
		std::lock_guard<std::mutex> lock(g_stateMutex);
		State& state = StateOf(read.id, generation);
		if (state.seen)
		{
			// The branches that started or ended since the last run
			for (int type = 1; type < kBranchTypes; type++)
			{
				const bool was = state.branches[type] != 0, is = read.branches[type] != 0;
				if (was != is)
					PushEvent(state, is ? EventKind::BranchStarted : EventKind::BranchEnded, nullptr, nullptr, static_cast<uint8_t>(type), nullptr, now);
			}
			if (read.outer != state.outer)
			{
				state.before = state.outer;
				state.beforeKnown = true;
			}
		}
		state.root = read.root;
		state.active = read.active;
		state.waiting = read.waiting;
		state.outer = static_cast<uint8_t>(read.outer < kBranchTypes ? read.outer : 0);
		memcpy(state.branches, read.branches, sizeof(state.branches));
		state.seen = true;
		state.at = now;

		// Characters not run for a while (gone, or no longer running a tree) are forgotten
		if (now - g_sweptAt > 2000)
		{
			g_sweptAt = now;
			for (auto it = g_states.begin(); it != g_states.end();)
				it = now - it->second.at > kForgetAfterMs && it->second.id != read.id ? g_states.erase(it) : std::next(it);
		}
	}

	// A text node run by a character's tree: the character's latest text, and the log (a new line, or one more count)
	void NoteText(const uint8_t* node, const uint8_t* character, bool assert)
	{
		TextRead read;
		if (!ReadText(node, character, assert, read))
			return;
		try
		{
			const ULONGLONG now = GetTickCount64();
			const uint32_t generation = g_generation;
			{
				std::lock_guard<std::mutex> lock(g_stateMutex);
				State& state = StateOf(read.id, generation);
				strncpy_s(state.lastText, read.text, _TRUNCATE);
				state.lastTextAssert = assert;
				state.lastTextAt = now;
			}

			const auto key = std::make_tuple(generation, read.id, static_cast<const void*>(node));
			{
				std::lock_guard<std::mutex> lock(g_logMutex);
				const auto latest = g_logLatest.find(key);
				if (latest != g_logLatest.end() && !g_log.empty() && latest->second >= g_log.front().sequence)
				{
					LogLine& line = g_log[static_cast<size_t>(latest->second - g_log.front().sequence)];
					if (now - line.lastAt < kLogAgainAfterMs && line.text == read.text)
					{
						line.count++;
						line.lastAt = now;
						return;
					}
				}
			}

			// A new line: where the node is, from the node map
			LogLine line;
			line.character = read.id;
			line.characterName = read.name;
			line.assert = assert;
			line.text = read.text;
			line.firstAt = line.lastAt = now;
			line.gameTime = ReadGameTime();
			const std::shared_ptr<const NodeMap> map = PublishedMap();
			if (map && map->generation == generation)
			{
				const auto found = map->nodes.find(node);
				if (found != map->nodes.end() && found->second.file >= 0)
				{
					const FileRecord& file = map->files[found->second.file];
					char where[192];
					if (file.name == file.tree)
						snprintf(where, sizeof(where), "%s #%d", file.name.c_str(), found->second.index);
					else
						snprintf(where, sizeof(where), "%s #%d (in %s)", file.name.c_str(), found->second.index, file.tree.c_str());
					line.where = where;
				}
			}
			DevTools::Log("Behaviour tree %s from %s%s%s: %s", assert ? "assert" : "debug text", read.name, line.where.empty() ? "" : " at ", line.where.c_str(), read.text);
			{
				std::lock_guard<std::mutex> lock(g_logMutex);
				line.sequence = g_nextSequence++;
				g_logLatest[key] = line.sequence;
				g_log.push_back(std::move(line));
				while (g_log.size() > kMaxLogLines)
				{
					g_log.pop_front();
					// Keys whose line went are dropped with it
					const uint64_t oldest = g_log.front().sequence;
					for (auto it = g_logLatest.begin(); it != g_logLatest.end();)
						it = it->second < oldest ? g_logLatest.erase(it) : std::next(it);
				}
			}
			{
				std::lock_guard<std::mutex> lock(g_stateMutex);
				State& state = StateOf(read.id, generation);
				PushEvent(state, assert ? EventKind::Assert : EventKind::DebugText, node, nullptr, 0, read.text, now);
			}
		}
		catch (...)
		{
		}
	}

	// ---- The replaced vtable entries (on the thread running the character's tree) ----

	// A switch node's label slot, or -1 (none noted: the loading was not followed)
	int LabelOf(const void* node)
	{
		int label = -1;
		AcquireSRWLockShared(&g_switchLock);
		for (int i = 0; i < g_switchNodeCount; i++)
			if (g_switchNodes[i].node == node)
			{
				label = g_switchNodes[i].label;
				break;
			}
		ReleaseSRWLockShared(&g_switchLock);
		return label;
	}

	// Whether a ticked switch applies to the character
	bool InScope(const uint8_t* character)
	{
		return !g_selectedOnly || ReadCharacterId(character) == g_scopeCharacter;
	}

	void __fastcall DebugTextStart(const uint8_t* node, void* /*edx*/, uint8_t* character, void* record, void* tree, bool a, bool b)
	{
		NoteText(node, character, false);
		reinterpret_cast<t_start>(g_debugTextSlot.original)(node, character, record, tree, a, b);
	}

	void __fastcall AssertStart(const uint8_t* node, void* /*edx*/, uint8_t* character, void* record, void* tree, bool a, bool b)
	{
		NoteText(node, character, true);
		reinterpret_cast<t_start>(g_assertSlot.original)(node, character, record, tree, a, b);
	}

	// The switch test: its default, the other way while its label is ticked (for this character)
	int __fastcall SwitchTest(const uint8_t* node, void* /*edx*/, uint8_t* character, void* record)
	{
		const int label = LabelOf(node);
		const int defaultValue = label >= 0 ? ReadDefault(node, kTestDefault) : -1;
		if (defaultValue < 0)
			return reinterpret_cast<t_condition_test>(g_testSlot.original)(node, character, record);
		const bool ticked = g_ticks[label] && InScope(character);
		return (ticked ? !defaultValue : defaultValue) ? 1 : 0;
	}

	// The switch action: sets its label's tick to its own default (so a switch the tree acts on unticks itself), then
	// succeeds as the game's own does
	void __fastcall SwitchItemStart(const uint8_t* node, void* /*edx*/, uint8_t* character, void* record, void* tree, bool a, bool b)
	{
		const int label = LabelOf(node);
		if (label >= 0 && InScope(character))
		{
			const int defaultValue = ReadDefault(node, kItemDefault);
			if (defaultValue >= 0 && g_ticks[label] != (defaultValue != 0))
			{
				g_ticks[label] = defaultValue != 0;
				const uint32_t id = ReadCharacterId(character);
				try
				{
					std::string text;
					{
						std::lock_guard<std::mutex> lock(g_labelMutex);
						if (label < static_cast<int>(g_labels.size()))
							text = g_labels[label].text;
					}
					std::lock_guard<std::mutex> lock(g_stateMutex);
					State& state = StateOf(id, g_generation);
					PushEvent(state, EventKind::SwitchReset, node, nullptr, 0, text.c_str(), GetTickCount64());
				}
				catch (...)
				{
				}
			}
		}
		reinterpret_cast<t_start>(g_itemSlot.original)(node, character, record, tree, a, b);
	}

	// Puts a replacement in a vtable entry, if the entry holds what the game has there; false otherwise (left alone)
	bool PatchSlot(Slot& slot, uintptr_t slotOffset, uintptr_t expectedOffset, void* replacement, const char* what)
	{
		uintptr_t* address = reinterpret_cast<uintptr_t*>(DEVTOOLS_RELATIVE_ADDRESS(slotOffset));
		const uintptr_t expected = DEVTOOLS_RELATIVE_ADDRESS(expectedOffset);
		if (!address || !expected)
		{
			DevTools::Log("Behaviour trees: no address for the %s in this build", what);
			return false;
		}
		if (*address != expected)
		{
			DevTools::Log("Behaviour trees: the %s holds something unexpected (0x%08X); left alone", what, static_cast<unsigned>(*address));
			return false;
		}
		slot.address = address;
		slot.original = *address;
		DWORD protection;
		VirtualProtect(address, sizeof(*address), PAGE_READWRITE, &protection);
		InterlockedExchange(reinterpret_cast<volatile LONG*>(address), static_cast<LONG>(reinterpret_cast<uintptr_t>(replacement)));
		VirtualProtect(address, sizeof(*address), protection, &protection);
		return true;
	}

	void RestoreSlot(Slot& slot)
	{
		if (!slot.address)
			return;
		DWORD protection;
		VirtualProtect(slot.address, sizeof(*slot.address), PAGE_READWRITE, &protection);
		InterlockedExchange(reinterpret_cast<volatile LONG*>(slot.address), static_cast<LONG>(slot.original));
		VirtualProtect(slot.address, sizeof(*slot.address), protection, &protection);
		slot.address = nullptr;
	}

	// ---- The menu (render thread) ----

	// How a node of a path reads: its editor class, Name and key attribute, from the node map when the loading was
	// followed (else the editor class from the node's own class)
	struct NodeText
	{
		std::string title;
		std::string editorClass;
		const NodeRecord* record = nullptr;
	};

	NodeText Describe(const void* node, const char* engineClass, uint8_t branch, const NodeMap* map)
	{
		NodeText text;
		if (map && node)
		{
			const auto found = map->nodes.find(node);
			if (found != map->nodes.end())
				text.record = &found->second;
		}
		if (text.record && !text.record->editorClass.empty())
			text.editorClass = text.record->editorClass;
		else if (const char* editor = EditorClassOf(engineClass))
			text.editorClass = editor;
		else
			text.editorClass = engineClass ? engineClass : "?";
		text.title = text.editorClass;
		if (text.record && !text.record->name.empty())
			text.title += " [" + text.record->name + "]";
		if (text.record && !text.record->key.empty())
			text.title += "  " + text.record->key;
		else if (branch != 0xFF)
			text.title += std::string("  ") + BranchName(branch);
		if (text.record && text.record->label >= 0 && text.record->label < kMaxLabels && g_ticks[text.record->label])
			text.title += " (ticked)";
		return text;
	}

	void NodeTooltip(const NodeText& text, const Entry* entry, const NodeMap* map)
	{
		if (!ImGui::IsItemHovered())
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 40.0f);
		if (entry)
			ImGui::Text("%s", StatusName(entry->status));
		if (text.record && map && text.record->file >= 0)
		{
			const FileRecord& file = map->files[text.record->file];
			ImGui::Text("Node #%d of %s (tree %s)", text.record->index, file.name.c_str(), file.tree.c_str());
			if (!text.record->params.empty())
				ImGui::Text("%s", text.record->params.c_str());
		}
		else
			ImGui::TextDisabled("Not named: the level's trees loaded before the debug menu could follow them");
		if (entry && entry->engineClass)
			ImGui::TextDisabled("Class in the game: %s", entry->engineClass);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	bool IsCondition(const std::string& editorClass)
	{
		return StartsWith(editorClass, "Condition");
	}

	bool IsComposite(const std::string& editorClass)
	{
		return StartsWith(editorClass, "Selector") || StartsWith(editorClass, "Sequence");
	}

	void DrawPath(const Path& path, const NodeMap* map)
	{
		const float step = 12.0f * DEBUG_MENU::Scale();
		const float start = ImGui::GetCursorPosX();
		int file = -1;
		for (int i = 0; i < path.count; i++)
		{
			const Entry& entry = path.entries[i];
			ImGui::SetCursorPosX(start + step * static_cast<float>((std::min)(i, 24)));
			if (!entry.valid)
			{
				ImGui::TextDisabled("(a node record that could not be read)");
				continue;
			}
			NodeText text = Describe(entry.node, entry.engineClass, entry.branch, map);
			std::string line = text.title;
			if (IsCondition(text.editorClass))
				line += entry.chosen ? "  -> true" : "  -> false";
			else if (IsComposite(text.editorClass) && i + 1 < path.count)
				line += "  (child #" + std::to_string(entry.child + 1) + ")";
			line += std::string("  - ") + StatusName(entry.status);
			ImGui::PushID(i);
			ImGui::TextColored(ImGui::ColorConvertU32ToFloat4(StatusColour(entry.status)), "%s", line.c_str());
			NodeTooltip(text, &entry, map);
			// Where the path goes into another file (a tree this one refers to), the file
			if (text.record && map && text.record->file >= 0 && text.record->file != file)
			{
				if (file >= 0)
				{
					ImGui::SameLine();
					ImGui::TextDisabled("(%s)", map->files[text.record->file].name.c_str());
				}
				file = text.record->file;
			}
			ImGui::PopID();
		}
		if (path.cut)
			ImGui::TextDisabled("(deeper nodes not shown)");
	}

	const char* EventName(EventKind kind)
	{
		switch (kind)
		{
		case EventKind::Succeeded: return "succeeded";
		case EventKind::Failed: return "failed";
		case EventKind::BranchStarted: return "branch on";
		case EventKind::BranchEnded: return "branch off";
		case EventKind::DebugText: return "debug text";
		case EventKind::Assert: return "assert";
		case EventKind::SwitchReset: return "switch reset";
		default: return "?";
		}
	}

	ImU32 EventColour(EventKind kind)
	{
		switch (kind)
		{
		case EventKind::Succeeded: return StatusColour(kStatusSucceeded);
		case EventKind::Failed: return StatusColour(kStatusFailed);
		case EventKind::BranchStarted: return IM_COL32(150, 200, 255, 255);
		case EventKind::BranchEnded: return IM_COL32(150, 160, 190, 255);
		case EventKind::DebugText: return IM_COL32(120, 230, 230, 255);
		case EventKind::Assert: return IM_COL32(255, 120, 200, 255);
		case EventKind::SwitchReset: return IM_COL32(230, 180, 255, 255);
		default: return IM_COL32(200, 200, 200, 255);
		}
	}

	// The tree a character runs, by its root node
	std::string TreeName(const State& state, const NodeMap* map)
	{
		if (map && state.root)
		{
			const auto found = map->roots.find(state.root);
			if (found != map->roots.end())
				return map->files[found->second].name;
		}
		return std::string();
	}

	bool CopyState(uint32_t id, State& out)
	{
		std::lock_guard<std::mutex> lock(g_stateMutex);
		const auto found = g_states.find(id);
		if (found == g_states.end() || found->second.generation != g_generation || !found->second.seen)
			return false;
		out = found->second;
		return true;
	}

	void KeepScopeCharacter()
	{
		g_scopeCharacter = DEBUG_MENU::SelectedId();
	}

	void DrawTreeTab()
	{
		KeepScopeCharacter();
		DEBUG_MENU::CharacterPicker("Character");
		bool overlay = g_overlay;
		if (ImGui::Checkbox("Show over characters", &overlay))
			g_overlay = overlay;
		ImGui::SameLine();
		ImGui::TextDisabled("(the leading node, outer branch and latest debug text)");

		const std::shared_ptr<const NodeMap> mapHeld = PublishedMap();
		const NodeMap* map = mapHeld && mapHeld->generation == g_generation ? mapHeld.get() : nullptr;
		if (!map)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Node names were not captured (the level's trees loaded before the debug menu could follow them): nodes show their class only.");
			ImGui::PopTextWrapPos();
		}

		const DEBUG_MENU::Character* character = DEBUG_MENU::Selected();
		if (!character)
		{
			ImGui::TextDisabled("Pick a character to see its behaviour tree.");
			return;
		}
		static State state;
		if (!CopyState(character->id, state))
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled(character->player ? "The player runs no behaviour tree." : "No run of this character's behaviour tree seen yet (a possessed character's tree is not run).");
			ImGui::PopTextWrapPos();
			return;
		}
		const ULONGLONG now = GetTickCount64();
		const std::string tree = TreeName(state, map);
		ImGui::Text("Tree: %s", tree.empty() ? "(not known)" : tree.c_str());
		if (now - state.at > 1000)
		{
			ImGui::SameLine();
			ImGui::TextDisabled("(not run for %.1f s)", (now - state.at) / 1000.0);
		}
		if (state.beforeKnown)
			ImGui::Text("Outer branch: %s (before it: %s)", BranchName(state.outer), BranchName(state.before));
		else
			ImGui::Text("Outer branch: %s", BranchName(state.outer));

		ImGui::Separator();
		ImGui::TextUnformatted("Active path (root first; the last is the leading node):");
		DrawPath(state.active, map);

		if (state.waiting.count > 0)
		{
			ImGui::Separator();
			const Entry* leading = state.active.count ? &state.active.entries[state.active.count - 1] : nullptr;
			const Entry& wants = state.waiting.entries[state.waiting.count - 1];
			const NodeText leadingText = leading ? Describe(leading->node, leading->engineClass, leading->branch, map) : NodeText();
			const NodeText wantsText = Describe(wants.node, wants.engineClass, wants.branch, map);
			ImGui::TextColored(ImGui::ColorConvertU32ToFloat4(StatusColour(7)), "Waiting for %s to shut down, so that %s can run:",
				leading ? leadingText.editorClass.c_str() : "the leading node", wantsText.editorClass.c_str());
			DrawPath(state.waiting, map);
		}

		ImGui::Separator();
		std::string branches;
		for (int type = 1; type < kBranchTypes; type++)
			if (state.branches[type])
			{
				if (!branches.empty())
					branches += ", ";
				branches += kBranchNames[type];
			}
		ImGui::TextWrapped("Active branches: %s", branches.empty() ? "none" : branches.c_str());

		if (ImGui::CollapsingHeader("Recent events", ImGuiTreeNodeFlags_DefaultOpen))
		{
			const uint32_t shown = (std::min<uint32_t>)(state.eventCount, kMaxEvents);
			if (!shown)
				ImGui::TextDisabled("None yet.");
			for (uint32_t i = 0; i < shown; i++)
			{
				const Event& event = state.events[(state.eventCount - 1 - i) % kMaxEvents];
				std::string what;
				if (event.kind == EventKind::BranchStarted || event.kind == EventKind::BranchEnded)
					what = BranchName(event.branch);
				else if (event.kind == EventKind::DebugText || event.kind == EventKind::Assert || event.kind == EventKind::SwitchReset)
					what = std::string("\"") + event.text + "\"";
				else
					what = Describe(event.node, event.engineClass, 0xFF, map).title;
				ImGui::TextColored(ImGui::ColorConvertU32ToFloat4(EventColour(event.kind)), "%6.1f s ago  %-12s %s", (now - event.at) / 1000.0, EventName(event.kind), what.c_str());
			}
		}

		if (ImGui::CollapsingHeader("This character's debug texts and asserts", ImGuiTreeNodeFlags_DefaultOpen))
		{
			std::vector<LogLine> lines;
			{
				std::lock_guard<std::mutex> lock(g_logMutex);
				for (auto it = g_log.rbegin(); it != g_log.rend() && lines.size() < 10; ++it)
					if (it->character == character->id)
						lines.push_back(*it);
			}
			if (lines.empty())
			{
				ImGui::PushTextWrapPos(0.0f);
				ImGui::TextDisabled("None (the shipped trees use no debug texts; their five asserts only fire in rare cases).");
				ImGui::PopTextWrapPos();
			}
			for (const LogLine& line : lines)
				ImGui::TextColored(ImGui::ColorConvertU32ToFloat4(EventColour(line.assert ? EventKind::Assert : EventKind::DebugText)), "%6.1f s ago  %s%s \"%s\"%s",
					(now - line.lastAt) / 1000.0, line.assert ? "assert" : "text", line.count > 1 ? (" x" + std::to_string(line.count)).c_str() : "", line.text.c_str(),
					line.where.empty() ? "" : ("  (" + line.where + ")").c_str());
		}

		if (map && ImGui::CollapsingHeader("Tree files loaded for this level"))
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Each file as loaded (a tree another refers to is loaded again in place), with its node count.");
			ImGui::PopTextWrapPos();
			for (size_t i = 0; i < map->files.size(); i++)
			{
				const FileRecord& file = map->files[i];
				if (file.parent < 0)
					ImGui::Text("%s: %d nodes", file.name.c_str(), file.nodes);
				else
					ImGui::TextDisabled("    %s: %d nodes (from %s #%d)", file.name.c_str(), file.nodes, map->files[file.parent].name.c_str(), file.parentIndex);
			}
		}
	}

	void DrawSwitchesTab()
	{
		KeepScopeCharacter();
		ImGui::TextWrapped("The behaviour trees' debug switches (the editor's DebugMenuLinkTest and DebugMenuLinkItem nodes): a ticked switch makes its "
			"test go the other way from its default, and an action of the same label sets it back to its default when the tree reaches it, so "
			"a one-shot switch unticks itself. They all go back to unticked when a level loads.");

		bool selectedOnly = g_selectedOnly;
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		if (ImGui::RadioButton("For every character", !selectedOnly))
			g_selectedOnly = false;
		ImGui::SameLine();
		char text[160];
		snprintf(text, sizeof(text), "Only for the selected character (%s)", selected ? selected->name.c_str() : "none selected");
		if (ImGui::RadioButton(text, selectedOnly))
			g_selectedOnly = true;
		// (the character only counts for "only for the selected character")
		ImGui::BeginDisabled(!g_selectedOnly);
		DEBUG_MENU::CharacterPicker("Character");
		ImGui::EndDisabled();

		std::vector<LabelInfo> labels;
		{
			std::lock_guard<std::mutex> lock(g_labelMutex);
			labels = g_labels;
		}
		ImGui::Separator();
		if (labels.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("No switches captured: none in this level's trees, or the trees loaded before the debug menu could follow them.");
			ImGui::PopTextWrapPos();
			return;
		}
		if (ImGui::Button("Untick all"))
			for (std::atomic<bool>& tick : g_ticks)
				tick = false;
		if (ImGui::BeginTable("switches", 3, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingStretchProp))
		{
			ImGui::TableSetupColumn("Switch");
			ImGui::TableSetupColumn("Default");
			ImGui::TableSetupColumn("In");
			ImGui::TableHeadersRow();
			for (size_t i = 0; i < labels.size(); i++)
			{
				const LabelInfo& label = labels[i];
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				bool ticked = g_ticks[i];
				ImGui::PushID(static_cast<int>(i));
				if (ImGui::Checkbox(label.text.c_str(), &ticked))
					g_ticks[i] = ticked;
				ImGui::PopID();
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(label.defaultValue < 0 ? "?" : label.defaultValue ? "on" : "off");
				ImGui::TableNextColumn();
				if (!label.inLevel)
					ImGui::TextDisabled("not in this level's trees");
				else
				{
					std::string where;
					for (const std::string& file : label.where)
						where += (where.empty() ? "" : ", ") + file;
					if (label.test && label.item)
						where += " (one-shot)";
					ImGui::TextWrapped("%s", where.c_str());
				}
			}
			ImGui::EndTable();
		}
		ImGui::Separator();
		ImGui::TextWrapped("\"Alien Gamepad control\", \"Android Direct Control\" and \"Human Direct Control\" lead to branches that also need the "
			"game's own free camera (not the Debug Menu's), which the game never turns on: on their own they do nothing (Possess Characters does that job). "
			"\"Allow Hit And Run\" is on by default: ticking it turns hit and run off.");
	}

	void DrawLogTab()
	{
		const ULONGLONG now = GetTickCount64();
		std::vector<LogLine> lines;
		{
			std::lock_guard<std::mutex> lock(g_logMutex);
			lines.assign(g_log.rbegin(), g_log.rend());
		}
		if (ImGui::Button("Clear"))
		{
			std::lock_guard<std::mutex> lock(g_logMutex);
			g_log.clear();
			g_logLatest.clear();
			lines.clear();
		}
		ImGui::SameLine();
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("%u lines, newest first. A line counts its repeats until a second goes by without one.", static_cast<unsigned>(lines.size()));
		ImGui::PopTextWrapPos();
		if (lines.empty())
		{
			ImGui::TextWrapped("Nothing logged yet. The editor's DebugString action logs its text here whenever a character's tree runs it, and its "
				"Assert action its message. The shipped trees use no DebugString, and their five asserts only fire in rare cases (a level with "
				"no vents for the alien, a character told to target one it cannot).");
			return;
		}
		if (ImGui::BeginTable("log", 5, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingStretchProp | ImGuiTableFlags_Resizable,
			ImVec2(0.0f, ImGui::GetContentRegionAvail().y)))
		{
			ImGui::TableSetupScrollFreeze(0, 1);
			ImGui::TableSetupColumn("Age");
			ImGui::TableSetupColumn("Character");
			ImGui::TableSetupColumn("Text");
			ImGui::TableSetupColumn("Where");
			ImGui::TableSetupColumn("Count");
			ImGui::TableHeadersRow();
			for (const LogLine& line : lines)
			{
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("%.1f s", (now - line.lastAt) / 1000.0);
				if (line.gameTime >= 0.0 && ImGui::IsItemHovered())
					ImGui::SetTooltip("First logged at %.2f s of game time", line.gameTime);
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(line.characterName.c_str());
				ImGui::TableNextColumn();
				ImGui::TextColored(ImGui::ColorConvertU32ToFloat4(EventColour(line.assert ? EventKind::Assert : EventKind::DebugText)), "%s%s", line.assert ? "Assert: " : "", line.text.c_str());
				// (each on one line, cut off by a narrow column: the whole of it on hovering)
				if (ImGui::IsItemHovered())
					ImGui::SetTooltip("%s", line.text.c_str());
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(line.where.empty() ? "(not known)" : line.where.c_str());
				if (!line.where.empty() && ImGui::IsItemHovered())
					ImGui::SetTooltip("%s", line.where.c_str());
				ImGui::TableNextColumn();
				ImGui::Text("%u", line.count);
			}
			ImGui::EndTable();
		}
	}

	// Over the characters: the leading node and outer branch, and the latest debug text for a few seconds
	void DrawOverlay(ImDrawList* drawList)
	{
		KeepScopeCharacter();
		if (!g_overlay)
			return;
		const std::vector<DEBUG_MENU::Character>& characters = DEBUG_MENU::Characters();
		float camera[3];
		if (characters.empty() || !DEBUG_MENU::CameraPosition(camera))
			return;

		// The nearest that run a tree
		struct Near { const DEBUG_MENU::Character* character; float distance; };
		std::vector<Near> nearest;
		for (const DEBUG_MENU::Character& character : characters)
		{
			if (character.player || character.dead)
				continue;
			const float d[3] = { character.position[0] - camera[0], character.position[1] - camera[1], character.position[2] - camera[2] };
			nearest.push_back(Near{ &character, d[0] * d[0] + d[1] * d[1] + d[2] * d[2] });
		}
		std::sort(nearest.begin(), nearest.end(), [](const Near& a, const Near& b) { return a.distance < b.distance; });
		if (nearest.size() > kOverlayMost)
			nearest.resize(kOverlayMost);

		const std::shared_ptr<const NodeMap> mapHeld = PublishedMap();
		const NodeMap* map = mapHeld && mapHeld->generation == g_generation ? mapHeld.get() : nullptr;
		const ULONGLONG now = GetTickCount64();
		const float line = 17.0f * DEBUG_MENU::Scale();
		static State state;
		for (const Near& one : nearest)
		{
			if (!CopyState(one.character->id, state) || !state.active.count)
				continue;
			const Entry& leading = state.active.entries[state.active.count - 1];
			const NodeText text = Describe(leading.node, leading.engineClass, leading.branch, map);
			const float at[3] = { one.character->position[0], one.character->position[1] + kOverlayHeight, one.character->position[2] };
			float offset = 0.0f;
			if (state.lastTextAt && now - state.lastTextAt < kOverlayTextMs)
			{
				const std::string quoted = std::string(state.lastTextAssert ? "Assert: \"" : "\"") + state.lastText + "\"";
				DEBUG_MENU::WorldText(drawList, at, quoted.c_str(), EventColour(state.lastTextAssert ? EventKind::Assert : EventKind::DebugText), offset);
				offset -= line;
			}
			DEBUG_MENU::WorldText(drawList, at, BranchName(state.outer), IM_COL32(200, 210, 230, 255), offset);
			offset -= line;
			const std::string title = text.editorClass + (text.record && !text.record->name.empty() ? " [" + text.record->name + "]" : "");
			DEBUG_MENU::WorldText(drawList, at, title.c_str(), StatusColour(state.waiting.count ? 7 : leading.status), offset);
		}
	}

	const bool g_viewsAdded = DEBUG_MENU::AddTab("Behaviour Tree", 20, DrawTreeTab, "AI") && DEBUG_MENU::AddTab("Tree Switches", 50, DrawSwitchesTab, "AI") &&
		DEBUG_MENU::AddTab("Tree Log", 60, DrawLogTab, "AI") && DEBUG_MENU::AddOverlay(20, DrawOverlay);
}

__declspec(noinline)
void __cdecl BT_DEBUG::h_load_all_trees()
{
	BeginLoading();
	load_all_trees();
	EndLoading();
}

__declspec(noinline)
void* __cdecl BT_DEBUG::h_load_tree_file(const char* name)
{
	const bool followed = Capturing() && PushFile(name);
	void* root = load_tree_file(name);
	if (followed)
		PopFile(root);
	return root;
}

__declspec(noinline)
void* __cdecl BT_DEBUG::h_read_tree_node(const void* tag)
{
	// A node read through the editor's wrapper for a tree's root comes back in here at once (the game's reading of the
	// wrapper hands on to the wrapped node's): the wrapped node is read and noted first, and the wrapper's note dropped
	int index = -1, previous = -1;
	const bool followed = Capturing() && BeforeNode(tag, index, previous);
	void* node = read_tree_node(tag);
	if (followed)
		AfterNode(tag, node, index, previous);
	return node;
}

__declspec(noinline)
void __fastcall BT_DEBUG::h_leading_node_done(void* _this, void* /*_EDX*/)
{
	// Read before the game moves on from it: the leading node, which has just succeeded or failed
	if (g_attached)
	{
		LeadingRead read;
		if (ReadLeading(static_cast<const uint8_t*>(_this), read) && (read.status == kStatusSucceeded || read.status == kStatusFailed))
		{
			try
			{
				std::lock_guard<std::mutex> lock(g_stateMutex);
				State& state = StateOf(read.id, g_generation);
				PushEvent(state, read.status == kStatusSucceeded ? EventKind::Succeeded : EventKind::Failed, read.node, read.engineClass, 0, nullptr, GetTickCount64());
			}
			catch (...)
			{
			}
		}
	}
	leading_node_done(_this);
}

void BT_DEBUG::AfterTreeUpdate(void* tree)
{
	if (!g_attached || !tree)
		return;
	TreeRead read;
	if (!ReadTree(static_cast<const uint8_t*>(tree), read))
		return;
	try
	{
		Store(read);
	}
	catch (...)
	{
	}
}

void BT_DEBUG::OnLevelClose()
{
	try
	{
		g_generation++;
		{
			std::lock_guard<std::mutex> lock(g_mapMutex);
			g_map.reset();
		}
		AcquireSRWLockExclusive(&g_switchLock);
		g_switchNodeCount = 0;
		ReleaseSRWLockExclusive(&g_switchLock);
		std::lock_guard<std::mutex> lock(g_stateMutex);
		g_states.clear();
	}
	catch (...)
	{
	}
}

void BT_DEBUG::AttachHooks(bool attach)
{
	if (attach)
	{
		// The game's image, the record pool and the clock
		const uintptr_t base = DevTools::GameProcess::GetBaseAddress();
		const IMAGE_DOS_HEADER* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
		const IMAGE_NT_HEADERS* nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
		g_imageStart = base;
		g_imageEnd = base + nt->OptionalHeader.SizeOfImage;
		const uintptr_t pool = DEVTOOLS_RELATIVE_ADDRESS(kNodeRecordPool);
		g_poolStart = pool ? pool + kPoolRecords : 0;
		g_gameTime = reinterpret_cast<const double*>(DEVTOOLS_RELATIVE_ADDRESS(kGameTime));

		DEBUG_MENU::AddSwitch("bt.overlay", &g_overlay);
		DEBUG_MENU::AddSwitch("bt.switches_selected_only", &g_selectedOnly);

		// Hooked only where this build has the function (it always does, for the three known builds)
		if ((g_hooked[0] = load_all_trees != nullptr))
			DEVTOOLS_DETOURS_ATTACH(load_all_trees, h_load_all_trees);
		if ((g_hooked[1] = load_tree_file != nullptr))
			DEVTOOLS_DETOURS_ATTACH(load_tree_file, h_load_tree_file);
		if ((g_hooked[2] = read_tree_node != nullptr))
			DEVTOOLS_DETOURS_ATTACH(read_tree_node, h_read_tree_node);
		if ((g_hooked[3] = leading_node_done != nullptr))
			DEVTOOLS_DETOURS_ATTACH(leading_node_done, h_leading_node_done);

		const int patched = PatchSlot(g_debugTextSlot, kDebugTextStartSlot, kSucceedAtStart, reinterpret_cast<void*>(&DebugTextStart), "debug text action's start") +
			PatchSlot(g_assertSlot, kAssertStartSlot, kSucceedAtStart, reinterpret_cast<void*>(&AssertStart), "assert action's start") +
			PatchSlot(g_itemSlot, kSwitchItemStartSlot, kSucceedAtStart, reinterpret_cast<void*>(&SwitchItemStart), "debug switch action's start") +
			PatchSlot(g_testSlot, kSwitchTestSlot, kSwitchTestShipped, reinterpret_cast<void*>(&SwitchTest), "debug switch test");
		DevTools::Log("Behaviour trees: %d of 4 debug node entries replaced (debug text, assert, debug switch action and test)", patched);
		g_attached = true;
	}
	else
	{
		g_attached = false;
		RestoreSlot(g_debugTextSlot);
		RestoreSlot(g_assertSlot);
		RestoreSlot(g_itemSlot);
		RestoreSlot(g_testSlot);
		if (g_hooked[0])
			DEVTOOLS_DETOURS_DETACH(load_all_trees, h_load_all_trees);
		if (g_hooked[1])
			DEVTOOLS_DETOURS_DETACH(load_tree_file, h_load_tree_file);
		if (g_hooked[2])
			DEVTOOLS_DETOURS_DETACH(read_tree_node, h_read_tree_node);
		if (g_hooked[3])
			DEVTOOLS_DETOURS_DETACH(leading_node_done, h_leading_node_done);
	}
}
