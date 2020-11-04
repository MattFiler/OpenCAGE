using Alien_Isolation_Mod_Tools;

namespace CATHODE
{
	//58-7C-11-BF
	public class EntityMethodInterface {
		//reference
		//callback
		//callback_finished
		//trigger
		//trigger_finished
		//triggered
		//refresh
		//refresh_finished
		//refreshed
		//start
		//start_finished
		//started
		//stop
		//stop_finished
		//stopped
		//pause
		//pause_finished
		//paused
		//resume
		//resume_finished
		//resumed
		//attach
		//attach_finished
		//attached
		//detach
		//detach_finished
		//detached
		//open
		//open_finished
		//opened
		//close
		//close_finished
		//closed
		//enable
		//enable_finished
		//enabled
		//disable
		//disable_finished
		//disabled
		//floating
		//floating_finished
		//disabled_gravity
		//sinking
		//sinking_finished
		//enabled_gravity
		//lock
		//lock_finished
		//locked
		//unlock
		//unlock_finished
		//unlocked
		//show
		//show_finished
		//shown
		//hide
		//hide_finished
		//hidden
		//spawn
		//spawn_finished
		//spawned
		//despawn
		//despawn_finished
		//despawned
		//light_switch_on
		//light_switch_on_finished
		//light_switched_on
		//light_switch_off
		//light_switch_off_finished
		//light_switched_off
		//proxy_enable
		//proxy_enable_finished
		//proxy_enabled
		//proxy_disable
		//proxy_disable_finished
		//proxy_disabled
		//simulate
		//simulate_finished
		//simulating
		//keyframe
		//keyframe_finished
		//keyframed
		//suspend
		//suspend_finished
		//suspended
		//allow
		//allow_finished
		//allowed
		//request_open
		//request_open_finished
		//requested_open
		//request_close
		//request_close_finished
		//requested_close
		//request_lock
		//request_lock_finished
		//requested_lock
		//request_unlock
		//request_unlock_finished
		//requested_unlock
		//force_open
		//force_open_finished
		//forced_open
		//force_close
		//force_close_finished
		//forced_close
		//request_restore
		//request_restore_finished
		//requested_restore
		//rewind
		//rewind_finished
		//rewound
		//kill
		//kill_finished
		//killed
		//set
		//set_finished
		//been_set
		//request_load
		//request_load_finished
		//load_requested
		//cancel_load
		//cancel_load_finished
		//load_cancelled
		//request_unload
		//request_unload_finished
		//unload_requested
		//cancel_unload
		//cancel_unload_finished
		//unload_cancelled
		//task_end
		//task_end_finished
		//task_ended
		//set_as_next_task
		//set_as_next_task_finished
		//task_set_as_next
		//completed_pre_move
		//completed_pre_move_finished
		//on_pre_move_completed
		//completed_interrupt
		//completed_interrupt_finished
		//on_completed_interrupt
		//allow_early_end
		//allow_early_end_finished
		//on_early_end_allowed
		//start_allowing_interrupts
		//start_allowing_interrupts_finished
		//on_start_allowing_interrupts
		//set_true
		//set_true_finished
		//set_to_true
		//set_false
		//set_false_finished
		//set_to_false
		//set_is_open
		//set_is_open_finished
		//set_to_open
		//set_is_closed
		//set_is_closed_finished
		//set_to_closed
		//apply_start
		//apply_start_finished
		//start_applied
		//apply_stop
		//apply_stop_finished
		//stop_applied
		//pause_activity
		//pause_activity_finished
		//pause_applied
		//resume_activity
		//resume_activity_finished
		//resume_applied
		//clear
		//clear_finished
		//cleared
		//enter
		//enter_finished
		//entered
		//exit
		//exit_finished
		//exited
		//reset
		//reset_finished
		//reseted
		//add_character
		//add_character_finished
		//added
		//remove_character
		//remove_character_finished
		//removed
		//purge
		//purge_finished
		//purged
		//abort
		//abort_finished
		//aborted
		//Evaluate
		//Evaluate_finished
		//Evaluated
		//terminate
		//terminate_finished
		//terminated
		//cancel
		//cancel_finished
		//cancelled
		//impact
		//impact_finished
		//impacted
		//reloading
		//reloading_finished
		//reloading_handled
		//out_of_ammo
		//out_of_ammo_finished
		//out_of_ammo_handled
		//started_aiming
		//started_aiming_finished
		//started_aiming_handled
		//stopped_aiming
		//stopped_aiming_finished
		//stopped_aiming_handled
		//expire
		//expire_finished
		//expired
		//Pin1
		//Pin1_finished
		//Pin1_Instant
		//Pin2
		//Pin2_finished
		//Pin2_Instant
		//Pin3
		//Pin3_finished
		//Pin3_Instant
		//Pin4
		//Pin4_finished
		//Pin4_Instant
		//Pin5
		//Pin5_finished
		//Pin5_Instant
		//Pin6
		//Pin6_finished
		//Pin6_Instant
		//Pin7
		//Pin7_finished
		//Pin7_Instant
		//Pin8
		//Pin8_finished
		//Pin8_Instant
		//Pin9
		//Pin9_finished
		//Pin9_Instant
		//Pin10
		//Pin10_finished
		//Pin10_Instant
		//Up
		//Up_finished
		//on_Up
		//Down
		//Down_finished
		//on_Down
		//Random
		//Random_finished
		//on_Random
		//reset_all
		//reset_all_finished
		//on_reset_all
		//reset_Random_1
		//reset_Random_1_finished
		//on_reset_Random_1
		//reset_Random_2
		//reset_Random_2_finished
		//on_reset_Random_2
		//reset_Random_3
		//reset_Random_3_finished
		//on_reset_Random_3
		//reset_Random_4
		//reset_Random_4_finished
		//on_reset_Random_4
		//reset_Random_5
		//reset_Random_5_finished
		//on_reset_Random_5
		//reset_Random_6
		//reset_Random_6_finished
		//on_reset_Random_6
		//reset_Random_7
		//reset_Random_7_finished
		//on_reset_Random_7
		//reset_Random_8
		//reset_Random_8_finished
		//on_reset_Random_8
		//reset_Random_9
		//reset_Random_9_finished
		//on_reset_Random_9
		//reset_Random_10
		//reset_Random_10_finished
		//on_reset_Random_10
		//Trigger_0
		//Trigger_0_finished
		//Pin_0
		//Trigger_1
		//Trigger_1_finished
		//Pin_1
		//Trigger_2
		//Trigger_2_finished
		//Pin_2
		//Trigger_3
		//Trigger_3_finished
		//Pin_3
		//Trigger_4
		//Trigger_4_finished
		//Pin_4
		//Trigger_5
		//Trigger_5_finished
		//Pin_5
		//Trigger_6
		//Trigger_6_finished
		//Pin_6
		//Trigger_7
		//Trigger_7_finished
		//Pin_7
		//Trigger_8
		//Trigger_8_finished
		//Pin_8
		//Trigger_9
		//Trigger_9_finished
		//Pin_9
		//Trigger_10
		//Trigger_10_finished
		//Pin_10
		//Trigger_11
		//Trigger_11_finished
		//Pin_11
		//Trigger_12
		//Trigger_12_finished
		//Pin_12
		//Trigger_13
		//Trigger_13_finished
		//Pin_13
		//Trigger_14
		//Trigger_14_finished
		//Pin_14
		//Trigger_15
		//Trigger_15_finished
		//Pin_15
		//Trigger_16
		//Trigger_16_finished
		//Pin_16
		//clear_user
		//clear_user_finished
		//user_cleared
		//clear_all
		//clear_all_finished
		//clear_of_alignment
		//clear_of_alignment_finished
		//clear_last
		//clear_last_finished
		//enable_dynamic_rtpc
		//enable_dynamic_rtpc_finished
		//disable_dynamic_rtpc
		//disable_dynamic_rtpc_finished
		//fail_game
		//fail_game_finished
		//start_X
		//start_X_finished
		//started_X
		//stop_X
		//stop_X_finished
		//stopped_X
		//start_Y
		//start_Y_finished
		//started_Y
		//stop_Y
		//stop_Y_finished
		//stopped_Y
		//start_Z
		//start_Z_finished
		//started_Z
		//stop_Z
		//stop_Z_finished
		//stopped_Z
		//fade_out
		//fade_out_finished
		//faded_out
		//set_decal_time
		//set_decal_time_finished
		//decal_time_set
		//increase_aggro
		//increase_aggro_finished
		//aggro_increased
		//decrease_aggro
		//decrease_aggro_finished
		//aggro_decreased
		//force_stand_down
		//force_stand_down_finished
		//forced_stand_down
		//force_aggressive
		//force_aggressive_finished
		//forced_aggressive
		//load_bank
		//load_bank_finished
		//unload_bank
		//unload_bank_finished
		//bank_loaded
		//bank_loaded_finished
		//set_override
		//set_override_finished
		//enable_stealth
		//enable_stealth_finished
		//disable_stealth
		//disable_stealth_finished
		//enable_threat
		//enable_threat_finished
		//disable_threat
		//disable_threat_finished
		//enable_music
		//enable_music_finished
		//disable_music
		//disable_music_finished
		//trigger_now
		//trigger_now_finished
		//barrier_open
		//barrier_open_finished
		//barrier_close
		//barrier_close_finished
		//enable_override
		//enable_override_finished
		//disable_override
		//disable_override_finished
		//clear_pending_ui
		//clear_pending_ui_finished
		//hide_ui
		//hide_ui_finished
		//ui_hidden
		//show_ui
		//show_ui_finished
		//ui_shown
		//update_cost
		//update_cost_finished
		//on_updated_cost
		//enable_chokepoint
		//enable_chokepoint_finished
		//on_enable_chokepoint
		//disable_chokepoint
		//disable_chokepoint_finished
		//on_disable_chokepoint
		//update_squad_params
		//update_squad_params_finished
		//squad_params_updated
		//start_ping
		//start_ping_finished
		//started_ping
		//stop_ping
		//stop_ping_finished
		//stopped_ping
		//start_monitor
		//start_monitor_finished
		//started_monitor
		//stop_monitor
		//stop_monitor_finished
		//stopped_monitor
		//start_monitoring
		//start_monitoring_finished
		//started_monitoring
		//stop_monitoring
		//stop_monitoring_finished
		//stopped_monitoring
		//activate_tracker
		//activate_tracker_finished
		//activated_tracker
		//deactivate_tracker
		//deactivate_tracker_finished
		//deactivated_tracker
		//start_benchmark
		//start_benchmark_finished
		//started_benchmark
		//stop_benchmark
		//stop_benchmark_finished
		//stopped_benchmark
		//apply_hide
		//apply_hide_finished
		//hide_applied
		//apply_show
		//apply_show_finished
		//show_applied
		//display_tutorial
		//display_tutorial_finished
		//transition_completed
		//transition_completed_finished
		//display_tutorial_breathing_1
		//display_tutorial_breathing_1_finished
		//display_tutorial_breathing_2
		//display_tutorial_breathing_2_finished
		//breathing_game_tutorial_fail
		//breathing_game_tutorial_fail_finished
		//refresh_value
		//refresh_value_finished
		//value_refeshed
		//refresh_text
		//refresh_text_finished
		//text_refeshed
		//stop_emitting
		//stop_emitting_finished
		//stopped_emitting
		//activate_camera
		//activate_camera_finished
		//camera_activated
		//deactivate_camera
		//deactivate_camera_finished
		//camera_deactivated
		//activate_behavior
		//activate_behavior_finished
		//behavior_activated
		//deactivate_behavior
		//deactivate_behavior_finished
		//behavior_deactivated
		//activate_modifier
		//activate_modifier_finished
		//modifier_activated
		//deactivate_modifier
		//deactivate_modifier_finished
		//modifier_deactivated
		//force_disable_highlight
		//force_disable_highlight_finished
		//cutting_panel_start
		//cutting_panel_start_finished
		//cutting_pannel_started
		//cutting_panel_finish
		//cutting_panel_finish_finished
		//cutting_pannel_finished
		//keypad_interaction_start
		//keypad_interaction_start_finished
		//keypad_interaction_started
		//keypad_interaction_finish
		//keypad_interaction_finish_finished
		//keypad_interaction_finished
		//traversal_interaction_start
		//traversal_interaction_start_finished
		//traversal_interaction_started
		//lever_interaction_start
		//lever_interaction_start_finished
		//lever_interaction_started
		//lever_interaction_finish
		//lever_interaction_finish_finished
		//lever_interaction_finished
		//button_interaction_start
		//button_interaction_start_finished
		//button_interaction_started
		//button_interaction_finish
		//button_interaction_finish_finished
		//button_interaction_finished
		//ladder_interaction_start
		//ladder_interaction_start_finished
		//ladder_interaction_started
		//ladder_interaction_finish
		//ladder_interaction_finish_finished
		//ladder_interaction_finished
		//hacking_interaction_start
		//hacking_interaction_start_finished
		//hacking_interaction_started
		//hacking_interaction_finish
		//hacking_interaction_finish_finished
		//hacking_interaction_finished
		//rewire_interaction_start
		//rewire_interaction_start_finished
		//rewire_interaction_started
		//rewire_interaction_finish
		//rewire_interaction_finish_finished
		//rewire_interaction_finished
		//terminal_interaction_start
		//terminal_interaction_start_finished
		//terminal_interaction_started
		//terminal_interaction_finish
		//terminal_interaction_finish_finished
		//terminal_interaction_finished
		//suit_change_interaction_start
		//suit_change_interaction_start_finished
		//suit_change_interaction_started
		//suit_change_interaction_finish
		//suit_change_interaction_finish_finished
		//suit_change_interaction_finished
		//cutscene_visibility_start
		//cutscene_visibility_start_finished
		//cutscene_visibility_started
		//cutscene_visibility_finish
		//cutscene_visibility_finish_finished
		//cutscene_visibility_finished
		//hiding_visibility_start
		//hiding_visibility_start_finished
		//hiding_visibility_started
		//hiding_visibility_finish
		//hiding_visibility_finish_finished
		//hiding_visibility_finished
		//disable_radial
		//disable_radial_finished
		//radial_disabled
		//enable_radial
		//enable_radial_finished
		//radial_enabled
		//disable_radial_hacking_info
		//disable_radial_hacking_info_finished
		//radial_hacking_info_disabled
		//enable_radial_hacking_info
		//enable_radial_hacking_info_finished
		//radial_hacking_info_enabled
		//disable_radial_cutting_info
		//disable_radial_cutting_info_finished
		//radial_cutting_info_disabled
		//enable_radial_cutting_info
		//enable_radial_cutting_info_finished
		//radial_cutting_info_enabled
		//disable_radial_battery_info
		//disable_radial_battery_info_finished
		//radial_battery_info_disabled
		//enable_radial_battery_info
		//enable_radial_battery_info_finished
		//radial_battery_info_enabled
		//disable_hud_battery_info
		//disable_hud_battery_info_finished
		//hud_battery_info_disabled
		//enable_hud_battery_info
		//enable_hud_battery_info_finished
		//hud_battery_info_enabled
		//hide_objective_message
		//hide_objective_message_finished
		//objective_message_hidden
		//show_objective_message
		//show_objective_message_finished
		//objective_message_shown
		//finished_closing_container
		//finished_closing_container_finished
		//closing_container_finished
		//seed
		//seed_finished
		//seeded
		//ignite
		//ignite_finished
		//electrify
		//electrify_finished
		//drench
		//drench_finished
		//poison
		//poison_finished
		//set_active
		//set_active_finished
		//activated
		//set_inactive
		//set_inactive_finished
		//deactivated
		//level_fade_start
		//level_fade_start_finished
		//level_fade_started
		//level_fade_finish
		//level_fade_finish_finished
		//level_fade_finished
		//torch_turned_on
		//torch_turned_on_finished
		//torch_turned_off
		//torch_turned_off_finished
		//torch_new_battery_added
		//torch_new_battery_added_finished
		//torch_battery_has_expired
		//torch_battery_has_expired_finished
		//torch_low_power
		//torch_low_power_finished
		//turn_off_torch
		//turn_off_torch_finished
		//Turn_off_
		//turn_on_torch
		//turn_on_torch_finished
		//Turn_on_
		//toggle_torch
		//toggle_torch_finished
		//Toggle_Torch_
		//resume_torch
		//resume_torch_finished
		//Resume_
		//allow_torch
		//allow_torch_finished
		//Allow_
		//start_timer
		//start_timer_finished
		//timer_started
		//stop_timer
		//stop_timer_finished
		//timer_stopped
		//notify_animation_started
		//notify_animation_started_finished
		//notify_animation_finished
		//notify_animation_finished_finished
		//load_cutscene
		//load_cutscene_finished
		//unload_cutscene
		//unload_cutscene_finished
		//start_cutscene
		//start_cutscene_finished
		//cutscene_started
		//stop_cutscene
		//stop_cutscene_finished
		//cutscene_stopped
		//pause_cutscene
		//pause_cutscene_finished
		//cutscene_paused
		//resume_cutscene
		//resume_cutscene_finished
		//cutscene_resumed
		//turn_on_system
		//turn_on_system_finished
		//turn_off_system
		//turn_off_system_finished
		//force_killtrap
		//force_killtrap_finished
		//killtrap_forced
		//cancel_force_killtrap
		//cancel_force_killtrap_finished
		//canceled_force_killtrap
		//disable_killtrap
		//disable_killtrap_finished
		//cancel_disable_killtrap
		//cancel_disable_killtrap_finished
		//hit_by_flamethrower
		//hit_by_flamethrower_finished
		//upon_hit_by_flamethrower
		//cancel_hit_by_flamethrower
		//cancel_hit_by_flamethrower_finished
		//reload_fill
		//reload_fill_finished
		//reload_filled
		//reload_empty
		//reload_empty_finished
		//reload_emptied
		//reload_load
		//reload_load_finished
		//reload_loaded
		//reload_open
		//reload_open_finished
		//reload_opened
		//reload_fire
		//reload_fire_finished
		//reload_fired
		//reload_finish
		//reload_finish_finished
		//reload_finished
		//display_hacking_upgrade
		//display_hacking_upgrade_finished
		//hacking_upgrade_displayed
		//hide_hacking_upgrade
		//hide_hacking_upgrade_finished
		//hacking_upgrade_hidden
		//reset_hacking_success_flag
		//reset_hacking_success_flag_finished
		//hacking_success_flag_reset
		//impact_with_world
		//impact_with_world_finished
		//impacted_with_world
		//start_interaction
		//start_interaction_finished
		//interaction_started
		//stop_interaction
		//stop_interaction_finished
		//interaction_stopped
		//allow_interrupt
		//allow_interrupt_finished
		//interrupt_allowed
		//disallow_interrupt
		//disallow_interrupt_finished
		//interrupt_disallowed
		//Get_In
		//Get_In_finished
		//Getting_in
		//Add_NPC
		//Add_NPC_finished
		//Start_Breathing_Game
		//Start_Breathing_Game_finished
		//Breathing_Game_Started
		//End_Breathing_Game
		//End_Breathing_Game_finished
		//Breathing_Game_Ended
		//bind_all
		//bind_all_finished
		//verify
		//verify_finished
		//fake_light_on
		//fake_light_on_finished
		//fake_light_on_triggered
		//fake_light_off
		//fake_light_off_finished
		//fake_light_off_triggered
	};

	//82-AD-06-32
	public class AccessTerminal: EntityMethodInterface {
		//closed
		//all_data_has_been_read
		//ui_breakout_triggered
		//light_on_reset
		//folder0
		//folder1
		//folder2
		//folder3
		//all_data_read
		//location
	};

	//6A-15-20-DA
	public class AchievementMonitor: EntityMethodInterface {
		public CathodeString achievement_id = new CathodeString(); 
	};

	//28-94-7F-F8
	public class AchievementStat: EntityMethodInterface {
		public CathodeString achievement_id = new CathodeString(); 
	};

	//8C-73-7E-79
	public class AchievementUniqueCounter: EntityMethodInterface {
		public CathodeString achievement_id = new CathodeString(); 
		public CathodeInteger _3A_5B_2F_9A = new CathodeInteger(); 
		//unique_object
	};

	//3E-B8-C5-86
	public class AddExitObjective: EntityMethodInterface {
		public CathodeEnum level_name = new CathodeEnum(); 
		//marker
	};

	//A1-84-75-9C
	public class AddItemsToGCPool: EntityMethodInterface {
		//items
	};

	//05-3E-42-CC
	public class AddToInventory: EntityMethodInterface {
		//success
		//fail
		//items
	};

	//01-5A-21-D0
	public class AILightCurveSettings: EntityMethodInterface {
		public CathodeFloat y0 = new CathodeFloat(); 
		public CathodeFloat x2 = new CathodeFloat(); 
		public CathodeFloat y1 = new CathodeFloat(); 
		public CathodeFloat x1 = new CathodeFloat(); 
		public CathodeFloat x3 = new CathodeFloat(); 
		public CathodeFloat y2 = new CathodeFloat(); 
	};

	//C6-EB-7B-7D
	public class AIMED_ITEM: EntityMethodInterface {
		public CathodeEnum equipment_slot = new CathodeEnum(); 
		public CathodeEnum weapon_handedness = new CathodeEnum(); 
		public CathodeString inventory_name = new CathodeString(); 
		public CathodeBool holsters_on_owner = new CathodeBool(); 
		public CathodeBool spawn_on_reset = new CathodeBool(); 
		public CathodeString holster_node = new CathodeString(); 
		public CathodeString character_animation_context = new CathodeString(); 
		public CathodeFloat holster_scale = new CathodeFloat(); 
		public CathodeString character_activate_animation_context = new CathodeString(); 
		public CathodeBool left_handed = new CathodeBool(); 
		public CathodeFloat fixed_target_distance_for_local_player = new CathodeFloat(); 
		//on_started_aiming
		//on_stopped_aiming
		//on_display_on
		//on_display_off
		//on_effect_on
		//on_effect_off
		//target_position
		//average_target_distance
		//min_target_distance
	};

	//9C-93-3E-AE
	public class AIMED_WEAPON: EntityMethodInterface {
		public CathodeEnum equipment_slot = new CathodeEnum(); 
		public CathodeFloat _7F_15_CD_1A = new CathodeFloat(); 
		public CathodeFloat _88_7A_C7_1C = new CathodeFloat(); 
		public CathodeString inventory_name = new CathodeString(); 
		public CathodeFloat accuracy_penalty_per_shot = new CathodeFloat(); 
		public CathodeFloat max_manual_shots_per_second = new CathodeFloat(); 
		public CathodeEnum default_ammo_type = new CathodeEnum(); 
		public CathodeFloat movement_accuracy_penalty_per_second = new CathodeFloat(); 
		public CathodeFloat accuracy_accumulated_per_second = new CathodeFloat(); 
		public CathodeBool alwaysDoFullReloadOfClips = new CathodeBool(); 
		public CathodeString item_animated_asset = new CathodeString(); 
		public CathodeBool holsters_on_owner = new CathodeBool(); 
		public CathodeBool spawn_on_reset = new CathodeBool(); 
		public CathodeInteger clip_size = new CathodeInteger(); 
		public CathodeInteger starting_ammo = new CathodeInteger(); 
		public CathodeString holster_node = new CathodeString(); 
		public CathodeEnum weapon_type = new CathodeEnum(); 
		public CathodeString character_animation_context = new CathodeString(); 
		public CathodeFloat holster_scale = new CathodeFloat(); 
		public CathodeEnum weapon_handedness = new CathodeEnum(); 
		public CathodeFloat wind_down_time_in_seconds = new CathodeFloat(); 
		public CathodeFloat maximum_continous_fire_time_in_seconds = new CathodeFloat(); 
		public CathodeFloat player_exposed_accuracy_penalty_per_shot = new CathodeFloat(); 
		public CathodeFloat max_auto_shots_per_second = new CathodeFloat(); 
		public CathodeFloat player_exposed_accuracy_accumulated_per_second = new CathodeFloat(); 
		public CathodeFloat aim_assist_scale = new CathodeFloat(); 
		public CathodeFloat consume_ammo_over_time_when_turned_on = new CathodeFloat(); 
		public CathodeFloat overheat_recharge_time_in_seconds = new CathodeFloat(); 
		public CathodeBool overheats = new CathodeBool(); 
		public CathodeBool requires_turning_on = new CathodeBool(); 
		public CathodeBool alien_threat_aware = new CathodeBool(); 
		public CathodeBool reloadIndividualAmmo = new CathodeBool(); 
		public CathodeFloat fixed_target_distance_for_local_player = new CathodeFloat(); 
		public CathodeBool recoils_on_fire = new CathodeBool(); 
		public CathodeBool automatic_firing = new CathodeBool(); 
		public CathodeFloat charging_duration = new CathodeFloat(); 
		public CathodeBool charged_firing = new CathodeBool(); 
		public CathodeFloat aim_rotation_accuracy_penalty_per_second = new CathodeFloat(); 
		public CathodeFloat overcharge_timer = new CathodeFloat(); 
		public CathodeFloat charge_noise_start_time = new CathodeFloat(); 
		public CathodeBool left_handed = new CathodeBool(); 
		public CathodeBool ejectsShellsOnFiring = new CathodeBool(); 
		//on_fired_success
		//on_fired_fail
		//on_fired_fail_single
		//on_impact
		//on_reload_started
		//on_reload_another
		//on_reload_empty_clip
		//on_reload_canceled
		//on_reload_success
		//on_reload_fail
		//on_shooting_started
		//on_shooting_wind_down
		//on_shooting_finished
		//on_overheated
		//on_cooled_down
		//on_charge_complete
		//on_charge_started
		//on_charge_stopped
		//on_turned_on
		//on_turned_off
		//on_torch_on_requested
		//on_torch_off_requested
		//ammoRemainingInClip
		//ammoToFillClip
		//ammoThatWasInClip
		//charge_percentage
		//charge_noise_percentage
		//min_charge_to_fire
	};

	//69-14-82-15
	public class AllocateGCItemFromPoolBySubset: EntityMethodInterface {
		public CathodeBool force_usage = new CathodeBool(); 
		//on_success
		//on_failure
		//selectable_items
		//item_name
		//item_quantity
		//distribution_bias
	};

	//7B-C5-AE-F0
	public class AllocateGCItemsFromPool: EntityMethodInterface {
		//on_success
		//on_failure
		//items
		//force_usage_count
		//distribution_bias
	};

	//15-66-6D-F7
	public class AnimatedModelAttachmentNode: EntityMethodInterface {
		public CathodeString bone_name = new CathodeString(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeBool use_offset = new CathodeBool(); 
		public CathodeFloat attach = new CathodeFloat(); 
		public CathodeTransform offset = new CathodeTransform(); 
		//animated_model
		//attachment
	};

	//19-E9-94-7D
	public class AnimationMask: EntityMethodInterface {
		public CathodeBool maskLeftFingers = new CathodeBool(); 
		public CathodeBool maskPrecedingLayers = new CathodeBool(); 
		public CathodeBool maskRightArm = new CathodeBool(); 
		public CathodeBool maskRightShoulder = new CathodeBool(); 
		public CathodeBool maskLeftArm = new CathodeBool(); 
		public CathodeBool maskLeftShoulder = new CathodeBool(); 
		public CathodeBool maskLeftHand = new CathodeBool(); 
		public CathodeBool maskRightFingers = new CathodeBool(); 
		public CathodeBool maskRightHand = new CathodeBool(); 
		public CathodeBool maskFollowingLayers = new CathodeBool(); 
		public CathodeBool maskHead = new CathodeBool(); 
		public CathodeBool maskTorso = new CathodeBool(); 
		public CathodeBool maskNeck = new CathodeBool(); 
		public CathodeBool maskLips = new CathodeBool(); 
		public CathodeBool maskEyes = new CathodeBool(); 
		public CathodeBool maskRoot = new CathodeBool(); 
		public CathodeBool maskLeftLeg = new CathodeBool(); 
		public CathodeBool maskTail = new CathodeBool(); 
		public CathodeBool maskRightLeg = new CathodeBool(); 
		public CathodeBool maskHips = new CathodeBool(); 
		public CathodeBool maskSelf = new CathodeBool(); 
		public CathodeBool maskFace = new CathodeBool(); 
		//weight
		//resource
	};

	//02-AE-0F-19
	public class ApplyRelativeTransform: EntityMethodInterface {
		public CathodeBool use_trigger_entity = new CathodeBool(); 
		//origin
		//destination
		//input
		//output
	};

	//88-47-3A-05
	public class AreaHitMonitor: EntityMethodInterface {
		public CathodeFloat SphereRadius = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_flamer_hit
		//on_shotgun_hit
		//on_pistol_hit
		//SpherePos
	};

	//76-E2-66-D6
	public class AssetSpawner: EntityMethodInterface {
		public CathodeBool allow_forced_despawn = new CathodeBool(); 
		public CathodeBool persist_on_callback = new CathodeBool(); 
		public CathodeBool spawn_on_load = new CathodeBool(); 
		public CathodeBool spawn_on_reset = new CathodeBool(); 
		//finished_spawning
		//callback_triggered
		//forced_despawn
		//asset
		//allow_physics
	};

	//E7-12-38-F4
	public class Benchmark: EntityMethodInterface {
		public CathodeString benchmark_name = new CathodeString(); 
		public CathodeBool save_stats = new CathodeBool(); 
	};

	//0F-6A-4C-5F
	public class BindObjectsMultiplexer: EntityMethodInterface {
		//Pin1_Bound
		//Pin2_Bound
		//Pin3_Bound
		//Pin4_Bound
		//Pin5_Bound
		//Pin6_Bound
		//Pin7_Bound
		//Pin8_Bound
		//Pin9_Bound
		//Pin10_Bound
		//objects
	};

	//91-11-9C-99
	public class BlendLowResFrame: EntityMethodInterface {
		public CathodeFloat intensity = new CathodeFloat(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
		public CathodeInteger priority = new CathodeInteger(); 
		//blend_value
		//CharacterMonitor
		//character
	};

	//54-5C-AD-C4
	public class BloomSettings: EntityMethodInterface {
		public CathodeFloat bloom_gather_scale = new CathodeFloat(); 
		public CathodeFloat frame_buffer_offset = new CathodeFloat(); 
		public CathodeFloat bloom_scale = new CathodeFloat(); 
		public CathodeFloat bloom_gather_exponent = new CathodeFloat(); 
		public CathodeFloat frame_buffer_scale = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
	};

	//48-73-41-B9
	public class BoneAttachedCamera: EntityMethodInterface {
		public CathodeVector3 rotation_offset = new CathodeVector3(); 
		public CathodeFloat blend_in = new CathodeFloat(); 
		public CathodeString bone_name = new CathodeString(); 
		public CathodeVector3 position_offset = new CathodeVector3(); 
		public CathodeString behavior_name = new CathodeString(); 
		public CathodeFloat blend_out = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat movement_damping = new CathodeFloat(); 
		//character
	};

	//34-81-BE-51
	public class Box: EntityMethodInterface {
		public CathodeString radius = new CathodeString(); 
		public CathodeFloat enable = new CathodeFloat(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool include_physics = new CathodeBool(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		//event
	};

	//34-20-89-9B
	public class BulletChamber: EntityMethodInterface {
		//Slot1
		//Slot2
		//Slot3
		//Slot4
		//Slot5
		//Slot6
		//Weapon
		//Geometry
	};

	//97-F2-28-17
	public class ButtonMashPrompt: EntityMethodInterface {
		public CathodeInteger mashes_to_completion = new CathodeInteger(); 
		public CathodeFloat time_between_degrades = new CathodeFloat(); 
		public CathodeBool use_degrade = new CathodeBool(); 
		//on_back_to_zero
		//on_degrade
		//on_mashed
		//on_success
		//count
		//hold_to_charge
	};

	//BC-07-00-79
	public class CAGEAnimation: EntityMethodInterface {
		public CathodeFloat _E0_20_7B_08 = new CathodeFloat(); 
		public CathodeFloat _BE_4C_01_09 = new CathodeFloat(); 
		public CathodeFloat _0B_86_B7_0D = new CathodeFloat(); 
		public CathodeFloat _32_9F_A7_13 = new CathodeFloat(); 
		public CathodeFloat start_cutscene = new CathodeFloat(); 
		public CathodeFloat _2F_43_90_3A = new CathodeFloat(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeFloat _97_E7_2E_9A = new CathodeFloat(); 
		public CathodeFloat _7A_8E_3E_A5 = new CathodeFloat(); 
		public CathodeBool capture_video = new CathodeBool(); 
		public CathodeFloat _D0_32_94_BD = new CathodeFloat(); 
		public CathodeBool is_cinematic = new CathodeBool(); 
		public CathodeFloat _FE_F3_DD_D0 = new CathodeFloat(); 
		public CathodeFloat _BA_15_67_D4 = new CathodeFloat(); 
		public CathodeFloat _79_0F_D3_D4 = new CathodeFloat(); 
		public CathodeFloat _95_1C_67_F0 = new CathodeFloat(); 
		public CathodeString capture_clip_name = new CathodeString(); 
		public CathodeFloat anim_length = new CathodeFloat(); 
		public CathodeBool use_external_time = new CathodeBool(); 
		public CathodeFloat _3A_07_4C_8B = new CathodeFloat(); 
		public CathodeFloat _DA_EC_92_BD = new CathodeFloat(); 
		public CathodeFloat _12_EA_6A_CC = new CathodeFloat(); 
		public CathodeFloat finished = new CathodeFloat(); 
		public CathodeFloat _08_BD_F1_0A = new CathodeFloat(); 
		public CathodeFloat _D4_10_04_0C = new CathodeFloat(); 
		public CathodeFloat _E3_88_EF_33 = new CathodeFloat(); 
		public CathodeFloat _81_6C_CA_3C = new CathodeFloat(); 
		public CathodeFloat _A5_FF_11_52 = new CathodeFloat(); 
		public CathodeFloat _66_64_2D_56 = new CathodeFloat(); 
		public CathodeFloat _5C_CC_B9_5D = new CathodeFloat(); 
		public CathodeFloat _09_18_A2_63 = new CathodeFloat(); 
		public CathodeFloat playspeed = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat _06_80_90_0B = new CathodeFloat(); 
		public CathodeFloat _C3_E7_7C_15 = new CathodeFloat(); 
		public CathodeFloat _ED_85_45_4C = new CathodeFloat(); 
		public CathodeFloat _21_5D_D5_5D = new CathodeFloat(); 
		public CathodeFloat _80_ED_18_79 = new CathodeFloat(); 
		public CathodeFloat _0B_CE_0A_97 = new CathodeFloat(); 
		public CathodeFloat _2E_DA_BF_98 = new CathodeFloat(); 
		public CathodeFloat _7A_45_C9_9E = new CathodeFloat(); 
		public CathodeFloat _BC_52_51_B2 = new CathodeFloat(); 
		public CathodeFloat _1B_D3_81_D5 = new CathodeFloat(); 
		public CathodeFloat _D3_7F_28_DF = new CathodeFloat(); 
		public CathodeFloat _5C_82_8B_E2 = new CathodeFloat(); 
		public CathodeFloat _4F_30_54_F0 = new CathodeFloat(); 
		public CathodeFloat _DF_12_4D_FC = new CathodeFloat(); 
		public CathodeFloat _35_6D_12_76 = new CathodeFloat(); 
		public CathodeFloat _32_BC_62_C7 = new CathodeFloat(); 
		public CathodeFloat _0C_FD_46_48 = new CathodeFloat(); 
		public CathodeFloat fade_out = new CathodeFloat(); 
		public CathodeFloat _56_85_3C_1F = new CathodeFloat(); 
		public CathodeFloat _BF_41_91_CC = new CathodeFloat(); 
		public CathodeFloat _C5_51_64_15 = new CathodeFloat(); 
		public CathodeFloat _F5_E9_78_3A = new CathodeFloat(); 
		public CathodeFloat _1C_1F_94_57 = new CathodeFloat(); 
		public CathodeFloat _FC_99_B3_62 = new CathodeFloat(); 
		public CathodeFloat _0C_90_C9_65 = new CathodeFloat(); 
		public CathodeFloat _4B_9E_5A_6D = new CathodeFloat(); 
		public CathodeFloat _33_EF_8C_75 = new CathodeFloat(); 
		public CathodeFloat _4F_79_DF_7B = new CathodeFloat(); 
		public CathodeFloat _0B_7C_10_7C = new CathodeFloat(); 
		public CathodeBool rewind_on_stop = new CathodeBool(); 
		public CathodeFloat _2B_DE_35_83 = new CathodeFloat(); 
		public CathodeFloat _A4_E5_0F_86 = new CathodeFloat(); 
		public CathodeFloat _52_7D_9E_89 = new CathodeFloat(); 
		public CathodeFloat _1C_0E_18_8A = new CathodeFloat(); 
		public CathodeFloat _B3_00_8E_92 = new CathodeFloat(); 
		public CathodeFloat _6E_B0_D2_96 = new CathodeFloat(); 
		public CathodeFloat _1F_66_39_A5 = new CathodeFloat(); 
		public CathodeFloat _6E_2B_D7_AF = new CathodeFloat(); 
		public CathodeFloat _F1_6B_7E_B1 = new CathodeFloat(); 
		public CathodeFloat _6B_EB_88_B3 = new CathodeFloat(); 
		public CathodeFloat _81_5D_A6_D9 = new CathodeFloat(); 
		public CathodeFloat _2E_EA_AB_F0 = new CathodeFloat(); 
		public CathodeFloat _36_32_88_FE = new CathodeFloat(); 
		public CathodeFloat _B3_24_F9_0B = new CathodeFloat(); 
		public CathodeFloat _94_F6_8B_14 = new CathodeFloat(); 
		public CathodeFloat _56_DB_71_1C = new CathodeFloat(); 
		public CathodeFloat _9A_D9_5A_25 = new CathodeFloat(); 
		public CathodeFloat _D6_CA_58_30 = new CathodeFloat(); 
		public CathodeFloat _CD_25_DF_38 = new CathodeFloat(); 
		public CathodeFloat _3B_C0_DA_3B = new CathodeFloat(); 
		public CathodeFloat _43_34_74_3D = new CathodeFloat(); 
		public CathodeFloat _D0_E7_91_40 = new CathodeFloat(); 
		public CathodeFloat _92_9C_21_57 = new CathodeFloat(); 
		public CathodeFloat _7D_20_D5_65 = new CathodeFloat(); 
		public CathodeFloat _1F_AE_C0_77 = new CathodeFloat(); 
		public CathodeFloat _F1_34_E1_82 = new CathodeFloat(); 
		public CathodeFloat _7A_F5_1E_91 = new CathodeFloat(); 
		public CathodeFloat _35_24_E5_A3 = new CathodeFloat(); 
		public CathodeFloat _08_D5_93_A5 = new CathodeFloat(); 
		public CathodeFloat _0F_70_EF_AB = new CathodeFloat(); 
		public CathodeFloat _51_F0_60_C4 = new CathodeFloat(); 
		public CathodeFloat _C4_47_D4_D6 = new CathodeFloat(); 
		public CathodeFloat _4F_2A_2C_DA = new CathodeFloat(); 
		public CathodeFloat _4C_C7_3C_04 = new CathodeFloat(); 
		public CathodeFloat _EF_54_74_34 = new CathodeFloat(); 
		public CathodeFloat disable = new CathodeFloat(); 
		public CathodeFloat _D2_E0_5C_02 = new CathodeFloat(); 
		public CathodeFloat _15_2A_CC_0A = new CathodeFloat(); 
		public CathodeFloat _A6_F0_74_1F = new CathodeFloat(); 
		public CathodeFloat _D9_59_61_33 = new CathodeFloat(); 
		public CathodeFloat _D0_F1_C6_4F = new CathodeFloat(); 
		public CathodeFloat _6C_CB_C5_5E = new CathodeFloat(); 
		public CathodeFloat _3A_BA_5A_76 = new CathodeFloat(); 
		public CathodeFloat _DF_FA_6E_7F = new CathodeFloat(); 
		public CathodeFloat _ED_77_C5_7F = new CathodeFloat(); 
		public CathodeFloat _60_9E_A4_82 = new CathodeFloat(); 
		public CathodeFloat _A5_A2_EA_8B = new CathodeFloat(); 
		public CathodeFloat _5B_62_43_96 = new CathodeFloat(); 
		public CathodeFloat _D3_C3_9E_A0 = new CathodeFloat(); 
		public CathodeFloat _70_8E_CA_A6 = new CathodeFloat(); 
		public CathodeFloat _02_E8_A6_DB = new CathodeFloat(); 
		public CathodeFloat _B6_B3_9B_EF = new CathodeFloat(); 
		public CathodeFloat _61_28_F2_1E = new CathodeFloat(); 
		public CathodeFloat _F2_50_AE_C3 = new CathodeFloat(); 
		public CathodeFloat _8B_DA_F1_02 = new CathodeFloat(); 
		public CathodeFloat _12_34_5F_49 = new CathodeFloat(); 
		public CathodeFloat _E2_DA_01_06 = new CathodeFloat(); 
		public CathodeFloat _E6_09_52_1B = new CathodeFloat(); 
		public CathodeFloat _D9_5E_56_28 = new CathodeFloat(); 
		public CathodeFloat _AE_57_66_58 = new CathodeFloat(); 
		public CathodeFloat _02_EC_1D_5B = new CathodeFloat(); 
		public CathodeFloat _2C_B9_7B_69 = new CathodeFloat(); 
		public CathodeFloat _C9_1E_09_74 = new CathodeFloat(); 
		public CathodeFloat _7C_D6_CD_7E = new CathodeFloat(); 
		public CathodeFloat _79_D1_6B_A4 = new CathodeFloat(); 
		public CathodeFloat _88_3E_79_A5 = new CathodeFloat(); 
		public CathodeFloat _A6_81_AD_B9 = new CathodeFloat(); 
		public CathodeFloat _2E_8A_20_BF = new CathodeFloat(); 
		public CathodeFloat _6E_2F_C0_D5 = new CathodeFloat(); 
		public CathodeFloat _94_50_BE_DB = new CathodeFloat(); 
		public CathodeFloat _BF_CC_DC_DB = new CathodeFloat(); 
		public CathodeFloat _F5_04_7F_DC = new CathodeFloat(); 
		public CathodeFloat _70_22_4F_B5 = new CathodeFloat(); 
		public CathodeFloat _35_1B_32_CA = new CathodeFloat(); 
		public CathodeFloat _8E_F2_0E_1D = new CathodeFloat(); 
		public CathodeFloat _3E_09_34_6E = new CathodeFloat(); 
		public CathodeFloat _7A_5A_13_53 = new CathodeFloat(); 
		public CathodeFloat _A0_36_23_76 = new CathodeFloat(); 
		public CathodeFloat _1F_29_D5_EC = new CathodeFloat(); 
		public CathodeFloat attach = new CathodeFloat(); 
		public CathodeFloat _75_BE_2B_14 = new CathodeFloat(); 
		public CathodeFloat _32_BC_BA_41 = new CathodeFloat(); 
		public CathodeFloat _8C_B5_5F_6E = new CathodeFloat(); 
		public CathodeFloat _07_B1_3F_7B = new CathodeFloat(); 
		public CathodeFloat _DB_FC_1C_A2 = new CathodeFloat(); 
		public CathodeFloat _62_FF_03_A4 = new CathodeFloat(); 
		public CathodeFloat _7B_3C_0A_D2 = new CathodeFloat(); 
		public CathodeFloat _C6_40_F4_D9 = new CathodeFloat(); 
		public CathodeFloat _26_3A_A1_88 = new CathodeFloat(); 
		public CathodeFloat detach = new CathodeFloat(); 
		public CathodeFloat _EF_F6_FB_0B = new CathodeFloat(); 
		public CathodeFloat _E4_0B_F0_3D = new CathodeFloat(); 
		public CathodeFloat _BC_10_88_4E = new CathodeFloat(); 
		public CathodeFloat _6E_38_B9_54 = new CathodeFloat(); 
		public CathodeFloat _82_22_66_55 = new CathodeFloat(); 
		public CathodeFloat _9D_40_9B_5F = new CathodeFloat(); 
		public CathodeFloat _CE_2A_C6_63 = new CathodeFloat(); 
		public CathodeFloat _3A_48_7C_70 = new CathodeFloat(); 
		public CathodeFloat _02_F6_B6_74 = new CathodeFloat(); 
		public CathodeFloat _88_4A_9E_7A = new CathodeFloat(); 
		public CathodeFloat _48_08_64_85 = new CathodeFloat(); 
		public CathodeFloat _3C_9B_76_EC = new CathodeFloat(); 
		public CathodeFloat _37_18_9C_21 = new CathodeFloat(); 
		public CathodeFloat _4F_C9_E3_82 = new CathodeFloat(); 
		public CathodeFloat _F7_CD_64_50 = new CathodeFloat(); 
		public CathodeFloat _6C_DD_ED_97 = new CathodeFloat(); 
		public CathodeFloat _E5_8E_AE_13 = new CathodeFloat(); 
		public CathodeFloat _BF_DF_76_1D = new CathodeFloat(); 
		public CathodeFloat _3C_41_76_26 = new CathodeFloat(); 
		public CathodeFloat _B8_F2_05_39 = new CathodeFloat(); 
		public CathodeFloat _BB_8D_81_5D = new CathodeFloat(); 
		public CathodeFloat _B5_23_44_90 = new CathodeFloat(); 
		public CathodeFloat _DC_C4_72_AD = new CathodeFloat(); 
		public CathodeFloat _77_D8_94_B9 = new CathodeFloat(); 
		public CathodeFloat _45_74_2A_D6 = new CathodeFloat(); 
		public CathodeFloat _B5_F2_C1_E8 = new CathodeFloat(); 
		public CathodeFloat _14_78_50_18 = new CathodeFloat(); 
		public CathodeFloat _54_17_07_22 = new CathodeFloat(); 
		public CathodeFloat _45_B3_01_27 = new CathodeFloat(); 
		public CathodeFloat _C1_9E_78_6D = new CathodeFloat(); 
		public CathodeFloat _7D_0D_17_80 = new CathodeFloat(); 
		public CathodeFloat _8D_34_E1_A8 = new CathodeFloat(); 
		public CathodeFloat _44_39_45_B1 = new CathodeFloat(); 
		public CathodeFloat _DF_54_87_B8 = new CathodeFloat(); 
		public CathodeFloat _C6_0C_94_CE = new CathodeFloat(); 
		public CathodeFloat _97_15_BE_DD = new CathodeFloat(); 
		public CathodeFloat _B3_64_B6_E0 = new CathodeFloat(); 
		public CathodeFloat _CD_CE_26_E3 = new CathodeFloat(); 
		public CathodeFloat _3B_D4_CA_F2 = new CathodeFloat(); 
		public CathodeFloat _2A_2E_AA_F7 = new CathodeFloat(); 
		public CathodeFloat hide = new CathodeFloat(); 
		public CathodeFloat _99_CE_AC_66 = new CathodeFloat(); 
		public CathodeFloat _66_F9_6C_6A = new CathodeFloat(); 
		public CathodeFloat _BD_F5_32_81 = new CathodeFloat(); 
		public CathodeFloat _41_6E_11_99 = new CathodeFloat(); 
		public CathodeFloat _72_0A_1D_D1 = new CathodeFloat(); 
		public CathodeFloat _8C_67_7C_E5 = new CathodeFloat(); 
		public CathodeFloat show = new CathodeFloat(); 
		public CathodeFloat _14_73_25_02 = new CathodeFloat(); 
		public CathodeFloat _49_3E_88_28 = new CathodeFloat(); 
		public CathodeFloat _51_FD_C7_51 = new CathodeFloat(); 
		public CathodeFloat _B1_DC_AE_59 = new CathodeFloat(); 
		public CathodeFloat _30_EF_03_7B = new CathodeFloat(); 
		public CathodeFloat _1D_29_2D_93 = new CathodeFloat(); 
		public CathodeFloat _26_FD_70_B5 = new CathodeFloat(); 
		public CathodeFloat _69_5E_DE_DA = new CathodeFloat(); 
		public CathodeFloat _2D_D4_46_2C = new CathodeFloat(); 
		public CathodeFloat _2E_C7_A4_36 = new CathodeFloat(); 
		public CathodeFloat _D7_00_13_3A = new CathodeFloat(); 
		public CathodeFloat _78_3D_B6_4C = new CathodeFloat(); 
		public CathodeFloat _13_48_E1_62 = new CathodeFloat(); 
		public CathodeFloat _6B_0D_A1_67 = new CathodeFloat(); 
		public CathodeFloat _28_BB_79_6B = new CathodeFloat(); 
		public CathodeFloat _F7_D1_28_7C = new CathodeFloat(); 
		public CathodeFloat _4F_E6_7C_B6 = new CathodeFloat(); 
		public CathodeFloat _E4_04_A2_CD = new CathodeFloat(); 
		public CathodeFloat _1B_1D_C4_E6 = new CathodeFloat(); 
		public CathodeFloat _FD_82_AE_F2 = new CathodeFloat(); 
		public CathodeFloat _D5_DE_55_FB = new CathodeFloat(); 
		public CathodeFloat _9E_BD_2E_FD = new CathodeFloat(); 
		public CathodeFloat _37_DD_96_2E = new CathodeFloat(); 
		public CathodeFloat _87_46_15_36 = new CathodeFloat(); 
		public CathodeFloat _C1_01_39_41 = new CathodeFloat(); 
		public CathodeFloat _9E_A0_6D_68 = new CathodeFloat(); 
		public CathodeFloat _C7_E2_01_77 = new CathodeFloat(); 
		public CathodeFloat _E9_76_AB_8F = new CathodeFloat(); 
		public CathodeFloat _08_9E_FC_9F = new CathodeFloat(); 
		public CathodeFloat _30_09_03_AA = new CathodeFloat(); 
		public CathodeFloat _C2_35_0A_C2 = new CathodeFloat(); 
		public CathodeFloat _76_CE_73_C9 = new CathodeFloat(); 
		public CathodeFloat _67_E4_37_CF = new CathodeFloat(); 
		public CathodeFloat _2B_23_E8_F2 = new CathodeFloat(); 
		public CathodeFloat _5D_10_D8_05 = new CathodeFloat(); 
		public CathodeFloat _B8_C1_90_0D = new CathodeFloat(); 
		public CathodeFloat _45_D3_B3_2F = new CathodeFloat(); 
		public CathodeFloat _21_98_AE_30 = new CathodeFloat(); 
		public CathodeFloat _A4_7B_1F_38 = new CathodeFloat(); 
		public CathodeFloat _EA_DC_61_3B = new CathodeFloat(); 
		public CathodeFloat _C3_BF_3E_4B = new CathodeFloat(); 
		public CathodeFloat _B1_0D_A3_65 = new CathodeFloat(); 
		public CathodeFloat _3B_0B_C5_87 = new CathodeFloat(); 
		public CathodeFloat _E7_C4_1E_A4 = new CathodeFloat(); 
		public CathodeFloat _E3_39_77_F8 = new CathodeFloat(); 
		public CathodeFloat _54_0A_AB_FA = new CathodeFloat(); 
		public CathodeFloat _B4_35_6E_30 = new CathodeFloat(); 
		public CathodeFloat _83_78_AF_48 = new CathodeFloat(); 
		public CathodeFloat _F6_47_26_82 = new CathodeFloat(); 
		public CathodeFloat _1E_1D_9E_FC = new CathodeFloat(); 
		public CathodeFloat _E9_60_E2_43 = new CathodeFloat(); 
		public CathodeFloat _AC_53_4E_6E = new CathodeFloat(); 
		public CathodeFloat _43_B5_F1_F0 = new CathodeFloat(); 
		public CathodeFloat _47_9E_56_FE = new CathodeFloat(); 
		public CathodeFloat _31_2E_57_7D = new CathodeFloat(); 
		public CathodeFloat _8D_A4_F6_CC = new CathodeFloat(); 
		public CathodeFloat _88_9F_5D_9E = new CathodeFloat(); 
		public CathodeFloat _7E_2B_7C_A1 = new CathodeFloat(); 
		public CathodeFloat _FA_E5_10_A5 = new CathodeFloat(); 
		public CathodeFloat _01_CB_77_C3 = new CathodeFloat(); 
		public CathodeFloat _8A_8F_B4_F0 = new CathodeFloat(); 
		public CathodeFloat _02_F2_17_FE = new CathodeFloat(); 
		public CathodeFloat _18_D8_E1_12 = new CathodeFloat(); 
		public CathodeFloat _D5_7B_2B_C0 = new CathodeFloat(); 
		public CathodeFloat _DC_56_1A_C5 = new CathodeFloat(); 
		public CathodeFloat _2D_2C_7F_F1 = new CathodeFloat(); 
		public CathodeFloat _70_8A_77_2E = new CathodeFloat(); 
		public CathodeFloat _5E_31_F9_3D = new CathodeFloat(); 
		public CathodeFloat _3A_B6_AF_82 = new CathodeFloat(); 
		public CathodeFloat _B0_22_79_AE = new CathodeFloat(); 
		public CathodeFloat _BB_B5_96_18 = new CathodeFloat(); 
		public CathodeFloat _71_A9_11_40 = new CathodeFloat(); 
		public CathodeFloat _43_70_4A_A9 = new CathodeFloat(); 
		public CathodeFloat skippable_timer = new CathodeFloat(); 
		public CathodeFloat _66_B6_D1_C5 = new CathodeFloat(); 
		public CathodeFloat _3F_9D_81_D3 = new CathodeFloat(); 
		public CathodeFloat _89_A7_86_D7 = new CathodeFloat(); 
		public CathodeFloat _4A_20_58_F0 = new CathodeFloat(); 
		public CathodeFloat _8F_06_4F_F1 = new CathodeFloat(); 
		public CathodeFloat _96_6F_93_A1 = new CathodeFloat(); 
		public CathodeFloat _6A_51_64_CD = new CathodeFloat(); 
		public CathodeFloat _3B_24_29_E0 = new CathodeFloat(); 
		public CathodeFloat _58_F2_D4_0E = new CathodeFloat(); 
		public CathodeFloat _98_92_0D_2A = new CathodeFloat(); 
		public CathodeFloat _10_F6_49_2F = new CathodeFloat(); 
		public CathodeFloat _E6_65_AA_AF = new CathodeFloat(); 
		public CathodeFloat _BA_73_87_C3 = new CathodeFloat(); 
		public CathodeFloat _B1_E4_FF_DF = new CathodeFloat(); 
		public CathodeFloat _89_40_2D_E8 = new CathodeFloat(); 
		public CathodeFloat _F3_53_98_E8 = new CathodeFloat(); 
		public CathodeFloat _21_D0_C0_FC = new CathodeFloat(); 
		public CathodeFloat _A2_41_D5_47 = new CathodeFloat(); 
		public CathodeFloat _F9_08_2F_F6 = new CathodeFloat(); 
		public CathodeFloat _13_E4_A2_39 = new CathodeFloat(); 
		public CathodeFloat _86_4F_ED_9D = new CathodeFloat(); 
		public CathodeFloat _AB_65_2A_1D = new CathodeFloat(); 
		public CathodeFloat _6F_BD_23_88 = new CathodeFloat(); 
		public CathodeFloat close = new CathodeFloat(); 
		public CathodeFloat _C8_5B_C4_E2 = new CathodeFloat(); 
		public CathodeFloat _0C_CA_97_09 = new CathodeFloat(); 
		public CathodeFloat _21_40_68_11 = new CathodeFloat(); 
		public CathodeFloat _97_0D_42_1A = new CathodeFloat(); 
		public CathodeFloat _6F_8C_E6_28 = new CathodeFloat(); 
		public CathodeFloat _47_A8_AC_2E = new CathodeFloat(); 
		public CathodeFloat _F4_51_B2_4E = new CathodeFloat(); 
		public CathodeFloat _BF_D4_1D_50 = new CathodeFloat(); 
		public CathodeFloat _6C_B3_7E_5B = new CathodeFloat(); 
		public CathodeFloat _CD_1E_69_5E = new CathodeFloat(); 
		public CathodeFloat _05_D7_F2_5E = new CathodeFloat(); 
		public CathodeFloat _6B_C2_96_7F = new CathodeFloat(); 
		public CathodeFloat _49_C6_4E_8D = new CathodeFloat(); 
		public CathodeFloat _9F_23_E7_A9 = new CathodeFloat(); 
		public CathodeFloat _9E_D6_83_DE = new CathodeFloat(); 
		public CathodeFloat _B5_0E_60_E1 = new CathodeFloat(); 
		public CathodeFloat _26_71_22_F9 = new CathodeFloat(); 
		public CathodeFloat _89_DF_37_38 = new CathodeFloat(); 
		public CathodeFloat _78_7E_82_3C = new CathodeFloat(); 
		public CathodeFloat _85_A3_4D_C0 = new CathodeFloat(); 
		public CathodeFloat _F7_59_4D_C3 = new CathodeFloat(); 
		public CathodeFloat _FB_36_29_D6 = new CathodeFloat(); 
		public CathodeFloat _F3_20_FE_F7 = new CathodeFloat(); 
		public CathodeBool jump_to_the_end = new CathodeBool(); 
		public CathodeFloat _F9_22_AE_4E = new CathodeFloat(); 
		public CathodeFloat _9D_12_02_77 = new CathodeFloat(); 
		public CathodeFloat _EE_81_CF_B9 = new CathodeFloat(); 
		public CathodeFloat _8B_28_68_EF = new CathodeFloat(); 
		public CathodeFloat _88_98_12_75 = new CathodeFloat(); 
		public CathodeFloat _2A_BC_32_C3 = new CathodeFloat(); 
		public CathodeFloat _FA_19_A4_03 = new CathodeFloat(); 
		public CathodeFloat _05_EE_E8_03 = new CathodeFloat(); 
		public CathodeFloat _5C_E5_03_2B = new CathodeFloat(); 
		public CathodeFloat _AB_6B_85_79 = new CathodeFloat(); 
		public CathodeFloat _10_18_06_7E = new CathodeFloat(); 
		public CathodeFloat _CD_EF_86_98 = new CathodeFloat(); 
		public CathodeFloat _55_CF_A5_D4 = new CathodeFloat(); 
		public CathodeFloat _F2_50_9B_DD = new CathodeFloat(); 
		public CathodeFloat _7C_C7_69_04 = new CathodeFloat(); 
		public CathodeFloat _48_2D_55_C8 = new CathodeFloat(); 
		public CathodeFloat _85_5A_26_08 = new CathodeFloat(); 
		public CathodeFloat _3A_F4_DD_A1 = new CathodeFloat(); 
		public CathodeFloat _F0_D8_21_0A = new CathodeFloat(); 
		public CathodeFloat _90_50_43_93 = new CathodeFloat(); 
		public CathodeFloat _BC_89_D9_17 = new CathodeFloat(); 
		public CathodeFloat _54_95_4E_5C = new CathodeFloat(); 
		public CathodeFloat _5B_3C_07_8A = new CathodeFloat(); 
		public CathodeFloat _D2_34_50_8E = new CathodeFloat(); 
		public CathodeFloat _39_B7_EB_CC = new CathodeFloat(); 
		public CathodeFloat _93_39_50_DE = new CathodeFloat(); 
		public CathodeFloat _14_D9_AE_45 = new CathodeFloat(); 
		public CathodeFloat _99_02_0B_56 = new CathodeFloat(); 
		public CathodeFloat _B3_33_F5_9D = new CathodeFloat(); 
		public CathodeFloat _CF_C6_6B_AF = new CathodeFloat(); 
		public CathodeFloat _C2_EB_DD_1A = new CathodeFloat(); 
		public CathodeFloat _D8_92_8B_66 = new CathodeFloat(); 
		public CathodeFloat _B3_E1_4F_CC = new CathodeFloat(); 
		public CathodeFloat _5C_30_A5_D0 = new CathodeFloat(); 
		public CathodeFloat _25_9C_AE_89 = new CathodeFloat(); 
		public CathodeFloat _44_DB_1F_9A = new CathodeFloat(); 
		public CathodeFloat _AC_F3_05_AA = new CathodeFloat(); 
		public CathodeFloat _A0_24_0C_B8 = new CathodeFloat(); 
		public CathodeFloat _8D_3F_15_F3 = new CathodeFloat(); 
		public CathodeFloat _64_18_19_F6 = new CathodeFloat(); 
		public CathodeFloat _47_72_25_4F = new CathodeFloat(); 
		public CathodeFloat _53_CC_1B_7A = new CathodeFloat(); 
		public CathodeFloat _E3_56_23_67 = new CathodeFloat(); 
		public CathodeFloat _28_E9_A4_7C = new CathodeFloat(); 
		public CathodeFloat _22_74_98_9A = new CathodeFloat(); 
		public CathodeFloat _A9_75_2C_CE = new CathodeFloat(); 
		public CathodeFloat despawn = new CathodeFloat(); 
		public CathodeFloat _E3_43_0F_1B = new CathodeFloat(); 
		public CathodeFloat _83_D3_E7_25 = new CathodeFloat(); 
		public CathodeFloat _9D_DE_A9_2C = new CathodeFloat(); 
		public CathodeFloat end = new CathodeFloat(); 
		public CathodeFloat _14_6D_4A_7E = new CathodeFloat(); 
		public CathodeFloat _97_31_50_7F = new CathodeFloat(); 
		public CathodeFloat _E1_B5_31_8D = new CathodeFloat(); 
		public CathodeFloat _60_60_C0_9B = new CathodeFloat(); 
		public CathodeFloat _3E_DF_24_B0 = new CathodeFloat(); 
		public CathodeFloat _8B_A7_B3_D3 = new CathodeFloat(); 
		public CathodeFloat _69_BC_66_D6 = new CathodeFloat(); 
		public CathodeFloat _BF_4E_29_00 = new CathodeFloat(); 
		public CathodeFloat _0E_CE_32_02 = new CathodeFloat(); 
		public CathodeFloat _A8_A0_88_03 = new CathodeFloat(); 
		public CathodeFloat _CD_9F_9A_0D = new CathodeFloat(); 
		public CathodeFloat _5E_3A_B8_13 = new CathodeFloat(); 
		public CathodeFloat _14_06_F1_16 = new CathodeFloat(); 
		public CathodeFloat _04_2D_9C_23 = new CathodeFloat(); 
		public CathodeFloat _06_F1_EA_2A = new CathodeFloat(); 
		public CathodeFloat _E9_A0_4E_2D = new CathodeFloat(); 
		public CathodeFloat _C3_55_C0_31 = new CathodeFloat(); 
		public CathodeFloat _B4_80_F0_31 = new CathodeFloat(); 
		public CathodeFloat _ED_9C_13_49 = new CathodeFloat(); 
		public CathodeFloat _E9_F8_A9_4C = new CathodeFloat(); 
		public CathodeFloat _16_5A_1A_52 = new CathodeFloat(); 
		public CathodeFloat _8A_42_49_57 = new CathodeFloat(); 
		public CathodeFloat _F8_7F_97_5E = new CathodeFloat(); 
		public CathodeFloat _A8_78_93_6F = new CathodeFloat(); 
		public CathodeFloat _F9_27_DB_9F = new CathodeFloat(); 
		public CathodeFloat _95_B9_74_A0 = new CathodeFloat(); 
		public CathodeFloat _77_E0_E5_A7 = new CathodeFloat(); 
		public CathodeFloat _4B_02_93_B3 = new CathodeFloat(); 
		public CathodeFloat _EE_CB_81_B5 = new CathodeFloat(); 
		public CathodeFloat _26_73_F1_B7 = new CathodeFloat(); 
		public CathodeFloat _2A_4F_41_B8 = new CathodeFloat(); 
		public CathodeFloat _BC_59_1D_B9 = new CathodeFloat(); 
		public CathodeFloat _05_89_6B_BA = new CathodeFloat(); 
		public CathodeFloat _EF_D5_90_BB = new CathodeFloat(); 
		public CathodeFloat _E1_35_77_CC = new CathodeFloat(); 
		public CathodeFloat _A7_E7_AD_E5 = new CathodeFloat(); 
		public CathodeFloat _76_F6_02_FB = new CathodeFloat(); 
		public CathodeFloat _B3_DE_43_01 = new CathodeFloat(); 
		public CathodeFloat _D0_13_0E_17 = new CathodeFloat(); 
		public CathodeFloat _D9_4C_4E_5B = new CathodeFloat(); 
		public CathodeFloat _BD_A9_89_69 = new CathodeFloat(); 
		public CathodeFloat _4D_B1_29_8D = new CathodeFloat(); 
		public CathodeFloat _63_6C_36_8F = new CathodeFloat(); 
		public CathodeFloat _17_3C_6E_CB = new CathodeFloat(); 
		public CathodeFloat _B7_13_AD_F0 = new CathodeFloat(); 
		public CathodeFloat _8D_DF_75_F7 = new CathodeFloat(); 
		public CathodeFloat _BF_52_C5_F8 = new CathodeFloat(); 
		public CathodeFloat _BF_21_00_04 = new CathodeFloat(); 
		public CathodeFloat _23_83_97_1E = new CathodeFloat(); 
		public CathodeFloat _29_AA_4C_1F = new CathodeFloat(); 
		public CathodeFloat _63_99_DC_25 = new CathodeFloat(); 
		public CathodeFloat _DA_56_48_2F = new CathodeFloat(); 
		public CathodeFloat _91_AC_7E_37 = new CathodeFloat(); 
		public CathodeFloat _50_A7_25_54 = new CathodeFloat(); 
		public CathodeFloat _41_3E_69_5E = new CathodeFloat(); 
		public CathodeFloat _47_0D_77_70 = new CathodeFloat(); 
		public CathodeFloat _B8_6E_11_A1 = new CathodeFloat(); 
		public CathodeFloat _74_75_57_A5 = new CathodeFloat(); 
		public CathodeFloat _36_09_13_BB = new CathodeFloat(); 
		public CathodeFloat _F3_E4_F2_F6 = new CathodeFloat(); 
		public CathodeFloat _31_6A_F5_FE = new CathodeFloat(); 
		public CathodeFloat _1E_A6_2E_12 = new CathodeFloat(); 
		public CathodeFloat LIGHTS_ON = new CathodeFloat(); 
		public CathodeFloat _53_44_59_42 = new CathodeFloat(); 
		public CathodeFloat _74_52_46_45 = new CathodeFloat(); 
		public CathodeFloat _7F_77_84_69 = new CathodeFloat(); 
		public CathodeFloat EXPLOSION = new CathodeFloat(); 
		public CathodeFloat _AE_96_DB_9A = new CathodeFloat(); 
		public CathodeFloat _30_33_39_DB = new CathodeFloat(); 
		public CathodeFloat _66_6E_5A_DE = new CathodeFloat(); 
		public CathodeFloat _BB_20_F0_EB = new CathodeFloat(); 
		public CathodeFloat _7E_00_CF_24 = new CathodeFloat(); 
		public CathodeFloat _DA_C7_04_47 = new CathodeFloat(); 
		public CathodeFloat _49_8B_EA_1A = new CathodeFloat(); 
		public CathodeFloat _35_30_E4_3B = new CathodeFloat(); 
		public CathodeFloat _F9_6A_0D_5D = new CathodeFloat(); 
		public CathodeFloat _DA_84_89_66 = new CathodeFloat(); 
		public CathodeFloat _E5_1C_E6_8D = new CathodeFloat(); 
		public CathodeFloat _C8_2E_21_96 = new CathodeFloat(); 
		public CathodeFloat _40_D5_B6_A4 = new CathodeFloat(); 
		public CathodeFloat _0A_0F_23_D0 = new CathodeFloat(); 
		public CathodeFloat _0B_FD_77_D3 = new CathodeFloat(); 
		public CathodeFloat _5E_F0_8B_D6 = new CathodeFloat(); 
		public CathodeFloat _AE_19_B8_E2 = new CathodeFloat(); 
		public CathodeFloat _F1_40_05_EC = new CathodeFloat(); 
		public CathodeFloat _05_E6_35_F2 = new CathodeFloat(); 
		public CathodeFloat _7D_08_9D_50 = new CathodeFloat(); 
		public CathodeFloat _50_2D_CD_3C = new CathodeFloat(); 
		public CathodeFloat _02_E9_DA_75 = new CathodeFloat(); 
		public CathodeFloat _E2_10_93_1F = new CathodeFloat(); 
		public CathodeFloat _3C_DA_A8_20 = new CathodeFloat(); 
		public CathodeFloat _CE_D9_1F_2D = new CathodeFloat(); 
		public CathodeFloat _A8_1D_9D_41 = new CathodeFloat(); 
		public CathodeFloat _44_31_F4_44 = new CathodeFloat(); 
		public CathodeFloat _7E_C8_0E_45 = new CathodeFloat(); 
		public CathodeFloat _75_2C_4B_53 = new CathodeFloat(); 
		public CathodeFloat _F3_46_94_59 = new CathodeFloat(); 
		public CathodeFloat _93_CD_AA_61 = new CathodeFloat(); 
		public CathodeFloat _FA_0A_CB_6A = new CathodeFloat(); 
		public CathodeFloat _EF_1F_11_6B = new CathodeFloat(); 
		public CathodeFloat _6D_81_B1_71 = new CathodeFloat(); 
		public CathodeFloat _DC_72_0B_77 = new CathodeFloat(); 
		public CathodeFloat _F8_2F_97_7A = new CathodeFloat(); 
		public CathodeFloat _5A_6E_B1_8D = new CathodeFloat(); 
		public CathodeFloat _40_8C_27_9D = new CathodeFloat(); 
		public CathodeFloat _4A_8E_2E_A2 = new CathodeFloat(); 
		public CathodeFloat _7D_F3_14_C2 = new CathodeFloat(); 
		public CathodeFloat _FE_09_99_FB = new CathodeFloat(); 
		public CathodeFloat _68_38_08_FF = new CathodeFloat(); 
		public CathodeFloat _E3_50_1A_30 = new CathodeFloat(); 
		public CathodeFloat _B1_93_4E_31 = new CathodeFloat(); 
		public CathodeFloat force_open = new CathodeFloat(); 
		public CathodeFloat _B9_0F_64_51 = new CathodeFloat(); 
		public CathodeFloat _F0_A7_CB_54 = new CathodeFloat(); 
		public CathodeFloat _9F_1E_6A_5D = new CathodeFloat(); 
		public CathodeFloat _D7_17_69_5F = new CathodeFloat(); 
		public CathodeFloat _CA_B5_7D_7C = new CathodeFloat(); 
		public CathodeFloat _28_74_DD_96 = new CathodeFloat(); 
		public CathodeFloat _1B_EB_6C_98 = new CathodeFloat(); 
		public CathodeFloat _4A_07_17_A1 = new CathodeFloat(); 
		public CathodeFloat _FF_F8_EA_C3 = new CathodeFloat(); 
		public CathodeFloat _7C_CE_B3_C7 = new CathodeFloat(); 
		public CathodeFloat _C8_1D_4A_D6 = new CathodeFloat(); 
		public CathodeFloat _55_A6_49_E9 = new CathodeFloat(); 
		public CathodeFloat _71_15_87_F6 = new CathodeFloat(); 
		public CathodeFloat _60_EF_45_0F = new CathodeFloat(); 
		public CathodeFloat _2C_18_2C_17 = new CathodeFloat(); 
		public CathodeFloat _8A_FF_61_21 = new CathodeFloat(); 
		public CathodeFloat _CC_BC_6C_25 = new CathodeFloat(); 
		public CathodeFloat _4E_2E_17_2E = new CathodeFloat(); 
		public CathodeFloat _F9_68_5D_30 = new CathodeFloat(); 
		public CathodeFloat _83_53_A4_33 = new CathodeFloat(); 
		public CathodeFloat _DE_10_28_36 = new CathodeFloat(); 
		public CathodeFloat _EB_B1_95_36 = new CathodeFloat(); 
		public CathodeFloat _4C_77_15_3F = new CathodeFloat(); 
		public CathodeFloat _EF_5D_0A_44 = new CathodeFloat(); 
		public CathodeFloat _3D_25_BF_4B = new CathodeFloat(); 
		public CathodeFloat _97_15_49_4C = new CathodeFloat(); 
		public CathodeFloat _E7_87_7F_60 = new CathodeFloat(); 
		public CathodeFloat _19_84_AA_68 = new CathodeFloat(); 
		public CathodeFloat _4D_39_03_71 = new CathodeFloat(); 
		public CathodeFloat _05_5A_8D_7A = new CathodeFloat(); 
		public CathodeFloat _9E_93_F6_7C = new CathodeFloat(); 
		public CathodeFloat _83_E6_21_7D = new CathodeFloat(); 
		public CathodeFloat _96_1E_35_86 = new CathodeFloat(); 
		public CathodeFloat _84_91_53_86 = new CathodeFloat(); 
		public CathodeFloat _F3_2B_EC_90 = new CathodeFloat(); 
		public CathodeFloat _C5_8A_2B_96 = new CathodeFloat(); 
		public CathodeFloat _2D_F2_C9_A1 = new CathodeFloat(); 
		public CathodeFloat _B5_16_EE_A1 = new CathodeFloat(); 
		public CathodeFloat _E4_E9_97_A3 = new CathodeFloat(); 
		public CathodeFloat _FA_EC_A6_A4 = new CathodeFloat(); 
		public CathodeFloat _99_03_74_B3 = new CathodeFloat(); 
		public CathodeFloat _D7_63_F0_B8 = new CathodeFloat(); 
		public CathodeFloat _9E_C7_99_B9 = new CathodeFloat(); 
		public CathodeFloat _FF_02_E0_C1 = new CathodeFloat(); 
		public CathodeFloat _26_94_7D_CB = new CathodeFloat(); 
		public CathodeFloat _DD_86_DB_DC = new CathodeFloat(); 
		public CathodeFloat _45_3D_9E_DE = new CathodeFloat(); 
		public CathodeFloat _A7_C7_FA_E1 = new CathodeFloat(); 
		public CathodeFloat _97_C0_A3_E3 = new CathodeFloat(); 
		public CathodeFloat _E8_CD_12_F9 = new CathodeFloat(); 
		public CathodeFloat _C0_C2_83_FC = new CathodeFloat(); 
		public CathodeFloat _64_4C_41_FD = new CathodeFloat(); 
		public CathodeFloat _20_64_16_FE = new CathodeFloat(); 
		public CathodeFloat _DC_CD_48_10 = new CathodeFloat(); 
		public CathodeFloat _5E_E2_55_1F = new CathodeFloat(); 
		public CathodeFloat _C4_56_94_21 = new CathodeFloat(); 
		public CathodeFloat _40_F8_3D_2A = new CathodeFloat(); 
		public CathodeFloat _2D_F3_C9_2D = new CathodeFloat(); 
		public CathodeFloat _2A_BE_ED_38 = new CathodeFloat(); 
		public CathodeFloat _A4_EC_0B_45 = new CathodeFloat(); 
		public CathodeFloat _E5_EF_66_58 = new CathodeFloat(); 
		public CathodeFloat _71_F8_FE_59 = new CathodeFloat(); 
		public CathodeFloat _E6_81_0E_64 = new CathodeFloat(); 
		public CathodeFloat _FE_B0_5E_66 = new CathodeFloat(); 
		public CathodeFloat _8E_0B_91_66 = new CathodeFloat(); 
		public CathodeFloat _9B_A1_BB_6B = new CathodeFloat(); 
		public CathodeFloat _32_56_F9_71 = new CathodeFloat(); 
		public CathodeFloat _51_ED_3A_78 = new CathodeFloat(); 
		public CathodeFloat _3A_D2_09_7D = new CathodeFloat(); 
		public CathodeFloat _9D_00_DA_93 = new CathodeFloat(); 
		public CathodeFloat _D4_E5_59_A5 = new CathodeFloat(); 
		public CathodeFloat _10_FA_0F_A8 = new CathodeFloat(); 
		public CathodeFloat _94_21_41_AD = new CathodeFloat(); 
		public CathodeFloat _96_52_1E_B2 = new CathodeFloat(); 
		public CathodeFloat _99_05_B7_D6 = new CathodeFloat(); 
		public CathodeFloat _87_3A_5E_D9 = new CathodeFloat(); 
		public CathodeFloat _BA_A8_CD_DB = new CathodeFloat(); 
		public CathodeFloat _C5_8C_26_F5 = new CathodeFloat(); 
		public CathodeFloat _B8_AC_FC_FE = new CathodeFloat(); 
		public CathodeFloat _89_6A_7C_23 = new CathodeFloat(); 
		public CathodeFloat _8F_D2_90_3B = new CathodeFloat(); 
		public CathodeFloat _81_9B_21_A9 = new CathodeFloat(); 
		public CathodeFloat _1F_D2_1D_B6 = new CathodeFloat(); 
		public CathodeFloat _89_6D_FA_D7 = new CathodeFloat(); 
		public CathodeFloat _46_78_91_F4 = new CathodeFloat(); 
		public CathodeFloat _B1_D4_B2_09 = new CathodeFloat(); 
		public CathodeFloat _67_00_27_79 = new CathodeFloat(); 
		public CathodeFloat _5A_70_C7_38 = new CathodeFloat(); 
		public CathodeFloat _EB_3D_B8_4E = new CathodeFloat(); 
		public CathodeFloat _F9_63_09_D5 = new CathodeFloat(); 
		public CathodeFloat _F2_48_B3_FC = new CathodeFloat(); 
		public CathodeFloat _91_A7_6A_57 = new CathodeFloat(); 
		public CathodeFloat _33_18_25_E3 = new CathodeFloat(); 
		public CathodeFloat _75_EA_86_06 = new CathodeFloat(); 
		public CathodeFloat _3D_16_BE_02 = new CathodeFloat(); 
		public CathodeFloat _04_8C_6B_23 = new CathodeFloat(); 
		public CathodeFloat _9D_5A_97_67 = new CathodeFloat(); 
		public CathodeFloat _95_00_09_86 = new CathodeFloat(); 
		public CathodeFloat _5E_A7_A2_89 = new CathodeFloat(); 
		public CathodeFloat _97_02_F8_AB = new CathodeFloat(); 
		public CathodeFloat _03_54_0B_EB = new CathodeFloat(); 
		public CathodeFloat _1D_45_68_F3 = new CathodeFloat(); 
		public CathodeFloat _72_53_AF_40 = new CathodeFloat(); 
		public CathodeFloat _A2_D5_A3_63 = new CathodeFloat(); 
		public CathodeFloat _AB_B3_96_71 = new CathodeFloat(); 
		public CathodeFloat _DD_5B_FC_9B = new CathodeFloat(); 
		public CathodeFloat _3E_42_56_B1 = new CathodeFloat(); 
		public CathodeFloat _69_C0_B4_BE = new CathodeFloat(); 
		public CathodeFloat _F1_32_39_2B = new CathodeFloat(); 
		public CathodeFloat _76_FF_C3_32 = new CathodeFloat(); 
		public CathodeFloat _C3_15_85_35 = new CathodeFloat(); 
		public CathodeFloat _57_28_EE_35 = new CathodeFloat(); 
		public CathodeFloat _0F_FE_51_36 = new CathodeFloat(); 
		public CathodeFloat _69_64_F6_41 = new CathodeFloat(); 
		public CathodeFloat _DE_19_23_5A = new CathodeFloat(); 
		public CathodeFloat _B1_CA_AB_62 = new CathodeFloat(); 
		public CathodeFloat _14_D7_21_78 = new CathodeFloat(); 
		public CathodeFloat _9C_A9_AB_7B = new CathodeFloat(); 
		public CathodeFloat _A2_97_C3_97 = new CathodeFloat(); 
		public CathodeFloat _63_C5_E6_99 = new CathodeFloat(); 
		public CathodeFloat _E7_C3_C2_A7 = new CathodeFloat(); 
		public CathodeFloat _E9_FF_E2_AD = new CathodeFloat(); 
		public CathodeFloat _B9_01_01_F0 = new CathodeFloat(); 
		public CathodeFloat _3B_A2_FF_FF = new CathodeFloat(); 
		public CathodeFloat _AE_8B_F5_0A = new CathodeFloat(); 
		public CathodeFloat _50_45_83_0D = new CathodeFloat(); 
		public CathodeFloat _73_5D_F3_16 = new CathodeFloat(); 
		public CathodeFloat _05_F1_8E_2B = new CathodeFloat(); 
		public CathodeFloat _52_52_AC_2B = new CathodeFloat(); 
		public CathodeFloat _34_3A_AF_3E = new CathodeFloat(); 
		public CathodeFloat _84_F3_09_70 = new CathodeFloat(); 
		public CathodeFloat _AD_92_2A_86 = new CathodeFloat(); 
		public CathodeFloat _61_87_40_AB = new CathodeFloat(); 
		public CathodeFloat _F9_89_6C_B5 = new CathodeFloat(); 
		public CathodeFloat _1C_29_20_B8 = new CathodeFloat(); 
		public CathodeFloat _7C_94_A7_B8 = new CathodeFloat(); 
		public CathodeFloat _C6_30_CD_F1 = new CathodeFloat(); 
		public CathodeFloat _2C_2C_16_FC = new CathodeFloat(); 
		public CathodeFloat _B3_4B_7A_03 = new CathodeFloat(); 
		public CathodeFloat _CA_DE_57_08 = new CathodeFloat(); 
		public CathodeFloat _35_E3_B9_08 = new CathodeFloat(); 
		public CathodeFloat _61_C3_C7_09 = new CathodeFloat(); 
		public CathodeFloat _6B_ED_2B_12 = new CathodeFloat(); 
		public CathodeFloat _57_28_D6_1C = new CathodeFloat(); 
		public CathodeFloat _F9_05_44_21 = new CathodeFloat(); 
		public CathodeFloat _4C_60_A7_25 = new CathodeFloat(); 
		public CathodeFloat _44_BF_C5_27 = new CathodeFloat(); 
		public CathodeFloat _DF_10_E4_28 = new CathodeFloat(); 
		public CathodeFloat _10_09_78_29 = new CathodeFloat(); 
		public CathodeFloat _CB_87_7E_2B = new CathodeFloat(); 
		public CathodeFloat _B2_54_96_2B = new CathodeFloat(); 
		public CathodeFloat _4E_DA_9D_2E = new CathodeFloat(); 
		public CathodeFloat _B5_9D_2A_2F = new CathodeFloat(); 
		public CathodeFloat _52_0C_E1_32 = new CathodeFloat(); 
		public CathodeFloat _18_B1_6D_33 = new CathodeFloat(); 
		public CathodeFloat _F2_7D_1B_39 = new CathodeFloat(); 
		public CathodeFloat _5A_D4_23_3A = new CathodeFloat(); 
		public CathodeFloat _AB_09_9C_43 = new CathodeFloat(); 
		public CathodeFloat _65_8E_65_44 = new CathodeFloat(); 
		public CathodeFloat _F5_0B_31_45 = new CathodeFloat(); 
		public CathodeFloat _58_D8_D9_47 = new CathodeFloat(); 
		public CathodeFloat _95_72_AC_48 = new CathodeFloat(); 
		public CathodeFloat _B8_E1_C8_49 = new CathodeFloat(); 
		public CathodeFloat _E1_29_1F_4C = new CathodeFloat(); 
		public CathodeFloat _DD_D7_C0_4C = new CathodeFloat(); 
		public CathodeFloat _0E_5A_7C_4D = new CathodeFloat(); 
		public CathodeFloat _6D_1F_A0_4F = new CathodeFloat(); 
		public CathodeFloat _39_13_A3_51 = new CathodeFloat(); 
		public CathodeFloat _F8_DA_DF_56 = new CathodeFloat(); 
		public CathodeFloat _33_10_4A_5A = new CathodeFloat(); 
		public CathodeFloat _98_B9_42_5B = new CathodeFloat(); 
		public CathodeFloat _33_35_FD_5C = new CathodeFloat(); 
		public CathodeFloat _1C_27_3A_5F = new CathodeFloat(); 
		public CathodeFloat _A7_3D_13_60 = new CathodeFloat(); 
		public CathodeFloat _B3_C5_6B_62 = new CathodeFloat(); 
		public CathodeFloat _68_9E_52_67 = new CathodeFloat(); 
		public CathodeFloat _CD_E8_C8_69 = new CathodeFloat(); 
		public CathodeFloat _77_A6_4B_6E = new CathodeFloat(); 
		public CathodeFloat _B0_0D_0A_77 = new CathodeFloat(); 
		public CathodeFloat _E2_69_80_77 = new CathodeFloat(); 
		public CathodeFloat _5B_0F_32_79 = new CathodeFloat(); 
		public CathodeFloat _EE_EC_6C_7B = new CathodeFloat(); 
		public CathodeFloat _C3_6F_63_7C = new CathodeFloat(); 
		public CathodeFloat _89_0B_73_7E = new CathodeFloat(); 
		public CathodeFloat _37_08_7B_7F = new CathodeFloat(); 
		public CathodeFloat _3A_2C_2C_81 = new CathodeFloat(); 
		public CathodeFloat _0A_29_4F_86 = new CathodeFloat(); 
		public CathodeFloat _F4_A4_0B_87 = new CathodeFloat(); 
		public CathodeFloat _39_C2_D7_8C = new CathodeFloat(); 
		public CathodeFloat _4A_BF_D8_8D = new CathodeFloat(); 
		public CathodeFloat _4D_49_4F_92 = new CathodeFloat(); 
		public CathodeFloat _53_58_1E_99 = new CathodeFloat(); 
		public CathodeFloat _B5_71_B1_9C = new CathodeFloat(); 
		public CathodeFloat _F7_3E_2A_A1 = new CathodeFloat(); 
		public CathodeFloat _AA_A1_6E_A1 = new CathodeFloat(); 
		public CathodeFloat _14_6F_0D_A4 = new CathodeFloat(); 
		public CathodeFloat _14_81_67_A4 = new CathodeFloat(); 
		public CathodeFloat _52_56_BB_AA = new CathodeFloat(); 
		public CathodeFloat _E2_85_11_AB = new CathodeFloat(); 
		public CathodeFloat _A2_E1_FC_AE = new CathodeFloat(); 
		public CathodeFloat _BA_C4_50_B2 = new CathodeFloat(); 
		public CathodeFloat _6E_C8_A7_B2 = new CathodeFloat(); 
		public CathodeFloat _81_9E_C2_B3 = new CathodeFloat(); 
		public CathodeFloat _B8_4B_7A_B9 = new CathodeFloat(); 
		public CathodeFloat _B6_30_CC_BA = new CathodeFloat(); 
		public CathodeFloat _57_8D_DD_BB = new CathodeFloat(); 
		public CathodeFloat _56_FE_02_BC = new CathodeFloat(); 
		public CathodeFloat _A4_D1_C1_BC = new CathodeFloat(); 
		public CathodeFloat _3D_CB_32_BF = new CathodeFloat(); 
		public CathodeFloat _87_11_9E_BF = new CathodeFloat(); 
		public CathodeFloat _CD_F0_F7_C3 = new CathodeFloat(); 
		public CathodeFloat _81_B1_6D_D3 = new CathodeFloat(); 
		public CathodeFloat _9A_0F_3F_D4 = new CathodeFloat(); 
		public CathodeFloat _27_00_17_D9 = new CathodeFloat(); 
		public CathodeFloat _C9_53_94_DE = new CathodeFloat(); 
		public CathodeFloat _D7_F4_0D_E0 = new CathodeFloat(); 
		public CathodeFloat _08_A4_BB_E0 = new CathodeFloat(); 
		public CathodeFloat _86_0C_7E_E8 = new CathodeFloat(); 
		public CathodeFloat _06_59_1D_E9 = new CathodeFloat(); 
		public CathodeFloat _1F_2B_E8_F4 = new CathodeFloat(); 
		public CathodeFloat _DB_E6_72_F8 = new CathodeFloat(); 
		public CathodeFloat _CA_E9_A2_F9 = new CathodeFloat(); 
		public CathodeFloat _B3_2C_2A_FB = new CathodeFloat(); 
		public CathodeFloat _45_A1_34_FF = new CathodeFloat(); 
		public CathodeFloat _F9_DA_02_0F = new CathodeFloat(); 
		public CathodeFloat _AC_EE_54_22 = new CathodeFloat(); 
		public CathodeFloat _73_46_CE_51 = new CathodeFloat(); 
		public CathodeFloat _97_47_D9_88 = new CathodeFloat(); 
		public CathodeFloat _96_A7_DD_98 = new CathodeFloat(); 
		public CathodeFloat _71_4D_0E_A1 = new CathodeFloat(); 
		public CathodeFloat _E4_C7_B4_BB = new CathodeFloat(); 
		public CathodeFloat _F3_2E_F4_D7 = new CathodeFloat(); 
		public CathodeFloat _94_B8_D0_03 = new CathodeFloat(); 
		public CathodeFloat _65_03_8A_08 = new CathodeFloat(); 
		public CathodeFloat _A0_60_10_0E = new CathodeFloat(); 
		public CathodeFloat _2F_30_84_11 = new CathodeFloat(); 
		public CathodeFloat _D4_74_6E_2F = new CathodeFloat(); 
		public CathodeFloat _6F_7E_3E_31 = new CathodeFloat(); 
		public CathodeFloat _72_36_1B_3C = new CathodeFloat(); 
		public CathodeFloat _2F_0B_30_43 = new CathodeFloat(); 
		public CathodeFloat _51_83_B1_63 = new CathodeFloat(); 
		public CathodeFloat _B2_DD_EB_70 = new CathodeFloat(); 
		public CathodeFloat _1B_01_54_95 = new CathodeFloat(); 
		public CathodeFloat _FF_DE_00_96 = new CathodeFloat(); 
		public CathodeFloat _EB_B9_40_A2 = new CathodeFloat(); 
		public CathodeFloat _FC_21_36_AE = new CathodeFloat(); 
		public CathodeFloat _00_43_12_BA = new CathodeFloat(); 
		public CathodeFloat _2E_5A_27_BA = new CathodeFloat(); 
		public CathodeFloat _B6_F7_F3_BD = new CathodeFloat(); 
		public CathodeFloat _96_5B_FF_BF = new CathodeFloat(); 
		public CathodeFloat _28_FE_A2_C7 = new CathodeFloat(); 
		public CathodeFloat _16_B5_1E_D6 = new CathodeFloat(); 
		public CathodeFloat _7C_54_42_D9 = new CathodeFloat(); 
		public CathodeFloat _B3_FA_59_FB = new CathodeFloat(); 
		public CathodeFloat _1F_28_47_02 = new CathodeFloat(); 
		public CathodeFloat _A0_67_C4_2E = new CathodeFloat(); 
		public CathodeFloat _55_E8_F0_4D = new CathodeFloat(); 
		public CathodeFloat _DC_0A_35_5C = new CathodeFloat(); 
		public CathodeFloat _93_90_03_64 = new CathodeFloat(); 
		public CathodeFloat _6A_DF_58_83 = new CathodeFloat(); 
		public CathodeFloat _6C_4A_00_03 = new CathodeFloat(); 
		public CathodeFloat _06_15_6C_06 = new CathodeFloat(); 
		public CathodeFloat _48_DF_C8_07 = new CathodeFloat(); 
		public CathodeFloat _20_B0_5A_0B = new CathodeFloat(); 
		public CathodeFloat _FC_29_E3_12 = new CathodeFloat(); 
		public CathodeFloat _EA_56_5F_1A = new CathodeFloat(); 
		public CathodeFloat _7B_6A_4F_2A = new CathodeFloat(); 
		public CathodeFloat _B9_E8_99_37 = new CathodeFloat(); 
		public CathodeFloat _EC_1B_65_3E = new CathodeFloat(); 
		public CathodeFloat _33_00_AD_42 = new CathodeFloat(); 
		public CathodeFloat _AB_26_E9_42 = new CathodeFloat(); 
		public CathodeFloat _E8_F0_3F_44 = new CathodeFloat(); 
		public CathodeFloat _ED_92_3A_4D = new CathodeFloat(); 
		public CathodeFloat _81_69_F8_4E = new CathodeFloat(); 
		public CathodeFloat _F0_C5_23_52 = new CathodeFloat(); 
		public CathodeFloat _02_94_DE_5A = new CathodeFloat(); 
		public CathodeFloat _C8_DD_18_5D = new CathodeFloat(); 
		public CathodeFloat _3B_BF_DD_62 = new CathodeFloat(); 
		public CathodeFloat _65_42_F6_64 = new CathodeFloat(); 
		public CathodeFloat _B4_F1_38_6A = new CathodeFloat(); 
		public CathodeFloat _1B_AA_A2_8B = new CathodeFloat(); 
		public CathodeFloat _C0_BE_F8_8D = new CathodeFloat(); 
		public CathodeFloat _24_08_7A_92 = new CathodeFloat(); 
		public CathodeFloat _78_02_95_98 = new CathodeFloat(); 
		public CathodeFloat _38_14_C8_98 = new CathodeFloat(); 
		public CathodeFloat _35_D5_FB_99 = new CathodeFloat(); 
		public CathodeFloat _2A_9B_80_9A = new CathodeFloat(); 
		public CathodeFloat _FD_86_A6_AB = new CathodeFloat(); 
		public CathodeFloat _0B_DB_91_AD = new CathodeFloat(); 
		public CathodeFloat _AD_0C_5B_B3 = new CathodeFloat(); 
		public CathodeFloat _49_88_58_B4 = new CathodeFloat(); 
		public CathodeFloat _F2_AC_2C_B5 = new CathodeFloat(); 
		public CathodeFloat _20_E2_09_BA = new CathodeFloat(); 
		public CathodeFloat _9E_3D_D7_BC = new CathodeFloat(); 
		public CathodeFloat _33_1E_00_C0 = new CathodeFloat(); 
		public CathodeFloat _8F_0E_60_C0 = new CathodeFloat(); 
		public CathodeFloat _5C_EE_7F_C6 = new CathodeFloat(); 
		public CathodeFloat _75_E6_9B_C8 = new CathodeFloat(); 
		public CathodeFloat _88_1A_AA_CA = new CathodeFloat(); 
		public CathodeFloat _99_4F_4D_D1 = new CathodeFloat(); 
		public CathodeFloat _C9_98_7C_D1 = new CathodeFloat(); 
		public CathodeFloat _E5_FD_49_D3 = new CathodeFloat(); 
		public CathodeFloat _E0_C4_66_E2 = new CathodeFloat(); 
		public CathodeFloat _C9_2E_42_E5 = new CathodeFloat(); 
		public CathodeFloat _6B_B4_D7_E6 = new CathodeFloat(); 
		public CathodeFloat _75_3A_4B_EB = new CathodeFloat(); 
		public CathodeFloat _50_40_EA_EC = new CathodeFloat(); 
		public CathodeFloat _7B_82_8B_EE = new CathodeFloat(); 
		public CathodeFloat _4C_6F_C6_F0 = new CathodeFloat(); 
		public CathodeFloat _1A_35_3B_F4 = new CathodeFloat(); 
		public CathodeFloat _A7_FF_D4_F5 = new CathodeFloat(); 
		public CathodeFloat _6B_CD_95_FE = new CathodeFloat(); 
		public CathodeFloat _96_CE_0C_08 = new CathodeFloat(); 
		public CathodeFloat _90_BD_A7_0E = new CathodeFloat(); 
		public CathodeFloat _25_18_A0_1D = new CathodeFloat(); 
		public CathodeFloat _E4_19_A4_1E = new CathodeFloat(); 
		public CathodeFloat _02_75_FC_20 = new CathodeFloat(); 
		public CathodeFloat _CE_CB_2D_27 = new CathodeFloat(); 
		public CathodeFloat _47_8F_B4_32 = new CathodeFloat(); 
		public CathodeFloat _F9_30_04_45 = new CathodeFloat(); 
		public CathodeFloat _70_02_CE_51 = new CathodeFloat(); 
		public CathodeFloat _71_73_A9_59 = new CathodeFloat(); 
		public CathodeFloat _D7_55_F7_63 = new CathodeFloat(); 
		public CathodeFloat _7E_26_E6_65 = new CathodeFloat(); 
		public CathodeFloat _0E_54_E1_75 = new CathodeFloat(); 
		public CathodeFloat _00_FB_89_82 = new CathodeFloat(); 
		public CathodeFloat _F6_C1_BE_93 = new CathodeFloat(); 
		public CathodeFloat _32_D7_BA_97 = new CathodeFloat(); 
		public CathodeFloat _F3_F7_5B_A9 = new CathodeFloat(); 
		public CathodeFloat _93_44_76_B7 = new CathodeFloat(); 
		public CathodeFloat _95_28_8F_C0 = new CathodeFloat(); 
		public CathodeFloat _61_1A_13_DE = new CathodeFloat(); 
		public CathodeFloat _48_1F_96_E0 = new CathodeFloat(); 
		public CathodeFloat _34_78_B6_F3 = new CathodeFloat(); 
		public CathodeFloat _C0_65_E5_F4 = new CathodeFloat(); 
		public CathodeFloat _F2_DF_2C_F8 = new CathodeFloat(); 
		public CathodeFloat _62_72_E3_04 = new CathodeFloat(); 
		public CathodeFloat _A3_40_DD_27 = new CathodeFloat(); 
		public CathodeFloat _1E_09_A3_43 = new CathodeFloat(); 
		public CathodeFloat _51_F2_DF_95 = new CathodeFloat(); 
		public CathodeFloat _5A_01_79_9C = new CathodeFloat(); 
		public CathodeFloat _20_73_BB_D1 = new CathodeFloat(); 
		public CathodeFloat _3F_5B_FF_E2 = new CathodeFloat(); 
		public CathodeFloat _DB_48_E1_01 = new CathodeFloat(); 
		public CathodeFloat _01_BD_FB_09 = new CathodeFloat(); 
		public CathodeFloat _81_97_FA_0E = new CathodeFloat(); 
		public CathodeFloat _6A_27_33_22 = new CathodeFloat(); 
		public CathodeFloat _B2_4C_39_2D = new CathodeFloat(); 
		public CathodeFloat _5F_1E_DC_2D = new CathodeFloat(); 
		public CathodeFloat _3A_20_7F_32 = new CathodeFloat(); 
		public CathodeFloat _31_6A_D4_34 = new CathodeFloat(); 
		public CathodeFloat _25_D5_0C_37 = new CathodeFloat(); 
		public CathodeFloat _63_0E_47_38 = new CathodeFloat(); 
		public CathodeFloat _09_0D_8F_3B = new CathodeFloat(); 
		public CathodeFloat _D8_DF_53_41 = new CathodeFloat(); 
		public CathodeFloat _68_9D_79_47 = new CathodeFloat(); 
		public CathodeFloat _F5_59_4B_4D = new CathodeFloat(); 
		public CathodeFloat _42_7E_A6_4F = new CathodeFloat(); 
		public CathodeFloat _5C_33_C5_54 = new CathodeFloat(); 
		public CathodeFloat _92_A4_48_57 = new CathodeFloat(); 
		public CathodeFloat _AA_89_1F_58 = new CathodeFloat(); 
		public CathodeFloat _BB_14_79_58 = new CathodeFloat(); 
		public CathodeFloat _13_05_98_61 = new CathodeFloat(); 
		public CathodeFloat _28_3E_C7_64 = new CathodeFloat(); 
		public CathodeFloat _8E_99_D5_6C = new CathodeFloat(); 
		public CathodeFloat _5D_81_E4_71 = new CathodeFloat(); 
		public CathodeFloat _8E_A7_03_8C = new CathodeFloat(); 
		public CathodeFloat _E5_AD_13_91 = new CathodeFloat(); 
		public CathodeFloat _C0_39_00_98 = new CathodeFloat(); 
		public CathodeFloat _05_69_92_99 = new CathodeFloat(); 
		public CathodeFloat _38_B3_0A_9C = new CathodeFloat(); 
		public CathodeFloat _57_C2_B1_A2 = new CathodeFloat(); 
		public CathodeFloat _73_57_26_AB = new CathodeFloat(); 
		public CathodeFloat _0D_8F_AC_B1 = new CathodeFloat(); 
		public CathodeFloat _3F_BA_F4_B1 = new CathodeFloat(); 
		public CathodeFloat _1E_D3_FA_BD = new CathodeFloat(); 
		public CathodeFloat _83_A8_25_C3 = new CathodeFloat(); 
		public CathodeFloat _4B_46_E7_C5 = new CathodeFloat(); 
		public CathodeFloat _4C_84_EB_C5 = new CathodeFloat(); 
		public CathodeFloat _68_A8_54_C8 = new CathodeFloat(); 
		public CathodeFloat _59_1B_99_C9 = new CathodeFloat(); 
		public CathodeFloat _ED_26_35_CA = new CathodeFloat(); 
		public CathodeFloat _47_8E_87_D4 = new CathodeFloat(); 
		public CathodeFloat _7E_CF_68_D5 = new CathodeFloat(); 
		public CathodeFloat _E7_1F_E5_D8 = new CathodeFloat(); 
		public CathodeFloat _84_25_0A_D9 = new CathodeFloat(); 
		public CathodeFloat _15_2B_E2_D9 = new CathodeFloat(); 
		public CathodeFloat _9F_95_A3_E1 = new CathodeFloat(); 
		public CathodeFloat _85_D2_F5_E6 = new CathodeFloat(); 
		public CathodeFloat _43_39_C4_EC = new CathodeFloat(); 
		public CathodeFloat _19_1C_DF_FE = new CathodeFloat(); 
		public CathodeFloat _D4_C5_95_09 = new CathodeFloat(); 
		public CathodeFloat _FF_96_7D_15 = new CathodeFloat(); 
		public CathodeFloat _EB_61_63_23 = new CathodeFloat(); 
		public CathodeFloat _7E_D4_E8_3D = new CathodeFloat(); 
		public CathodeFloat _E0_20_81_6A = new CathodeFloat(); 
		public CathodeFloat _85_E8_6E_98 = new CathodeFloat(); 
		public CathodeFloat _70_19_CE_9B = new CathodeFloat(); 
		public CathodeFloat _E1_BA_D5_AA = new CathodeFloat(); 
		public CathodeFloat _F0_2B_A3_D8 = new CathodeFloat(); 
		public CathodeFloat _84_65_60_E7 = new CathodeFloat(); 
		public CathodeFloat _7F_79_69_2C = new CathodeFloat(); 
		public CathodeFloat _F8_54_5F_34 = new CathodeFloat(); 
		public CathodeFloat _A5_C0_AB_37 = new CathodeFloat(); 
		public CathodeFloat _92_EE_81_44 = new CathodeFloat(); 
		public CathodeFloat _F0_D7_CA_4E = new CathodeFloat(); 
		public CathodeFloat _E3_42_A7_4F = new CathodeFloat(); 
		public CathodeFloat _FC_3B_FA_52 = new CathodeFloat(); 
		public CathodeFloat _DF_4F_88_53 = new CathodeFloat(); 
		public CathodeFloat _D3_FA_DB_5D = new CathodeFloat(); 
		public CathodeFloat _7B_2D_71_61 = new CathodeFloat(); 
		public CathodeFloat _5B_31_F6_61 = new CathodeFloat(); 
		public CathodeFloat _00_69_A2_A0 = new CathodeFloat(); 
		public CathodeFloat _6C_DD_14_B4 = new CathodeFloat(); 
		public CathodeFloat _81_18_9D_CA = new CathodeFloat(); 
		public CathodeFloat _B2_80_D6_F0 = new CathodeFloat(); 
		public CathodeFloat _57_31_B0_FE = new CathodeFloat(); 
		public CathodeFloat _43_83_21_55 = new CathodeFloat(); 
		public CathodeFloat _D0_31_35_6C = new CathodeFloat(); 
		public CathodeFloat _FF_BD_01_AF = new CathodeFloat(); 
		public CathodeFloat _8E_08_31_DE = new CathodeFloat(); 
		public CathodeFloat _EF_C7_52_01 = new CathodeFloat(); 
		public CathodeFloat _B0_11_98_05 = new CathodeFloat(); 
		public CathodeFloat _01_DD_DC_18 = new CathodeFloat(); 
		public CathodeFloat _3A_45_96_1E = new CathodeFloat(); 
		public CathodeFloat _F5_12_98_22 = new CathodeFloat(); 
		public CathodeFloat _E4_3D_3E_2B = new CathodeFloat(); 
		public CathodeFloat stop_benchmark = new CathodeFloat(); 
		public CathodeFloat _74_CA_53_3C = new CathodeFloat(); 
		public CathodeFloat _0F_4F_4F_59 = new CathodeFloat(); 
		public CathodeFloat _9A_AB_D4_5E = new CathodeFloat(); 
		public CathodeFloat _17_6D_D6_76 = new CathodeFloat(); 
		public CathodeFloat _1F_54_16_7B = new CathodeFloat(); 
		public CathodeFloat _F3_BA_11_84 = new CathodeFloat(); 
		public CathodeFloat _45_B7_54_A3 = new CathodeFloat(); 
		public CathodeFloat _3E_8C_F4_B2 = new CathodeFloat(); 
		public CathodeFloat _69_B1_5A_CE = new CathodeFloat(); 
		public CathodeFloat _10_71_5D_CF = new CathodeFloat(); 
		public CathodeFloat _7B_DD_DB_D3 = new CathodeFloat(); 
		public CathodeFloat _FA_06_2A_EC = new CathodeFloat(); 
		public CathodeFloat _68_77_5A_F0 = new CathodeFloat(); 
		public CathodeFloat _09_0F_1A_02 = new CathodeFloat(); 
		public CathodeFloat _9B_54_9F_07 = new CathodeFloat(); 
		public CathodeFloat _7F_CB_5B_27 = new CathodeFloat(); 
		public CathodeFloat _E6_B9_59_2A = new CathodeFloat(); 
		public CathodeFloat _1B_80_02_2F = new CathodeFloat(); 
		public CathodeFloat _89_B0_B5_3A = new CathodeFloat(); 
		public CathodeFloat _32_00_BC_42 = new CathodeFloat(); 
		public CathodeFloat _73_53_D9_52 = new CathodeFloat(); 
		public CathodeFloat _D5_17_09_62 = new CathodeFloat(); 
		public CathodeFloat _4D_77_3B_68 = new CathodeFloat(); 
		public CathodeFloat _A5_3A_96_9A = new CathodeFloat(); 
		public CathodeFloat _36_F9_76_AB = new CathodeFloat(); 
		public CathodeFloat _97_E9_DC_C5 = new CathodeFloat(); 
		public CathodeFloat _B3_15_A6_D0 = new CathodeFloat(); 
		//animation_finished
		//animation_interrupted
		//animation_changed
		//cinematic_loaded
		//cinematic_unloaded
		//external_time
		//current_time
		//is_cinematic_skippable
		//playback
	};

	//44-42-78-2B
	public class CameraAimAssistant: EntityMethodInterface {
		public CathodeFloat _6A_BC_C2_08 = new CathodeFloat(); 
		public CathodeFloat _25_40_EF_1A = new CathodeFloat(); 
		public CathodeFloat min_activation_distance = new CathodeFloat(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeFloat camera_speed_attenuation = new CathodeFloat(); 
		public CathodeFloat fading_range = new CathodeFloat(); 
		public CathodeFloat inner_radius = new CathodeFloat(); 
		public CathodeFloat activation_radius = new CathodeFloat(); 
	};

	//24-D1-57-F2
	public class CameraPlayAnimation: EntityMethodInterface {
		public CathodeInteger shot_number = new CathodeInteger(); 
		public CathodeString data_file = new CathodeString(); 
		public CathodeBool is_cinematic = new CathodeBool(); 
		//on_animation_finished
		//animated_camera
		//position_marker
		//character_to_focus
		//focal_length_mm
		//focal_plane_m
		//fnum
		//focal_point
		//animation_length
		//frames_count
		//result_transformation
		//start_frame
		//end_frame
		//play_speed
		//loop_play
		//clipping_planes_preset
		//dof_key
		//override_dof
		//focal_point_offset
		//bone_to_focus
	};

	//FB-2A-F2-4B
	public class CameraResource: EntityMethodInterface {
		public CathodeFloat transition_duration = new CathodeFloat(); 
		public CathodeFloat exit_transition_ease_out = new CathodeFloat(); 
		public CathodeBool reset_player_camera_on_exit = new CathodeBool(); 
		public CathodeBool enable_exit_transition = new CathodeBool(); 
		public CathodeString camera_name = new CathodeString(); 
		public CathodeFloat transition_ease_in = new CathodeFloat(); 
		public CathodeFloat deactivate_camera = new CathodeFloat(); 
		public CathodeBool converge_to_player_camera = new CathodeBool(); 
		public CathodeFloat fov = new CathodeFloat(); 
		public CathodeFloat exit_transition_ease_in = new CathodeFloat(); 
		public CathodeFloat transition_ease_out = new CathodeFloat(); 
		public CathodeFloat exit_transition_duration = new CathodeFloat(); 
		public CathodeBool enable_enter_transition = new CathodeBool(); 
		public CathodeTransform camera_transformation = new CathodeTransform(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat exit_transition_curve_strength = new CathodeFloat(); 
		public CathodeBool is_camera_transformation_local = new CathodeBool(); 
		public CathodeFloat transition_curve_strength = new CathodeFloat(); 
		//on_enter_transition_finished
		//on_exit_transition_finished
		//enable_on_reset
		//clipping_planes_preset
		//is_ghost
		//transition_curve_direction
		//exit_transition_curve_direction
	};

	//A6-3D-B4-AB
	public class CameraShake: EntityMethodInterface {
		public CathodeBool _A9_20_CC_05 = new CathodeBool(); 
		public CathodeEnum shake_type = new CathodeEnum(); 
		public CathodeBool shake_rotation = new CathodeBool(); 
		public CathodeBool explosion_push_back = new CathodeBool(); 
		public CathodeBool _3F_04_F1_4E = new CathodeBool(); 
		public CathodeFloat blend_in = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat spring_damping = new CathodeFloat(); 
		public CathodeVector3 max_rotation_angles = new CathodeVector3(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool shake_position = new CathodeBool(); 
		public CathodeVector3 shake_frequency = new CathodeVector3(); 
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeBool global = new CathodeBool(); 
		public CathodeVector3 max_position_offset = new CathodeVector3(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeFloat external_radius = new CathodeFloat(); 
		public CathodeFloat spring_constant = new CathodeFloat(); 
		public CathodeFloat trigger = new CathodeFloat(); 
		public CathodeString behavior_name = new CathodeString(); 
		public CathodeFloat impulse_intensity = new CathodeFloat(); 
		public CathodeFloat blend_out = new CathodeFloat(); 
		public CathodeFloat internal_radius = new CathodeFloat(); 
		public CathodeFloat strength_damping = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeTransform relative_transformation = new CathodeTransform(); 
		public CathodeBool override_weapon_swing = new CathodeBool(); 
		public CathodeBool bone_shaking = new CathodeBool(); 
		public CathodeString _8C_A8_96_EC = new CathodeString(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		public CathodeFloat threshold = new CathodeFloat(); 
		//impulse_position
	};

	//1C-1D-E4-60
	public class CamPeek: EntityMethodInterface {
		public CathodeFloat speed_x = new CathodeFloat(); 
		public CathodeFloat range_forward = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeString linked_cameras = new CathodeString(); 
		public CathodeFloat range_right = new CathodeFloat(); 
		public CathodeFloat damping_x = new CathodeFloat(); 
		public CathodeFloat blend_in = new CathodeFloat(); 
		public CathodeFloat speed_y = new CathodeFloat(); 
		public CathodeFloat _B4_CC_D7_85 = new CathodeFloat(); 
		public CathodeFloat range_left = new CathodeFloat(); 
		public CathodeFloat range_up = new CathodeFloat(); 
		public CathodeFloat damping_y = new CathodeFloat(); 
		public CathodeFloat _BA_26_83_CF = new CathodeFloat(); 
		public CathodeBool use_ik_solver = new CathodeBool(); 
		public CathodeFloat focal_distance_y = new CathodeFloat(); 
		public CathodeString behavior_name = new CathodeString(); 
		public CathodeFloat range_down = new CathodeFloat(); 
		public CathodeFloat blend_out = new CathodeFloat(); 
		public CathodeFloat range_backward = new CathodeFloat(); 
		public CathodeFloat focal_distance = new CathodeFloat(); 
		public CathodeBool disable_collision_test = new CathodeBool(); 
		public CathodeBool use_horizontal_plane = new CathodeBool(); 
		//pos
		//x_ratio
		//y_ratio
		//roll_factor
		//stick
	};

	//8A-79-61-C5
	public class Character: EntityMethodInterface {
		public CathodeEnum inventory_set = new CathodeEnum(); 
		public CathodeString anim_set = new CathodeString(); 
		public CathodeString reference_skeleton = new CathodeString(); 
		public CathodeBool is_player = new CathodeBool(); 
		public CathodeString _9D_CC_CE_56 = new CathodeString(); 
		public CathodeString attribute_set = new CathodeString(); 
		public CathodeString footwear_sound = new CathodeString(); 
		public CathodeEnum alliance_group = new CathodeEnum(); 
		public CathodeString _CD_8E_31_AF = new CathodeString(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeString anim_tree_set = new CathodeString(); 
		public CathodeString display_model = new CathodeString(); 
		public CathodeEnum character_class = new CathodeEnum(); 
		public CathodeBool spawn_on_reset = new CathodeBool(); 
		public CathodeEnum custom_character_type = new CathodeEnum(); 
		public CathodeString container_interaction_text = new CathodeString(); 
		public CathodeString torso_sound = new CathodeString(); 
		//Weapon
		//is_holstered
		//finished_spawning
		//finished_respawning
		//dead_container_take_slot
		//dead_container_emptied
		//on_ragdoll_impact
		//on_footstep
		//on_despawn_requested
		//show_on_reset
		//contents_of_dead_container
		//PopToNavMesh
		//is_cinematic
		//disable_dead_container
		//allow_container_without_death
		//is_backstage
		//force_backstage_on_respawn
		//dialogue_voice
		//spawn_id
		//leg_sound
		//custom_character_accessory_override
		//custom_character_population_type
		//named_custom_character
		//named_custom_character_assets_set
		//gcip_distribution_bias
		//use_alliance_at_death
	};

	//97-FC-93-A4
	public class CharacterAttachmentNode: EntityMethodInterface {
		public CathodeVector3 Translation = new CathodeVector3(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeFloat AdditiveNodeIntensity = new CathodeFloat(); 
		public CathodeEnum AdditiveNode = new CathodeEnum(); 
		public CathodeEnum Node = new CathodeEnum(); 
		public CathodeVector3 Rotation = new CathodeVector3(); 
		public CathodeBool UseOffset = new CathodeBool(); 
		//character
		//attachment
	};

	//A0-15-DA-64
	public class CharacterShivaArms: EntityMethodInterface {
	};

	//06-8E-09-76
	public class CharacterTypeMonitor: EntityMethodInterface {
		public CathodeEnum character_class = new CathodeEnum(); 
		//spawned
		//despawned
		//all_despawned
		//AreAny
		//trigger_on_start
		//trigger_on_checkpoint_restart
	};

	//0F-AD-F6-3D
	public class Checkpoint: EntityMethodInterface {
		public CathodeBool is_first_checkpoint = new CathodeBool(); 
		public CathodeString section = new CathodeString(); 
		public CathodeBool is_first_autorun_checkpoint = new CathodeBool(); 
		public CathodeEnum checkpoint_type = new CathodeEnum(); 
		public CathodeFloat mission_number = new CathodeFloat(); 
		public CathodeBool _DE_8F_67_A4 = new CathodeBool(); 
		public CathodeFloat finished_saving_to_hdd = new CathodeFloat(); 
		public CathodeFloat on_saved = new CathodeFloat(); 
		//on_checkpoint
		//on_captured
		//finished_saving
		//finished_loading
		//cancelled_saving
		//player_spawn_position
	};

	//F0-96-1C-0A
	public class CheckpointRestoredNotify: EntityMethodInterface {
		//restored
	};

	//38-D1-A7-96
	public class ChokePoint: EntityMethodInterface {
		public CathodeTransform position = new CathodeTransform(); 
		//resource
	};

	//E4-8C-B9-0A
	public class CHR_DamageMonitor: EntityMethodInterface {
		public CathodeEnum AmmoType = new CathodeEnum(); 
		public CathodeEnum DamageType = new CathodeEnum(); 
		//damaged
		//InstigatorFilter
		//DamageDone
		//Instigator
	};

	//AF-BE-6E-A9
	public class CHR_DeathMonitor: EntityMethodInterface {
		public CathodeEnum DamageType = new CathodeEnum(); 
		//dying
		//killed
		//KillerFilter
		//Killer
	};

	//23-77-8E-F6
	public class CHR_DeepCrouch: EntityMethodInterface {
		public CathodeFloat crouch_amount = new CathodeFloat(); 
		public CathodeFloat smooth_damping = new CathodeFloat(); 
		//allow_stand_up
	};

	//35-5D-1D-76
	public class CHR_GetHealth: EntityMethodInterface {
		//Health
	};

	//88-7F-80-D5
	public class CHR_GetTorch: EntityMethodInterface {
		//torch_on
		//torch_off
		//TorchOn
	};

	//87-4C-86-FA
	public class CHR_HoldBreath: EntityMethodInterface {
		//ExhaustionOnStop
	};

	//4A-70-FD-06
	public class CHR_IsWithinRange: EntityMethodInterface {
		public CathodeFloat Radius = new CathodeFloat(); 
		public CathodeFloat Height = new CathodeFloat(); 
		public CathodeEnum Range_test_shape = new CathodeEnum(); 
		//In_range
		//Out_of_range
		//Position
	};

	//6C-4B-26-A3
	public class CHR_LocomotionEffect: EntityMethodInterface {
		//Effect
	};

	//B8-4E-14-0B
	public class CHR_LocomotionModifier: EntityMethodInterface {
		public CathodeBool Is_In_Spacesuit = new CathodeBool(); 
		public CathodeBool Can_Run = new CathodeBool(); 
		public CathodeBool Must_Walk = new CathodeBool(); 
		public CathodeBool Must_Crouch = new CathodeBool(); 
		public CathodeBool Can_Crouch = new CathodeBool(); 
		public CathodeBool Can_Injured = new CathodeBool(); 
		public CathodeBool Can_Aim = new CathodeBool(); 
		public CathodeBool Must_Run = new CathodeBool(); 
		//Must_Aim
		//Must_Injured
	};

	//CA-1E-02-5F
	public class CHR_ModifyBreathing: EntityMethodInterface {
		//Exhaustion
	};

	//BB-75-07-DD
	public class CHR_PlayNPCBark: EntityMethodInterface {
		public CathodeString action = new CathodeString(); 
		public CathodeString _A3_64_0A_36 = new CathodeString(); 
		public CathodeFloat queue_time = new CathodeFloat(); 
		public CathodeString dialogue_mode = new CathodeString(); 
		public CathodeString _52_53_EB_E9 = new CathodeString(); 
		public CathodeEnum speech_priority = new CathodeEnum(); 
		public CathodeString _C9_10_40_6D = new CathodeString(); 
		//on_speech_started
		//on_speech_finished
		//sound_event
	};

	//EC-CF-66-2B
	public class CHR_PlaySecondaryAnimation: EntityMethodInterface {
		public CathodeEnum AnimationLayer = new CathodeEnum(); 
		public CathodeString AnimationSet = new CathodeString(); 
		public CathodeBool AllowInterruption = new CathodeBool(); 
		public CathodeFloat PlaySpeed = new CathodeFloat(); 
		public CathodeFloat _26_FF_DE_87 = new CathodeFloat(); 
		public CathodeFloat BlendInTime = new CathodeFloat(); 
		public CathodeBool StartInstantly = new CathodeBool(); 
		public CathodeInteger PlayCount = new CathodeInteger(); 
		public CathodeString Animation = new CathodeString(); 
		//Interrupted
		//finished
		//on_loaded
		//Marker
		//OptionalMask
		//ExternalStartTime
		//ExternalTime
		//animationLength
		//StartFrame
		//EndFrame
		//GaitSyncStart
		//Mirror
		//AutomaticZoning
		//ManualLoading
	};

	//A8-9D-27-C7
	public class CHR_SetAlliance: EntityMethodInterface {
		public CathodeEnum Alliance = new CathodeEnum(); 
	};

	//97-03-2B-B8
	public class CHR_SetDebugDisplayName: EntityMethodInterface {
		//DebugName
	};

	//59-D3-A0-BB
	public class CHR_SetFacehuggerAggroRadius: EntityMethodInterface {
		//radius
	};

	//4B-F3-EF-90
	public class CHR_SetFocalPoint: EntityMethodInterface {
		public CathodeEnum priority = new CathodeEnum(); 
		//focal_point
		//speed
		//steal_camera
		//line_of_sight_test
	};

	//66-A8-D0-82
	public class CHR_SetHealth: EntityMethodInterface {
		public CathodeFloat been_set = new CathodeFloat(); 
		public CathodeBool UsePercentageOfCurrentHeath = new CathodeBool(); 
		//HealthPercentage
	};

	//DB-96-C7-FC
	public class CHR_SetInvincibility: EntityMethodInterface {
		public CathodeEnum damage_mode = new CathodeEnum(); 
	};

	//10-54-06-0A
	public class CHR_SetMood: EntityMethodInterface {
		public CathodeEnum mood = new CathodeEnum(); 
		public CathodeEnum moodIntensity = new CathodeEnum(); 
		public CathodeFloat timeOut = new CathodeFloat(); 
	};

	//5A-04-54-B9
	public class CHR_SetShowInMotionTracker: EntityMethodInterface {
	};

	//6D-A8-FF-60
	public class CHR_SetSubModelVisibility: EntityMethodInterface {
		public CathodeBool is_visible = new CathodeBool(); 
		public CathodeString matching = new CathodeString(); 
	};

	//C6-9E-E6-F5
	public class CHR_SetTacticalPosition: EntityMethodInterface {
		public CathodeEnum sweep_type = new CathodeEnum(); 
		public CathodeFloat fixed_sweep_radius = new CathodeFloat(); 
		//tactical_position
	};

	//78-80-7A-BB
	public class CHR_SetTacticalPositionToTarget: EntityMethodInterface {
	};

	//0E-C5-E1-BA
	public class CHR_SetTorch: EntityMethodInterface {
		public CathodeBool TorchOn = new CathodeBool(); 
	};

	//1F-C0-17-BD
	public class CHR_TakeDamage: EntityMethodInterface {
		public CathodeBool DamageIsAPercentage = new CathodeBool(); 
		public CathodeEnum AmmoType = new CathodeEnum(); 
		public CathodeInteger Damage = new CathodeInteger(); 
	};

	//AF-C0-94-FC
	public class CHR_TorchMonitor: EntityMethodInterface {
		public CathodeBool trigger_on_start = new CathodeBool(); 
		//torch_on
		//torch_off
		//TorchOn
		//trigger_on_checkpoint_restart
	};

	//87-D3-16-2E
	public class CHR_VentMonitor: EntityMethodInterface {
		public CathodeBool trigger_on_start = new CathodeBool(); 
		public CathodeBool trigger_on_checkpoint_restart = new CathodeBool(); 
		//entered_vent
		//exited_vent
		//IsInVent
	};

	//CD-2C-C3-B1
	public class CHR_WeaponFireMonitor: EntityMethodInterface {
		//fired
	};

	//B5-74-2C-C6
	public class ChromaticAberrations: EntityMethodInterface {
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat intensity = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
		public CathodeFloat aberration_scalar = new CathodeFloat(); 
	};

	//1B-A7-B5-FB
	public class ClearPrimaryObjective: EntityMethodInterface {
		public CathodeBool clear_all_sub_objectives = new CathodeBool(); 
	};

	//2A-68-FC-AC
	public class ClearSubObjective: EntityMethodInterface {
		public CathodeInteger slot_number = new CathodeInteger(); 
	};

	//D1-3B-28-EF
	public class ClipPlanesController: EntityMethodInterface {
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeBool update_near = new CathodeBool(); 
		public CathodeFloat far_plane = new CathodeFloat(); 
		public CathodeFloat near_plane = new CathodeFloat(); 
		public CathodeBool update_far = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeString behavior_name = new CathodeString(); 
		public CathodeFloat blend_in = new CathodeFloat(); 
		public CathodeFloat blend_out = new CathodeFloat(); 
		public CathodeFloat threshold = new CathodeFloat(); 
	};

	//C5-B9-F5-2E
	public class CMD_AimAt: EntityMethodInterface {
		public CathodeBool Raise_gun = new CathodeBool(); 
		public CathodeBool override_all_ai = new CathodeBool(); 
		//finished
		//AimTarget
		//use_current_target
	};

	//75-F1-22-27
	public class CMD_Die: EntityMethodInterface {
		public CathodeBool override_all_ai = new CathodeBool(); 
		public CathodeEnum death_style = new CathodeEnum(); 
		//Killer
	};

	//C8-9C-67-36
	public class CMD_Follow: EntityMethodInterface {
		public CathodeEnum move_type = new CathodeEnum(); 
		public CathodeFloat outer_radius = new CathodeFloat(); 
		public CathodeFloat inner_radius = new CathodeFloat(); 
		//entered_inner_radius
		//exitted_outer_radius
		//failed
		//Waypoint
		//idle_stance
		//prefer_traversals
	};

	//51-EC-81-A9
	public class CMD_FollowUsingJobs: EntityMethodInterface {
		public CathodeFloat job_cancel_radius = new CathodeFloat(); 
		public CathodeEnum follow_type = new CathodeEnum(); 
		public CathodeEnum fastest_allowed_move_type = new CathodeEnum(); 
		public CathodeEnum slowest_allowed_move_type = new CathodeEnum(); 
		public CathodeBool override_all_ai = new CathodeBool(); 
		public CathodeFloat outer_radius = new CathodeFloat(); 
		public CathodeFloat job_select_radius = new CathodeFloat(); 
		public CathodeFloat teleport_required_range = new CathodeFloat(); 
		public CathodeFloat inner_radius = new CathodeFloat(); 
		public CathodeBool allow_teleports = new CathodeBool(); 
		public CathodeFloat teleport_radius = new CathodeFloat(); 
		public CathodeBool avoid_player = new CathodeBool(); 
		public CathodeBool clamp_speed = new CathodeBool(); 
		//failed
		//target_to_follow
		//who_Im_leading
		//centre_job_restart_radius
		//prefer_traversals
	};

	//E8-F9-1D-31
	public class CMD_ForceMeleeAttack: EntityMethodInterface {
		public CathodeEnum melee_attack_type = new CathodeEnum(); 
		public CathodeEnum enemy_type = new CathodeEnum(); 
		//melee_attack_index
	};

	//7E-0C-57-24
	public class CMD_GoTo: EntityMethodInterface {
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeBool _A7_2D_D1_27 = new CathodeBool(); 
		public CathodeBool should_be_aiming = new CathodeBool(); 
		public CathodeBool use_stopping_anim = new CathodeBool(); 
		public CathodeEnum move_type = new CathodeEnum(); 
		public CathodeBool enable_lookaround = new CathodeBool(); 
		public CathodeBool override_all_ai = new CathodeBool(); 
		public CathodeBool always_stop_at_radius = new CathodeBool(); 
		public CathodeFloat arrived_radius = new CathodeFloat(); 
		public CathodeBool stop_at_radius_if_lined_up = new CathodeBool(); 
		public CathodeBool continue_from_previous_move = new CathodeBool(); 
		public CathodeBool DestinationIsBackstage = new CathodeBool(); 
		public CathodeBool allow_to_use_vents = new CathodeBool(); 
		public CathodeBool start_instantly = new CathodeBool(); 
		public CathodeBool disallow_traversal = new CathodeBool(); 
		public CathodeBool _7E_51_E4_D6 = new CathodeBool(); 
		public CathodeBool use_current_target_as_aim = new CathodeBool(); 
		public CathodeBool prefer_traversals = new CathodeBool(); 
		public CathodeBool maintain_current_facing = new CathodeBool(); 
		//succeeded
		//failed
		//Waypoint
		//AimTarget
	};

	//B3-E2-4A-C5
	public class CMD_GoToCover: EntityMethodInterface {
		public CathodeEnum move_type = new CathodeEnum(); 
		public CathodeFloat SearchRadius = new CathodeFloat(); 
		public CathodeBool enable_lookaround = new CathodeBool(); 
		public CathodeBool override_all_ai = new CathodeBool(); 
		public CathodeFloat apply_stop = new CathodeFloat(); 
		public CathodeBool _7E_51_E4_D6 = new CathodeBool(); 
		public CathodeBool continue_from_previous_move = new CathodeBool(); 
		//succeeded
		//failed
		//entered_cover
		//CoverPoint
		//AimTarget
		//duration
		//disallow_traversal
		//should_be_aiming
		//use_current_target_as_aim
	};

	//C2-D2-A4-F5
	public class CMD_HolsterWeapon: EntityMethodInterface {
		public CathodeEnum equipment_slot = new CathodeEnum(); 
		public CathodeFloat apply_start = new CathodeFloat(); 
		public CathodeBool force_player_unarmed_on_holster = new CathodeBool(); 
		public CathodeBool should_holster = new CathodeBool(); 
		public CathodeBool skip_anims = new CathodeBool(); 
		//failed
		//success
		//force_drop_held_item
	};

	//12-AB-DC-3E
	public class CMD_Idle: EntityMethodInterface {
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeBool start_instantly = new CathodeBool(); 
		public CathodeBool override_all_ai = new CathodeBool(); 
		public CathodeBool lock_cameras = new CathodeBool(); 
		public CathodeEnum desired_stance = new CathodeEnum(); 
		public CathodeBool should_face_target = new CathodeBool(); 
		public CathodeEnum idle_style = new CathodeEnum(); 
		public CathodeBool should_raise_gun_while_turning = new CathodeBool(); 
		public CathodeBool anchor = new CathodeBool(); 
		public CathodeEnum _E2_D6_52_9C = new CathodeEnum(); 
		//finished
		//interrupted
		//target_to_face
	};

	//F2-8D-8C-4A
	public class CMD_LaunchMeleeAttack: EntityMethodInterface {
		public CathodeEnum melee_attack_type = new CathodeEnum(); 
		public CathodeBool override_all_ai = new CathodeBool(); 
		public CathodeBool skip_convergence = new CathodeBool(); 
		public CathodeFloat command_started = new CathodeFloat(); 
		//finished
		//enemy_type
		//melee_attack_index
	};

	//02-60-6B-CE
	public class CMD_ModifyCombatBehaviour: EntityMethodInterface {
		public CathodeEnum behaviour_type = new CathodeEnum(); 
		public CathodeBool status = new CathodeBool(); 
	};

	//51-1E-56-CD
	public class CMD_PlayAnimation: EntityMethodInterface {
		public CathodeBool LocationConvergence = new CathodeBool(); 
		public CathodeString AnimationSet = new CathodeString(); 
		public CathodeBool AllowInterruption = new CathodeBool(); 
		public CathodeString Animation = new CathodeString(); 
		public CathodeBool OrientationConvergence = new CathodeBool(); 
		public CathodeBool AllowGravity = new CathodeBool(); 
		public CathodeBool AutomaticZoning = new CathodeBool(); 
		public CathodeFloat ConvergenceTime = new CathodeFloat(); 
		public CathodeBool AllowCollision = new CathodeBool(); 
		public CathodeFloat _26_FF_DE_87 = new CathodeFloat(); 
		public CathodeFloat BlendInTime = new CathodeFloat(); 
		public CathodeInteger shot_number = new CathodeInteger(); 
		public CathodeBool PlayerAnimDrivenView = new CathodeBool(); 
		public CathodeBool FullCinematic = new CathodeBool(); 
		public CathodeBool NoIK = new CathodeBool(); 
		public CathodeBool StartInstantly = new CathodeBool(); 
		public CathodeBool NoLayers = new CathodeBool(); 
		public CathodeFloat ExitConvergenceTime = new CathodeFloat(); 
		public CathodeBool override_all_ai = new CathodeBool(); 
		public CathodeBool ManualLoading = new CathodeBool(); 
		public CathodeFloat ExertionFactor = new CathodeFloat(); 
		public CathodeFloat PlaySpeed = new CathodeFloat(); 
		public CathodeBool InitiallyBackstage = new CathodeBool(); 
		public CathodeBool UseShivaArms = new CathodeBool(); 
		public CathodeInteger PlayCount = new CathodeInteger(); 
		public CathodeBool RemoveMotion = new CathodeBool(); 
		public CathodeBool UseExitConvergence = new CathodeBool(); 
		public CathodeInteger EndFrame = new CathodeInteger(); 
		public CathodeInteger StartFrame = new CathodeInteger(); 
		public CathodeBool Start_Instantly = new CathodeBool(); 
		public CathodeBool GaitSyncStart = new CathodeBool(); 
		public CathodeBool _EE_54_A1_5C = new CathodeBool(); 
		public CathodeBool RagdollEnabled = new CathodeBool(); 
		public CathodeBool NoFootIK = new CathodeBool(); 
		public CathodeBool IsCrouchedAnim = new CathodeBool(); 
		public CathodeBool Death_by_ragdoll_only = new CathodeBool(); 
		public CathodeBool DisableGunLayer = new CathodeBool(); 
		//Interrupted
		//finished
		//badInterrupted
		//on_loaded
		//SafePos
		//Marker
		//ExitPosition
		//ExternalStartTime
		//ExternalTime
		//OverrideCharacter
		//OptionalMask
		//animationLength
		//Mirror
		//dof_key
		//resource
	};

	//95-67-7A-30
	public class CMD_StopScript: EntityMethodInterface {
		public CathodeBool override_all_ai = new CathodeBool(); 
	};

	//84-82-23-E0
	public class CollectIDTag: EntityMethodInterface {
		public CathodeString tag_id = new CathodeString(); 
	};

	//32-AF-76-84
	public class CollectNostromoLog: EntityMethodInterface {
		//log_id
	};

	//D2-0D-7E-FF
	public class CollectSevastopolLog: EntityMethodInterface {
		public CathodeString log_id = new CathodeString(); 
	};

	//67-10-41-B4
	public class CollisionBarrier: EntityMethodInterface {
		public CathodeString radius = new CathodeString(); 
		public CathodeBool static_collision = new CathodeBool(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeEnum collision_type = new CathodeEnum(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool include_physics = new CathodeBool(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		//on_damaged
		//deleted
	};

	//1B-F1-3F-6F
	public class ColourCorrectionTransition: EntityMethodInterface {
		public CathodeString colour_lut_b = new CathodeString(); 
		public CathodeString colour_lut_a = new CathodeString(); 
		public CathodeFloat interpolate = new CathodeFloat(); 
		public CathodeFloat lut_a_contribution = new CathodeFloat(); 
		public CathodeInteger colour_lut_b_index = new CathodeInteger(); 
		public CathodeFloat lut_b_contribution = new CathodeFloat(); 
		public CathodeInteger colour_lut_a_index = new CathodeInteger(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
	};

	//92-2D-99-0C
	public class ColourSettings: EntityMethodInterface {
		public CathodeFloat green_tint = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat contrast = new CathodeFloat(); 
		public CathodeFloat brightness = new CathodeFloat(); 
		public CathodeFloat blue_tint = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat red_tint = new CathodeFloat(); 
		public CathodeFloat saturation = new CathodeFloat(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
	};

	//91-F3-16-49
	public class CompoundVolume: EntityMethodInterface {
		//event
	};

	//E2-04-65-38
	public class ControllableRange: EntityMethodInterface {
		public CathodeFloat min_feather_range_y = new CathodeFloat(); 
		public CathodeFloat speed_x = new CathodeFloat(); 
		public CathodeFloat max_range_x = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat max_feather_range_x = new CathodeFloat(); 
		public CathodeFloat max_range_y = new CathodeFloat(); 
		public CathodeFloat min_feather_range_x = new CathodeFloat(); 
		public CathodeFloat speed_y = new CathodeFloat(); 
		public CathodeFloat max_feather_range_y = new CathodeFloat(); 
		public CathodeFloat min_range_x = new CathodeFloat(); 
		public CathodeFloat min_range_y = new CathodeFloat(); 
		public CathodeString behavior_name = new CathodeString(); 
		//damping_x
		//damping_y
		//mouse_speed_x
		//mouse_speed_y
	};

	//DB-7D-07-BC
	public class Convo: EntityMethodInterface {
		public CathodeBool positionNPCs = new CathodeBool(); 
		public CathodeFloat personalSpaceRadius = new CathodeFloat(); 
		public CathodeFloat started = new CathodeFloat(); 
		public CathodeBool playerCanJoin = new CathodeBool(); 
		public CathodeBool alwaysTalkToPlayerIfPresent = new CathodeBool(); 
		public CathodeBool circularShape = new CathodeBool(); 
		public CathodeBool playerCanLeave = new CathodeBool(); 
		//everyoneArrived
		//playerJoined
		//playerLeft
		//npcJoined
		//members
		//speaker
		//convoPosition
	};

	//56-85-4E-78
	public class Counter: EntityMethodInterface {
		public CathodeInteger trigger_limit = new CathodeInteger(); 
		public CathodeBool is_limitless = new CathodeBool(); 
		//on_under_limit
		//on_limit
		//on_over_limit
		//Count
	};

	//34-0A-CE-E3
	public class CoverExclusionArea: EntityMethodInterface {
		public CathodeString radius = new CathodeString(); 
		public CathodeBool exclude_spotting_positions = new CathodeBool(); 
		public CathodeBool exclude_assault_positions = new CathodeBool(); 
		public CathodeBool exclude_jump_downs = new CathodeBool(); 
		public CathodeBool exclude_mantles = new CathodeBool(); 
		public CathodeBool exclude_vaults = new CathodeBool(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		//exclude_cover
		//exclude_crawl_space_spotting_positions
	};

	//FD-39-F4-61
	public class Custom_Hiding_Vignette_controller: EntityMethodInterface {
		//StartFade
		//StopFade
		//Breath
		//Blackout_start_time
		//run_out_time
		//Vignette
		//FadeValue
	};

	//22-BC-81-9B
	public class DayToneMappingSettings: EntityMethodInterface {
		public CathodeBool pause_on_reset = new CathodeBool(); 
		public CathodeFloat toe_strength = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat black_point = new CathodeFloat(); 
		public CathodeFloat cross_over_point = new CathodeFloat(); 
		public CathodeFloat white_point = new CathodeFloat(); 
		public CathodeFloat shoulder_strength = new CathodeFloat(); 
		public CathodeFloat luminance_scale = new CathodeFloat(); 
	};

	//4F-51-4A-2D
	public class DEBUG_SenseLevels: EntityMethodInterface {
		public CathodeEnum Sense = new CathodeEnum(); 
		//no_activation
		//trace_activation
		//lower_activation
		//normal_activation
		//upper_activation
	};

	//B6-43-45-92
	public class DebugCamera: EntityMethodInterface {
		//linked_cameras
	};

	//D1-C0-B5-12
	public class DebugCheckpoint: EntityMethodInterface {
		//on_checkpoint
		//section
		//level_reset
	};

	//53-9F-65-FC
	public class DebugEnvironmentMarker: EntityMethodInterface {
		public CathodeVector3 colour = new CathodeVector3(); 
		public CathodeFloat scroll_speed = new CathodeFloat(); 
		public CathodeFloat size = new CathodeFloat(); 
		public CathodeInteger max_string_length = new CathodeInteger(); 
		public CathodeString text = new CathodeString(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeBool scale_with_distance = new CathodeBool(); 
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeBool show_distance_from_target = new CathodeBool(); 
		//target
		//float_input
		//int_input
		//bool_input
		//vector_input
		//enum_input
		//namespace
		//world_pos
		//DebugPositionMarker
		//world_pos
	};

	//40-1A-8A-EB
	public class DebugLoadCheckpoint: EntityMethodInterface {
		//previous_checkpoint
	};

	//E8-E2-A4-B6
	public class DebugPositionMarker: EntityMethodInterface {
		public CathodeBool start_on_reset = new CathodeBool(); 
	};

	//39-D4-49-3D
	public class DebugText: EntityMethodInterface {
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeVector3 colour = new CathodeVector3(); 
		public CathodeString text = new CathodeString(); 
		public CathodeEnum alignment = new CathodeEnum(); 
		public CathodeInteger size = new CathodeInteger(); 
		public CathodeBool cancel_pause_with_button_press = new CathodeBool(); 
		public CathodeBool pause_game = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeEnum ci_type = new CathodeEnum(); 
		//duration_finished
		//float_input
		//int_input
		//bool_input
		//vector_input
		//enum_input
		//text_input
		//namespace
		//priority
	};

	//90-2E-DF-D4
	public class DebugTextStacking: EntityMethodInterface {
		public CathodeVector3 colour = new CathodeVector3(); 
		public CathodeFloat float_input = new CathodeFloat(); 
		public CathodeString text = new CathodeString(); 
		public CathodeInteger int_input = new CathodeInteger(); 
		public CathodeEnum ci_type = new CathodeEnum(); 
		public CathodeVector3 vector_input = new CathodeVector3(); 
		public CathodeInteger size = new CathodeInteger(); 
		//bool_input
		//enum_input
		//namespace
		//needs_debug_opt_to_render
	};

	//9B-6F-86-FD
	public class DeleteBlankPanel: EntityMethodInterface {
		//door_mechanism
	};

	//96-B7-00-B0
	public class DeleteButtonDisk: EntityMethodInterface {
		//door_mechanism
		//button_type
	};

	//91-7E-59-28
	public class DeleteButtonKeys: EntityMethodInterface {
		//door_mechanism
		//button_type
	};

	//1A-66-22-67
	public class DeleteCuttingPanel: EntityMethodInterface {
		//door_mechanism
	};

	//4A-F8-05-1F
	public class DeleteHacking: EntityMethodInterface {
		//door_mechanism
	};

	//E7-3E-C9-7F
	public class DeleteHousing: EntityMethodInterface {
		//door_mechanism
		//is_door
	};

	//68-BA-C9-80
	public class DeleteKeypad: EntityMethodInterface {
		//door_mechanism
	};

	//97-23-A4-BF
	public class DeletePullLever: EntityMethodInterface {
		//door_mechanism
		//lever_type
	};

	//3A-C3-90-16
	public class DeleteRotateLever: EntityMethodInterface {
		//door_mechanism
		//lever_type
	};

	//32-1E-BC-6E
	public class DepthOfFieldSettings: EntityMethodInterface {
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat intensity = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat focal_plane_m = new CathodeFloat(); 
		public CathodeFloat focal_length_mm = new CathodeFloat(); 
		public CathodeFloat fnum = new CathodeFloat(); 
		public CathodeBool use_camera_target = new CathodeBool(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
		//focal_point
	};

	//96-9E-DF-C8
	public class DespawnCharacter: EntityMethodInterface {
		//despawned
	};

	//68-4F-C3-1C
	public class DespawnPlayer: EntityMethodInterface {
		//despawned
	};

	//B2-0B-88-FE
	public class Display_Element_On_Map: EntityMethodInterface {
		public CathodeString element_name = new CathodeString(); 
		public CathodeString map_name = new CathodeString(); 
	};

	//53-70-1C-3C
	public class DisplayMessage: EntityMethodInterface {
		public CathodeString message_id = new CathodeString(); 
		public CathodeString title_id = new CathodeString(); 
	};

	//37-8A-AE-68
	public class DisplayMessageWithCallbacks: EntityMethodInterface {
		public CathodeBool no_button = new CathodeBool(); 
		public CathodeBool yes_button = new CathodeBool(); 
		public CathodeString message_text = new CathodeString(); 
		public CathodeString title_text = new CathodeString(); 
		//on_yes
		//on_no
		//on_cancel
		//yes_text
		//no_text
		//cancel_text
		//cancel_button
	};

	//27-1F-65-F2
	public class DistortionSettings: EntityMethodInterface {
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat intensity = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat radial_distort_scalar = new CathodeFloat(); 
		public CathodeFloat radial_distort_factor = new CathodeFloat(); 
		public CathodeFloat radial_distort_constraint = new CathodeFloat(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
	};

	//BB-49-65-AF
	public class Door: EntityMethodInterface {
		public CathodeFloat auto_close_delay = new CathodeFloat(); 
		public CathodeFloat request_close = new CathodeFloat(); 
		public CathodeFloat request_open = new CathodeFloat(); 
		public CathodeBool is_auto = new CathodeBool(); 
		public CathodeString locked_text = new CathodeString(); 
		public CathodeString unlocked_text = new CathodeString(); 
		public CathodeBool invert_nav_mesh_barrier = new CathodeBool(); 
		public CathodeString action_text = new CathodeString(); 
		//started_opening
		//started_closing
		//finished_opening
		//finished_closing
		//used_locked
		//used_unlocked
		//used_forced_open
		//used_forced_closed
		//waiting_to_open
		//highlight
		//unhighlight
		//zone_link
		//animation
		//trigger_filter
		//icon_pos
		//icon_usable_radius
		//show_icon_when_locked
		//nav_mesh
		//wait_point_1
		//wait_point_2
		//geometry
		//is_scripted
		//wait_to_open
		//is_waiting
		//icon_keyframe
		//detach_anim
	};

	//A3-14-A7-78
	public class DoorStatus: EntityMethodInterface {
		//hacking_difficulty
		//door_mechanism
		//gate_type
		//has_correct_keycard
		//cutting_tool_level
		//is_locked
		//is_powered
		//is_cutting_complete
	};

	//28-58-CE-60
	public class EFFECT_DirectionalPhysics: EntityMethodInterface {
		public CathodeFloat max_force = new CathodeFloat(); 
		public CathodeFloat angular_falloff = new CathodeFloat(); 
		public CathodeFloat effect_distance = new CathodeFloat(); 
		public CathodeVector3 relative_direction = new CathodeVector3(); 
		//min_force
	};

	//4C-E8-6E-22
	public class EFFECT_EntityGenerator: EntityMethodInterface {
		public CathodeFloat lifetime_max = new CathodeFloat(); 
		public CathodeInteger count = new CathodeInteger(); 
		public CathodeFloat lifetime_min = new CathodeFloat(); 
		public CathodeFloat spread = new CathodeFloat(); 
		public CathodeFloat force_min = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat force_max = new CathodeFloat(); 
		public CathodeFloat force_offset_XY_min = new CathodeFloat(); 
		public CathodeFloat force_offset_Z_max = new CathodeFloat(); 
		public CathodeFloat force_offset_XY_max = new CathodeFloat(); 
		public CathodeBool use_local_rotation = new CathodeBool(); 
		public CathodeBool trigger_on_reset = new CathodeBool(); 
		public CathodeFloat force_offset_Z_min = new CathodeFloat(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		//entities
	};

	//AB-CB-33-6F
	public class EFFECT_ImpactGenerator: EntityMethodInterface {
		public CathodeFloat distance = new CathodeFloat(); 
		public CathodeInteger count = new CathodeInteger(); 
		public CathodeFloat spread = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool skip_characters = new CathodeBool(); 
		public CathodeInteger max_count = new CathodeInteger(); 
		public CathodeBool use_local_rotation = new CathodeBool(); 
		public CathodeFloat min_distance = new CathodeFloat(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		//on_impact
		//on_failed
		//trigger_on_reset
	};

	//69-2F-D9-16
	public class ElapsedTimer: EntityMethodInterface {
	};

	//26-00-03-DA
	public class ENT_Debug_Exit_Game: EntityMethodInterface {
		//FailureText
		//FailureCode
	};

	//D7-D2-69-B7
	public class EnvironmentMap: EntityMethodInterface {
		public CathodeInteger Texture_Index = new CathodeInteger(); 
		public CathodeFloat EmissiveFactor = new CathodeFloat(); 
		public CathodeString Texture = new CathodeString(); 
		public CathodeInteger environmentmap_index = new CathodeInteger(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeVector3 ColourFactor = new CathodeVector3(); 
		public CathodeInteger Priority = new CathodeInteger(); 
		//Entities
	};

	//FA-80-45-1E
	public class EnvironmentModelReference: EntityMethodInterface {
		public CathodeResource resource = new CathodeResource(); 
	};

	//FC-BE-29-C0
	public class EQUIPPABLE_ITEM: EntityMethodInterface {
		public CathodeEnum equipment_slot = new CathodeEnum(); 
		public CathodeBool _A1_78_5A_31 = new CathodeBool(); 
		public CathodeString character_animation_context = new CathodeString(); 
		//finished_spawning
		//equipped
		//unequipped
		//on_pickup
		//on_discard
		//on_melee_impact
		//on_used_basic_function
		//spawn_on_reset
		//item_animated_asset
		//owner
		//has_owner
		//character_activate_animation_context
		//left_handed
		//inventory_name
		//holsters_on_owner
		//holster_node
		//holster_scale
		//weapon_handedness
	};

	//1E-86-08-3B
	public class ExclusiveMaster: EntityMethodInterface {
		//active_objects
		//inactive_objects
		//resource
	};

	//43-A8-C3-72
	public class Explosion_AINotifier: EntityMethodInterface {
		//on_character_damage_fx
		//ExplosionPos
		//AmmoType
	};

	//0F-26-AD-61
	public class FakeAILightSourceInPlayersHand: EntityMethodInterface {
		public CathodeFloat radius = new CathodeFloat(); 
	};

	//0E-D2-7B-41
	public class FilmGrainSettings: EntityMethodInterface {
		public CathodeFloat noise_texture_scale = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat high_lum_amplifier = new CathodeFloat(); 
		public CathodeFloat adaptation_scalar = new CathodeFloat(); 
		public CathodeFloat low_lum_amplifier = new CathodeFloat(); 
		public CathodeFloat mid_lum_amplifier = new CathodeFloat(); 
		public CathodeBool adaptive = new CathodeBool(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
		public CathodeFloat low_lum_range = new CathodeFloat(); 
		public CathodeFloat high_lum_range = new CathodeFloat(); 
		public CathodeFloat unadapted_low_lum_amplifier = new CathodeFloat(); 
		public CathodeFloat mid_lum_range = new CathodeFloat(); 
		public CathodeFloat adaptation_time_scalar = new CathodeFloat(); 
		//unadapted_mid_lum_amplifier
		//unadapted_high_lum_amplifier
	};

	//76-05-62-21
	public class FilterAnd: EntityMethodInterface {
		//filter
	};

	//70-AB-23-EC
	public class FilterBelongsToAlliance: EntityMethodInterface {
		public CathodeEnum alliance_group = new CathodeEnum(); 
	};

	//DF-17-14-44
	public class FilterCanSeeTarget: EntityMethodInterface {
		//Target
	};

	//83-BE-E7-33
	public class FilterHasBehaviourTreeFlagSet: EntityMethodInterface {
		public CathodeEnum BehaviourTreeFlag = new CathodeEnum(); 
	};

	//F7-47-CA-A0
	public class FilterHasWeaponOfType: EntityMethodInterface {
		public CathodeEnum weapon_type = new CathodeEnum(); 
	};

	//EE-B6-FF-B9
	public class FilterIsACharacter: EntityMethodInterface {
	};

	//76-A6-2A-36
	public class FilterIsAgressing: EntityMethodInterface {
		//Target
	};

	//E8-DB-7B-91
	public class FilterIsAnySaveInProgress: EntityMethodInterface {
	};

	//09-1E-9B-4B
	public class FilterIsAPlayer: EntityMethodInterface {
	};

	//96-B7-8E-95
	public class FilterIsCharacter: EntityMethodInterface {
		//character
	};

	//45-63-3B-A6
	public class FilterIsCharacterClass: EntityMethodInterface {
		public CathodeEnum character_class = new CathodeEnum(); 
	};

	//3C-9B-2E-AE
	public class FilterIsCharacterClassCombo: EntityMethodInterface {
		public CathodeEnum character_classes = new CathodeEnum(); 
	};

	//08-08-1B-8C
	public class FilterIsDead: EntityMethodInterface {
	};

	//AC-2B-E5-9F
	public class FilterIsEnemyOfCharacter: EntityMethodInterface {
		public CathodeBool use_alliance_at_death = new CathodeBool(); 
	};

	//2A-6C-EC-68
	public class FilterIsEnemyOfPlayer: EntityMethodInterface {
	};

	//6E-4F-CB-7C
	public class FilterIsFacingTarget: EntityMethodInterface {
		//target
		//tolerance
	};

	//8D-09-3D-AD
	public class FilterIsHumanNPC: EntityMethodInterface {
	};

	//68-B2-D3-71
	public class FilterIsInLocomotionState: EntityMethodInterface {
		public CathodeEnum State = new CathodeEnum(); 
	};

	//92-1C-CA-F2
	public class FilterIsLocalPlayer: EntityMethodInterface {
	};

	//8A-7B-1D-25
	public class FilterIsNotDeadManWalking: EntityMethodInterface {
	};

	//7E-8F-02-77
	public class FilterIsObject: EntityMethodInterface {
		//objects
	};

	//95-90-38-59
	public class FilterIsPhysics: EntityMethodInterface {
	};

	//4D-8C-B9-7A
	public class FilterIsPhysicsObject: EntityMethodInterface {
		//object
	};

	//9D-1B-F3-9D
	public class FilterIsPlatform: EntityMethodInterface {
		public CathodeEnum Platform = new CathodeEnum(); 
	};

	//C4-12-C1-49
	public class FilterIsUsingDevice: EntityMethodInterface {
		//Device
	};

	//0B-EC-FA-AB
	public class FilterIsValidInventoryItem: EntityMethodInterface {
		//item
	};

	//82-80-C9-4C
	public class FilterIsWithdrawnAlien: EntityMethodInterface {
	};

	//D7-8B-30-57
	public class FilterNot: EntityMethodInterface {
		//filter
	};

	//77-BE-FE-F8
	public class FilterOr: EntityMethodInterface {
		//filter
	};

	//27-43-F7-C2
	public class FixedCamera: EntityMethodInterface {
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat blend_in = new CathodeFloat(); 
		public CathodeString behavior_name = new CathodeString(); 
		public CathodeBool apply_target = new CathodeBool(); 
		public CathodeFloat blend_out = new CathodeFloat(); 
		public CathodeVector3 camera_position_offset = new CathodeVector3(); 
		public CathodeBool use_transform_position = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool apply_position = new CathodeBool(); 
		//transform_position
		//camera_position
		//camera_target
		//camera_target_offset
		//use_target_offset
		//use_position_offset
	};

	//08-14-60-1E
	public class FlareSettings: EntityMethodInterface {
		public CathodeFloat flareAttenuation3 = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat flareIntensity0 = new CathodeFloat(); 
		public CathodeFloat flareOffset1 = new CathodeFloat(); 
		public CathodeFloat flareOffset0 = new CathodeFloat(); 
		public CathodeFloat flareAttenuation1 = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat flareOffset2 = new CathodeFloat(); 
		public CathodeFloat flareAttenuation2 = new CathodeFloat(); 
		public CathodeFloat flareIntensity2 = new CathodeFloat(); 
		public CathodeFloat flareAttenuation0 = new CathodeFloat(); 
		public CathodeFloat flareOffset3 = new CathodeFloat(); 
		public CathodeFloat flareIntensity1 = new CathodeFloat(); 
		public CathodeFloat flareIntensity3 = new CathodeFloat(); 
	};

	//A8-BA-46-24
	public class FlareTask: EntityMethodInterface {
		public CathodeFloat reached_distance_threshold = new CathodeFloat(); 
		//specific_character
		//filter_options
	};

	//55-4C-06-A6
	public class FlashInvoke: EntityMethodInterface {
		public CathodeString method = new CathodeString(); 
		public CathodeString layer_name = new CathodeString(); 
		public CathodeEnum invoke_type = new CathodeEnum(); 
		public CathodeInteger int_argument_2 = new CathodeInteger(); 
		public CathodeString mrtt_texture = new CathodeString(); 
		//int_argument_0
		//int_argument_1
		//int_argument_3
		//float_argument_0
		//float_argument_1
		//float_argument_2
		//float_argument_3
	};

	//9E-26-7E-FE
	public class FlashScript: EntityMethodInterface {
		public CathodeEnum type = new CathodeEnum(); 
		public CathodeString target_texture_name = new CathodeString(); 
		public CathodeString filename = new CathodeString(); 
		public CathodeString layer_name = new CathodeString(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
	};

	//14-52-7C-F4
	public class FloatAbsolute: EntityMethodInterface {
	};

	//77-2E-80-E8
	public class FloatAdd: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
	};

	//CA-04-6B-64
	public class FloatAdd_All: EntityMethodInterface {
	};

	//07-89-34-A0
	public class FloatClamp: EntityMethodInterface {
		public CathodeFloat Max = new CathodeFloat(); 
		public CathodeFloat Min = new CathodeFloat(); 
		//Value
		//Result
	};

	//90-BD-C2-6E
	public class FloatClampMultiply: EntityMethodInterface {
		public CathodeFloat Min = new CathodeFloat(); 
		//Max
	};

	//C5-F1-56-77
	public class FloatDivide: EntityMethodInterface {
	};

	//6F-11-3E-16
	public class FloatEquals: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
		public CathodeFloat Threshold = new CathodeFloat(); 
	};

	//29-15-13-AE
	public class FloatGetLinearProportion: EntityMethodInterface {
		public CathodeFloat Max = new CathodeFloat(); 
		public CathodeFloat Min = new CathodeFloat(); 
		//Input
		//Proportion
	};

	//BC-DA-A3-68
	public class FloatGreaterThan: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
		public CathodeFloat Threshold = new CathodeFloat(); 
	};

	//50-CD-48-EC
	public class FloatGreaterThanOrEqual: EntityMethodInterface {
		public CathodeFloat Threshold = new CathodeFloat(); 
		public CathodeFloat RHS = new CathodeFloat(); 
	};

	//3D-1B-99-48
	public class FloatLessThan: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
		public CathodeFloat Threshold = new CathodeFloat(); 
	};

	//24-C3-02-A6
	public class FloatLessThanOrEqual: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
		public CathodeFloat LHS = new CathodeFloat(); 
		public CathodeFloat Threshold = new CathodeFloat(); 
	};

	//11-B7-FF-14
	public class FloatLinearInterpolateSpeed: EntityMethodInterface {
		public CathodeFloat Speed = new CathodeFloat(); 
		public CathodeBool Loop = new CathodeBool(); 
		public CathodeBool PingPong = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_finished
		//on_think
		//Result
		//Initial_Value
		//Target_Value
	};

	//35-4A-44-F2
	public class FloatLinearInterpolateSpeedAdvanced: EntityMethodInterface {
		public CathodeFloat Min_Value = new CathodeFloat(); 
		public CathodeBool Loop = new CathodeBool(); 
		public CathodeBool PingPong = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_finished
		//on_think
		//trigger_on_min
		//trigger_on_max
		//trigger_on_loop
		//Result
		//Initial_Value
		//Max_Value
		//Speed
	};

	//5A-A3-19-6E
	public class FloatLinearInterpolateTimed: EntityMethodInterface {
		public CathodeFloat Initial_Value = new CathodeFloat(); 
		public CathodeFloat Target_Value = new CathodeFloat(); 
		public CathodeFloat Time = new CathodeFloat(); 
		public CathodeFloat Result = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeBool Loop = new CathodeBool(); 
		public CathodeBool PingPong = new CathodeBool(); 
		public CathodeFloat pause = new CathodeFloat(); 
		//on_finished
		//on_think
	};

	//86-83-99-07
	public class FloatLinearProportion: EntityMethodInterface {
		public CathodeFloat Initial_Value = new CathodeFloat(); 
		public CathodeFloat Target_Value = new CathodeFloat(); 
		//Proportion
		//Result
	};

	//16-03-BD-A6
	public class FloatMax: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
		public CathodeFloat Result = new CathodeFloat(); 
	};

	//0D-EF-65-20
	public class FloatMax_All: EntityMethodInterface {
	};

	//E5-0A-01-1D
	public class FloatMin: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
	};

	//A6-DB-6B-54
	public class FloatModulate: EntityMethodInterface {
		public CathodeEnum wave_shape = new CathodeEnum(); 
		public CathodeFloat frequency = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat amplitude = new CathodeFloat(); 
		public CathodeFloat phase = new CathodeFloat(); 
		public CathodeFloat bias = new CathodeFloat(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		//on_think
		//Result
	};

	//8B-CC-69-DF
	public class FloatModulateRandom: EntityMethodInterface {
		public CathodeEnum behaviour_anim = new CathodeEnum(); 
		public CathodeFloat behaviour_frequency = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeEnum switch_on_anim = new CathodeEnum(); 
		public CathodeFloat oscillate_range_min = new CathodeFloat(); 
		public CathodeFloat switch_on_duration = new CathodeFloat(); 
		public CathodeFloat switch_off_duration = new CathodeFloat(); 
		public CathodeFloat switch_on_custom_frequency = new CathodeFloat(); 
		public CathodeEnum switch_off_anim = new CathodeEnum(); 
		public CathodeFloat flicker_off_range_min = new CathodeFloat(); 
		public CathodeFloat sparking_speed = new CathodeFloat(); 
		public CathodeFloat behaviour_frequency_variance = new CathodeFloat(); 
		public CathodeFloat flicker_off_rate = new CathodeFloat(); 
		public CathodeFloat flicker_range_min = new CathodeFloat(); 
		public CathodeFloat flicker_rate = new CathodeFloat(); 
		public CathodeFloat switch_on_delay = new CathodeFloat(); 
		public CathodeFloat behaviour_offset = new CathodeFloat(); 
		public CathodeFloat switch_off_custom_frequency = new CathodeFloat(); 
		public CathodeFloat pulse_modulation = new CathodeFloat(); 
		public CathodeFloat blink_rate = new CathodeFloat(); 
		public CathodeFloat blink_range_min = new CathodeFloat(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		//on_full_switched_on
		//on_full_switched_off
		//on_think
		//Result
		//disable_behaviour
	};

	//91-98-66-D7
	public class FloatMultiply: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
	};

	//19-45-7F-14
	public class FloatMultiplyClamp: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
		public CathodeFloat Max = new CathodeFloat(); 
		public CathodeFloat Min = new CathodeFloat(); 
	};

	//72-E9-B9-A5
	public class FloatNotEqual: EntityMethodInterface {
		public CathodeFloat Threshold = new CathodeFloat(); 
		public CathodeFloat RHS = new CathodeFloat(); 
	};

	//71-D9-A2-67
	public class FloatSmoothStep: EntityMethodInterface {
		public CathodeFloat High_Edge = new CathodeFloat(); 
		public CathodeFloat Low_Edge = new CathodeFloat(); 
		//Value
		//Result
	};

	//7F-7D-D0-F2
	public class FloatSqrt: EntityMethodInterface {
	};

	//AB-92-6F-C6
	public class FloatSubtract: EntityMethodInterface {
		public CathodeFloat LHS = new CathodeFloat(); 
	};

	//C5-C0-32-27
	public class FlushZoneCache: EntityMethodInterface {
		public CathodeBool NextGen = new CathodeBool(); 
		public CathodeFloat triggered = new CathodeFloat(); 
		//CurrentGen
	};

	//F4-3D-03-E0
	public class FogBox: EntityMethodInterface {
		public CathodeString radius = new CathodeString(); 
		public CathodeFloat THICKNESS = new CathodeFloat(); 
		public CathodeBool EARLY_ALPHA = new CathodeBool(); 
		public CathodeVector3 COLOUR_TINT = new CathodeVector3(); 
		public CathodeVector3 DEPTH_INTERSECT_MIDPOINT_COLOUR = new CathodeVector3(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeFloat DISTANCE_FADE = new CathodeFloat(); 
		public CathodeBool DEPTH_INTERSECT_COLOUR = new CathodeBool(); 
		public CathodeBool LOW_RES = new CathodeBool(); 
		public CathodeBool SMOOTH_HEIGHT_DENSITY = new CathodeBool(); 
		public CathodeFloat START_DISTANCE_FADE = new CathodeFloat(); 
		public CathodeFloat HEIGHT_MAX_DENSITY = new CathodeFloat(); 
		public CathodeFloat FRESNEL_POWER = new CathodeFloat(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeEnum GEOMETRY_TYPE = new CathodeEnum(); 
		public CathodeBool FRESNEL_FALLOFF = new CathodeBool(); 
		public CathodeBool SOFTNESS = new CathodeBool(); 
		public CathodeFloat SOFTNESS_EDGE = new CathodeFloat(); 
		public CathodeVector3 DEPTH_INTERSECT_END_COLOUR = new CathodeVector3(); 
		public CathodeBool CONVEX_GEOM = new CathodeBool(); 
		public CathodeBool START_DISTANT_CLIP = new CathodeBool(); 
		public CathodeVector3 DEPTH_INTERSECT_INITIAL_COLOUR = new CathodeVector3(); 
		public CathodeBool LINEAR_HEIGHT_DENSITY = new CathodeBool(); 
		public CathodeBool BILLBOARD = new CathodeBool(); 
		public CathodeBool deleted = new CathodeBool(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeFloat ANGLE_FADE = new CathodeFloat(); 
		public CathodeFloat DEPTH_INTERSECT_INITIAL_ALPHA = new CathodeFloat(); 
		public CathodeFloat DEPTH_INTERSECT_MIDPOINT_DEPTH = new CathodeFloat(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeFloat DEPTH_INTERSECT_END_ALPHA = new CathodeFloat(); 
		public CathodeFloat DEPTH_INTERSECT_END_DEPTH = new CathodeFloat(); 
		public CathodeFloat DEPTH_INTERSECT_MIDPOINT_ALPHA = new CathodeFloat(); 
		//resource
	};

	//18-72-33-E1
	public class FogPlane: EntityMethodInterface {
		public CathodeFloat linear_heigth_density_max_scalar = new CathodeFloat(); 
		public CathodeFloat distance_fade_scalar = new CathodeFloat(); 
		public CathodeFloat thickness_scalar = new CathodeFloat(); 
		public CathodeFloat diffuse_0_uv_scalar = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat start_distance_fade_scalar = new CathodeFloat(); 
		public CathodeFloat linear_height_density_fresnel_power_scalar = new CathodeFloat(); 
		public CathodeFloat edge_softness_scalar = new CathodeFloat(); 
		public CathodeFloat angle_fade_scalar = new CathodeFloat(); 
		public CathodeVector3 tint = new CathodeVector3(); 
		public CathodeFloat diffuse_1_speed_scalar = new CathodeFloat(); 
		public CathodeFloat diffuse_0_speed_scalar = new CathodeFloat(); 
		//fog_plane_resource
		//diffuse_1_uv_scalar
	};

	//79-92-6A-CB
	public class FogSetting: EntityMethodInterface {
		public CathodeFloat linear_density = new CathodeFloat(); 
		public CathodeFloat linear_distance = new CathodeFloat(); 
		public CathodeVector3 near_colour = new CathodeVector3(); 
		public CathodeVector3 far_colour = new CathodeVector3(); 
		public CathodeFloat trigger = new CathodeFloat(); 
		public CathodeFloat max_distance = new CathodeFloat(); 
		public CathodeFloat exponential_density = new CathodeFloat(); 
	};

	//77-8E-46-87
	public class FogSphere: EntityMethodInterface {
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeFloat FAR_BLEND_DISTANCE = new CathodeFloat(); 
		public CathodeVector3 COLOUR_TINT = new CathodeVector3(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeString half_dimensions = new CathodeString(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat DENSITY = new CathodeFloat(); 
		public CathodeBool LOW_RES_ALPHA = new CathodeBool(); 
		public CathodeBool SOFTNESS = new CathodeBool(); 
		public CathodeFloat SOFTNESS_EDGE = new CathodeFloat(); 
		public CathodeBool CONVEX_GEOM = new CathodeBool(); 
		public CathodeBool FRESNEL_TERM = new CathodeBool(); 
		public CathodeBool EXPONENTIAL_DENSITY = new CathodeBool(); 
		public CathodeFloat FRESNEL_POWER = new CathodeFloat(); 
		public CathodeBool SCENE_DEPENDANT_DENSITY = new CathodeBool(); 
		public CathodeBool EARLY_ALPHA = new CathodeBool(); 
		public CathodeBool BLEND_ALPHA_OVER_DISTANCE = new CathodeBool(); 
		public CathodeBool NO_CLIP = new CathodeBool(); 
		public CathodeFloat NEAR_BLEND_DISTANCE = new CathodeFloat(); 
		public CathodeBool ALPHA_LIGHTING = new CathodeBool(); 
		public CathodeVector3 DEPTH_INTERSECT_COLOUR_VALUE = new CathodeVector3(); 
		public CathodeBool DEPTH_INTERSECT_COLOUR = new CathodeBool(); 
		public CathodeFloat DEPTH_INTERSECT_RANGE = new CathodeFloat(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeBool DISABLE_SIZE_CULLING = new CathodeBool(); 
		public CathodeBool SECONDARY_BLEND_ALPHA_OVER_DISTANCE = new CathodeBool(); 
		public CathodeFloat SECONDARY_FAR_BLEND_DISTANCE = new CathodeFloat(); 
		public CathodeFloat SECONDARY_NEAR_BLEND_DISTANCE = new CathodeFloat(); 
		public CathodeBool DYNAMIC_ALPHA_LIGHTING = new CathodeBool(); 
		public CathodeFloat DEPTH_INTERSECT_ALPHA_VALUE = new CathodeFloat(); 
		//deleted
		//INTENSITY
		//OPACITY
		//resource
	};

	//43-2E-EB-AC
	public class FollowCameraModifier: EntityMethodInterface {
		public CathodeFloat transition_duration = new CathodeFloat(); 
		public CathodeVector3 target_offset = new CathodeVector3(); 
		public CathodeEnum modifier_type = new CathodeEnum(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeFloat transition_ease_in = new CathodeFloat(); 
		public CathodeFloat movement_speed = new CathodeFloat(); 
		public CathodeFloat mouse_speed_hori = new CathodeFloat(); 
		public CathodeVector3 position_offset = new CathodeVector3(); 
		public CathodeFloat field_of_view = new CathodeFloat(); 
		public CathodeFloat mouse_speed_vert = new CathodeFloat(); 
		public CathodeFloat transition_ease_out = new CathodeFloat(); 
		public CathodeFloat vertical_limit_max = new CathodeFloat(); 
		public CathodeFloat vertical_limit_min = new CathodeFloat(); 
		public CathodeBool is_first_person = new CathodeBool(); 
		public CathodeFloat movement_speed_vertical = new CathodeFloat(); 
		public CathodeBool force_state = new CathodeBool(); 
		public CathodeFloat movement_damping = new CathodeFloat(); 
		public CathodeFloat acceleration_duration = new CathodeFloat(); 
		public CathodeFloat bone_blending_ratio = new CathodeFloat(); 
		public CathodeFloat acceleration_ease_out = new CathodeFloat(); 
		public CathodeFloat acceleration_ease_in = new CathodeFloat(); 
		public CathodeFloat horizontal_limit_min = new CathodeFloat(); 
		public CathodeFloat horizontal_limit_max = new CathodeFloat(); 
		public CathodeBool can_mirror = new CathodeBool(); 
		//position_curve
		//target_curve
		//force_state_initial_value
	};

	//B3-09-40-3E
	public class FollowTask: EntityMethodInterface {
		public CathodeFloat timeout = new CathodeFloat(); 
		public CathodeBool has_pre_move_script = new CathodeBool(); 
		public CathodeBool has_interrupt_script = new CathodeBool(); 
		public CathodeFloat reached_distance_threshold = new CathodeFloat(); 
		public CathodeEnum selection_priority = new CathodeEnum(); 
		//can_initially_end_early
		//stop_radius
	};

	//78-44-23-F9
	public class Force_UI_Visibility: EntityMethodInterface {
		public CathodeFloat show_ui = new CathodeFloat(); 
		public CathodeFloat rewire_interaction_finish = new CathodeFloat(); 
		public CathodeBool also_disable_interactions = new CathodeBool(); 
	};

	//95-7C-2B-EB
	public class FullScreenBlurSettings: EntityMethodInterface {
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat contribution = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
	};

	//22-DC-05-6D
	public class FullScreenOverlay: EntityMethodInterface {
		public CathodeString overlay_texture = new CathodeString(); 
		public CathodeFloat threshold_value = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat _58_11_2D_7F = new CathodeFloat(); 
		public CathodeFloat threshold_start = new CathodeFloat(); 
		public CathodeFloat threshold_range = new CathodeFloat(); 
		public CathodeFloat alpha_scalar = new CathodeFloat(); 
		public CathodeFloat threshold_stop = new CathodeFloat(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
		public CathodeInteger priority = new CathodeInteger(); 
	};

	//94-57-5B-9E
	public class GameDVR: EntityMethodInterface {
		public CathodeInteger start_time = new CathodeInteger(); 
		public CathodeInteger duration = new CathodeInteger(); 
		public CathodeEnum moment_ID = new CathodeEnum(); 
	};

	//EE-AD-97-2F
	public class GameOver: EntityMethodInterface {
		public CathodeBool level_tips_enabled = new CathodeBool(); 
		public CathodeString tip_string_id = new CathodeString(); 
		public CathodeBool default_tips_enabled = new CathodeBool(); 
	};

	//79-12-31-C6
	public class GameOverCredits: EntityMethodInterface {
	};

	//C6-1B-83-65
	public class GameStateChanged: EntityMethodInterface {
		//mission_number
	};

	//8C-5A-11-24
	public class GenericHighlightEntity: EntityMethodInterface {
		//highlight_geometry
	};

	//50-8E-72-23
	public class GetBlueprintAvailable: EntityMethodInterface {
		public CathodeString type = new CathodeString(); 
		//available
	};

	//0D-58-CD-EE
	public class GetBlueprintLevel: EntityMethodInterface {
		public CathodeString type = new CathodeString(); 
		//level
	};

	//BF-11-02-C6
	public class GetClosestPercentOnSpline: EntityMethodInterface {
		//spline
		//pos_to_be_near
		//position_on_spline
		//Result
		//bidirectional
	};

	//BB-EF-AD-18
	public class GetClosestPoint: EntityMethodInterface {
		//bound_to_closest
		//Positions
		//pos_to_be_near
		//position_of_closest
	};

	//09-76-04-C7
	public class GetClosestPointFromSet: EntityMethodInterface {
		//closest_is_1
		//closest_is_2
		//closest_is_3
		//closest_is_4
		//closest_is_5
		//closest_is_6
		//closest_is_7
		//closest_is_8
		//closest_is_9
		//closest_is_10
		//Position_1
		//Position_2
		//Position_3
		//Position_4
		//Position_5
		//Position_6
		//Position_7
		//Position_8
		//Position_9
		//Position_10
		//pos_to_be_near
		//position_of_closest
		//index_of_closest
	};

	//3A-61-D1-2C
	public class GetClosestPointOnSpline: EntityMethodInterface {
		public CathodeFloat look_ahead_distance = new CathodeFloat(); 
		public CathodeBool unidirectional = new CathodeBool(); 
		public CathodeFloat directional_damping_threshold = new CathodeFloat(); 
		//spline
		//pos_to_be_near
		//position_on_spline
	};

	//C5-3E-7E-9B
	public class GetCurrentCameraFov: EntityMethodInterface {
	};

	//D6-91-BD-E8
	public class GetCurrentCameraPos: EntityMethodInterface {
	};

	//CE-EC-62-39
	public class GetCurrentPlaylistLevelIndex: EntityMethodInterface {
		//index
	};

	//D0-25-76-80
	public class GetFlashFloatValue: EntityMethodInterface {
		public CathodeString callback_name = new CathodeString(); 
		//callback
		//enable_on_reset
		//float_value
	};

	//F0-AE-75-96
	public class GetFlashIntValue: EntityMethodInterface {
		public CathodeString callback_name = new CathodeString(); 
		public CathodeFloat disabled = new CathodeFloat(); 
		//callback
		//enable_on_reset
		//int_value
	};

	//85-A6-E7-50
	public class GetGatingToolLevel: EntityMethodInterface {
		public CathodeEnum tool_type = new CathodeEnum(); 
		//level
	};

	//0B-7E-D6-17
	public class GetInventoryItemName: EntityMethodInterface {
		public CathodeString equippable_item = new CathodeString(); 
		//item
	};

	//B4-5E-2E-E8
	public class GetNextPlaylistLevelName: EntityMethodInterface {
		//level_name
	};

	//69-E2-59-1C
	public class GetPlayerHasGatingTool: EntityMethodInterface {
		public CathodeEnum tool_type = new CathodeEnum(); 
		//has_tool
		//doesnt_have_tool
	};

	//87-18-B4-DC
	public class GetPlayerHasKeycard: EntityMethodInterface {
		//has_card
		//doesnt_have_card
		//card_uid
	};

	//72-12-D6-02
	public class GetPointOnSpline: EntityMethodInterface {
		//spline
		//percentage_of_spline
		//Result
	};

	//0A-4F-5D-D9
	public class GetRotation: EntityMethodInterface {
		//Input
		//Result
	};

	//7F-42-A3-1C
	public class GetSelectedCharacterId: EntityMethodInterface {
		//character_id
	};

	//7F-ED-CC-4D
	public class GetTranslation: EntityMethodInterface {
		//Input
		//Result
	};

	//78-95-28-CA
	public class GetX: EntityMethodInterface {
	};

	//3A-02-F3-71
	public class GetY: EntityMethodInterface {
	};

	//E2-B2-74-55
	public class GetZ: EntityMethodInterface {
	};

	//12-9E-2E-C3
	public class GlobalEvent: EntityMethodInterface {
		public CathodeString EventName = new CathodeString(); 
		public CathodeInteger EventValue = new CathodeInteger(); 
	};

	//AB-DE-15-5C
	public class GlobalEventMonitor: EntityMethodInterface {
		public CathodeString EventName = new CathodeString(); 
		//Event_1
		//Event_2
		//Event_3
		//Event_4
		//Event_5
		//Event_6
		//Event_7
		//Event_8
		//Event_9
		//Event_10
		//Event_11
		//Event_12
		//Event_13
		//Event_14
		//Event_15
		//Event_16
		//Event_17
		//Event_18
		//Event_19
		//Event_20
	};

	//E8-63-51-CE
	public class GlobalPosition: EntityMethodInterface {
		public CathodeString PositionName = new CathodeString(); 
	};

	//59-AA-7E-D0
	public class GoToFrontend: EntityMethodInterface {
		public CathodeEnum frontend_state = new CathodeEnum(); 
	};

	//49-21-8C-FE
	public class GPU_PFXEmitterReference: EntityMethodInterface {
		public CathodeFloat SPEED = new CathodeFloat(); 
		public CathodeInteger SPAWN_NUMBER = new CathodeInteger(); 
		public CathodeFloat LIFETIME = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat LIFETIME_VAR = new CathodeFloat(); 
		public CathodeFloat SPEED_VAR = new CathodeFloat(); 
		public CathodeFloat SPAWN_RATE = new CathodeFloat(); 
		public CathodeString EFFECT_NAME = new CathodeString(); 
		public CathodeFloat SPREAD = new CathodeFloat(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeFloat ASPECT_RATIO = new CathodeFloat(); 
		public CathodeFloat GRAVITY_STRENGTH = new CathodeFloat(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeBool _59_24_93_BA = new CathodeBool(); 
		public CathodeFloat _B3_91_C4_D0 = new CathodeFloat(); 
		public CathodeFloat SPREAD_MAX = new CathodeFloat(); 
		public CathodeFloat EMITTER_SIZE = new CathodeFloat(); 
		public CathodeFloat SPREAD_MIN = new CathodeFloat(); 
		public CathodeBool deleted = new CathodeBool(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		//mastered_by_visibility
	};

	//8F-41-26-58
	public class HackingGame: EntityMethodInterface {
		public CathodeString _DB_19_16_56 = new CathodeString(); 
		public CathodeString _67_89_5B_DF = new CathodeString(); 
		//win
		//fail
		//alarm_triggered
		//closed
		//loaded_idle
		//loaded_success
		//ui_breakout_triggered
		//resources_finished_unloading
		//resources_finished_loading
		//lock_on_reset
		//light_on_reset
		//completion_percentage
		//hacking_difficulty
		//auto_exit
	};

	//58-A9-B2-57
	public class HasAccessAtDifficulty: EntityMethodInterface {
		//difficulty
	};

	//D2-6F-17-CF
	public class HeldItem_AINotifier: EntityMethodInterface {
		//Item
		//Duration
	};

	//F5-C7-CA-4C
	public class IdleTask: EntityMethodInterface {
		public CathodeFloat reached_distance_threshold = new CathodeFloat(); 
		public CathodeBool should_auto_move_to_position = new CathodeBool(); 
		public CathodeBool _94_41_A4_85 = new CathodeBool(); 
		public CathodeEnum selection_priority = new CathodeEnum(); 
		//start_pre_move
		//start_interrupt
		//interrupted_while_moving
		//specific_character
		//ignored_for_auto_selection
		//has_pre_move_script
		//has_interrupt_script
		//filter_options
	};

	//1F-80-41-AC
	public class ImpactSphere: EntityMethodInterface {
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		//event
		//include_physics
	};

	//EB-50-79-D7
	public class InhibitActionsUntilRelease: EntityMethodInterface {
	};

	//2D-C2-52-49
	public class IntegerAbsolute: EntityMethodInterface {
	};

	//2F-2E-7C-9E
	public class IntegerAdd: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//FB-F4-5C-D0
	public class IntegerAdd_All: EntityMethodInterface {
		public CathodeInteger Numbers = new CathodeInteger(); 
	};

	//CB-9A-17-4B
	public class IntegerAnalyse: EntityMethodInterface {
		public CathodeInteger Val5 = new CathodeInteger(); 
		public CathodeInteger Val4 = new CathodeInteger(); 
		public CathodeInteger Val2 = new CathodeInteger(); 
		public CathodeInteger Val7 = new CathodeInteger(); 
		public CathodeInteger Val8 = new CathodeInteger(); 
		public CathodeInteger Val1 = new CathodeInteger(); 
		public CathodeInteger Val9 = new CathodeInteger(); 
		public CathodeInteger Val3 = new CathodeInteger(); 
		public CathodeInteger Val6 = new CathodeInteger(); 
		//Input
		//Result
		//Val0
	};

	//A4-B3-05-36
	public class IntegerAnd: EntityMethodInterface {
	};

	//5F-4D-FE-2F
	public class IntegerDivide: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//76-06-59-D2
	public class IntegerEquals: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//B3-62-12-15
	public class IntegerGreaterThan: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//9A-8B-B0-2D
	public class IntegerGreaterThanOrEqual: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//08-B6-0F-93
	public class IntegerLessThan: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//15-C4-F6-0E
	public class IntegerLessThanOrEqual: EntityMethodInterface {
	};

	//29-FE-79-B8
	public class IntegerMax: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//36-2D-B8-C1
	public class IntegerMin: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//C6-72-7D-A0
	public class IntegerMultiply: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//26-9A-81-89
	public class IntegerNotEqual: EntityMethodInterface {
		public CathodeInteger RHS = new CathodeInteger(); 
	};

	//C5-E8-A9-0B
	public class IntegerSubtract: EntityMethodInterface {
	};

	//A8-71-E6-71
	public class Interaction: EntityMethodInterface {
		public CathodeBool interruptible_on_start = new CathodeBool(); 
		//on_damaged
		//on_interrupt
		//on_killed
	};

	//CA-AA-1F-60
	public class InteractiveMovementControl: EntityMethodInterface {
		public CathodeFloat reset = new CathodeFloat(); 
		public CathodeBool can_go_both_ways = new CathodeBool(); 
		public CathodeFloat movement_threshold = new CathodeFloat(); 
		public CathodeFloat base_progress_speed = new CathodeFloat(); 
		public CathodeFloat momentum_damping = new CathodeFloat(); 
		//completed
		//duration
		//start_time
		//progress_path
		//result
		//speed
		//use_left_input_stick
		//track_bone_position
		//character_node
		//track_position
		//n:\content\build\library\ayz\animation\logichelpers\playforminduration
		//timer_expired
		//first_animation_started
		//next_animation
		//all_animations_finished
		//MinDuration
		//n:\content\build\library\archetypes\gameplay\gcip_worldpickup
		//spawn_completed
		//pickup_collected
		//Pipe
		//Gasoline
		//Explosive
		//Battery
		//Blade
		//Gel
		//Adhesive
		//BoltGun Ammo
		//Revolver Ammo
		//Shotgun Ammo
		//BoltGun
		//Revolver
		//Shotgun
		//Flare
		//Flamer Fuel
		//Flamer
		//Scrap
		//Torch Battery
		//Torch
		//Cattleprod Ammo
		//Cattleprod
		//StartOnReset
	};

	//C4-B8-3A-98
	public class Internal_JOB_SearchTarget: EntityMethodInterface {
	};

	//B7-BE-02-F8
	public class InventoryItem: EntityMethodInterface {
		public CathodeString item = new CathodeString(); 
		public CathodeString itemName = new CathodeString(); 
		public CathodeInteger gcip_instances_count = new CathodeInteger(); 
		public CathodeBool clear_on_collect = new CathodeBool(); 
		//collect
		//out_itemName
		//out_quantity
		//quantity
	};

	//CC-B8-B6-CB
	public class IrawanToneMappingSettings: EntityMethodInterface {
		public CathodeBool pause_on_reset = new CathodeBool(); 
		public CathodeFloat target_device_luminance = new CathodeFloat(); 
		public CathodeFloat superbright_adaptation = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat saccadic_time = new CathodeFloat(); 
		public CathodeFloat fb_luminance_to_candelas_per_m2 = new CathodeFloat(); 
		public CathodeFloat target_device_adaptation = new CathodeFloat(); 
		public CathodeFloat adaptation_percentile = new CathodeFloat(); 
		public CathodeFloat max_adaptation_lum = new CathodeFloat(); 
	};

	//D7-E3-FB-F1
	public class IsInstallComplete: EntityMethodInterface {
	};

	//0B-2D-55-14
	public class IsLoaded: EntityMethodInterface {
	};

	//5B-6F-75-D7
	public class IsLoading: EntityMethodInterface {
		public CathodeFloat on_true = new CathodeFloat(); 
	};

	//1B-55-4A-FF
	public class IsPlaylistTypeAll: EntityMethodInterface {
		//all
	};

	//D5-90-E8-2D
	public class IsPlaylistTypeMarathon: EntityMethodInterface {
		//marathon
	};

	//38-66-2C-57
	public class IsSpawned: EntityMethodInterface {
	};

	//19-FB-11-81
	public class JOB_AreaSweep: EntityMethodInterface {
	};

	//06-7A-63-9D
	public class JOB_Follow: EntityMethodInterface {
	};

	//F7-B4-F7-A3
	public class JOB_Follow_Centre: EntityMethodInterface {
	};

	//BB-94-A9-0C
	public class JOB_Idle: EntityMethodInterface {
		public CathodeEnum task_operation_mode = new CathodeEnum(); 
		public CathodeBool should_perform_all_tasks = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
	};

	//84-E0-0E-8D
	public class JOB_Panic: EntityMethodInterface {
	};

	//8A-DB-00-EE
	public class JOB_SystematicSearch: EntityMethodInterface {
	};

	//C1-3A-68-45
	public class JOB_SystematicSearchFlare: EntityMethodInterface {
	};

	//53-68-34-A4
	public class LeaderboardWriter: EntityMethodInterface {
		public CathodeBool star1 = new CathodeBool(); 
		//time_elapsed
		//score
		//level_number
		//grade
		//player_character
		//combat
		//stealth
		//improv
		//star2
		//star3
	};

	//02-8B-60-95
	public class LensDustSettings: EntityMethodInterface {
		public CathodeFloat DUST_MAX_REFLECTED_BLOOM_INTENSITY = new CathodeFloat(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeFloat DUST_MAX_BLOOM_INTENSITY = new CathodeFloat(); 
		public CathodeFloat DUST_BLOOM_INTENSITY_SCALAR = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat DUST_THRESHOLD = new CathodeFloat(); 
		public CathodeFloat DUST_REFLECTED_BLOOM_INTENSITY_SCALAR = new CathodeFloat(); 
		public CathodeBool _99_AC_AB_DA = new CathodeBool(); 
		public CathodeFloat _E0_DE_55_1C = new CathodeFloat(); 
		public CathodeBool _18_63_50_91 = new CathodeBool(); 
		public CathodeFloat intensity = new CathodeFloat(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
	};

	//49-52-91-4B
	public class LevelInfo: EntityMethodInterface {
		public CathodeString save_level_name_id = new CathodeString(); 
	};

	//3A-ED-82-B0
	public class LevelLoaded: EntityMethodInterface {
	};

	//0E-8D-42-1C
	public class LightAdaptationSettings: EntityMethodInterface {
		public CathodeFloat pigment_bleaching_t0 = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat fb_luminance_to_candelas_per_m2 = new CathodeFloat(); 
		public CathodeFloat slow_neural_t0 = new CathodeFloat(); 
		public CathodeFloat max_adaptation_lum = new CathodeFloat(); 
		public CathodeEnum adaptation_mechanism = new CathodeEnum(); 
		public CathodeFloat fast_neural_t0 = new CathodeFloat(); 
		public CathodeFloat high_bracket = new CathodeFloat(); 
		//min_adaptation_lum
		//adaptation_percentile
		//low_bracket
	};

	//E7-21-E0-DB
	public class LightingMaster: EntityMethodInterface {
		//light_on_reset
		//objects
	};

	//EE-2D-A6-F5
	public class LightReference: EntityMethodInterface {
		public CathodeVector3 colour = new CathodeVector3(); 
		public CathodeBool physical_attenuation = new CathodeBool(); 
		public CathodeEnum type = new CathodeEnum(); 
		public CathodeFloat area_light_radius = new CathodeFloat(); 
		public CathodeBool on = new CathodeBool(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeBool is_specular = new CathodeBool(); 
		public CathodeFloat start_attenuation = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat strip_length = new CathodeFloat(); 
		public CathodeFloat end_attenuation = new CathodeFloat(); 
		public CathodeFloat near_dist = new CathodeFloat(); 
		public CathodeBool light_on_reset = new CathodeBool(); 
		public CathodeFloat inner_cone_angle = new CathodeFloat(); 
		public CathodeBool cast_shadow = new CathodeBool(); 
		public CathodeBool include_in_planar_reflections = new CathodeBool(); 
		public CathodeFloat radiosity_multiplier = new CathodeFloat(); 
		public CathodeFloat outer_cone_angle = new CathodeFloat(); 
		public CathodeBool distance_mip_selection_gobo = new CathodeBool(); 
		public CathodeFloat volume_density = new CathodeFloat(); 
		public CathodeEnum fade_type = new CathodeEnum(); 
		public CathodeFloat volume_end_attenuation = new CathodeFloat(); 
		public CathodeFloat diffuse_softness = new CathodeFloat(); 
		public CathodeBool has_lens_flare = new CathodeBool(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeFloat aspect_ratio = new CathodeFloat(); 
		public CathodeBool is_square_light = new CathodeBool(); 
		public CathodeBool volume = new CathodeBool(); 
		public CathodeBool is_flash_light = new CathodeBool(); 
		public CathodeString gobo_texture = new CathodeString(); 
		public CathodeFloat near_dist_shadow_offset = new CathodeFloat(); 
		public CathodeBool horizontal_gobo_flip = new CathodeBool(); 
		public CathodeFloat defocus_attenuation = new CathodeFloat(); 
		public CathodeInteger slope_scale_depth_bias = new CathodeInteger(); 
		public CathodeFloat depth_bias = new CathodeFloat(); 
		public CathodeFloat flare_intensity_scale = new CathodeFloat(); 
		public CathodeFloat flare_spot_offset = new CathodeFloat(); 
		public CathodeBool no_alphalight = new CathodeBool(); 
		public CathodeFloat diffuse_bias = new CathodeFloat(); 
		public CathodeFloat glossiness_scale = new CathodeFloat(); 
		public CathodeBool has_noclip = new CathodeBool(); 
		public CathodeFloat flare_occluder_radius = new CathodeFloat(); 
		public CathodeString _2A_45_F2_A7 = new CathodeString(); 
		public CathodeInteger shadow_priority = new CathodeInteger(); 
		public CathodeVector3 volume_colour_factor = new CathodeVector3(); 
		public CathodeString occlusion_geometry = new CathodeString(); 
		public CathodeFloat _E3_D9_9F_61 = new CathodeFloat(); 
		public CathodeString _FA_32_7B_22 = new CathodeString(); 
		public CathodeString intensity = new CathodeString(); 
		//deleted
		//mastered_by_visibility
		//exclude_shadow_entities
		//intensity_multiplier
		//resource
	};

	//AF-F4-D0-C5
	public class LODControls: EntityMethodInterface {
		public CathodeBool disable_lods = new CathodeBool(); 
		//lod_range_scalar
	};

	//68-3E-C6-57
	public class Logic_MultiGate: EntityMethodInterface {
		public CathodeFloat trigger = new CathodeFloat(); 
		//Underflow
		//Pin_1
		//Pin_2
		//Pin_3
		//Pin_4
		//Pin_5
		//Pin_6
		//Pin_7
		//Pin_8
		//Pin_9
		//Pin_10
		//Pin_11
		//Pin_12
		//Pin_13
		//Pin_14
		//Pin_15
		//Pin_16
		//Pin_17
		//Pin_18
		//Pin_19
		//Pin_20
		//Overflow
		//trigger_pin
	};

	//8D-04-62-29
	public class Logic_Vent_Entrance: EntityMethodInterface {
		public CathodeBool force_stand_on_exit = new CathodeBool(); 
		//Hide_Pos
		//Emit_Pos
	};

	//BF-A1-A4-3C
	public class Logic_Vent_System: EntityMethodInterface {
		//Vent_Entrances
	};

	//08-A7-6E-8F
	public class LogicAll: EntityMethodInterface {
		public CathodeInteger num = new CathodeInteger(); 
		public CathodeBool reset_on_trigger = new CathodeBool(); 
		//Pin1_Synced
		//Pin2_Synced
		//Pin3_Synced
		//Pin4_Synced
		//Pin5_Synced
		//Pin6_Synced
		//Pin7_Synced
		//Pin8_Synced
		//Pin9_Synced
		//Pin10_Synced
	};

	//3A-6B-81-91
	public class LogicCounter: EntityMethodInterface {
		public CathodeInteger trigger_limit = new CathodeInteger(); 
		public CathodeBool non_persistent = new CathodeBool(); 
		public CathodeBool is_limitless = new CathodeBool(); 
		//on_under_limit
		//on_limit
		//on_over_limit
		//restored_on_under_limit
		//restored_on_limit
		//restored_on_over_limit
		//Count
	};

	//46-79-F9-F3
	public class LogicDelay: EntityMethodInterface {
		public CathodeFloat delay = new CathodeFloat(); 
		public CathodeBool can_suspend = new CathodeBool(); 
		//on_delay_finished
	};

	//70-C5-72-4C
	public class LogicGate: EntityMethodInterface {
		public CathodeFloat trigger = new CathodeFloat(); 
		//on_allowed
		//on_disallowed
		//allow
	};

	//6B-30-67-13
	public class LogicGateAnd: EntityMethodInterface {
	};

	//7E-F6-2E-40
	public class LogicGateEquals: EntityMethodInterface {
		public CathodeBool RHS = new CathodeBool(); 
	};

	//9D-80-3D-2A
	public class LogicGateOr: EntityMethodInterface {
	};

	//2B-27-12-61
	public class LogicNot: EntityMethodInterface {
	};

	//0F-07-6B-93
	public class LogicOnce: EntityMethodInterface {
		//on_success
		//on_failure
	};

	//43-73-E0-DF
	public class LogicPressurePad: EntityMethodInterface {
		public CathodeInteger Limit = new CathodeInteger(); 
		//Pad_Activated
		//Pad_Deactivated
		//bound_characters
		//Count
	};

	//CC-3F-42-0E
	public class LogicSwitch: EntityMethodInterface {
		public CathodeBool initial_value = new CathodeBool(); 
		public CathodeBool is_persistent = new CathodeBool(); 
		//true_now_false
		//false_now_true
		//on_true
		//on_false
		//on_restored_true
		//on_restored_false
	};

	//AC-2D-58-7A
	public class LowResFrameCapture: EntityMethodInterface {
		public CathodeBool _8A_73_C4_09 = new CathodeBool(); 
	};

	//E1-41-42-6D
	public class Map_Floor_Change: EntityMethodInterface {
		public CathodeString floor_name = new CathodeString(); 
	};

	//82-A0-8F-F5
	public class MapAnchor: EntityMethodInterface {
		public CathodeFloat map_scale = new CathodeFloat(); 
		public CathodeString keyframe = new CathodeString(); 
		public CathodeString keyframe1 = new CathodeString(); 
		public CathodeString keyframe3 = new CathodeString(); 
		public CathodeString keyframe2 = new CathodeString(); 
		public CathodeVector3 map_north = new CathodeVector3(); 
		public CathodeString keyframe4 = new CathodeString(); 
		public CathodeString keyframe5 = new CathodeString(); 
		public CathodeVector3 map_pos = new CathodeVector3(); 
		//world_pos
		//is_default_for_items
	};

	//AA-0E-13-53
	public class MapItem: EntityMethodInterface {
		public CathodeEnum item_type = new CathodeEnum(); 
		public CathodeBool show_ui_on_reset = new CathodeBool(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		//map_keyframe
	};

	//24-C2-3D-3E
	public class Master: EntityMethodInterface {
		public CathodeBool suspend_on_reset = new CathodeBool(); 
		public CathodeBool disable_collision = new CathodeBool(); 
		public CathodeBool disable_display = new CathodeBool(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeBool disable_simulation = new CathodeBool(); 
		//objects
	};

	//80-0C-30-77
	public class MELEE_WEAPON: EntityMethodInterface {
		public CathodeEnum equipment_slot = new CathodeEnum(); 
		public CathodeEnum weapon_handedness = new CathodeEnum(); 
		public CathodeString inventory_name = new CathodeString(); 
		public CathodeFloat normal_attack_damage = new CathodeFloat(); 
		public CathodeBool holsters_on_owner = new CathodeBool(); 
		public CathodeBool spawn_on_reset = new CathodeBool(); 
		public CathodeString holster_node = new CathodeString(); 
		public CathodeString character_animation_context = new CathodeString(); 
		public CathodeFloat power_attack_damage = new CathodeFloat(); 
		public CathodeFloat holster_scale = new CathodeFloat(); 
		//item_animated_model_and_collision
		//position_input
	};

	//8B-A7-14-58
	public class Minigames: EntityMethodInterface {
		public CathodeBool _F7_1C_C9_1D = new CathodeBool(); 
		public CathodeBool game_green_text_active = new CathodeBool(); 
		public CathodeBool game_docking_active = new CathodeBool(); 
		public CathodeBool game_overloc_fail_active = new CathodeBool(); 
		public CathodeBool _1C_8D_27_66 = new CathodeBool(); 
		public CathodeFloat on_failure = new CathodeFloat(); 
		public CathodeBool game_environ_ctr_active = new CathodeBool(); 
		public CathodeBool _0A_8D_56_AE = new CathodeBool(); 
		public CathodeBool _1D_C2_35_C4 = new CathodeBool(); 
		public CathodeBool game_yellow_chart_active = new CathodeBool(); 
		public CathodeBool game_inertial_damping_active = new CathodeBool(); 
		public CathodeBool _6C_4D_69_E9 = new CathodeBool(); 
		public CathodeBool _5F_DC_C0_ED = new CathodeBool(); 
		public CathodeInteger config_pass_number = new CathodeInteger(); 
		//on_success
		//config_fail_limit
		//config_difficulty
	};

	//63-4E-59-7D
	public class MissionNumber: EntityMethodInterface {
		//on_changed
	};

	//94-A8-B4-B9
	public class ModelReference: EntityMethodInterface {
		public CathodeResource resource = new CathodeResource(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeBool is_debris = new CathodeBool(); 
		public CathodeFloat intensity_multiplier = new CathodeFloat(); 
		public CathodeFloat radiosity_multiplier = new CathodeFloat(); 
		public CathodeFloat alpha_light_scale_x = new CathodeFloat(); 
		public CathodeVector3 alpha_light_average_normal = new CathodeVector3(); 
		public CathodeFloat alpha_light_scale_y = new CathodeFloat(); 
		public CathodeFloat alpha_light_offset_x = new CathodeFloat(); 
		public CathodeFloat alpha_light_offset_y = new CathodeFloat(); 
		public CathodeVector3 decal_scale = new CathodeVector3(); 
		public CathodeBool replace_intensity = new CathodeBool(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool light_on_reset = new CathodeBool(); 
		public CathodeBool simulate_on_reset = new CathodeBool(); 
		public CathodeBool replace_tint = new CathodeBool(); 
		public CathodeVector3 emissive_tint = new CathodeVector3(); 
		public CathodeFloat _8F_68_FB_43 = new CathodeFloat(); 
		public CathodeVector3 diffuse_colour_scale = new CathodeVector3(); 
		public CathodeFloat _9A_D1_62_CC = new CathodeFloat(); 
		public CathodeBool disable_size_culling = new CathodeBool(); 
		public CathodeBool cast_shadows_in_torch = new CathodeBool(); 
		public CathodeBool force_keyframed = new CathodeBool(); 
		public CathodeString material = new CathodeString(); 
		public CathodeFloat diffuse_opacity_scale = new CathodeFloat(); 
		public CathodeVector3 lightdecal_tint = new CathodeVector3(); 
		public CathodeFloat alpha_blend_noise_power_scale = new CathodeFloat(); 
		public CathodeFloat dirt_multiply_blend_spec_power_scale = new CathodeFloat(); 
		public CathodeFloat lightdecal_intensity = new CathodeFloat(); 
		public CathodeFloat vertex_opacity_scale = new CathodeFloat(); 
		public CathodeBool include_in_planar_reflections = new CathodeBool(); 
		public CathodeBool force_transparent = new CathodeBool(); 
		public CathodeFloat alpha_blend_noise_uv_scale = new CathodeFloat(); 
		public CathodeBool remove_on_damaged = new CathodeBool(); 
		public CathodeInteger damage_threshold = new CathodeInteger(); 
		public CathodeBool allow_reposition_of_physics = new CathodeBool(); 
		public CathodeVector3 vertex_colour_scale = new CathodeVector3(); 
		public CathodeFloat dirt_map_uv_scale = new CathodeFloat(); 
		public CathodeFloat alpha_blend_noise_uv_offset_X = new CathodeFloat(); 
		public CathodeFloat alpha_blend_noise_uv_offset_Y = new CathodeFloat(); 
		public CathodeBool occludes_atmosphere = new CathodeBool(); 
		public CathodeFloat _56_D1_74_E8 = new CathodeFloat(); 
		public CathodeBool deleted = new CathodeBool(); 
		public CathodeFloat uv_scroll_speed_x = new CathodeFloat(); 
		public CathodeString mapping = new CathodeString(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeFloat _B5_E4_D4_4F = new CathodeFloat(); 
		public CathodeFloat _06_7D_A2_E9 = new CathodeFloat(); 
		public CathodeFloat uv_scroll_speed_y = new CathodeFloat(); 
		public CathodeString lod_ranges = new CathodeString(); 
		//on_damaged
		//convert_to_physics
		//is_prop
		//is_thrown
		//report_sliding
		//soft_collision
		//cast_shadows
	};

	//C1-2E-E9-73
	public class MonitorActionMap: EntityMethodInterface {
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_pressed_use
		//on_released_use
		//on_pressed_crouch
		//on_released_crouch
		//on_pressed_run
		//on_released_run
		//on_pressed_aim
		//on_released_aim
		//on_pressed_shoot
		//on_released_shoot
		//on_pressed_reload
		//on_released_reload
		//on_pressed_melee
		//on_released_melee
		//on_pressed_activate_item
		//on_released_activate_item
		//on_pressed_switch_weapon
		//on_released_switch_weapon
		//on_pressed_change_dof_focus
		//on_released_change_dof_focus
		//on_pressed_select_motion_tracker
		//on_released_select_motion_tracker
		//on_pressed_select_torch
		//on_released_select_torch
		//on_pressed_torch_beam
		//on_released_torch_beam
		//on_pressed_peek
		//on_released_peek
		//on_pressed_back_close
		//on_released_back_close
		//movement_stick_x
		//movement_stick_y
		//camera_stick_x
		//camera_stick_y
		//mouse_x
		//mouse_y
		//analog_aim
		//analog_shoot
	};

	//B5-BA-4D-32
	public class MonitorPadInput: EntityMethodInterface {
		public CathodeFloat on_pressed_A = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_released_A
		//on_pressed_B
		//on_released_B
		//on_pressed_X
		//on_released_X
		//on_pressed_Y
		//on_released_Y
		//on_pressed_L1
		//on_released_L1
		//on_pressed_R1
		//on_released_R1
		//on_pressed_L2
		//on_released_L2
		//on_pressed_R2
		//on_released_R2
		//on_pressed_L3
		//on_released_L3
		//on_pressed_R3
		//on_released_R3
		//on_dpad_left
		//on_released_dpad_left
		//on_dpad_right
		//on_released_dpad_right
		//on_dpad_up
		//on_released_dpad_up
		//on_dpad_down
		//on_released_dpad_down
		//left_stick_x
		//left_stick_y
		//right_stick_x
		//right_stick_y
	};

	//EF-2C-1F-19
	public class MotionTrackerMonitor: EntityMethodInterface {
		//on_motion_sound
		//on_enter_range_sound
	};

	//B6-42-37-41
	public class MotionTrackerPing: EntityMethodInterface {
		public CathodeFloat stop_ping = new CathodeFloat(); 
		//FakePosition
	};

	//13-CF-0D-03
	public class MoveAlongSpline: EntityMethodInterface {
		public CathodeFloat speed = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_think
		//on_finished
		//spline
		//Result
	};

	//7B-19-91-94
	public class MoveInTime: EntityMethodInterface {
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		//on_finished
		//start_position
		//end_position
		//result
	};

	//74-05-81-6A
	public class MoviePlayer: EntityMethodInterface {
		public CathodeString filename = new CathodeString(); 
		public CathodeFloat trigger = new CathodeFloat(); 
		public CathodeBool skippable = new CathodeBool(); 
		public CathodeBool trigger_end_on_skipped = new CathodeBool(); 
		//start
		//end
		//skipped
		//enable_debug_skip
	};

	//D6-CF-CD-62
	public class MultipleCharacterAttachmentNode: EntityMethodInterface {
		public CathodeEnum node = new CathodeEnum(); 
		public CathodeVector3 rotation = new CathodeVector3(); 
		public CathodeBool is_cinematic = new CathodeBool(); 
		public CathodeFloat attach = new CathodeFloat(); 
		public CathodeVector3 translation = new CathodeVector3(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		//character_01
		//attachment_01
		//character_02
		//attachment_02
		//character_03
		//attachment_03
		//character_04
		//attachment_04
		//character_05
		//attachment_05
		//use_offset
	};

	//DF-25-55-72
	public class MusicController: EntityMethodInterface {
		public CathodeString music_start_event = new CathodeString(); 
		public CathodeString music_restart_event = new CathodeString(); 
		public CathodeBool _85_D5_0D_CC = new CathodeBool(); 
		public CathodeFloat smooth_rate = new CathodeFloat(); 
		public CathodeString music_end_event = new CathodeString(); 
		public CathodeFloat enable_music = new CathodeFloat(); 
		public CathodeString layer_control_rtpc = new CathodeString(); 
		public CathodeFloat enable_dynamic_rtpc = new CathodeFloat(); 
		//alien_max_distance
		//object_max_distance
	};

	//BF-63-D7-26
	public class MusicTrigger: EntityMethodInterface {
		public CathodeString music_event = new CathodeString(); 
		public CathodeBool interrupt_all = new CathodeBool(); 
		public CathodeBool trigger_once = new CathodeBool(); 
		public CathodeFloat smooth_rate = new CathodeFloat(); 
		public CathodeEnum rtpc_set_mode = new CathodeEnum(); 
		public CathodeEnum rtpc_set_return_mode = new CathodeEnum(); 
		public CathodeFloat rtpc_target_value = new CathodeFloat(); 
		public CathodeFloat rtpc_duration = new CathodeFloat(); 
		public CathodeFloat rtpc_return_value = new CathodeFloat(); 
		public CathodeFloat rtpc_value = new CathodeFloat(); 
		public CathodeFloat queue_time = new CathodeFloat(); 
		//on_triggered
		//connected_object
	};

	//5A-84-FC-3B
	public class NavMeshBarrier: EntityMethodInterface {
		public CathodeString radius = new CathodeString(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeEnum allowed_character_classes_when_closed = new CathodeEnum(); 
		public CathodeEnum allowed_character_classes_when_open = new CathodeEnum(); 
		public CathodeBool include_physics = new CathodeBool(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool opaque = new CathodeBool(); 
		//open_on_reset
		//resource
	};

	//92-E6-99-F9
	public class NavMeshExclusionArea: EntityMethodInterface {
		//position
		//half_dimensions
	};

	//44-AA-3B-1A
	public class NavMeshReachabilitySeedPoint: EntityMethodInterface {
		public CathodeTransform position = new CathodeTransform(); 
	};

	//EE-AA-E4-2E
	public class NavMeshWalkablePlatform: EntityMethodInterface {
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeString radius = new CathodeString(); 
	};

	//81-3E-B4-67
	public class NonInteractiveWater: EntityMethodInterface {
		public CathodeVector3 DEPTH_FOG_INITIAL_COLOUR = new CathodeVector3(); 
		public CathodeFloat SECONDARY_NORMAL_MAP_STRENGTH = new CathodeFloat(); 
		public CathodeFloat DEPTH_FOG_INITIAL_ALPHA = new CathodeFloat(); 
		public CathodeFloat SHININESS = new CathodeFloat(); 
		public CathodeFloat SPEED = new CathodeFloat(); 
		public CathodeFloat SCALE_Z = new CathodeFloat(); 
		public CathodeFloat DEPTH_FOG_MIDPOINT_ALPHA = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat MAX_FRESNEL = new CathodeFloat(); 
		public CathodeFloat NORMAL_MAP_STRENGTH = new CathodeFloat(); 
		public CathodeFloat FLOW_WARP_STRENGTH = new CathodeFloat(); 
		public CathodeFloat SECONDARY_SCALE = new CathodeFloat(); 
		public CathodeVector3 DEPTH_FOG_MIDPOINT_COLOUR = new CathodeVector3(); 
		public CathodeFloat FRESNEL_POWER = new CathodeFloat(); 
		public CathodeFloat MIN_FRESNEL = new CathodeFloat(); 
		public CathodeFloat SECONDARY_SPEED = new CathodeFloat(); 
		public CathodeFloat SCALE_X = new CathodeFloat(); 
		public CathodeVector3 DEPTH_FOG_END_COLOUR = new CathodeVector3(); 
		public CathodeFloat softness_edge = new CathodeFloat(); 
		public CathodeFloat DEPTH_FOG_END_ALPHA = new CathodeFloat(); 
		public CathodeFloat FLOW_TEX_SCALE = new CathodeFloat(); 
		public CathodeFloat REFLECTION_PERTURBATION_STRENGTH = new CathodeFloat(); 
		public CathodeFloat SCALE = new CathodeFloat(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		public CathodeFloat ENVIRONMENT_MAP_MULT = new CathodeFloat(); 
		public CathodeFloat DEPTH_FOG_END_DEPTH = new CathodeFloat(); 
		public CathodeFloat ALPHA_PERTURBATION_STRENGTH = new CathodeFloat(); 
		public CathodeFloat ENVMAP_SIZE = new CathodeFloat(); 
		public CathodeFloat ALPHALIGHT_MULT = new CathodeFloat(); 
		public CathodeFloat FLOW_SPEED = new CathodeFloat(); 
		public CathodeVector3 ENVMAP_BOXPROJ_BB_SCALE = new CathodeVector3(); 
		public CathodeFloat CYCLE_TIME = new CathodeFloat(); 
		public CathodeFloat DEPTH_FOG_MIDPOINT_DEPTH = new CathodeFloat(); 
		//water_resource
	};

	//D7-25-04-60
	public class NPC_Aggression_Monitor: EntityMethodInterface {
		//on_interrogative
		//on_warning
		//on_last_chance
		//on_stand_down
		//on_idle
		//on_aggressive
	};

	//EE-ED-F2-6B
	public class NPC_AlienConfig: EntityMethodInterface {
		public CathodeString AlienConfigString = new CathodeString(); 
	};

	//7C-FE-01-1B
	public class NPC_AllSensesLimiter: EntityMethodInterface {
		public CathodeFloat set_false = new CathodeFloat(); 
	};

	//E0-52-9D-4D
	public class NPC_ambush_monitor: EntityMethodInterface {
		public CathodeEnum ambush_type = new CathodeEnum(); 
		//setup
		//abandoned
		//trap_sprung
		//trigger_on_start
		//trigger_on_checkpoint_restart
	};

	//F1-34-2B-C5
	public class NPC_AreaBox: EntityMethodInterface {
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeString radius = new CathodeString(); 
	};

	//EC-5C-D2-B0
	public class NPC_behaviour_monitor: EntityMethodInterface {
		public CathodeEnum behaviour = new CathodeEnum(); 
		public CathodeBool trigger_on_start = new CathodeBool(); 
		//state_set
		//state_unset
		//trigger_on_checkpoint_restart
	};

	//8C-85-F8-AA
	public class NPC_ClearDefendArea: EntityMethodInterface {
	};

	//93-57-7B-CE
	public class NPC_ClearPursuitArea: EntityMethodInterface {
	};

	//E0-A7-16-F6
	public class NPC_Coordinator: EntityMethodInterface {
		public CathodeFloat _50_EB_D8_7C = new CathodeFloat(); 
		public CathodeInteger _69_E0_26_6F = new CathodeInteger(); 
		//Target
		//trigger_on_start
		//CheckAllNPCs
	};

	//C7-CB-16-20
	public class NPC_DefineBackstageAvoidanceArea: EntityMethodInterface {
		//AreaObjects
	};

	//27-0B-8E-AB
	public class NPC_DynamicDialogue: EntityMethodInterface {
		public CathodeFloat enable = new CathodeFloat(); 
	};

	//76-5E-05-6D
	public class NPC_FakeSense: EntityMethodInterface {
		public CathodeEnum ForceThreshold = new CathodeEnum(); 
		public CathodeEnum Sense = new CathodeEnum(); 
		public CathodeFloat triggered = new CathodeFloat(); 
		//SensedObject
		//FakePosition
	};

	//33-D0-47-03
	public class NPC_FollowOffset: EntityMethodInterface {
		//offset
		//target_to_follow
		//Result
	};

	//FE-4C-6C-9D
	public class NPC_ForceCombatTarget: EntityMethodInterface {
		public CathodeBool LockOtherAttackersOut = new CathodeBool(); 
		//Target
	};

	//7B-7B-E7-C3
	public class NPC_ForceNextJob: EntityMethodInterface {
		//job_started
		//job_completed
		//job_interrupted
		//ShouldInterruptCurrentTask
	};

	//16-08-FF-55
	public class NPC_ForceRetreat: EntityMethodInterface {
		//AreaObjects
	};

	//5E-F6-2D-0E
	public class NPC_Gain_Aggression_In_Radius: EntityMethodInterface {
		public CathodeFloat Radius = new CathodeFloat(); 
		public CathodeEnum AggressionGain = new CathodeEnum(); 
		//Position
	};

	//97-5A-A7-39
	public class NPC_Group_Death_Monitor: EntityMethodInterface {
		public CathodeBool CheckAllNPCs = new CathodeBool(); 
		//last_man_dying
		//all_killed
		//squad_coordinator
	};

	//C8-11-F7-F5
	public class NPC_Group_DeathCounter: EntityMethodInterface {
		//on_threshold
		//TriggerThreshold
	};

	//2C-41-B8-2D
	public class NPC_Highest_Awareness_Monitor: EntityMethodInterface {
		public CathodeBool CheckAllNPCs = new CathodeBool(); 
		public CathodeBool trigger_on_start = new CathodeBool(); 
		//All_Dead
		//Stunned
		//Unaware
		//Suspicious
		//SearchingArea
		//SearchingLastSensed
		//Aware
		//on_changed
	};

	//A7-0F-53-5C
	public class NPC_MeleeContext: EntityMethodInterface {
		public CathodeFloat Radius = new CathodeFloat(); 
		public CathodeEnum Context_Type = new CathodeEnum(); 
		//ConvergePos
	};

	//2E-CD-E6-86
	public class NPC_multi_behaviour_monitor: EntityMethodInterface {
		//Cinematic_set
		//Cinematic_unset
		//Damage_Response_set
		//Damage_Response_unset
		//Target_Is_NPC_set
		//Target_Is_NPC_unset
		//Breakout_set
		//Breakout_unset
		//Attack_set
		//Attack_unset
		//Stunned_set
		//Stunned_unset
		//Backstage_set
		//Backstage_unset
		//In_Vent_set
		//In_Vent_unset
		//Killtrap_set
		//Killtrap_unset
		//Threat_Aware_set
		//Threat_Aware_unset
		//Suspect_Target_Response_set
		//Suspect_Target_Response_unset
		//Player_Hiding_set
		//Player_Hiding_unset
		//Suspicious_Item_set
		//Suspicious_Item_unset
		//Search_set
		//Search_unset
		//Area_Sweep_set
		//Area_Sweep_unset
		//trigger_on_start
		//trigger_on_checkpoint_restart
	};

	//42-9D-6B-2A
	public class NPC_navmesh_type_monitor: EntityMethodInterface {
		public CathodeBool trigger_on_checkpoint_restart = new CathodeBool(); 
		public CathodeBool trigger_on_start = new CathodeBool(); 
		//state_set
		//state_unset
		//nav_mesh_type
	};

	//C8-86-3F-44
	public class NPC_NotifyDynamicDialogueEvent: EntityMethodInterface {
		public CathodeEnum DialogueEvent = new CathodeEnum(); 
	};

	//78-FB-07-0A
	public class NPC_Once: EntityMethodInterface {
		//on_success
		//on_failure
	};

	//7C-7E-AC-D6
	public class NPC_ResetSensesAndMemory: EntityMethodInterface {
		public CathodeBool ResetSensesLimiters = new CathodeBool(); 
		public CathodeBool ResetMenaceToFull = new CathodeBool(); 
	};

	//31-E1-7E-4F
	public class NPC_SenseLimiter: EntityMethodInterface {
		public CathodeEnum Sense = new CathodeEnum(); 
	};

	//C7-D4-4C-FE
	public class NPC_set_behaviour_tree_flags: EntityMethodInterface {
		public CathodeBool FlagSetting = new CathodeBool(); 
		public CathodeEnum BehaviourTreeFlag = new CathodeEnum(); 
		public CathodeFloat trigger = new CathodeFloat(); 
	};

	//FC-C1-1B-4E
	public class NPC_SetAlertness: EntityMethodInterface {
		public CathodeEnum AlertState = new CathodeEnum(); 
	};

	//49-1F-08-EF
	public class NPC_SetDefendArea: EntityMethodInterface {
		//AreaObjects
	};

	//87-AB-E3-42
	public class NPC_SetFiringAccuracy: EntityMethodInterface {
		public CathodeFloat Accuracy = new CathodeFloat(); 
	};

	//93-54-06-FF
	public class NPC_SetHidingNearestLocation: EntityMethodInterface {
		//hiding_pos
	};

	//F9-F3-80-E2
	public class NPC_SetHidingSearchRadius: EntityMethodInterface {
		//Radius
	};

	//22-35-78-10
	public class NPC_SetInvisible: EntityMethodInterface {
	};

	//70-66-6D-86
	public class NPC_SetLocomotionTargetSpeed: EntityMethodInterface {
		//Speed
	};

	//6C-3D-B8-7F
	public class NPC_SetPursuitArea: EntityMethodInterface {
		//AreaObjects
	};

	//C8-79-C7-49
	public class NPC_SetSafePoint: EntityMethodInterface {
		//SafePositions
	};

	//E4-FF-63-7C
	public class NPC_SetStartPos: EntityMethodInterface {
		//StartPos
	};

	//BF-15-7A-D5
	public class NPC_SetupMenaceManager: EntityMethodInterface {
		//AgressiveMenace
		//ProgressionFraction
		//ResetMenaceMeter
	};

	//36-05-22-80
	public class NPC_Sleeping_Android_Monitor: EntityMethodInterface {
		//Twitch
		//SitUp_Start
		//SitUp_End
		//Sleeping_GetUp
		//Sitting_GetUp
		//Android_NPC
	};

	//FA-B8-3C-9B
	public class NPC_Squad_DialogueMonitor: EntityMethodInterface {
		//Suspicious_Item_Initial
		//Suspicious_Item_Close
		//Suspicious_Warning
		//Suspicious_Warning_Fail
		//Missing_Buddy
		//Search_Started
		//Search_Loop
		//Search_Complete
		//Detected_Enemy
		//Alien_Heard_Backstage
		//Interrogative
		//Warning
		//Last_Chance
		//Stand_Down
		//Attack
		//Advance
		//Melee
		//Hit_By_Weapon
		//Go_to_Cover
		//No_Cover
		//Shoot_From_Cover
		//Cover_Broken
		//Retreat
		//Panic
		//Final_Hit
		//Ally_Death
		//Incoming_IED
		//Alert_Squad
		//My_Death
		//Idle_Passive
		//Idle_Aggressive
		//Block
		//Enter_Grapple
		//Grapple_From_Cover
		//Player_Observed
		//squad_coordinator
	};

	//97-59-8E-F3
	public class NPC_StopAiming: EntityMethodInterface {
	};

	//DA-A2-16-97
	public class NPC_SuspiciousItem: EntityMethodInterface {
		public CathodeBool DoCloseToReactionSubsequentGroupMember = new CathodeBool(); 
		public CathodeBool UseSamePriorityRecentTimeConstraint = new CathodeBool(); 
		public CathodeFloat RetriggerDelay = new CathodeFloat(); 
		public CathodeBool MoveCloseToSuspectPositionSubsequentGroupMember = new CathodeBool(); 
		public CathodeBool MoveCloseToSuspectPosition = new CathodeBool(); 
		public CathodeFloat FurtherReactionValidStartDuration = new CathodeFloat(); 
		public CathodeBool AllowSamePriorityToOveride = new CathodeBool(); 
		public CathodeBool DoSystematicSearch = new CathodeBool(); 
		public CathodeEnum Item = new CathodeEnum(); 
		public CathodeEnum BehaviourTreePriority = new CathodeEnum(); 
		public CathodeEnum GroupNotify = new CathodeEnum(); 
		public CathodeFloat start = new CathodeFloat(); 
		public CathodeBool DoIntialReactionSubsequentGroupMember = new CathodeBool(); 
		public CathodeFloat enter = new CathodeFloat(); 
		public CathodeBool DoCloseToWaitForGroupMembers = new CathodeBool(); 
		public CathodeBool DoCloseToReaction = new CathodeBool(); 
		public CathodeBool DoSystematicSearchSubsequentGroupMember = new CathodeBool(); 
		public CathodeBool DoCloseToWaitForGroupMembersSubsequentGroupMember = new CathodeBool(); 
		public CathodeInteger MaxGroupMembersInteract = new CathodeInteger(); 
		public CathodeBool DoIntialReaction = new CathodeBool(); 
		public CathodeEnum Trigger = new CathodeEnum(); 
		public CathodeFloat SamePriorityRecentTimeConstraint = new CathodeFloat(); 
		public CathodeFloat InitialReactionValidStartDuration = new CathodeFloat(); 
		public CathodeBool DetectableByBackstageAlien = new CathodeBool(); 
		public CathodeFloat SystematicSearchRadius = new CathodeFloat(); 
		public CathodeInteger InteruptSubPriority = new CathodeInteger(); 
		public CathodeFloat SamePriorityCloserDistanceConstraint = new CathodeFloat(); 
		public CathodeBool UseSamePriorityCloserDistanceConstraint = new CathodeBool(); 
		//ItemPosition
		//ShouldMakeAggressive
	};

	//12-5B-1F-88
	public class NPC_TargetAcquire: EntityMethodInterface {
		//no_targets
	};

	//21-EF-7E-61
	public class NPC_TriggerAimRequest: EntityMethodInterface {
		public CathodeBool Raise_gun = new CathodeBool(); 
		public CathodeBool clear_current_requests = new CathodeBool(); 
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeFloat triggered = new CathodeFloat(); 
		public CathodeBool use_current_target = new CathodeBool(); 
		public CathodeFloat clamp_angle = new CathodeFloat(); 
		//started_aiming
		//finished_aiming
		//interrupted
		//AimTarget
	};

	//F6-78-0C-25
	public class NPC_TriggerShootRequest: EntityMethodInterface {
		public CathodeInteger shot_count = new CathodeInteger(); 
		public CathodeBool clear_current_requests = new CathodeBool(); 
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeBool empty_current_clip = new CathodeBool(); 
		//started_shooting
		//finished_shooting
		//interrupted
	};

	//15-17-DB-A8
	public class NPC_WithdrawAlien: EntityMethodInterface {
		public CathodeBool permanent = new CathodeBool(); 
		public CathodeFloat time_to_force = new CathodeFloat(); 
		public CathodeBool killtraps = new CathodeBool(); 
		public CathodeBool allow_any_searches_to_complete = new CathodeBool(); 
		public CathodeFloat timed_out_radius = new CathodeFloat(); 
		public CathodeFloat initial_radius = new CathodeFloat(); 
	};

	//45-5C-59-BA
	public class PadLightBar: EntityMethodInterface {
		public CathodeFloat reset = new CathodeFloat(); 
		//colour
	};

	//3A-97-0F-73
	public class ParticleEmitterReference: EntityMethodInterface {
		public CathodeInteger LAUNCH_DECELERATE_SPEED = new CathodeInteger(); 
		public CathodeInteger ROTATION_RANDOM_START = new CathodeInteger(); 
		public CathodeFloat SIZE_END_MAX = new CathodeFloat(); 
		public CathodeInteger TEXTURE_ANIMATION = new CathodeInteger(); 
		public CathodeFloat SIZE_START_MAX = new CathodeFloat(); 
		public CathodeFloat EMISSION_AREA_X = new CathodeFloat(); 
		public CathodeInteger SPAWN_NUMBER = new CathodeInteger(); 
		public CathodeInteger LIGHTING = new CathodeInteger(); 
		public CathodeInteger START_MID_END_SPEED = new CathodeInteger(); 
		public CathodeFloat ROTATION_MAX = new CathodeFloat(); 
		public CathodeFloat ROTATION_MIN = new CathodeFloat(); 
		public CathodeFloat FADE_NEAR_CAMERA_THRESHOLD = new CathodeFloat(); 
		public CathodeFloat LIFETIME = new CathodeFloat(); 
		public CathodeFloat PARTICLE_EXPIRY_TIME_MAX = new CathodeFloat(); 
		public CathodeInteger ROTATION = new CathodeInteger(); 
		public CathodeFloat ALPHA_IN = new CathodeFloat(); 
		public CathodeFloat SOFTNESS_ALPHA_THICKNESS = new CathodeFloat(); 
		public CathodeInteger COLOUR_TINT = new CathodeInteger(); 
		public CathodeFloat LIFETIME_VAR = new CathodeFloat(); 
		public CathodeFloat FADE_AT_DISTANCE = new CathodeFloat(); 
		public CathodeFloat GRAVITY_STRENGTH = new CathodeFloat(); 
		public CathodeFloat SYSTEM_EXPIRY_TIME = new CathodeFloat(); 
		public CathodeString TEXTURE_MAP = new CathodeString(); 
		public CathodeFloat LAUNCH_DECELERATE_SPEED_START_MIN = new CathodeFloat(); 
		public CathodeInteger EMISSION_AREA = new CathodeInteger(); 
		public CathodeInteger ANIMATED_ALPHA = new CathodeInteger(); 
		public CathodeInteger CPU = new CathodeInteger(); 
		public CathodeInteger LOOPING = new CathodeInteger(); 
		public CathodeFloat SIZE_START_MIN = new CathodeFloat(); 
		public CathodeFloat EMISSION_AREA_Y = new CathodeFloat(); 
		public CathodeVector3 COLOUR_TINT_END = new CathodeVector3(); 
		public CathodeFloat WIND_X = new CathodeFloat(); 
		public CathodeInteger FADE_NEAR_CAMERA = new CathodeInteger(); 
		public CathodeFloat SPREAD_MIN = new CathodeFloat(); 
		public CathodeFloat WORLD_TO_LOCAL_BLEND_START = new CathodeFloat(); 
		public CathodeString material = new CathodeString(); 
		public CathodeFloat LAUNCH_DECELERATE_SPEED_START_MAX = new CathodeFloat(); 
		public CathodeFloat WORLD_TO_LOCAL_BLEND_END = new CathodeFloat(); 
		public CathodeFloat SPAWN_RATE = new CathodeFloat(); 
		public CathodeFloat EMISSION_AREA_Z = new CathodeFloat(); 
		public CathodeFloat WIND_Z = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeInteger NUM_ROWS = new CathodeInteger(); 
		public CathodeInteger PARTICLE_COUNT = new CathodeInteger(); 
		public CathodeInteger TEXTURE_ANIMATION_FRAMES = new CathodeInteger(); 
		public CathodeInteger SOFTNESS = new CathodeInteger(); 
		public CathodeFloat SPREAD = new CathodeFloat(); 
		public CathodeFloat ALPHA_OUT = new CathodeFloat(); 
		public CathodeFloat SIZE_END_MIN = new CathodeFloat(); 
		public CathodeFloat SOFTNESS_EDGE = new CathodeFloat(); 
		public CathodeFloat LAUNCH_DECELERATE_DEC_RATE = new CathodeFloat(); 
		public CathodeFloat PARTICLE_EXPIRY_TIME_MIN = new CathodeFloat(); 
		public CathodeInteger GRAVITY = new CathodeInteger(); 
		public CathodeFloat WIND_Y = new CathodeFloat(); 
		public CathodeVector3 COLOUR_TINT_START = new CathodeVector3(); 
		public CathodeInteger Y_AXIS_FLIP = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING_ON_AXIS_Z = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING_ON_AXIS_Y = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING_VELOCITY_STRETCHED = new CathodeInteger(); 
		public CathodeFloat FADE_NEAR_CAMERA_MAX_DIST = new CathodeFloat(); 
		public CathodeFloat MASK_AMOUNT_MAX = new CathodeFloat(); 
		public CathodeFloat PIVOT_OFFSET_MAX = new CathodeFloat(); 
		public CathodeFloat MASK_AMOUNT_MIN = new CathodeFloat(); 
		public CathodeFloat _A2_13_0F_1D = new CathodeFloat(); 
		public CathodeInteger PER_PARTICLE_LIGHTING = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING_ON_AXIS_FADEOUT = new CathodeInteger(); 
		public CathodeFloat _CA_B0_DF_26 = new CathodeFloat(); 
		public CathodeFloat ASPECT_RATIO = new CathodeFloat(); 
		public CathodeInteger BLENDING_ALPHA_REF = new CathodeInteger(); 
		public CathodeInteger PIVOT_AND_TURBULENCE = new CathodeInteger(); 
		public CathodeFloat TURBULENCE_AMOUNT_MIN = new CathodeFloat(); 
		public CathodeString _77_42_D6_41 = new CathodeString(); 
		public CathodeString _88_51_66_42 = new CathodeString(); 
		public CathodeString _E1_12_44_43 = new CathodeString(); 
		public CathodeFloat PIVOT_Y = new CathodeFloat(); 
		public CathodeFloat TEXTURE_ANIMATION_LOOP_COUNT = new CathodeFloat(); 
		public CathodeFloat SPEED_END_MIN = new CathodeFloat(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeFloat SPEED_MID_MAX = new CathodeFloat(); 
		public CathodeInteger DRAW_PASS = new CathodeInteger(); 
		public CathodeFloat ROTATION_DAMP = new CathodeFloat(); 
		public CathodeFloat MASK_AMOUNT_MIDPOINT = new CathodeFloat(); 
		public CathodeInteger NONE = new CathodeInteger(); 
		public CathodeInteger LOW_RES = new CathodeInteger(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeInteger BLENDING_ADDITIVE = new CathodeInteger(); 
		public CathodeString _D5_55_1E_68 = new CathodeString(); 
		public CathodeVector3 AMBIENT_LIGHTING_COLOUR = new CathodeVector3(); 
		public CathodeFloat GRAVITY_MAX_STRENGTH = new CathodeFloat(); 
		public CathodeInteger COLOUR_RAMP = new CathodeInteger(); 
		public CathodeString _F0_F1_82_78 = new CathodeString(); 
		public CathodeFloat TURBULENCE_AMOUNT_MAX = new CathodeFloat(); 
		public CathodeString _45_F7_7E_7D = new CathodeString(); 
		public CathodeFloat SPEED_START_MIN = new CathodeFloat(); 
		public CathodeFloat COLOUR_SCALE_MIN = new CathodeFloat(); 
		public CathodeInteger BLENDING_PREMULTIPLIED = new CathodeInteger(); 
		public CathodeString _B5_11_18_97 = new CathodeString(); 
		public CathodeInteger X_AXIS_FLIP = new CathodeInteger(); 
		public CathodeInteger BLENDING_DISTORTION = new CathodeInteger(); 
		public CathodeFloat PIVOT_OFFSET_MIN = new CathodeFloat(); 
		public CathodeInteger BILLBOARDING_ON_AXIS_X = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING_VELOCITY_ALIGNED = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING_NONE = new CathodeInteger(); 
		public CathodeString _20_9E_43_C6 = new CathodeString(); 
		public CathodeString _F5_4D_28_C7 = new CathodeString(); 
		public CathodeFloat TURBULENCE_FREQUENCY_MAX = new CathodeFloat(); 
		public CathodeInteger ALPHA_TEST = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING = new CathodeInteger(); 
		public CathodeInteger SPREAD_FEATURE = new CathodeInteger(); 
		public CathodeInteger AMBIENT_LIGHTING = new CathodeInteger(); 
		public CathodeInteger BLENDING_STANDARD = new CathodeInteger(); 
		public CathodeFloat SOFTNESS_ALPHA_DEPTH_MODIFIER = new CathodeFloat(); 
		public CathodeInteger _E9_13_19_D6 = new CathodeInteger(); 
		public CathodeFloat SPEED_MID_MIN = new CathodeFloat(); 
		public CathodeFloat TURBULENCE_FREQUENCY_MIN = new CathodeFloat(); 
		public CathodeString _5F_16_AB_EF = new CathodeString(); 
		public CathodeFloat COLOUR_SCALE_MAX = new CathodeFloat(); 
		public CathodeFloat ROTATION_VAR = new CathodeFloat(); 
		public CathodeFloat SPEED_END_MAX = new CathodeFloat(); 
		public CathodeFloat SPEED_START_MAX = new CathodeFloat(); 
		public CathodeString COLOUR_RAMP_MAP = new CathodeString(); 
		public CathodeFloat ALPHA_REF_VALUE = new CathodeFloat(); 
		public CathodeInteger BILLBOARDING_SPHERE_PROJECTION = new CathodeInteger(); 
		public CathodeInteger SUB_FRAME_BLEND = new CathodeInteger(); 
		public CathodeInteger EMISSION_DIRECTION_SURFACE = new CathodeInteger(); 
		public CathodeInteger AREA_CUBOID = new CathodeInteger(); 
		public CathodeInteger COLOUR_RAMP_ALPHA = new CathodeInteger(); 
		public CathodeInteger AREA_CYLINDER = new CathodeInteger(); 
		public CathodeFloat ROTATION_IN = new CathodeFloat(); 
		public CathodeInteger WRAP_FRAMES = new CathodeInteger(); 
		public CathodeInteger BILLBOARD_FACING = new CathodeInteger(); 
		public CathodeInteger EMISSION_SURFACE = new CathodeInteger(); 
		public CathodeInteger AREA_SPHEROID = new CathodeInteger(); 
		public CathodeInteger COLOUR_USE_MID = new CathodeInteger(); 
		public CathodeString _3D_F9_79_1C = new CathodeString(); 
		public CathodeFloat COLOUR_MIDPOINT = new CathodeFloat(); 
		public CathodeString _DF_25_E9_5E = new CathodeString(); 
		public CathodeVector3 COLOUR_TINT_MID = new CathodeVector3(); 
		public CathodeString _DE_44_FC_F5 = new CathodeString(); 
		public CathodeBool use_local_rotation = new CathodeBool(); 
		public CathodeVector3 bounds_max = new CathodeVector3(); 
		public CathodeInteger NO_ANIM = new CathodeInteger(); 
		public CathodeInteger RANDOM_START_FRAME = new CathodeInteger(); 
		public CathodeVector3 bounds_min = new CathodeVector3(); 
		public CathodeFloat ROTATION_BASE = new CathodeFloat(); 
		public CathodeBool unique_material = new CathodeBool(); 
		public CathodeInteger FLOW_UV_ANIMATION = new CathodeInteger(); 
		public CathodeFloat FLOW_SPEED = new CathodeFloat(); 
		public CathodeFloat FLOW_WARP_STRENGTH = new CathodeFloat(); 
		public CathodeString FLOW_TEXTURE_MAP = new CathodeString(); 
		public CathodeString FLOW_MAP = new CathodeString(); 
		public CathodeInteger NO_CLIP = new CathodeInteger(); 
		public CathodeFloat WORLD_TO_LOCAL_MAX_DIST = new CathodeFloat(); 
		public CathodeInteger ROTATION_RAMP = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING_LS = new CathodeInteger(); 
		public CathodeFloat SPAWN_RATE_VAR = new CathodeFloat(); 
		public CathodeFloat PIVOT_X = new CathodeFloat(); 
		public CathodeFloat ROTATION_OUT = new CathodeFloat(); 
		public CathodeBool deleted = new CathodeBool(); 
		public CathodeInteger _2B_D3_EE_02 = new CathodeInteger(); 
		public CathodeInteger _A4_62_18_07 = new CathodeInteger(); 
		public CathodeInteger ALPHATHRESHOLD = new CathodeInteger(); 
		public CathodeFloat ALPHATHRESHOLD_TOTALTIME = new CathodeFloat(); 
		public CathodeFloat _F6_08_25_5F = new CathodeFloat(); 
		public CathodeFloat ALPHATHRESHOLD_BEGINSTOP = new CathodeFloat(); 
		public CathodeFloat _4C_EC_42_6D = new CathodeFloat(); 
		public CathodeFloat ALPHATHRESHOLD_RANGE = new CathodeFloat(); 
		public CathodeFloat ALPHATHRESHOLD_BEGINSTART = new CathodeFloat(); 
		public CathodeFloat _6E_90_91_AB = new CathodeFloat(); 
		public CathodeFloat DEPTH_FADE_AXIS_PERCENT = new CathodeFloat(); 
		public CathodeInteger _89_3B_D9_BB = new CathodeInteger(); 
		public CathodeFloat ALPHATHRESHOLD_ENDSTART = new CathodeFloat(); 
		public CathodeFloat DEPTH_FADE_AXIS_DIST = new CathodeFloat(); 
		public CathodeFloat _39_1C_C6_DE = new CathodeFloat(); 
		public CathodeInteger DEPTH_FADE_AXIS = new CathodeInteger(); 
		public CathodeFloat ALPHATHRESHOLD_ENDSTOP = new CathodeFloat(); 
		public CathodeFloat CYCLE_TIME = new CathodeFloat(); 
		public CathodeFloat FLOW_TEX_SCALE = new CathodeFloat(); 
		public CathodeFloat REVERSE_SOFTNESS_EDGE = new CathodeFloat(); 
		public CathodeInteger REVERSE_SOFTNESS = new CathodeInteger(); 
		public CathodeInteger ZTEST = new CathodeInteger(); 
		public CathodeFloat DISTORTION_STRENGTH = new CathodeFloat(); 
		public CathodeInteger DISTORTION_OCCLUSION = new CathodeInteger(); 
		public CathodeFloat CAMERA_RELATIVE_POS_Z = new CathodeFloat(); 
		public CathodeInteger CUSTOM_SEED_CPU = new CathodeInteger(); 
		public CathodeInteger BILLBOARDING_CAMERA_LOCKED = new CathodeInteger(); 
		public CathodeInteger SEED = new CathodeInteger(); 
		public CathodeFloat CELL_MAX_DIST = new CathodeFloat(); 
		public CathodeInteger CELL_EMISSION = new CathodeInteger(); 
		public CathodeInteger EARLY_ALPHA = new CathodeInteger(); 
		public CathodeString _7B_62_C9_E7 = new CathodeString(); 
		public CathodeFloat CAMERA_RELATIVE_POS_X = new CathodeFloat(); 
		public CathodeFloat CAMERA_RELATIVE_POS_Y = new CathodeFloat(); 
		public CathodeFloat SPHERE_PROJECTION_RADIUS = new CathodeFloat(); 
		public CathodeFloat _F5_FA_8C_22 = new CathodeFloat(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeBool include_in_planar_reflections = new CathodeBool(); 
		public CathodeInteger INFINITE_PROJECTION = new CathodeInteger(); 
		public CathodeInteger quality_level = new CathodeInteger(); 
		public CathodeString _B5_4A_6D_05 = new CathodeString(); 
		public CathodeString _31_AD_31_63 = new CathodeString(); 
		public CathodeString _CE_3B_ED_A5 = new CathodeString(); 
		//mastered_by_visibility
		//SCALE_MODIFIER
		//PARALLAX_POSITION
		//resource
	};

	//FD-60-B9-1A
	public class PathfindingAlienBackstageNode: EntityMethodInterface {
		public CathodeFloat cancel_hit_by_flamethrower = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat open = new CathodeFloat(); 
		public CathodeBool open_on_reset = new CathodeBool(); 
		public CathodeFloat extra_cost = new CathodeFloat(); 
		//started_animating_Entry
		//stopped_animating_Entry
		//started_animating_Exit
		//stopped_animating_Exit
		//killtrap_anim_started
		//killtrap_anim_stopped
		//killtrap_fx_start
		//killtrap_fx_stop
		//on_loaded
		//PlayAnimData_Entry
		//PlayAnimData_Exit
		//Killtrap_alien
		//Killtrap_victim
		//build_into_navmesh
		//top
		//network_id
	};

	//C7-9C-6E-50
	public class PathfindingManualNode: EntityMethodInterface {
		public CathodeTransform destination = new CathodeTransform(); 
		public CathodeEnum character_classes = new CathodeEnum(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool open_on_reset = new CathodeBool(); 
		public CathodeFloat extra_cost = new CathodeFloat(); 
		//character_arriving
		//character_stopped
		//started_animating
		//stopped_animating
		//on_loaded
		//PlayAnimData
		//build_into_navmesh
	};

	//AA-60-BE-28
	public class PathfindingTeleportNode: EntityMethodInterface {
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool open_on_reset = new CathodeBool(); 
		//started_teleporting
		//stopped_teleporting
		//destination
		//build_into_navmesh
		//extra_cost
		//character_classes
	};

	//00-20-35-67
	public class PathfindingWaitNode: EntityMethodInterface {
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool open_on_reset = new CathodeBool(); 
		public CathodeFloat extra_cost = new CathodeFloat(); 
		//character_getting_near
		//character_arriving
		//character_stopped
		//started_waiting
		//stopped_waiting
		//destination
		//build_into_navmesh
		//character_classes
	};

	//B4-94-52-E7
	public class PhysicsApplyImpulse: EntityMethodInterface {
		public CathodeFloat force = new CathodeFloat(); 
		public CathodeVector3 offset = new CathodeVector3(); 
		public CathodeVector3 direction = new CathodeVector3(); 
		public CathodeBool can_damage = new CathodeBool(); 
		//objects
	};

	//FB-C0-DB-0D
	public class PhysicsApplyVelocity: EntityMethodInterface {
		public CathodeVector3 linear_velocity = new CathodeVector3(); 
		public CathodeFloat trigger = new CathodeFloat(); 
		public CathodeVector3 angular_velocity = new CathodeVector3(); 
		public CathodeFloat propulsion_velocity = new CathodeFloat(); 
		//objects
	};

	//3D-7A-B3-D8
	public class PhysicsModifyGravity: EntityMethodInterface {
		public CathodeFloat _2E_E5_80_CB = new CathodeFloat(); 
		//float_on_reset
		//objects
	};

	//21-CF-6C-DD
	public class PhysicsSystem: EntityMethodInterface {
		public CathodeInteger system_index = new CathodeInteger(); 
	};

	//6A-12-C0-53
	public class PickupSpawner: EntityMethodInterface {
		public CathodeInteger item_quantity = new CathodeInteger(); 
		public CathodeBool spawn_on_reset = new CathodeBool(); 
		//collect
		//pos
		//item_name
	};

	//E8-AD-2E-5E
	public class Planet: EntityMethodInterface {
		public CathodeFloat light_wrap_angle_scalar = new CathodeFloat(); 
		public CathodeFloat penumbra_falloff_power_scalar = new CathodeFloat(); 
		public CathodeFloat lens_flare_brightness = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeVector3 global_tint = new CathodeVector3(); 
		public CathodeFloat overbright_scalar = new CathodeFloat(); 
		public CathodeInteger planet_sort_key = new CathodeInteger(); 
		public CathodeFloat override_global_tint = new CathodeFloat(); 
		public CathodeFloat atmosphere_edge_transparency = new CathodeFloat(); 
		public CathodeVector3 lens_flare_colour = new CathodeVector3(); 
		public CathodeFloat atmosphere_edge_falloff_power = new CathodeFloat(); 
		public CathodeFloat light_shaft_density = new CathodeFloat(); 
		public CathodeFloat flow_warp_strength = new CathodeFloat(); 
		public CathodeFloat terrain_normal_strength = new CathodeFloat(); 
		public CathodeFloat detail_uv_scale = new CathodeFloat(); 
		public CathodeFloat atmosphere_detail_scroll_speed = new CathodeFloat(); 
		public CathodeFloat _F1_97_6F_25 = new CathodeFloat(); 
		public CathodeFloat light_shaft_decay = new CathodeFloat(); 
		public CathodeFloat flow_speed = new CathodeFloat(); 
		public CathodeBool light_shaft_source_occlusion = new CathodeBool(); 
		public CathodeFloat flow_cycle_time = new CathodeFloat(); 
		public CathodeFloat light_shaft_min_occlusion_distance = new CathodeFloat(); 
		public CathodeFloat flow_tex_scale = new CathodeFloat(); 
		public CathodeFloat atmosphere_normal_strength = new CathodeFloat(); 
		public CathodeFloat atmosphere_scroll_speed = new CathodeFloat(); 
		public CathodeFloat terrain_uv_scale = new CathodeFloat(); 
		public CathodeFloat normal_uv_scale = new CathodeFloat(); 
		public CathodeBool blocks_light_shafts = new CathodeBool(); 
		public CathodeFloat light_shaft_range = new CathodeFloat(); 
		//planet_resource
		//parallax_position
		//sun_position
		//light_shaft_source_position
		//parallax_scale
		//light_shaft_colour
		//light_shaft_intensity
	};

	//29-DF-DE-39
	public class PlatformConstantBool: EntityMethodInterface {
		public CathodeBool X360 = new CathodeBool(); 
		public CathodeBool PS3 = new CathodeBool(); 
		public CathodeBool NextGen = new CathodeBool(); 
	};

	//1C-56-BD-6D
	public class PlatformConstantFloat: EntityMethodInterface {
		public CathodeFloat X360 = new CathodeFloat(); 
		public CathodeFloat PS3 = new CathodeFloat(); 
		public CathodeFloat NextGen = new CathodeFloat(); 
	};

	//51-49-52-F6
	public class PlatformConstantInt: EntityMethodInterface {
		public CathodeInteger X360 = new CathodeInteger(); 
		public CathodeInteger PS3 = new CathodeInteger(); 
		public CathodeInteger NextGen = new CathodeInteger(); 
	};

	//0A-84-9C-5C
	public class PlayEnvironmentAnimation: EntityMethodInterface {
		public CathodeString AnimationSet = new CathodeString(); 
		public CathodeBool jump_to_the_end_on_play = new CathodeBool(); 
		public CathodeString Animation = new CathodeString(); 
		public CathodeInteger end_frame = new CathodeInteger(); 
		public CathodeBool loop = new CathodeBool(); 
		public CathodeInteger start_frame = new CathodeInteger(); 
		public CathodeBool is_cinematic = new CathodeBool(); 
		public CathodeInteger shot_number = new CathodeInteger(); 
		public CathodeFloat play_speed = new CathodeFloat(); 
		//on_finished
		//on_finished_streaming
		//play_on_reset
		//geometry
		//marker
		//external_start_time
		//external_time
		//animation_length
		//animation_info
	};

	//AA-93-DC-54
	public class Player_ExploitableArea: EntityMethodInterface {
		//NpcSafePositions
	};

	//53-0D-AD-64
	public class Player_Sensor: EntityMethodInterface {
		public CathodeBool start_on_reset = new CathodeBool(); 
		//Standard
		//Running
		//Aiming
		//Vent
		//Grapple
		//Death
		//Cover
		//Motion_Tracked
		//Motion_Tracked_Vent
		//Leaning
	};

	//4D-ED-77-B0
	public class PlayerCamera: EntityMethodInterface {
	};

	//6A-86-B0-92
	public class PlayerCameraMonitor: EntityMethodInterface {
		//AndroidNeckSnap
		//AlienKill
		//AlienKillBroken
		//AlienKillInVent
		//StandardAnimDrivenView
		//StopNonStandardCameras
	};

	//77-63-D7-9F
	public class PlayerDeathCounter: EntityMethodInterface {
		public CathodeInteger Limit = new CathodeInteger(); 
		//on_limit
		//above_limit
		//filter
		//count
	};

	//E8-4D-34-8B
	public class PlayerDiscardsWeapons: EntityMethodInterface {
		public CathodeBool discard_cattleprod = new CathodeBool(); 
		public CathodeBool discard_melee = new CathodeBool(); 
		//discard_pistol
		//discard_shotgun
		//discard_flamethrower
		//discard_boltgun
	};

	//82-FD-16-C9
	public class PlayerHasEnoughItems: EntityMethodInterface {
		public CathodeInteger quantity = new CathodeInteger(); 
		//items
	};

	//C4-80-57-68
	public class PlayerHasItem: EntityMethodInterface {
		//items
	};

	//9C-3F-7D-66
	public class PlayerHasItemWithName: EntityMethodInterface {
		public CathodeString item_name = new CathodeString(); 
	};

	//00-18-B0-51
	public class PlayerHasSpaceForItem: EntityMethodInterface {
		//items
	};

	//19-E2-41-C1
	public class PlayerKilledAllyMonitor: EntityMethodInterface {
		//ally_killed
		//start_on_reset
	};

	//47-DD-BD-6B
	public class PlayerTorch: EntityMethodInterface {
		//requested_torch_holster
		//requested_torch_draw
		//start_on_reset
		//power_in_current_battery
		//battery_count
	};

	//D3-FA-3E-2F
	public class PlayerTriggerBox: EntityMethodInterface {
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeString radius = new CathodeString(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		//on_entered
		//on_exited
	};

	//7A-7E-93-46
	public class PlayerUseTriggerBox: EntityMethodInterface {
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeString text = new CathodeString(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeString radius = new CathodeString(); 
		public CathodeFloat disable = new CathodeFloat(); 
		//on_entered
		//on_exited
		//on_use
	};

	//11-83-9E-20
	public class PointAt: EntityMethodInterface {
		//origin
		//target
		//Result
	};

	//ED-9E-97-15
	public class PointTracker: EntityMethodInterface {
		public CathodeFloat max_speed = new CathodeFloat(); 
		public CathodeFloat damping_factor = new CathodeFloat(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeVector3 target_offset = new CathodeVector3(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//origin
		//target
		//result
		//origin_offset
	};

	//39-73-2C-C5
	public class PopupMessage: EntityMethodInterface {
		public CathodeString main_text = new CathodeString(); 
		public CathodeFloat start = new CathodeFloat(); 
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeEnum sound_event = new CathodeEnum(); 
		public CathodeString header_text = new CathodeString(); 
		//display
		//finished
		//icon_keyframe
	};

	//EA-FC-96-92
	public class PositionDistance: EntityMethodInterface {
		//LHS
		//RHS
		//Result
	};

	//C1-46-0F-21
	public class PositionMarker: EntityMethodInterface {
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
	};

	//0A-DC-C9-42
	public class ProjectileMotion: EntityMethodInterface {
		//on_think
		//on_finished
		//start_pos
		//start_velocity
		//duration
		//Current_Position
		//Current_Velocity
	};

	//55-57-2E-36
	public class ProjectileMotionComplex: EntityMethodInterface {
		//on_think
		//on_finished
		//start_position
		//start_velocity
		//start_angular_velocity
		//flight_time_in_seconds
		//current_position
		//current_velocity
		//current_angular_velocity
		//current_flight_time_in_seconds
	};

	//C4-6A-16-51
	public class ProximityDetector: EntityMethodInterface {
		public CathodeFloat proximity_duration = new CathodeFloat(); 
		//in_proximity
		//filter
		//detector_position
		//min_distance
		//max_distance
		//requires_line_of_sight
	};

	//3D-AE-6B-3D
	public class ProximityTrigger: EntityMethodInterface {
		public CathodeFloat ignition_range = new CathodeFloat(); 
		public CathodeFloat fire_spread_rate = new CathodeFloat(); 
		//ignited
		//electrified
		//drenched
		//poisoned
		//water_permeate_rate
		//electrical_conduction_rate
		//gas_diffusion_rate
		//electrical_arc_range
		//water_flow_range
		//gas_dispersion_range
	};

	//50-81-F1-12
	public class RadiosityIsland: EntityMethodInterface {
		//composites
		//exclusions
	};

	//FB-DC-FA-54
	public class RadiosityProxy: EntityMethodInterface {
		public CathodeResource resource = new CathodeResource(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool disable_size_culling = new CathodeBool(); 
		public CathodeBool cast_shadows_in_torch = new CathodeBool(); 
		public CathodeBool cast_shadows = new CathodeBool(); 
		public CathodeFloat diffuse_opacity_scale = new CathodeFloat(); 
		public CathodeVector3 lightdecal_tint = new CathodeVector3(); 
		public CathodeVector3 diffuse_colour_scale = new CathodeVector3(); 
		public CathodeVector3 decal_scale = new CathodeVector3(); 
		public CathodeVector3 emissive_tint = new CathodeVector3(); 
		public CathodeBool include_in_planar_reflections = new CathodeBool(); 
		public CathodeFloat intensity_multiplier = new CathodeFloat(); 
		public CathodeFloat radiosity_multiplier = new CathodeFloat(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool simulate_on_reset = new CathodeBool(); 
		public CathodeBool light_on_reset = new CathodeBool(); 
		public CathodeBool force_keyframed = new CathodeBool(); 
		public CathodeBool deleted = new CathodeBool(); 
	};

	//10-1A-8F-3E
	public class RandomBool: EntityMethodInterface {
		//Result
	};

	//A5-45-E9-7F
	public class RandomFloat: EntityMethodInterface {
		public CathodeFloat Max = new CathodeFloat(); 
		public CathodeFloat Min = new CathodeFloat(); 
		//Result
	};

	//3B-8A-2D-D1
	public class RandomInt: EntityMethodInterface {
		public CathodeInteger Max = new CathodeInteger(); 
		public CathodeInteger Result = new CathodeInteger(); 
		public CathodeInteger Min = new CathodeInteger(); 
	};

	//87-1A-F2-E2
	public class RandomSelect: EntityMethodInterface {
		public CathodeFloat Seed = new CathodeFloat(); 
		//Input
		//Result
	};

	//0B-07-2A-4E
	public class RandomVector: EntityMethodInterface {
		public CathodeFloat MaxX = new CathodeFloat(); 
		public CathodeFloat MinX = new CathodeFloat(); 
		public CathodeFloat MaxY = new CathodeFloat(); 
		public CathodeFloat MaxZ = new CathodeFloat(); 
		public CathodeFloat MinZ = new CathodeFloat(); 
		public CathodeFloat MinY = new CathodeFloat(); 
		public CathodeBool Normalised = new CathodeBool(); 
		//Result
	};

	//96-55-D7-46
	public class Raycast: EntityMethodInterface {
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat max_distance = new CathodeFloat(); 
		public CathodeEnum priority = new CathodeEnum(); 
		//Obstructed
		//Unobstructed
		//OutOfRange
		//source_position
		//target_position
		//hit_object
		//hit_distance
		//hit_position
	};

	//75-11-2B-FB
	public class Refraction: EntityMethodInterface {
		public CathodeFloat SPEED = new CathodeFloat(); 
		public CathodeFloat REFRACTFACTOR = new CathodeFloat(); 
		public CathodeFloat SCALE = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat SECONDARY_REFRACTFACTOR = new CathodeFloat(); 
		public CathodeFloat SECONDARY_SCALE = new CathodeFloat(); 
		public CathodeFloat SECONDARY_SPEED = new CathodeFloat(); 
		public CathodeFloat SCALE_Z = new CathodeFloat(); 
		public CathodeFloat FLOW_SPEED = new CathodeFloat(); 
		public CathodeFloat FLOW_WARP_STRENGTH = new CathodeFloat(); 
		public CathodeFloat SCALE_X = new CathodeFloat(); 
		public CathodeFloat DISTANCEFACTOR = new CathodeFloat(); 
		public CathodeFloat CYCLE_TIME = new CathodeFloat(); 
		public CathodeFloat MIN_OCCLUSION_DISTANCE = new CathodeFloat(); 
		public CathodeFloat FLOW_TEX_SCALE = new CathodeFloat(); 
		//refraction_resource
	};

	//67-D7-25-2E
	public class RegisterCharacterModel: EntityMethodInterface {
		public CathodeString display_model = new CathodeString(); 
		//reference_skeleton
	};

	//EB-BC-2D-FD
	public class Rewire: EntityMethodInterface {
		//closed
		//locations
		//access_points
		//map_keyframe
		//total_power
	};

	//63-C7-3A-1A
	public class RewireAccess_Point: EntityMethodInterface {
		public CathodeFloat trigger = new CathodeFloat(); 
		//closed
		//ui_breakout_triggered
		//interactive_locations
		//visible_locations
		//additional_power
		//display_name
		//map_element_name
		//map_name
		//map_x_offset
		//map_y_offset
		//map_zoom
	};

	//84-11-33-58
	public class RewireLocation: EntityMethodInterface {
		public CathodeString element_name = new CathodeString(); 
		public CathodeString display_name = new CathodeString(); 
		public CathodeString systems = new CathodeString(); 
		//power_draw_increased
		//power_draw_reduced
	};

	//9A-A2-5B-93
	public class RewireSystem: EntityMethodInterface {
		public CathodeBool on_by_default = new CathodeBool(); 
		public CathodeInteger running_cost = new CathodeInteger(); 
		public CathodeString element_name = new CathodeString(); 
		public CathodeString display_name = new CathodeString(); 
		public CathodeString map_name = new CathodeString(); 
		public CathodeEnum system_type = new CathodeEnum(); 
		public CathodeEnum display_name_enum = new CathodeEnum(); 
		//on
		//off
		//world_pos
	};

	//02-28-D7-01
	public class RibbonEmitterReference: EntityMethodInterface {
		public CathodeFloat COLOUR_SCALE_MID = new CathodeFloat(); 
		public CathodeInteger MULTI_TEXTURE_MAX = new CathodeInteger(); 
		public CathodeFloat COLOUR_SCALE_END = new CathodeFloat(); 
		public CathodeFloat WIDTH_IN = new CathodeFloat(); 
		public CathodeInteger LIGHTING = new CathodeInteger(); 
		public CathodeFloat FADE_IN = new CathodeFloat(); 
		public CathodeInteger ALPHA_ERODE = new CathodeInteger(); 
		public CathodeFloat WIDTH_OUT = new CathodeFloat(); 
		public CathodeInteger SECOND_TEXTURE = new CathodeInteger(); 
		public CathodeInteger MULTI_TEXTURE_MULT = new CathodeInteger(); 
		public CathodeInteger SIDE_ON_FADE = new CathodeInteger(); 
		public CathodeFloat LIFETIME = new CathodeFloat(); 
		public CathodeFloat WIDTH_MID = new CathodeFloat(); 
		public CathodeFloat V2_SCROLLSPEED = new CathodeFloat(); 
		public CathodeInteger BLENDING_ALPHA_REF = new CathodeInteger(); 
		public CathodeFloat SOFTNESS_ALPHA_THICKNESS = new CathodeFloat(); 
		public CathodeInteger COLOUR_TINT = new CathodeInteger(); 
		public CathodeFloat UV_SCROLLSPEED = new CathodeFloat(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeFloat GRAVITY_STRENGTH = new CathodeFloat(); 
		public CathodeFloat COLOUR_SCALE_START = new CathodeFloat(); 
		public CathodeFloat SIDE_FADE_END = new CathodeFloat(); 
		public CathodeFloat SYSTEM_EXPIRY_TIME = new CathodeFloat(); 
		public CathodeInteger LOW_RES = new CathodeInteger(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeString TEXTURE_MAP = new CathodeString(); 
		public CathodeInteger BLENDING_ADDITIVE = new CathodeInteger(); 
		public CathodeVector3 AMBIENT_LIGHTING_COLOUR = new CathodeVector3(); 
		public CathodeFloat GRAVITY_MAX_STRENGTH = new CathodeFloat(); 
		public CathodeInteger MULTI_TEXTURE_MIN = new CathodeInteger(); 
		public CathodeFloat SPEED_START_MIN = new CathodeFloat(); 
		public CathodeFloat V2_REPEAT = new CathodeFloat(); 
		public CathodeVector3 COLOUR_TINT_END = new CathodeVector3(); 
		public CathodeString TEXTURE_MAP2 = new CathodeString(); 
		public CathodeFloat UV_REPEAT = new CathodeFloat(); 
		public CathodeInteger BLENDING_PREMULTIPLIED = new CathodeInteger(); 
		public CathodeInteger BLENDING_DISTORTION = new CathodeInteger(); 
		public CathodeInteger SMOOTHED = new CathodeInteger(); 
		public CathodeFloat WIDTH_START = new CathodeFloat(); 
		public CathodeString material = new CathodeString(); 
		public CathodeFloat WIDTH_END = new CathodeFloat(); 
		public CathodeFloat SPAWN_RATE = new CathodeFloat(); 
		public CathodeInteger MULTI_TEXTURE = new CathodeInteger(); 
		public CathodeInteger MULTI_TEXTURE_ADD = new CathodeInteger(); 
		public CathodeFloat WIND_Z = new CathodeFloat(); 
		public CathodeInteger BASE_LOCKED = new CathodeInteger(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat DRAG_STRENGTH = new CathodeFloat(); 
		public CathodeInteger SOFTNESS = new CathodeInteger(); 
		public CathodeFloat SPREAD = new CathodeFloat(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeInteger AMBIENT_LIGHTING = new CathodeInteger(); 
		public CathodeInteger BLENDING_STANDARD = new CathodeInteger(); 
		public CathodeFloat SIDE_FADE_START = new CathodeFloat(); 
		public CathodeFloat U2_SCALE = new CathodeFloat(); 
		public CathodeVector3 COLOUR_TINT_MID = new CathodeVector3(); 
		public CathodeInteger EDGE_FADE = new CathodeInteger(); 
		public CathodeInteger MULTI_TEXTURE_BLEND = new CathodeInteger(); 
		public CathodeVector3 COLOUR_TINT_START = new CathodeVector3(); 
		public CathodeFloat FADE_OUT = new CathodeFloat(); 
		public CathodeFloat SPEED_START_MAX = new CathodeFloat(); 
		public CathodeInteger NO_MIPS = new CathodeInteger(); 
		public CathodeFloat MASK_AMOUNT_MAX = new CathodeFloat(); 
		public CathodeFloat TRAIL_DELAY = new CathodeFloat(); 
		public CathodeInteger DISTANCE_SCALING = new CathodeInteger(); 
		public CathodeFloat EMISSION_AREA_X = new CathodeFloat(); 
		public CathodeFloat MASK_AMOUNT_MIN = new CathodeFloat(); 
		public CathodeFloat _A2_13_0F_1D = new CathodeFloat(); 
		public CathodeInteger INSTANT = new CathodeInteger(); 
		public CathodeFloat TRAIL_SPAWN_RATE = new CathodeFloat(); 
		public CathodeInteger UV_SQUARED = new CathodeInteger(); 
		public CathodeString _C1_F6_AC_3A = new CathodeString(); 
		public CathodeInteger DRAW_PASS = new CathodeInteger(); 
		public CathodeFloat MASK_AMOUNT_MIDPOINT = new CathodeFloat(); 
		public CathodeFloat EMISSION_AREA_Y = new CathodeFloat(); 
		public CathodeFloat MAX_TRAILS = new CathodeFloat(); 
		public CathodeFloat WIND_X = new CathodeFloat(); 
		public CathodeInteger NO_CLIP = new CathodeInteger(); 
		public CathodeFloat SPREAD_MIN = new CathodeFloat(); 
		public CathodeFloat WORLD_TO_LOCAL_BLEND_START = new CathodeFloat(); 
		public CathodeFloat WORLD_TO_LOCAL_BLEND_END = new CathodeFloat(); 
		public CathodeFloat EMISSION_AREA_Z = new CathodeFloat(); 
		public CathodeInteger CONTINUOUS = new CathodeInteger(); 
		public CathodeInteger SPREAD_FEATURE = new CathodeInteger(); 
		public CathodeInteger TRAILING = new CathodeInteger(); 
		public CathodeFloat DIST_SCALE = new CathodeFloat(); 
		public CathodeInteger RATE = new CathodeInteger(); 
		public CathodeFloat WIND_Y = new CathodeFloat(); 
		public CathodeFloat SOFTNESS_EDGE = new CathodeFloat(); 
		public CathodeBool deleted = new CathodeBool(); 
		public CathodeFloat WORLD_TO_LOCAL_MAX_DIST = new CathodeFloat(); 
		public CathodeFloat ABS_FADE_IN_1 = new CathodeFloat(); 
		public CathodeFloat ABS_FADE_IN_0 = new CathodeFloat(); 
		public CathodeInteger POINT_TO_POINT = new CathodeInteger(); 
		public CathodeFloat DENSITY = new CathodeFloat(); 
		public CathodeBool use_local_rotation = new CathodeBool(); 
		public CathodeInteger AREA_CUBOID = new CathodeInteger(); 
		public CathodeInteger AREA_CYLINDER = new CathodeInteger(); 
		public CathodeInteger COLOUR_RAMP = new CathodeInteger(); 
		public CathodeString _4D_87_BC_E4 = new CathodeString(); 
		public CathodeInteger AREA_SPHEROID = new CathodeInteger(); 
		public CathodeString COLOUR_RAMP_MAP = new CathodeString(); 
		public CathodeString DISTORTION_OCCLUSION = new CathodeString(); 
		public CathodeBool unique_material = new CathodeBool(); 
		public CathodeString EMISSION_SURFACE = new CathodeString(); 
		public CathodeString _7B_62_C9_E7 = new CathodeString(); 
		public CathodeFloat SOFTNESS_ALPHA_DEPTH_MODIFIER = new CathodeFloat(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeString _DA_8B_13_09 = new CathodeString(); 
		//mastered_by_visibility
		//include_in_planar_reflections
		//quality_level
		//TEXTURE
		//TARGET_POINT_POSITION
		//FORCES
		//START_MID_END_SPEED
		//WIDTH
		//ALPHA_FADE
		//EMISSION_AREA
		//resource
	};

	//57-5D-B8-73
	public class RotateAtSpeed: EntityMethodInterface {
		public CathodeBool loop = new CathodeBool(); 
		public CathodeFloat speed_X = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat speed_Z = new CathodeFloat(); 
		public CathodeFloat duration = new CathodeFloat(); 
		//on_finished
		//on_think
		//start_pos
		//origin
		//timer
		//Result
		//speed_Y
	};

	//13-A8-A7-B8
	public class RotateInTime: EntityMethodInterface {
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeBool loop = new CathodeBool(); 
		public CathodeFloat time_X = new CathodeFloat(); 
		public CathodeFloat time_Z = new CathodeFloat(); 
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeFloat timer = new CathodeFloat(); 
		//on_finished
		//on_think
		//start_pos
		//origin
		//Result
		//time_Y
	};

	//4A-B1-26-18
	public class RTT_MoviePlayer: EntityMethodInterface {
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeString target_texture_name = new CathodeString(); 
		public CathodeString filename = new CathodeString(); 
		public CathodeFloat trigger = new CathodeFloat(); 
		public CathodeString layer_name = new CathodeString(); 
		//start
		//end
	};

	//B4-DE-96-C4
	public class SaveManagers: EntityMethodInterface {
	};

	//84-89-4D-18
	public class ScreenEffectEventMonitor: EntityMethodInterface {
		//MeleeHit
		//BulletHit
		//MedkitHeal
		//StartStrangle
		//StopStrangle
		//StartLowHealth
		//StopLowHealth
		//StartDeath
		//StopDeath
		//AcidHit
		//FlashbangHit
		//HitAndRun
		//CancelHitAndRun
	};

	//C3-CD-47-1B
	public class ScreenFadeIn: EntityMethodInterface {
		//fade_value
	};

	//EE-5A-02-FF
	public class ScreenFadeInTimed: EntityMethodInterface {
		public CathodeFloat time = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_finished
	};

	//8E-BC-D3-9B
	public class ScreenFadeOutToBlack: EntityMethodInterface {
		public CathodeFloat fade_value = new CathodeFloat(); 
	};

	//1E-D1-58-35
	public class ScreenFadeOutToBlackTimed: EntityMethodInterface {
		public CathodeFloat time = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_finished
	};

	//7A-38-BB-E6
	public class ScreenFadeOutToWhite: EntityMethodInterface {
		public CathodeFloat fade_value = new CathodeFloat(); 
	};

	//EA-D0-4C-9D
	public class SetAsActiveMissionLevel: EntityMethodInterface {
		//clear_level
	};

	//22-93-7C-09
	public class SetBlueprintInfo: EntityMethodInterface {
		public CathodeString type = new CathodeString(); 
		//level
		//available
	};

	//8C-BA-56-A7
	public class SetBool: EntityMethodInterface {
		public CathodeBool Input = new CathodeBool(); 
	};

	//5D-AD-13-0A
	public class SetColour: EntityMethodInterface {
		public CathodeVector3 Colour = new CathodeVector3(); 
		//Result
	};

	//09-9E-6B-71
	public class SetEnum: EntityMethodInterface {
		public CathodeEnum Output = new CathodeEnum(); 
		public CathodeEnum initial_value = new CathodeEnum(); 
	};

	//1D-3F-AF-21
	public class SetFloat: EntityMethodInterface {
	};

	//65-4B-1E-DB
	public class SetGatingToolLevel: EntityMethodInterface {
		public CathodeInteger level = new CathodeInteger(); 
		public CathodeEnum tool_type = new CathodeEnum(); 
	};

	//2B-63-A9-33
	public class SetHackingToolLevel: EntityMethodInterface {
		public CathodeInteger level = new CathodeInteger(); 
	};

	//3B-67-A3-02
	public class SetInteger: EntityMethodInterface {
		public CathodeInteger Input = new CathodeInteger(); 
	};

	//E7-21-B5-23
	public class SetLocationAndOrientation: EntityMethodInterface {
		public CathodeVector3 local_offset = new CathodeVector3(); 
		public CathodeVector3 axis = new CathodeVector3(); 
		public CathodeEnum axis_is = new CathodeEnum(); 
		//location
		//result
	};

	//00-53-89-08
	public class SetNextLoadingMovie: EntityMethodInterface {
		//playlist_to_load
	};

	//5D-B1-1D-AA
	public class SetPlayerHasGatingTool: EntityMethodInterface {
		public CathodeEnum tool_type = new CathodeEnum(); 
	};

	//EA-50-F5-A8
	public class SetPlayerHasKeycard: EntityMethodInterface {
		//card_uid
	};

	//00-8E-91-DF
	public class SetPosition: EntityMethodInterface {
		public CathodeBool set_on_reset = new CathodeBool(); 
		public CathodeVector3 Rotation = new CathodeVector3(); 
		//Translation
		//Input
		//Result
	};

	//11-58-62-58
	public class SetPrimaryObjective: EntityMethodInterface {
		public CathodeString title = new CathodeString(); 
		public CathodeBool show_message = new CathodeBool(); 
		public CathodeString additional_info = new CathodeString(); 
		public CathodeString title_list = new CathodeString(); 
		public CathodeString additional_info_list = new CathodeString(); 
	};

	//BF-31-B6-3E
	public class SetRichPresence: EntityMethodInterface {
		public CathodeString presence_id = new CathodeString(); 
		//mission_number
	};

	//1C-BF-35-01
	public class SetString: EntityMethodInterface {
		public CathodeString initial_value = new CathodeString(); 
		//Output
		//SetEnumString
	};

	//57-D7-48-CD
	public class SetSubObjective: EntityMethodInterface {
		public CathodeString title = new CathodeString(); 
		public CathodeEnum objective_type = new CathodeEnum(); 
		public CathodeInteger slot_number = new CathodeInteger(); 
		public CathodeBool show_message = new CathodeBool(); 
		public CathodeString map_description_list = new CathodeString(); 
		public CathodeString title_list = new CathodeString(); 
		public CathodeString map_description = new CathodeString(); 
		//target_position
	};

	//E9-2B-F7-B0
	public class SetupGCDistribution: EntityMethodInterface {
		public CathodeFloat c07 = new CathodeFloat(); 
		public CathodeFloat c10 = new CathodeFloat(); 
		public CathodeFloat c04 = new CathodeFloat(); 
		public CathodeFloat divisor = new CathodeFloat(); 
		public CathodeFloat c08 = new CathodeFloat(); 
		public CathodeFloat c02 = new CathodeFloat(); 
		public CathodeFloat c05 = new CathodeFloat(); 
		public CathodeFloat c01 = new CathodeFloat(); 
		public CathodeFloat c06 = new CathodeFloat(); 
		public CathodeFloat lookup_decrease_time = new CathodeFloat(); 
		public CathodeFloat c09 = new CathodeFloat(); 
		public CathodeFloat c03 = new CathodeFloat(); 
		public CathodeInteger lookup_point_increase = new CathodeInteger(); 
		//c00
		//minimum_multiplier
	};

	//4F-EC-F1-4E
	public class SetVector: EntityMethodInterface {
		public CathodeFloat x = new CathodeFloat(); 
		public CathodeFloat y = new CathodeFloat(); 
		public CathodeFloat z = new CathodeFloat(); 
		//Result
	};

	//00-35-5D-71
	public class SimpleRefraction: EntityMethodInterface {
		public CathodeString radius = new CathodeString(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat DISTANCEFACTOR = new CathodeFloat(); 
		public CathodeString SECONDARY_NORMAL_MAP = new CathodeString(); 
		public CathodeBool FLOW_UV_ANIMATION = new CathodeBool(); 
		public CathodeFloat SPEED = new CathodeFloat(); 
		public CathodeFloat REFRACTFACTOR = new CathodeFloat(); 
		public CathodeBool SECONDARY_NORMAL_MAPPING = new CathodeBool(); 
		public CathodeFloat SCALE = new CathodeFloat(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeFloat SECONDARY_REFRACTFACTOR = new CathodeFloat(); 
		public CathodeFloat FLOW_SPEED = new CathodeFloat(); 
		public CathodeFloat FLOW_WARP_STRENGTH = new CathodeFloat(); 
		public CathodeFloat SECONDARY_SCALE = new CathodeFloat(); 
		public CathodeString NORMAL_MAP = new CathodeString(); 
		public CathodeFloat SECONDARY_SPEED = new CathodeFloat(); 
		public CathodeString FLOW_MAP = new CathodeString(); 
		public CathodeFloat MIN_OCCLUSION_DISTANCE = new CathodeFloat(); 
		public CathodeFloat FLOW_TEX_SCALE = new CathodeFloat(); 
		public CathodeBool DISTORTION_OCCLUSION = new CathodeBool(); 
		//deleted
		//ALPHA_MASKING
		//ALPHA_MASK
		//CYCLE_TIME
		//resource
	};

	//2D-E6-BA-56
	public class SimpleWater: EntityMethodInterface {
		public CathodeVector3 DEPTH_FOG_INITIAL_COLOUR = new CathodeVector3(); 
		public CathodeFloat SECONDARY_NORMAL_MAP_STRENGTH = new CathodeFloat(); 
		public CathodeString radius = new CathodeString(); 
		public CathodeFloat SHININESS = new CathodeFloat(); 
		public CathodeFloat SPEED = new CathodeFloat(); 
		public CathodeBool _A2_DC_DD_1D = new CathodeBool(); 
		public CathodeBool LOW_RES_ALPHA_PASS = new CathodeBool(); 
		public CathodeFloat DEPTH_FOG_MIDPOINT_ALPHA = new CathodeFloat(); 
		public CathodeBool SECONDARY_NORMAL_MAPPING = new CathodeBool(); 
		public CathodeBool LOCALISED_ENVIRONMENT_MAPPING = new CathodeBool(); 
		public CathodeFloat SCALE = new CathodeFloat(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeFloat MAX_FRESNEL = new CathodeFloat(); 
		public CathodeFloat NORMAL_MAP_STRENGTH = new CathodeFloat(); 
		public CathodeBool REFLECTIVE_MAPPING = new CathodeBool(); 
		public CathodeBool ALPHA_MASKING = new CathodeBool(); 
		public CathodeFloat SECONDARY_SCALE = new CathodeFloat(); 
		public CathodeString NORMAL_MAP = new CathodeString(); 
		public CathodeVector3 DEPTH_FOG_MIDPOINT_COLOUR = new CathodeVector3(); 
		public CathodeBool ENVIRONMENT_MAPPING = new CathodeBool(); 
		public CathodeFloat FRESNEL_POWER = new CathodeFloat(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat MIN_FRESNEL = new CathodeFloat(); 
		public CathodeVector3 ENVMAP_BOXPROJ_BB_SCALE = new CathodeVector3(); 
		public CathodeBool deleted = new CathodeBool(); 
		public CathodeFloat SECONDARY_SPEED = new CathodeFloat(); 
		public CathodeBool ATMOSPHERIC_FOGGING = new CathodeBool(); 
		public CathodeFloat softness_edge = new CathodeFloat(); 
		public CathodeBool LOCALISED_ENVMAP_BOX_PROJECTION = new CathodeBool(); 
		public CathodeBool CAUSTIC_REFRACTIONS = new CathodeBool(); 
		public CathodeFloat DEPTH_FOG_INITIAL_ALPHA = new CathodeFloat(); 
		public CathodeFloat CAUSTIC_HEIGHT = new CathodeFloat(); 
		public CathodeString ENVIRONMENT_MAP = new CathodeString(); 
		public CathodeFloat DEPTH_FOG_END_DEPTH = new CathodeFloat(); 
		public CathodeFloat CAUSTIC_SPEED_SCALAR = new CathodeFloat(); 
		public CathodeInteger CAUSTIC_TEXTURE_INDEX = new CathodeInteger(); 
		public CathodeFloat FLOW_SPEED = new CathodeFloat(); 
		public CathodeFloat CAUSTIC_TEXTURE_SCALE = new CathodeFloat(); 
		public CathodeString CAUSTIC_TEXTURE = new CathodeString(); 
		public CathodeBool CAUSTIC_REFLECTIONS = new CathodeBool(); 
		public CathodeBool FLOW_MAPPING = new CathodeBool(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeFloat FLOW_WARP_STRENGTH = new CathodeFloat(); 
		public CathodeFloat CAUSTIC_INTENSITY = new CathodeFloat(); 
		public CathodeVector3 DEPTH_FOG_END_COLOUR = new CathodeVector3(); 
		public CathodeString FLOW_MAP = new CathodeString(); 
		public CathodeFloat CAUSTIC_SURFACE_WRAP = new CathodeFloat(); 
		public CathodeFloat DEPTH_FOG_END_ALPHA = new CathodeFloat(); 
		public CathodeFloat DEPTH_FOG_MIDPOINT_DEPTH = new CathodeFloat(); 
		public CathodeFloat FLOW_TEX_SCALE = new CathodeFloat(); 
		public CathodeFloat REFLECTION_PERTURBATION_STRENGTH = new CathodeFloat(); 
		public CathodeString ALPHA_MASK = new CathodeString(); 
		//CYCLE_TIME
		//ENVIRONMENT_MAP_MULT
		//ENVMAP_SIZE
		//resource
	};

	//A4-71-03-08
	public class SmokeCylinder: EntityMethodInterface {
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeFloat height = new CathodeFloat(); 
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//pos
	};

	//3E-BA-2A-F7
	public class SmokeCylinderAttachmentInterface: EntityMethodInterface {
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeFloat height = new CathodeFloat(); 
		public CathodeFloat duration = new CathodeFloat(); 
	};

	//71-6E-2A-E0
	public class SmoothMove: EntityMethodInterface {
		public CathodeFloat timer = new CathodeFloat(); 
		public CathodeFloat duration = new CathodeFloat(); 
		public CathodeVector3 end_velocity = new CathodeVector3(); 
		public CathodeVector3 start_velocity = new CathodeVector3(); 
		//on_finished
		//start_position
		//end_position
		//result
	};

	//CD-F8-D2-01
	public class Sound: EntityMethodInterface {
		public CathodeString sound_event = new CathodeString(); 
		public CathodeBool start_on = new CathodeBool(); 
		public CathodeBool is_static_ambience = new CathodeBool(); 
		public CathodeString stop_event = new CathodeString(); 
		public CathodeBool multi_trigger = new CathodeBool(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeString argument_2 = new CathodeString(); 
		public CathodeString argument_3 = new CathodeString(); 
		public CathodeString argument_1 = new CathodeString(); 
		public CathodeString argument_5 = new CathodeString(); 
		public CathodeString argument_4 = new CathodeString(); 
		public CathodeBool is_occludable = new CathodeBool(); 
		public CathodeBool create_sound_object = new CathodeBool(); 
		public CathodeBool use_multi_emitter = new CathodeBool(); 
		public CathodeBool restore_on_checkpoint = new CathodeBool(); 
		public CathodeBool resume_after_suspended = new CathodeBool(); 
		public CathodeString switch_name = new CathodeString(); 
		public CathodeBool last_gen_enabled = new CathodeBool(); 
		//switch_value
	};

	//FD-A5-B7-F2
	public class SoundBarrier: EntityMethodInterface {
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool default_open = new CathodeBool(); 
		public CathodeString radius = new CathodeString(); 
		public CathodeBool band_aid = new CathodeBool(); 
		public CathodeFloat override_value = new CathodeFloat(); 
		//resource
	};

	//16-5B-74-36
	public class SoundEnvironmentMarker: EntityMethodInterface {
		public CathodeString room_size = new CathodeString(); 
		public CathodeString reverb_name = new CathodeString(); 
		public CathodeString on_exit_event = new CathodeString(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeString on_enter_event = new CathodeString(); 
		public CathodeFloat linked_network_occlusion_scaler = new CathodeFloat(); 
		public CathodeBool disable_network_creation = new CathodeBool(); 
	};

	//EC-49-AB-00
	public class SoundImpact: EntityMethodInterface {
		public CathodeString argument_2 = new CathodeString(); 
		public CathodeString sound_event = new CathodeString(); 
		public CathodeString argument_1 = new CathodeString(); 
		public CathodeString argument_3 = new CathodeString(); 
		public CathodeBool is_occludable = new CathodeBool(); 
	};

	//69-BA-A3-55
	public class SoundLevelInitialiser: EntityMethodInterface {
		public CathodeFloat network_node_ceiling_height = new CathodeFloat(); 
		public CathodeBool auto_generate_networks = new CathodeBool(); 
		public CathodeFloat network_node_min_spacing = new CathodeFloat(); 
		public CathodeFloat network_node_max_visibility = new CathodeFloat(); 
	};

	//FD-4C-67-24
	public class SoundLoadBank: EntityMethodInterface {
		public CathodeBool trigger_via_pin = new CathodeBool(); 
		public CathodeEnum memory_pool = new CathodeEnum(); 
		//bank_loaded
		//sound_bank
	};

	//16-CC-B8-28
	public class SoundLoadSlot: EntityMethodInterface {
		public CathodeEnum memory_pool = new CathodeEnum(); 
		public CathodeString sound_bank = new CathodeString(); 
		//bank_loaded
	};

	//F8-BF-B8-09
	public class SoundMissionInitialiser: EntityMethodInterface {
		public CathodeFloat android_max_threat = new CathodeFloat(); 
		public CathodeFloat human_max_threat = new CathodeFloat(); 
		//alien_max_threat
	};

	//3C-7C-B9-37
	public class SoundNetworkNode: EntityMethodInterface {
		public CathodeTransform position = new CathodeTransform(); 
	};

	//85-09-59-C0
	public class SoundObject: EntityMethodInterface {
		public CathodeTransform position = new CathodeTransform(); 
	};

	//D0-F3-49-91
	public class SoundPhysicsInitialiser: EntityMethodInterface {
		public CathodeFloat impact_min_speed = new CathodeFloat(); 
		public CathodeFloat impact_max_trigger_distance = new CathodeFloat(); 
		public CathodeFloat contact_max_timeout = new CathodeFloat(); 
		public CathodeFloat contact_smoothing_decay_rate = new CathodeFloat(); 
		public CathodeFloat contact_smoothing_attack_rate = new CathodeFloat(); 
		public CathodeFloat contact_min_magnitude = new CathodeFloat(); 
		public CathodeFloat contact_max_trigger_distance = new CathodeFloat(); 
		//ragdoll_min_timeout
		//ragdoll_min_speed
	};

	//F4-63-CD-D7
	public class SoundPlayerFootwearOverride: EntityMethodInterface {
		public CathodeString footwear_sound = new CathodeString(); 
	};

	//26-9C-D6-BF
	public class SoundRTPCController: EntityMethodInterface {
		//stealth_default_on
		//threat_default_on
	};

	//CE-34-31-89
	public class SoundSetRTPC: EntityMethodInterface {
		public CathodeString rtpc_name = new CathodeString(); 
		public CathodeFloat rtpc_value = new CathodeFloat(); 
		public CathodeBool start_on = new CathodeBool(); 
		public CathodeFloat smooth_rate = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//sound_object
	};

	//80-FE-94-2F
	public class SoundSetState: EntityMethodInterface {
		public CathodeString state_value = new CathodeString(); 
		public CathodeString state_name = new CathodeString(); 
	};

	//0D-70-2E-90
	public class SoundSetSwitch: EntityMethodInterface {
		public CathodeString switch_name = new CathodeString(); 
		//sound_object
		//switch_value
	};

	//EC-84-FA-48
	public class SoundTimelineTrigger: EntityMethodInterface {
		public CathodeString sound_event = new CathodeString(); 
		public CathodeFloat trigger_time = new CathodeFloat(); 
		public CathodeFloat trigger_now = new CathodeFloat(); 
	};

	//CD-40-A5-1E
	public class SpaceSuitVisor: EntityMethodInterface {
		public CathodeFloat _12_0B_9E_0A = new CathodeFloat(); 
		public CathodeFloat breath_level = new CathodeFloat(); 
		public CathodeFloat _05_AB_6D_C8 = new CathodeFloat(); 
	};

	//CA-2A-D3-4A
	public class SpaceTransform: EntityMethodInterface {
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat pitch_speed = new CathodeFloat(); 
		public CathodeFloat yaw_speed = new CathodeFloat(); 
		public CathodeFloat roll_speed = new CathodeFloat(); 
		//affected_geometry
	};

	//7F-90-AA-B7
	public class Speech: EntityMethodInterface {
		public CathodeString sound_event = new CathodeString(); 
		public CathodeEnum speech_priority = new CathodeEnum(); 
		public CathodeFloat queue_time = new CathodeFloat(); 
		public CathodeBool is_occludable = new CathodeBool(); 
		public CathodeFloat on_speech_started = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeString attached_sound_object = new CathodeString(); 
		public CathodeString argument_1 = new CathodeString(); 
		public CathodeBool restore_on_checkpoint = new CathodeBool(); 
		//character
		//alt_character
	};

	//5A-23-82-BC
	public class SpeechScript: EntityMethodInterface {
		public CathodeInteger line_03_character = new CathodeInteger(); 
		public CathodeString line_03_event = new CathodeString(); 
		public CathodeString line_01_event = new CathodeString(); 
		public CathodeInteger line_02_character = new CathodeInteger(); 
		public CathodeInteger line_04_character = new CathodeInteger(); 
		public CathodeString line_04_event = new CathodeString(); 
		public CathodeString line_05_event = new CathodeString(); 
		public CathodeInteger line_01_character = new CathodeInteger(); 
		public CathodeString line_02_event = new CathodeString(); 
		public CathodeBool is_occludable = new CathodeBool(); 
		public CathodeEnum speech_priority = new CathodeEnum(); 
		public CathodeString line_06_event = new CathodeString(); 
		public CathodeInteger line_05_character = new CathodeInteger(); 
		public CathodeString line_07_event = new CathodeString(); 
		public CathodeInteger line_06_character = new CathodeInteger(); 
		public CathodeString line_08_event = new CathodeString(); 
		public CathodeString line_09_event = new CathodeString(); 
		public CathodeFloat line_05_delay = new CathodeFloat(); 
		public CathodeFloat line_04_delay = new CathodeFloat(); 
		public CathodeInteger line_07_character = new CathodeInteger(); 
		public CathodeFloat line_08_delay = new CathodeFloat(); 
		public CathodeInteger line_08_character = new CathodeInteger(); 
		public CathodeFloat line_03_delay = new CathodeFloat(); 
		public CathodeInteger line_10_character = new CathodeInteger(); 
		public CathodeInteger line_09_character = new CathodeInteger(); 
		public CathodeString line_10_event = new CathodeString(); 
		public CathodeFloat line_02_delay = new CathodeFloat(); 
		public CathodeFloat line_09_delay = new CathodeFloat(); 
		public CathodeFloat line_06_delay = new CathodeFloat(); 
		public CathodeFloat line_07_delay = new CathodeFloat(); 
		public CathodeFloat line_10_delay = new CathodeFloat(); 
		//on_script_ended
		//character_01
		//character_02
		//character_03
		//character_04
		//character_05
		//alt_character_01
		//alt_character_02
		//alt_character_03
		//alt_character_04
		//alt_character_05
		//restore_on_checkpoint
	};

	//39-B9-14-0B
	public class Sphere: EntityMethodInterface {
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeString half_dimensions = new CathodeString(); 
		public CathodeTransform position = new CathodeTransform(); 
		//event
		//include_physics
	};

	//0C-CC-2D-7A
	public class SplineDistanceLerp: EntityMethodInterface {
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_think
		//spline
		//lerp_position
		//Result
	};

	//2D-C1-10-33
	public class SplinePath: EntityMethodInterface {
		public CathodeVector3 _C2_23_2F_69 = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeSpline points = new CathodeSpline(); 
		public CathodeBool loop = new CathodeBool(); 
		public CathodeBool orientated = new CathodeBool(); 
	};

	//19-41-46-B2
	public class Squad_SetMaxEscalationLevel: EntityMethodInterface {
		public CathodeEnum max_level = new CathodeEnum(); 
		//squad_coordinator
	};

	//D0-35-5D-04
	public class StealCamera: EntityMethodInterface {
		public CathodeFloat blend_in_duration = new CathodeFloat(); 
		public CathodeEnum steal_type = new CathodeEnum(); 
		public CathodeFloat disable = new CathodeFloat(); 
		public CathodeFloat enabled = new CathodeFloat(); 
		//on_converged
		//focus_position
		//check_line_of_sight
	};

	//E7-D9-0B-A8
	public class StreamingMonitor: EntityMethodInterface {
		//on_loaded
	};

	//C1-B5-5D-EE
	public class SurfaceEffectBox: EntityMethodInterface {
		public CathodeFloat SPARKLE_SCALE = new CathodeFloat(); 
		public CathodeString radius = new CathodeString(); 
		public CathodeVector3 FALLOFF = new CathodeVector3(); 
		public CathodeFloat OPACITY = new CathodeFloat(); 
		public CathodeFloat SURFACE_WRAP = new CathodeFloat(); 
		public CathodeFloat ROUGHNESS_SCALE = new CathodeFloat(); 
		public CathodeString TEXTURE_MAP = new CathodeString(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat INTENSITY = new CathodeFloat(); 
		public CathodeString SPARKLE_MAP = new CathodeString(); 
		public CathodeFloat TILING_ZY = new CathodeFloat(); 
		public CathodeString ENVIRONMENT_MAP = new CathodeString(); 
		public CathodeFloat ENVMAP_PERCENT_EMISSIVE = new CathodeFloat(); 
		public CathodeVector3 COLOUR_TINT = new CathodeVector3(); 
		public CathodeFloat TILING_ZX = new CathodeFloat(); 
		public CathodeFloat TILING_XY = new CathodeFloat(); 
		public CathodeBool ENVMAP = new CathodeBool(); 
		public CathodeVector3 COLOUR_TINT_OUTER = new CathodeVector3(); 
		public CathodeFloat FADE_OUT_TIME = new CathodeFloat(); 
		public CathodeFloat SHININESS_OPACITY = new CathodeFloat(); 
		public CathodeFloat METAL_STYLE_REFLECTIONS = new CathodeFloat(); 
		public CathodeBool WS_LOCKED = new CathodeBool(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		//deleted
		//show_on_reset
		//SPHERE
		//BOX
		//resource
	};

	//5A-D3-61-25
	public class SurfaceEffectSphere: EntityMethodInterface {
		public CathodeFloat SPARKLE_SCALE = new CathodeFloat(); 
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeFloat SURFACE_WRAP = new CathodeFloat(); 
		public CathodeFloat TILING_ZY = new CathodeFloat(); 
		public CathodeVector3 COLOUR_TINT = new CathodeVector3(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeFloat ROUGHNESS_SCALE = new CathodeFloat(); 
		public CathodeString TEXTURE_MAP = new CathodeString(); 
		public CathodeFloat TILING_ZX = new CathodeFloat(); 
		public CathodeFloat TILING_XY = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeVector3 COLOUR_TINT_OUTER = new CathodeVector3(); 
		public CathodeString SPARKLE_MAP = new CathodeString(); 
		public CathodeString ENVIRONMENT_MAP = new CathodeString(); 
		public CathodeFloat ENVMAP_PERCENT_EMISSIVE = new CathodeFloat(); 
		public CathodeBool WS_LOCKED = new CathodeBool(); 
		public CathodeBool ENVMAP = new CathodeBool(); 
		public CathodeFloat INTENSITY = new CathodeFloat(); 
		public CathodeFloat FADE_OUT_TIME = new CathodeFloat(); 
		public CathodeFloat SHININESS_OPACITY = new CathodeFloat(); 
		public CathodeFloat METAL_STYLE_REFLECTIONS = new CathodeFloat(); 
		public CathodeString half_dimensions = new CathodeString(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeFloat disable = new CathodeFloat(); 
		//deleted
		//OPACITY
		//SPHERE
		//resource
	};

	//38-A7-1A-AC
	public class SwitchLevel: EntityMethodInterface {
		//level_name
	};

	//45-52-F3-5D
	public class Task: EntityMethodInterface {
		public CathodeFloat timeout = new CathodeFloat(); 
		public CathodeBool should_orientate_when_reached = new CathodeBool(); 
		public CathodeBool should_stop_moving_when_reached = new CathodeBool(); 
		//start_command
		//selected_by_npc
		//clean_up
		//start_on_reset
	};

	//1E-5A-94-FF
	public class TerminalContent: EntityMethodInterface {
		public CathodeString additional_info = new CathodeString(); 
		public CathodeString content_title = new CathodeString(); 
		public CathodeBool is_single_use = new CathodeBool(); 
		public CathodeBool is_triggerable = new CathodeBool(); 
		public CathodeString content_decoration_title = new CathodeString(); 
		public CathodeBool is_connected_to_audio_log = new CathodeBool(); 
		//selected
	};

	//25-24-E0-E1
	public class TerminalFolder: EntityMethodInterface {
		public CathodeString folder_title = new CathodeString(); 
		public CathodeString content1 = new CathodeString(); 
		public CathodeEnum folder_lock_type = new CathodeEnum(); 
		public CathodeString code = new CathodeString(); 
		public CathodeBool lock_on_reset = new CathodeBool(); 
		//code_success
		//code_fail
		//selected
		//content0
	};

	//80-C2-20-47
	public class Thinker: EntityMethodInterface {
		public CathodeBool is_continuous = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat total_thinking_time = new CathodeFloat(); 
		public CathodeFloat delay_between_triggers = new CathodeFloat(); 
		public CathodeBool use_random_start = new CathodeBool(); 
		public CathodeFloat random_start_delay = new CathodeFloat(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		//on_think
	};

	//31-E3-47-07
	public class ThinkOnce: EntityMethodInterface {
		public CathodeFloat random_start_delay = new CathodeFloat(); 
		public CathodeBool use_random_start = new CathodeBool(); 
		public CathodeBool pause_on_reset = new CathodeBool(); 
		//on_think
		//start_on_reset
	};

	//93-66-1A-DC
	public class ToggleFunctionality: EntityMethodInterface {
	};

	//8C-C9-60-B2
	public class TogglePlayerTorch: EntityMethodInterface {
	};

	//43-21-16-4A
	public class TorchDynamicMovement: EntityMethodInterface {
		public CathodeEnum _26_93_7C_0E = new CathodeEnum(); 
		public CathodeFloat target_damping = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat max_spatial_velocity = new CathodeFloat(); 
		public CathodeFloat start = new CathodeFloat(); 
		public CathodeFloat max_angular_velocity = new CathodeFloat(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeFloat position_damping = new CathodeFloat(); 
		public CathodeFloat max_target_displacement = new CathodeFloat(); 
		public CathodeFloat max_position_displacement = new CathodeFloat(); 
		//torch
	};

	//D5-5C-FA-24
	public class TRAV_1ShotSpline: EntityMethodInterface {
		public CathodeTransform _15_1B_89_1B = new CathodeTransform(); 
		public CathodeVector3 _91_E3_3D_2E = new CathodeVector3(); 
		public CathodeTransform _CD_DE_A1_39 = new CathodeTransform(); 
		public CathodeVector3 _7C_29_0D_3A = new CathodeVector3(); 
		public CathodeString animationTree = new CathodeString(); 
		public CathodeVector3 _8C_68_A2_50 = new CathodeVector3(); 
		public CathodeTransform _2B_B2_C1_90 = new CathodeTransform(); 
		public CathodeBool template = new CathodeBool(); 
		public CathodeEnum character_classes = new CathodeEnum(); 
		public CathodeTransform _D0_99_C9_CD = new CathodeTransform(); 
		public CathodeVector3 _B5_B8_0D_DD = new CathodeVector3(); 
		public CathodeEnum max_speed = new CathodeEnum(); 
		public CathodeFloat extra_cost = new CathodeFloat(); 
		public CathodeFloat headroom = new CathodeFloat(); 
		public CathodeEnum min_speed = new CathodeEnum(); 
		public CathodeBool fit_end_to_edge = new CathodeBool(); 
		//OnEnter
		//OnExit
		//enable_on_reset
		//open_on_reset
		//EntrancePath
		//ExitPath
		//MinimumPath
		//MaximumPath
		//MinimumSupport
		//MaximumSupport
		//resource
	};

	//76-74-ED-C1
	public class TRAV_ContinuousLadder: EntityMethodInterface {
		//OnEnter
		//OnExit
		//enable_on_reset
		//LinePath
		//InUse
		//RungSpacing
		//character_classes
	};

	//58-C4-36-50
	public class Trigger_AudioOccluded: EntityMethodInterface {
		public CathodeFloat Range = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		//NotOccluded
		//Occluded
	};

	//3B-80-99-45
	public class TriggerBindAllCharactersOfType: EntityMethodInterface {
		public CathodeEnum character_class = new CathodeEnum(); 
		//bound_trigger
	};

	//1B-9F-A0-25
	public class TriggerBindAllNPCs: EntityMethodInterface {
		public CathodeBool filter = new CathodeBool(); 
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeFloat trigger = new CathodeFloat(); 
		//npc_inside
		//npc_outside
		//centre
	};

	//13-54-4F-ED
	public class TriggerBindCharacter: EntityMethodInterface {
		//bound_trigger
		//characters
	};

	//B9-60-C1-F8
	public class TriggerBindCharactersInSquad: EntityMethodInterface {
		//bound_trigger
	};

	//2D-23-AF-CA
	public class TriggerCameraViewCone: EntityMethodInterface {
		public CathodeFloat visible_area_horizontal = new CathodeFloat(); 
		public CathodeFloat visible_area_vertical = new CathodeFloat(); 
		public CathodeEnum visible_area_type = new CathodeEnum(); 
		public CathodeBool intersect_with_geometry = new CathodeBool(); 
		public CathodeFloat raycast_grace = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//enter
		//exit
		//target
		//fov
		//aspect_ratio
		//use_camera_fov
		//target_offset
	};

	//FC-12-CB-CB
	public class TriggerCameraVolume: EntityMethodInterface {
		public CathodeFloat radius = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat start_radius = new CathodeFloat(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeString half_dimensions = new CathodeString(); 
		//inside
		//enter
		//exit
		//inside_factor
		//lookat_factor
		//lookat_X_position
		//lookat_Y_position
	};

	//C7-E3-37-A6
	public class TriggerCheckDifficulty: EntityMethodInterface {
		public CathodeEnum DifficultyLevel = new CathodeEnum(); 
		//on_success
		//on_failure
	};

	//FE-2C-A2-8A
	public class TriggerDamaged: EntityMethodInterface {
		//on_damaged
		//enable_on_reset
		//physics_object
		//impact_normal
		//threshold
	};

	//BF-5E-0D-F2
	public class TriggerDelay: EntityMethodInterface {
		public CathodeFloat Sec = new CathodeFloat(); 
		public CathodeFloat Min = new CathodeFloat(); 
		//delayed_trigger
		//purged_trigger
		//time_left
		//Hrs
	};

	//50-BD-D7-EC
	public class TriggerExtractBoundCharacter: EntityMethodInterface {
		//unbound_trigger
		//bound_character
	};

	//06-8C-EB-F3
	public class TriggerExtractBoundObject: EntityMethodInterface {
		//unbound_trigger
		//bound_object
	};

	//AC-FB-EC-42
	public class TriggerFilter: EntityMethodInterface {
		//on_success
		//on_failure
		//filter
	};

	//C3-F0-EC-23
	public class TriggerLooper: EntityMethodInterface {
		public CathodeInteger count = new CathodeInteger(); 
		public CathodeFloat delay = new CathodeFloat(); 
		//target
	};

	//D8-8B-B6-81
	public class TriggerObjectsFilter: EntityMethodInterface {
		//on_success
		//on_failure
		//filter
		//objects
	};

	//0C-67-62-A8
	public class TriggerRandom: EntityMethodInterface {
		public CathodeInteger Num = new CathodeInteger(); 
		public CathodeFloat Random_11 = new CathodeFloat(); 
		public CathodeFloat Random_10 = new CathodeFloat(); 
		//Random_1
		//Random_2
		//Random_3
		//Random_4
		//Random_5
		//Random_6
		//Random_7
		//Random_8
		//Random_9
		//Random_12
	};

	//D5-BF-89-31
	public class TriggerRandomSequence: EntityMethodInterface {
		public CathodeInteger num = new CathodeInteger(); 
		//Random_1
		//Random_2
		//Random_3
		//Random_4
		//Random_5
		//Random_6
		//Random_7
		//Random_8
		//Random_9
		//Random_10
		//All_triggered
		//current
	};

	//FF-12-A7-0C
	public class TriggerSelect: EntityMethodInterface {
		//Pin_0
		//Pin_1
		//Pin_2
		//Pin_3
		//Pin_4
		//Pin_5
		//Pin_6
		//Pin_7
		//Pin_8
		//Pin_9
		//Pin_10
		//Pin_11
		//Pin_12
		//Pin_13
		//Pin_14
		//Pin_15
		//Pin_16
		//Object_0
		//Object_1
		//Object_2
		//Object_3
		//Object_4
		//Object_5
		//Object_6
		//Object_7
		//Object_8
		//Object_9
		//Object_10
		//Object_11
		//Object_12
		//Object_13
		//Object_14
		//Object_15
		//Object_16
		//Result
		//index
	};

	//C3-FA-EA-BC
	public class TriggerSelect_Direct: EntityMethodInterface {
		public CathodeFloat Trigger_0 = new CathodeFloat(); 
		//Changed_to_0
		//Changed_to_1
		//Changed_to_2
		//Changed_to_3
		//Changed_to_4
		//Changed_to_5
		//Changed_to_6
		//Changed_to_7
		//Changed_to_8
		//Changed_to_9
		//Changed_to_10
		//Changed_to_11
		//Changed_to_12
		//Changed_to_13
		//Changed_to_14
		//Changed_to_15
		//Changed_to_16
		//Object_0
		//Object_1
		//Object_2
		//Object_3
		//Object_4
		//Object_5
		//Object_6
		//Object_7
		//Object_8
		//Object_9
		//Object_10
		//Object_11
		//Object_12
		//Object_13
		//Object_14
		//Object_15
		//Object_16
		//Result
		//TriggeredIndex
		//Changes_only
	};

	//0D-A3-76-BF
	public class TriggerSequence: EntityMethodInterface {
		public CathodeBool proxy_enable_on_reset = new CathodeBool(); 
		public CathodeEnum trigger_mode = new CathodeEnum(); 
		public CathodeBool filter = new CathodeBool(); 
		public CathodeBool no_duplicates = new CathodeBool(); 
		public CathodeFloat _13_41_F5_07 = new CathodeFloat(); 
		public CathodeFloat _96_F5_99_28 = new CathodeFloat(); 
		public CathodeBool use_random_intervals = new CathodeBool(); 
		public CathodeFloat stop_finished = new CathodeFloat(); 
		public CathodeFloat terminate_finished = new CathodeFloat(); 
		public CathodeFloat start_finished = new CathodeFloat(); 
		public CathodeFloat hide_finished = new CathodeFloat(); 
		public CathodeFloat refresh_finished = new CathodeFloat(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeFloat random_seed = new CathodeFloat(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeFloat _75_EA_86_06 = new CathodeFloat(); 
		public CathodeFloat _C6_9C_6E_07 = new CathodeFloat(); 
		public CathodeFloat _CF_9C_28_0E = new CathodeFloat(); 
		public CathodeFloat _5F_F2_D2_12 = new CathodeFloat(); 
		public CathodeFloat _D3_23_E9_23 = new CathodeFloat(); 
		public CathodeFloat _98_4A_A1_2A = new CathodeFloat(); 
		public CathodeFloat _AD_56_C3_33 = new CathodeFloat(); 
		public CathodeString _FB_37_75_37 = new CathodeString(); 
		public CathodeFloat _12_D9_F3_47 = new CathodeFloat(); 
		public CathodeFloat _07_7F_CB_62 = new CathodeFloat(); 
		public CathodeFloat _69_C8_A5_63 = new CathodeFloat(); 
		public CathodeFloat _A4_7E_87_6E = new CathodeFloat(); 
		public CathodeFloat _BE_19_55_77 = new CathodeFloat(); 
		public CathodeFloat _81_A9_A1_8A = new CathodeFloat(); 
		public CathodeFloat _6B_7B_AE_8B = new CathodeFloat(); 
		public CathodeFloat _E2_3B_4E_98 = new CathodeFloat(); 
		public CathodeFloat _79_52_B6_9C = new CathodeFloat(); 
		public CathodeFloat _BC_48_90_A9 = new CathodeFloat(); 
		public CathodeFloat _89_6D_43_AD = new CathodeFloat(); 
		public CathodeFloat _1E_5D_31_BB = new CathodeFloat(); 
		public CathodeFloat _4F_30_54_F0 = new CathodeFloat(); 
		public CathodeFloat _43_52_D7_F8 = new CathodeFloat(); 
		public CathodeFloat interval_multiplier = new CathodeFloat(); 
		public CathodeFloat _C0_C5_2C_65 = new CathodeFloat(); 
		public CathodeFloat _7C_A9_9B_86 = new CathodeFloat(); 
		public CathodeFloat _C6_BE_C7_C6 = new CathodeFloat(); 
		public CathodeFloat _0C_54_B8_C8 = new CathodeFloat(); 
		public CathodeFloat _45_50_31_30 = new CathodeFloat(); 
		public CathodeFloat _CD_7E_9B_B5 = new CathodeFloat(); 
		public CathodeFloat proxy_enable = new CathodeFloat(); 
		public CathodeFloat disable_finished = new CathodeFloat(); 
		public CathodeFloat _67_D2_50_00 = new CathodeFloat(); 
		public CathodeFloat _D9_20_90_D9 = new CathodeFloat(); 
		public CathodeFloat _F0_AF_20_F7 = new CathodeFloat(); 
		public CathodeFloat despawn = new CathodeFloat(); 
		public CathodeFloat despawn_finished = new CathodeFloat(); 
		public CathodeFloat _28_31_AB_60 = new CathodeFloat(); 
		public CathodeFloat _85_43_05_61 = new CathodeFloat(); 
		public CathodeFloat suspend_finished = new CathodeFloat(); 
		public CathodeFloat allow = new CathodeFloat(); 
		public CathodeFloat _95_83_09_C2 = new CathodeFloat(); 
		public CathodeFloat allow_finished = new CathodeFloat(); 
		public CathodeFloat _F5_46_88_18 = new CathodeFloat(); 
		public CathodeFloat _AC_DE_56_24 = new CathodeFloat(); 
		public CathodeFloat _07_7B_00_3A = new CathodeFloat(); 
		public CathodeFloat _EF_F8_0F_C6 = new CathodeFloat(); 
		public CathodeFloat _12_D5_DC_DE = new CathodeFloat(); 
		public CathodeFloat _CB_A1_EF_E3 = new CathodeFloat(); 
		public CathodeFloat _BA_FA_29_2D = new CathodeFloat(); 
		public CathodeFloat _98_CC_30_46 = new CathodeFloat(); 
		public CathodeFloat _F2_A1_AE_F4 = new CathodeFloat(); 
		public CathodeFloat _11_4C_D4_34 = new CathodeFloat(); 
		public CathodeFloat _29_F2_F0_34 = new CathodeFloat(); 
		public CathodeFloat _79_FE_4B_82 = new CathodeFloat(); 
		public CathodeFloat _6A_30_C5_B2 = new CathodeFloat(); 
		public CathodeFloat _A3_E0_31_EB = new CathodeFloat(); 
		public CathodeFloat _21_DE_FF_FE = new CathodeFloat(); 
		public CathodeFloat _B2_36_47_E9 = new CathodeFloat(); 
		public CathodeFloat _11_89_D0_73 = new CathodeFloat(); 
		public CathodeBool _FD_6F_0B_58 = new CathodeBool(); 
		public CathodeFloat _66_70_86_22 = new CathodeFloat(); 
		public CathodeFloat _EE_7C_D1_66 = new CathodeFloat(); 
		public CathodeFloat _C7_88_75_FB = new CathodeFloat(); 
		public CathodeFloat simulate_finished = new CathodeFloat(); 
		public CathodeFloat Reset = new CathodeFloat(); 
		public CathodeFloat _BD_45_79_A0 = new CathodeFloat(); 
		public CathodeFloat _FB_96_CC_18 = new CathodeFloat(); 
		public CathodeFloat _5D_1B_32_1D = new CathodeFloat(); 
		public CathodeFloat _84_2F_D7_9F = new CathodeFloat(); 
		public CathodeFloat _E1_4E_E0_6C = new CathodeFloat(); 
		public CathodeFloat trigger_finished = new CathodeFloat(); 
		public CathodeFloat trigger = new CathodeFloat(); 
		public CathodeFloat _84_3A_63_1E = new CathodeFloat(); 
		public CathodeFloat _73_E8_D8_89 = new CathodeFloat(); 
		public CathodeFloat _5D_D3_D6_9F = new CathodeFloat(); 
		public CathodeFloat INTENSITY = new CathodeFloat(); 
		public CathodeFloat _05_69_EA_19 = new CathodeFloat(); 
		public CathodeFloat attach_finished = new CathodeFloat(); 
		public CathodeFloat _ED_AA_4D_5A = new CathodeFloat(); 
		public CathodeFloat _8D_B8_D1_D8 = new CathodeFloat(); 
		public CathodeFloat attach = new CathodeFloat(); 
		public CathodeFloat _8E_EB_EA_4D = new CathodeFloat(); 
		public CathodeFloat _D8_97_73_FD = new CathodeFloat(); 
		public CathodeFloat _1D_A5_E7_1C = new CathodeFloat(); 
		public CathodeFloat _DB_4F_3D_43 = new CathodeFloat(); 
		public CathodeFloat _7D_22_74_45 = new CathodeFloat(); 
		public CathodeFloat _98_2A_C5_7C = new CathodeFloat(); 
		public CathodeFloat _F0_FB_98_A2 = new CathodeFloat(); 
		public CathodeFloat _A9_B8_10_D1 = new CathodeFloat(); 
		public CathodeFloat _4B_38_CF_5C = new CathodeFloat(); 
		public CathodeFloat _E5_A8_B7_CF = new CathodeFloat(); 
		public CathodeFloat _95_4A_84_0B = new CathodeFloat(); 
		public CathodeFloat _A8_D4_36_1E = new CathodeFloat(); 
		public CathodeFloat _25_65_F4_23 = new CathodeFloat(); 
		public CathodeFloat _C2_9E_77_86 = new CathodeFloat(); 
		public CathodeFloat _F2_A1_54_22 = new CathodeFloat(); 
		public CathodeFloat _C8_17_10_33 = new CathodeFloat(); 
		public CathodeFloat _73_60_51_A8 = new CathodeFloat(); 
		public CathodeFloat _9B_F4_6E_C1 = new CathodeFloat(); 
		public CathodeFloat _1A_84_E5_CB = new CathodeFloat(); 
		public CathodeFloat _9D_DC_14_D7 = new CathodeFloat(); 
		public CathodeFloat cancel_load = new CathodeFloat(); 
		public CathodeFloat cancel_load_finished = new CathodeFloat(); 
		public CathodeFloat cancel_unload = new CathodeFloat(); 
		public CathodeFloat _D3_0A_C6_20 = new CathodeFloat(); 
		public CathodeFloat request_unload = new CathodeFloat(); 
		public CathodeFloat request_load = new CathodeFloat(); 
		public CathodeFloat request_load_finished = new CathodeFloat(); 
		public CathodeFloat request_unload_finished = new CathodeFloat(); 
		public CathodeFloat cancel_unload_finished = new CathodeFloat(); 
		public CathodeFloat _F4_D4_AD_D7 = new CathodeFloat(); 
		public CathodeFloat _13_F2_21_DB = new CathodeFloat(); 
		public CathodeFloat _13_F2_8B_E7 = new CathodeFloat(); 
		public CathodeFloat _DE_C2_6D_1F = new CathodeFloat(); 
		public CathodeString _0C_3A_DE_9E = new CathodeString(); 
		public CathodeFloat _88_24_E9_1D = new CathodeFloat(); 
		public CathodeFloat _91_93_E0_C8 = new CathodeFloat(); 
		public CathodeFloat _DB_3F_75_E2 = new CathodeFloat(); 
		public CathodeFloat _B3_DB_56_BE = new CathodeFloat(); 
		public CathodeFloat _50_21_9F_0A = new CathodeFloat(); 
		public CathodeString _94_3A_F4_D8 = new CathodeString(); 
		//attach_on_reset
		//duration
	};

	//D6-65-5A-DD
	public class TriggerSimple: EntityMethodInterface {
	};

	//E6-49-09-25
	public class TriggerSwitch: EntityMethodInterface {
		public CathodeInteger num = new CathodeInteger(); 
		public CathodeBool loop = new CathodeBool(); 
		//Pin_1
		//Pin_2
		//Pin_3
		//Pin_4
		//Pin_5
		//Pin_6
		//Pin_7
		//Pin_8
		//Pin_9
		//Pin_10
		//current
	};

	//99-69-1C-73
	public class TriggerUnbindCharacter: EntityMethodInterface {
		//unbound_trigger
	};

	//7F-4A-04-6F
	public class TriggerViewCone: EntityMethodInterface {
		public CathodeTransform source_position = new CathodeTransform(); 
		public CathodeFloat stop = new CathodeFloat(); 
		public CathodeFloat visible_area_horizontal = new CathodeFloat(); 
		public CathodeFloat visible_area_vertical = new CathodeFloat(); 
		public CathodeEnum visible_area_type = new CathodeEnum(); 
		public CathodeFloat max_distance = new CathodeFloat(); 
		public CathodeFloat raycast_grace = new CathodeFloat(); 
		public CathodeFloat aspect_ratio = new CathodeFloat(); 
		public CathodeBool intersect_with_geometry = new CathodeBool(); 
		public CathodeTransform target_offset = new CathodeTransform(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//enter
		//exit
		//target_is_visible
		//no_target_visible
		//target
		//fov
		//filter
		//visible_target
	};

	//D4-33-61-F8
	public class TriggerVolumeFilter: EntityMethodInterface {
		//on_event_entered
		//on_event_exited
		//filter
	};

	//EA-01-34-30
	public class TriggerVolumeFilter_Monitored: EntityMethodInterface {
		//on_event_entered
		//on_event_exited
		//filter
	};

	//DD-17-24-10
	public class TriggerWeightedRandom: EntityMethodInterface {
		public CathodeFloat Weighting_04 = new CathodeFloat(); 
		public CathodeFloat Weighting_05 = new CathodeFloat(); 
		public CathodeFloat Weighting_01 = new CathodeFloat(); 
		public CathodeFloat Weighting_03 = new CathodeFloat(); 
		public CathodeFloat Weighting_02 = new CathodeFloat(); 
		public CathodeFloat Weighting_07 = new CathodeFloat(); 
		public CathodeFloat Weighting_06 = new CathodeFloat(); 
		public CathodeFloat Weighting_08 = new CathodeFloat(); 
		public CathodeBool allow_same_pin_in_succession = new CathodeBool(); 
		public CathodeFloat Random_6 = new CathodeFloat(); 
		public CathodeFloat Weighting_09 = new CathodeFloat(); 
		public CathodeFloat Weighting_10 = new CathodeFloat(); 
		//Random_1
		//Random_2
		//Random_3
		//Random_4
		//Random_5
		//Random_7
		//Random_8
		//Random_9
		//Random_10
		//current
	};

	//E0-84-85-05
	public class TriggerWhenSeeTarget: EntityMethodInterface {
		//seen
		//Target
	};

	//C0-40-6A-24
	public class TutorialMessage: EntityMethodInterface {
		public CathodeFloat start = new CathodeFloat(); 
		public CathodeString text = new CathodeString(); 
		public CathodeBool show_animation = new CathodeBool(); 
		//text_list
	};

	//52-B7-FA-37
	public class UI_Container: EntityMethodInterface {
		public CathodeInteger ui_icon = new CathodeInteger(); 
		public CathodeBool is_temporary = new CathodeBool(); 
		public CathodeBool is_persistent = new CathodeBool(); 
		//take_slot
		//emptied
		//contents
		//has_been_used
	};

	//46-DD-69-56
	public class UI_Icon: EntityMethodInterface {
		public CathodeString action_text = new CathodeString(); 
		public CathodeBool show_on_reset = new CathodeBool(); 
		public CathodeFloat highlight_distance_threshold = new CathodeFloat(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool lock_on_reset = new CathodeBool(); 
		public CathodeEnum category = new CathodeEnum(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
		public CathodeFloat interaction_distance_threshold = new CathodeFloat(); 
		//start
		//start_fail
		//button_released
		//broadcasted_start
		//highlight
		//unhighlight
		//lock_looked_at
		//lock_interaction
		//geometry
		//highlight_geometry
		//target_pickup_item
		//icon_user
		//unlocked_text
		//locked_text
		//icon_keyframe
		//can_be_used
		//push_hold_time
	};

	//94-6C-CE-DC
	public class UI_KeyGate: EntityMethodInterface {
		public CathodeFloat _6B_64_8F_1B = new CathodeFloat(); 
		public CathodeInteger carduid = new CathodeInteger(); 
		public CathodeFloat request_open = new CathodeFloat(); 
		public CathodeString code = new CathodeString(); 
		//keycard_success
		//keycode_success
		//keycard_fail
		//keycode_fail
		//keycard_cancelled
		//keycode_cancelled
		//ui_breakout_triggered
		//lock_on_reset
		//light_on_reset
		//key_type
	};

	//75-D7-D9-B9
	public class UI_ReactionGame: EntityMethodInterface {
		//success
		//fail
		//stage0_success
		//stage0_fail
		//stage1_success
		//stage1_fail
		//stage2_success
		//stage2_fail
		//ui_breakout_triggered
		//resources_finished_unloading
		//resources_finished_loading
		//completion_percentage
		//exit_on_fail
	};

	//7B-96-77-BA
	public class UIBreathingGameIcon: EntityMethodInterface {
		//fill_percentage
		//prompt_text
	};

	//26-D1-5E-78
	public class UiSelectionBox: EntityMethodInterface {
		public CathodeString radius = new CathodeString(); 
		public CathodeVector3 half_dimensions = new CathodeVector3(); 
		public CathodeTransform position = new CathodeTransform(); 
		public CathodeBool enable_on_reset = new CathodeBool(); 
		public CathodeBool is_priority = new CathodeBool(); 
		public CathodeBool attach_on_reset = new CathodeBool(); 
	};

	//02-BF-0D-A4
	public class UnlockAchievement: EntityMethodInterface {
		public CathodeString achievement_id = new CathodeString(); 
	};

	//E3-21-E3-8D
	public class UnlockMapDetail: EntityMethodInterface {
		//map_keyframe
		//details
	};

	//03-06-03-44
	public class UpdateGlobalPosition: EntityMethodInterface {
		public CathodeString PositionName = new CathodeString(); 
	};

	//36-A7-68-D3
	public class UpdateLeaderBoardDisplay: EntityMethodInterface {
		//time
	};

	//A8-44-A7-9D
	public class UpdatePrimaryObjective: EntityMethodInterface {
		public CathodeBool show_message = new CathodeBool(); 
		public CathodeBool clear_objective = new CathodeBool(); 
	};

	//40-BD-17-84
	public class UpdateSubObjective: EntityMethodInterface {
		public CathodeInteger slot_number = new CathodeInteger(); 
		public CathodeBool show_message = new CathodeBool(); 
		public CathodeBool clear_objective = new CathodeBool(); 
	};

	//04-B5-1C-0E
	public class VariableBool: EntityMethodInterface {
		public CathodeFloat refresh = new CathodeFloat(); 
		public CathodeBool is_persistent = new CathodeBool(); 
		public CathodeBool initial_value = new CathodeBool(); 
	};

	//6B-E1-68-0C
	public class VariableColour: EntityMethodInterface {
		public CathodeVector3 initial_colour = new CathodeVector3(); 
	};

	//B7-72-C7-39
	public class VariableEnum: EntityMethodInterface {
		public CathodeEnum initial_value = new CathodeEnum(); 
		//is_persistent
		//VariableEnumString
	};

	//30-11-7B-69
	public class VariableFlashScreenColour: EntityMethodInterface {
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeVector3 initial_colour = new CathodeVector3(); 
		public CathodeString flash_layer_name = new CathodeString(); 
		//pause_on_reset
	};

	//17-1D-94-4A
	public class VariableFloat: EntityMethodInterface {
		public CathodeFloat initial_value = new CathodeFloat(); 
		public CathodeBool is_persistent = new CathodeBool(); 
		public CathodeFloat reseted = new CathodeFloat(); 
	};

	//A9-BF-F3-59
	public class VariableInt: EntityMethodInterface {
		public CathodeInteger initial_value = new CathodeInteger(); 
		//is_persistent
	};

	//CD-90-05-42
	public class VariableObject: EntityMethodInterface {
		//initial
	};

	//80-DE-6E-A5
	public class VariablePosition: EntityMethodInterface {
	};

	//97-D8-31-55
	public class VariableString: EntityMethodInterface {
		public CathodeString initial_value = new CathodeString(); 
		public CathodeFloat refresh = new CathodeFloat(); 
		public CathodeBool is_persistent = new CathodeBool(); 
	};

	//71-0A-23-27
	public class VariableThePlayer: EntityMethodInterface {
	};

	//A8-AF-32-47
	public class VariableTriggerObject: EntityMethodInterface {
	};

	//50-49-D6-AF
	public class VariableVector: EntityMethodInterface {
		public CathodeFloat initial_y = new CathodeFloat(); 
		public CathodeFloat initial_z = new CathodeFloat(); 
		public CathodeFloat initial_x = new CathodeFloat(); 
	};

	//E0-3F-42-97
	public class VariableVector2: EntityMethodInterface {
		public CathodeVector3 initial_value = new CathodeVector3(); 
	};

	//05-52-91-B4
	public class VectorAdd: EntityMethodInterface {
		public CathodeVector3 RHS = new CathodeVector3(); 
	};

	//D8-FE-76-52
	public class VectorDistance: EntityMethodInterface {
		public CathodeFloat triggered = new CathodeFloat(); 
		//LHS
		//RHS
		//Result
	};

	//45-B9-E8-17
	public class VectorLinearInterpolateSpeed: EntityMethodInterface {
		public CathodeFloat started = new CathodeFloat(); 
		public CathodeBool PingPong = new CathodeBool(); 
		//on_finished
		//on_think
		//Initial_Value
		//Target_Value
		//Reverse
		//Result
		//Speed
		//Loop
	};

	//93-1D-76-2A
	public class VectorLinearInterpolateTimed: EntityMethodInterface {
		public CathodeVector3 Initial_Value = new CathodeVector3(); 
		public CathodeVector3 Target_Value = new CathodeVector3(); 
		public CathodeFloat Time = new CathodeFloat(); 
		public CathodeBool Loop = new CathodeBool(); 
		public CathodeBool PingPong = new CathodeBool(); 
		public CathodeBool Reverse = new CathodeBool(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		//on_finished
		//on_think
		//Result
	};

	//E2-C3-F7-56
	public class VectorLinearProportion: EntityMethodInterface {
		public CathodeVector3 Initial_Value = new CathodeVector3(); 
		public CathodeVector3 Target_Value = new CathodeVector3(); 
		//Proportion
		//Result
	};

	//7B-56-FC-9F
	public class VectorMultiply: EntityMethodInterface {
		public CathodeVector3 RHS = new CathodeVector3(); 
		public CathodeVector3 LHS = new CathodeVector3(); 
	};

	//BB-36-AB-B4
	public class VectorMultiplyByPos: EntityMethodInterface {
		public CathodeVector3 Vector = new CathodeVector3(); 
		//WorldPos
		//Result
	};

	//4D-31-67-28
	public class VectorReflect: EntityMethodInterface {
		public CathodeBool _02_DD_46_3D = new CathodeBool(); 
		//Input
		//Normal
		//Result
	};

	//06-B7-81-BD
	public class VectorScale: EntityMethodInterface {
		public CathodeFloat RHS = new CathodeFloat(); 
		public CathodeVector3 LHS = new CathodeVector3(); 
		//Result
	};

	//50-0B-97-E1
	public class VectorSubtract: EntityMethodInterface {
	};

	//C8-2C-E2-94
	public class VignetteSettings: EntityMethodInterface {
		public CathodeFloat vignette_factor = new CathodeFloat(); 
		public CathodeFloat intensity = new CathodeFloat(); 
		public CathodeBool start_on_reset = new CathodeBool(); 
		public CathodeInteger priority = new CathodeInteger(); 
		public CathodeEnum blend_mode = new CathodeEnum(); 
		//vignette_chromatic_aberration_scale
	};

	//5F-DE-AF-64
	public class Weapon_AINotifier: EntityMethodInterface {
	};

	//56-0A-6D-46
	public class WEAPON_AmmoTypeFilter: EntityMethodInterface {
		public CathodeEnum AmmoType = new CathodeEnum(); 
		//passed
		//failed
	};

	//57-10-C0-81
	public class WEAPON_DamageFilter: EntityMethodInterface {
		public CathodeInteger damage_threshold = new CathodeInteger(); 
		//passed
		//failed
		//WEAPON_DidHitSomethingFilter
		//passed
		//failed
	};

	//C1-62-0C-BB
	public class WEAPON_DidHitSomethingFilter: EntityMethodInterface {
	};

	//9D-20-F1-28
	public class WEAPON_Effect: EntityMethodInterface {
		public CathodeFloat LifeTime = new CathodeFloat(); 
		//WorldPos
		//AttachedEffects
		//UnattachedEffects
	};

	//EB-02-FC-CB
	public class WEAPON_GiveToCharacter: EntityMethodInterface {
		public CathodeBool is_holstered = new CathodeBool(); 
	};

	//F7-6F-B6-18
	public class WEAPON_GiveToPlayer: EntityMethodInterface {
		public CathodeEnum weapon = new CathodeEnum(); 
		public CathodeBool holster = new CathodeBool(); 
		//starting_ammo
	};

	//65-22-D9-5F
	public class WEAPON_ImpactAngleFilter: EntityMethodInterface {
		public CathodeFloat ReferenceAngle = new CathodeFloat(); 
		//greater
		//less
	};

	//D7-95-33-6A
	public class WEAPON_ImpactCharacterFilter: EntityMethodInterface {
		public CathodeEnum character_classes = new CathodeEnum(); 
		public CathodeEnum character_body_location = new CathodeEnum(); 
		//passed
		//failed
	};

	//85-DA-1B-2A
	public class WEAPON_ImpactEffect: EntityMethodInterface {
		public CathodeBool RandomRotation = new CathodeBool(); 
		public CathodeFloat LifeTime = new CathodeFloat(); 
		public CathodeInteger Priority = new CathodeInteger(); 
		public CathodeEnum Orientation = new CathodeEnum(); 
		public CathodeFloat SafeDistant = new CathodeFloat(); 
		public CathodeFloat impacted = new CathodeFloat(); 
		public CathodeEnum Type = new CathodeEnum(); 
		public CathodeFloat character_damage_offset = new CathodeFloat(); 
		//StaticEffects
		//DynamicEffects
		//DynamicAttachedEffects
	};

	//7C-15-3C-25
	public class WEAPON_ImpactFilter: EntityMethodInterface {
		public CathodeString PhysicMaterial = new CathodeString(); 
		//passed
		//failed
	};

	//37-FF-AF-E8
	public class WEAPON_ImpactInspector: EntityMethodInterface {
		//damage
		//impact_position
		//impact_target
	};

	//BE-DF-25-36
	public class WEAPON_ImpactOrientationFilter: EntityMethodInterface {
		public CathodeFloat ThresholdAngle = new CathodeFloat(); 
		public CathodeEnum Orientation = new CathodeEnum(); 
		//passed
		//failed
	};

	//42-ED-20-28
	public class WEAPON_MultiFilter: EntityMethodInterface {
		public CathodeEnum DamageType = new CathodeEnum(); 
		public CathodeInteger DamageThreshold = new CathodeInteger(); 
		public CathodeBool UseAmmoFilter = new CathodeBool(); 
		public CathodeEnum AmmoType = new CathodeEnum(); 
		//passed
		//failed
		//AttackerFilter
		//TargetFilter
	};

	//60-E4-69-5E
	public class Zone: EntityMethodInterface {
		public CathodeBool force_visible_on_load = new CathodeBool(); 
		public CathodeString name = new CathodeString(); 
		public CathodeBool suspend_on_unload = new CathodeBool(); 
		public CathodeBool space_visible = new CathodeBool(); 
		//composites
	};

	//6A-CB-C1-63
	public class ZoneExclusionLink: EntityMethodInterface {
		public CathodeBool exclude_streaming = new CathodeBool(); 
		//ZoneA
		//ZoneB
	};

	//D6-BB-78-BF
	public class ZoneLink: EntityMethodInterface {
		public CathodeBool open_on_reset = new CathodeBool(); 
		public CathodeBool lock_on_reset = new CathodeBool(); 
		public CathodeInteger cost = new CathodeInteger(); 
		public CathodeFloat open = new CathodeFloat(); 
		//ZoneA
		//ZoneB
	};

	//48-2F-0F-87
	public class ZoneLoaded: EntityMethodInterface {
		//on_loaded
		//on_unloaded
	};

	//2C-58-DA-9C
	//public class UiSelectionSphere {
		//is_priority
	//};

	//C1-E1-21-FD
	//public class ProjectiveDecal {
		//deleted
		//show_on_reset
		//time
		//include_in_planar_reflections
		//material
		//resource
	//};

	//61-F1-0B-BA
	//public class CameraCollisionBox {
	//};

	//8A-B7-06-C0
	//public class CameraFinder {
		//camera_name
	//};

	//0D-8C-3E-81
	//public class CameraBehaviorInterface {
		//start_on_reset
		//pause_on_reset
		//enable_on_reset
		//linked_cameras
		//behavior_name
		//priority
		//threshold
		//blend_in
		//duration
		//blend_out
	//};

	//8C-68-95-C0
	//public class HandCamera {
		//noise_type
		//frequency
		//damping
		//rotation_intensity
		//min_fov_range
		//max_fov_range
		//min_noise
		//max_noise
	//};

	//24-6B-AE-A2
	//public class CameraPathDriven {
		//position_path
		//target_path
		//reference_path
		//position_path_transform
		//target_path_transform
		//reference_path_transform
		//point_to_project
		//path_driven_type
		//invert_progression
		//position_path_offset
		//target_path_offset
		//animation_duration
	//};

	//CA-FD-76-9C
	//public class CameraPath {
		//linked_splines
		//path_name
		//path_type
		//path_class
		//is_local
		//relative_position
		//is_loop
		//duration
	//};

	//93-7F-A3-AF
	//public class CameraDofController {
		//character_to_focus
		//focal_length_mm
		//focal_plane_m
		//fnum
		//focal_point
		//focal_point_offset
		//bone_to_focus
	//};

	//6B-4D-F6-7E
	//public class GetCurrentCameraTarget {
		//target
		//distance
	//};

	//E9-67-37-FA
	//public class CharacterCommand {
		//command_started
		//override_all_ai
	//};

	//5A-AF-92-61
	//public class CMD_MoveTowards {
		//succeeded
		//failed
		//MoveTarget
		//AimTarget
		//move_type
		//disallow_traversal
		//should_be_aiming
		//use_current_target_as_aim
		//never_succeed
	//};

	//78-6D-4A-52
	//public class CMD_ForceReloadWeapon {
		//success
	//};

	//BA-2C-25-47
	//public class CHR_LocomotionDuck {
		//Height
	//};

	//DD-63-83-1E
	//public class CMD_ShootAt {
		//succeeded
		//failed
		//Target
	//};

	//C4-B2-24-87
	//public class CMD_AimAtCurrentTarget {
		//succeeded
		//Raise_gun
	//};

	//59-83-9B-E2
	//public class CMD_Ragdoll {
		//finished
		//actor
		//impact_velocity
	//};

	//31-F8-65-3D
	//public class CHR_SetAndroidThrowTarget {
		//thrown
		//throw_position
	//};

	//1D-50-E7-77
	//public class CHR_GetAlliance {
		//Alliance
	//};

	//21-DD-D8-A7
	//public class ALLIANCE_SetDisposition {
		//A
		//B
		//Disposition
	//};

	//0A-92-AB-09
	//public class ALLIANCE_ResetAll {
	//};

	//E4-5C-08-0F
	//public class CHR_SetHeadVisibility {
		//is_visible
	//};

	//52-57-BE-5A
	//public class MonitorBase {
	//};

	//F9-BC-B1-FF
	//public class CHR_KnockedOutMonitor {
		//on_knocked_out
		//on_recovered
	//};

	//57-24-08-B1
	//public class CHR_RetreatMonitor {
		//reached_retreat
		//started_retreating
	//};

	//12-D5-B4-CF
	//public class NPC_SetSenseSet {
		//SenseSet
	//};

	//9E-2E-6C-6C
	//public class NPC_GetLastSensedPositionOfTarget {
		//NoRecentSense
		//SensedOnLeft
		//SensedOnRight
		//SensedInFront
		//SensedBehind
		//OptionalTarget
		//LastSensedPosition
		//MaxTimeSince
	//};

	//56-AF-D8-F0
	//public class NPC_Squad_GetAwarenessState {
		//All_Dead
		//Stunned
		//Unaware
		//Suspicious
		//SearchingArea
		//SearchingLastSensed
		//Aware
	//};
	
	//B5-72-D4-B4
	//public class NPC_Squad_GetAwarenessWatermark {
		//All_Dead
		//Stunned
		//Unaware
		//Suspicious
		//SearchingArea
		//SearchingLastSensed
		//Aware
	//};

	//4D-8F-DB-EF
	//public class NPC_SetAlienDevelopmentStage {
		//AlienStage
		//Reset
	//};

	//A3-24-4C-D6
	//public class NPC_SetAimTarget {
		//Target
	//};

	//84-A6-F5-2A
	//public class NPC_SetAutoTorchMode {
		//AutoUseTorchInDark
	//};

	//15-69-EB-C1
	//public class NPC_GetCombatTarget {
		//bound_trigger
		//target
	//};

	//62-7A-05-56
	//public class NPC_SetTotallyBlindInDark {
	//};

	//D4-91-54-93
	//public class NPC_SetAgressionProgression {
		//allow_progression
	//};

	//04-E4-3E-D6
	//public class NPC_SetLocomotionStyleForJobs {
	//};

	//3C-50-BA-D8
	//public class NPC_SetGunAimMode {
		//AimingMode
	//};

	//AA-59-94-39
	//public class CHR_HasWeaponOfType {
		//on_true
		//on_false
		//Result
		//weapon_type
		//check_if_weapon_draw
	//};

	//D3-07-0C-8C
	//public class NPC_StopShooting {
	//};

	//88-37-12-72
	//public class Chr_PlayerCrouch {
		//crouch
	//};

	//35-83-31-F5
	//public class Custom_Hiding_Controller {
		//Started_Idle
		//Started_Exit
		//Got_Out
		//Prompt_1
		//Prompt_2
		//Start_choking
		//Start_oxygen_starvation
		//Show_MT
		//Hide_MT
		//Spawn_MT
		//Despawn_MT
		//Start_Busted_By_Alien
		//Start_Busted_By_Android
		//End_Busted_By_Android
		//Start_Busted_By_Human
		//End_Busted_By_Human
		//Enter_Anim
		//Idle_Anim
		//Exit_Anim
		//has_MT
		//is_high
		//AlienBusted_Player_1
		//AlienBusted_Alien_1
		//AlienBusted_Player_2
		//AlienBusted_Alien_2
		//AlienBusted_Player_3
		//AlienBusted_Alien_3
		//AlienBusted_Player_4
		//AlienBusted_Alien_4
		//AndroidBusted_Player_1
		//AndroidBusted_Android_1
		//AndroidBusted_Player_2
		//AndroidBusted_Android_2
		//MT_pos
	//};

	//BE-D8-24-58
	//public class PlayerWeaponMonitor {
		//on_clip_above_percentage
		//on_clip_below_percentage
		//on_clip_empty
		//on_clip_full
		//weapon_type
		//ammo_percentage_in_clip
	//};

	//11-A9-47-32
	//public class RemoveWeaponsFromPlayer {
	//};

	//A4-C1-CF-1C
	//public class PlayerDiscardsItems {
		//discard_ieds
		//discard_medikits
		//discard_ammo
		//discard_flares_and_lights
		//discard_materials
		//discard_batteries
	//};

	//23-C0-88-B8
	//public class PlayerDiscardsTools {
		//discard_motion_tracker
		//discard_cutting_torch
		//discard_hacking_tool
		//discard_keycard
	//};

	//C2-02-F1-59
	//public class WEAPON_AttackerFilter {
		//passed
		//failed
		//filter
	//};

	//B7-56-4B-0F
	//public class WEAPON_TargetObjectFilter {
		//passed
		//failed
		//filter
	//};

	//80-42-AD-3B
	//public class VariableHackingConfig {
		//nodes
		//sensors
		//victory_nodes
		//victory_sensors
	//};

	//7E-23-18-65
	//public class VariableFilterObject {
	//};

	//98-83-5A-C9
	//public class VariableAnimationInfo {
		//AnimationSet
		//Animation
	//};

	//CE-B7-8A-B4
	//public class ExternalVariableBool {
		//game_variable
	//};

	//80-CB-19-D4
	//public class NonPersistentBool {
		//initial_value
	//};

	//42-5E-D3-C8
	//public class NonPersistentInt {
		//initial_value
		//is_persistent
	//};

	//D4-1B-92-50
	//public class StateQuery {
		//on_true
		//on_false
		//Input
		//Result
	//};

	//93-40-60-2F
	//public class IsActive {
	//};

	//23-D8-9F-30
	//public class IsStarted {
	//};

	//49-FC-8B-4A
	//public class IsPaused {
	//};

	//F3-E3-CD-28
	//public class IsSuspended {
	//};

	//3E-1D-BE-FF
	//public class IsAttached {
	//};

	//20-FE-FE-EB
	//public class IsEnabled {
	//};

	//0D-9B-DF-41
	//public class IsOpen {
	//};

	//4B-B9-C5-7F
	//public class IsOpening {
	//};

	//9A-32-81-7A
	//public class IsLocked {
	//};

	//B2-82-EF-68
	//public class IsVisible {
	//};

	//48-3A-AA-90
	//public class BooleanLogicInterface {
		//on_true
		//on_false
		//LHS
		//RHS
		//Result
	//};

	//64-6B-3C-30
	//public class LogicGateNotEqual {
	//};

	//BB-32-2D-ED
	//public class BooleanLogicOperation {
		//Input
		//Result
	//};

	//03-A5-D9-E5
	//public class FloatMath_All {
		//Numbers
		//Result
	//};

	//8A-93-65-BC
	//public class FloatMultiply_All {
		//Invert
	//};

	//BB-4E-99-91
	//public class FloatMin_All {
	//};

	//DA-11-C8-7F
	//public class FloatMath {
		//LHS
		//RHS
		//Result
	//};

	//71-9E-9E-A5
	//public class FloatRemainder {
	//};

	//6A-0D-82-1D
	//public class FloatOperation {
		//Input
		//Result
	//};

	//B5-DB-72-D5
	//public class FloatReciprocal {
	//};

	//8A-4E-E9-33
	//public class FloatCompare {
		//on_true
		//on_false
		//LHS
		//RHS
		//Threshold
		//Result
	//};

	//E0-DF-59-AF
	//public class FilterAbsorber {
		//output
		//factor
		//start_value
		//input
	//};

	//3F-64-5F-E2
	//public class IntegerMath_All {
		//Numbers
		//Result
	//};

	//1D-86-40-8C
	//public class IntegerMultiply_All {
	//};

	//57-DE-38-55
	//public class IntegerMax_All {
	//};

	//4D-D1-B7-89
	//public class IntegerMin_All {
	//};

	//C5-4B-46-3B
	//public class IntegerMath {
		//LHS
		//RHS
		//Result
	//};

	//2D-F1-FD-98
	//public class IntegerRemainder {
	//};

	//B6-F6-D3-04
	//public class IntegerOr {
	//};

	//04-D1-D2-92
	//public class IntegerOperation {
		//Input
		//Result
	//};

	//D3-71-D8-33
	//public class IntegerCompliment {
	//};

	//56-D6-AB-9F
	//public class IntegerCompare {
		//on_true
		//on_false
		//LHS
		//RHS
		//Result
	//};

	//FD-60-91-60
	//public class VectorMath {
		//LHS
		//RHS
		//Result
	//};

	//B9-87-5D-6F
	//public class VectorProduct {
	//};

	//2B-D3-F0-47
	//public class VectorNormalise {
		//Input
		//Result
	//};

	//70-59-F2-5D
	//public class VectorModulus {
		//Input
		//Result
	//};

	//63-F0-EE-CB
	//public class ScalarProduct {
		//LHS
		//RHS
		//Result
	//};

	//C2-5B-FE-EE
	//public class VectorDirection {
		//From
		//To
		//Result
	//};

	//C6-A3-7D-1D
	//public class VectorYaw {
		//Vector
		//Result
	//};

	//54-E5-E6-86
	//public class VectorRotateYaw {
		//Vector
		//Yaw
		//Result
	//};

	//B6-7B-E7-A4
	//public class VectorRotateRoll {
		//Vector
		//Roll
		//Result
	//};

	//A8-D7-94-7A
	//public class VectorRotatePitch {
		//Vector
		//Pitch
		//Result
	//};

	//A5-0C-73-08
	//public class VectorRotateByPos {
		//Vector
		//WorldPos
		//Result
	//};

	//13-C2-A1-76
	//public class SetVector2 {
		//Input
		//Result
	//};

	//D2-3E-36-A0
	//public class GetComponentInterface {
		//Input
		//Result
	//};

	//9B-10-73-B8
	//public class Persistent_TriggerRandomSequence {
		//Random_1
		//Random_2
		//Random_3
		//Random_4
		//Random_5
		//Random_6
		//Random_7
		//Random_8
		//Random_9
		//Random_10
		//All_triggered
		//current
		//num
	//};

	//99-7A-75-FC
	//public class MultitrackLoop {
		//current_time
		//loop_condition
		//start_time
		//end_time
	//};

	//89-73-7C-05
	//public class ReTransformer {
		//new_transform
		//result
	//};

	//3A-29-9F-6E
	//public class SaveGlobalProgression {
	//};

	//E6-9B-55-C3
	//public class PlayerCampaignDeaths {
	//};

	//D7-00-7A-1B
	//public class PlayerCampaignDeathsInARow {
	//};

	//83-12-A6-DC
	//public class EndGame {
		//on_game_end_started
		//on_game_ended
		//success
	//};

	//8D-D6-5C-A2
	//public class LeaveGame {
		//disconnect_from_session
	//};

	//CB-78-F2-42
	//public class DebugCaptureScreenShot {
		//finished_capturing
		//wait_for_streamer
		//capture_filename
		//fov
		//near
		//far
	//};

	//77-18-F1-6C
	//public class DebugCaptureCorpse {
		//finished_capturing
		//character
		//corpse_name
	//};

	//9B-75-F6-DE
	//public class DebugMenuToggle {
		//debug_variable
		//value
	//};

	//2B-1C-D7-2A
	//public class AllPlayersReady {
		//on_all_players_ready
		//start_on_reset
		//pause_on_reset
		//activation_delay
	//};

	//C7-B2-B0-17
	//public class SyncOnAllPlayers {
		//on_synchronized
		//on_synchronized_host
	//};

	//7A-C8-51-DB
	//public class SyncOnFirstPlayer {
		//on_synchronized
		//on_synchronized_host
		//on_synchronized_local
	//};

	//47-2F-6F-5C
	//public class ParticipatingPlayersList {
	//};

	//D3-09-45-FC
	//public class NetPlayerCounter {
		//on_full
		//on_empty
		//on_intermediate
		//is_full
		//is_empty
		//contains_local_player
	//};

	//6E-DB-27-E9
	//public class BroadcastTrigger {
		//on_triggered
	//};

	//C2-23-FA-40
	//public class HostOnlyTrigger {
		//on_triggered
	//};

	//34-20-DD-F4
	//public class SpawnGroup {
		//on_spawn_request
		//default_group
		//trigger_on_reset
	//};

	//C6-5A-80-14
	//public class RespawnExcluder {
		//excluded_points
	//};

	//D1-1D-15-2D
	//public class RespawnConfig {
		//min_dist
		//preferred_dist
		//max_dist
		//respawn_mode
		//respawn_wait_time
		//uncollidable_time
		//is_default
	//};

	//59-21-A8-CA
	//public class NumConnectedPlayers {
		//on_count_changed
		//count
	//};

	//91-1D-17-9F
	//public class NumPlayersOnStart {
		//count
	//};

	//47-86-10-E4
	//public class NumDeadPlayers {
	//};

	//D1-70-5D-2D
	//public class NetworkedTimer {
		//on_second_changed
		//on_started_counting
		//on_finished_counting
		//time_elapsed
		//time_left
		//time_elapsed_sec
		//time_left_sec
		//duration
	//};

	//BA-41-C3-28
	//public class DebugObjectMarker {
		//marked_object
		//marked_name
	//};

	//D7-62-A4-84
	//public class EggSpawner {
		//egg_position
		//hostile_egg
	//};

	//A6-91-73-66
	//public class RandomObjectSelector {
		//objects
		//chosen_object
	//};

	//97-4C-1F-AB
	//public class IsMultiplayerMode {
	//};

	//A5-5B-75-D3
	//public class TriggerObjectsFilterCounter {
		//none_passed
		//some_passed
		//all_passed
		//objects
		//filter
	//};

	//4D-7E-81-00
	//public class TriggerContainerObjectsFilterCounter {
		//none_passed
		//some_passed
		//all_passed
		//filter
		//container
	//};

	//BC-4E-17-A9
	//public class TriggerTouch {
		//touch_event
		//enable_on_reset
		//physics_object
		//impact_normal
	//};

	//48-0F-A5-71
	//public class TriggerSync {
		//Pin1_Synced
		//Pin2_Synced
		//Pin3_Synced
		//Pin4_Synced
		//Pin5_Synced
		//Pin6_Synced
		//Pin7_Synced
		//Pin8_Synced
		//Pin9_Synced
		//Pin10_Synced
		//reset_on_trigger
	//};

	//AE-36-C7-AD
	//public class SetObject {
		//Input
		//Output
	//};

	//EE-A6-18-AC
	//public class GateResourceInterface {
		//gate_status_changed
		//request_open_on_reset
		//request_lock_on_reset
		//force_open_on_reset
		//force_close_on_reset
		//is_auto
		//auto_close_delay
		//is_open
		//is_locked
		//gate_status
	//};

	//1C-86-DF-7D
	//public class PadRumbleImpulse {
		//low_frequency_rumble
		//high_frequency_rumble
		//left_trigger_impulse
		//right_trigger_impulse
		//aim_trigger_impulse
		//shoot_trigger_impulse
	//};

	//CB-88-A8-E9
	//public class TriggerCameraViewConeMulti {
		//enter
		//exit
		//enter1
		//exit1
		//enter2
		//exit2
		//enter3
		//exit3
		//enter4
		//exit4
		//enter5
		//exit5
		//enter6
		//exit6
		//enter7
		//exit7
		//enter8
		//exit8
		//enter9
		//exit9
		//target
		//target1
		//target2
		//target3
		//target4
		//target5
		//target6
		//target7
		//target8
		//target9
		//fov
		//aspect_ratio
		//intersect_with_geometry
		//number_of_inputs
		//use_camera_fov
		//visible_area_type
		//visible_area_horizontal
		//visible_area_vertical
		//raycast_grace
	//};

	//ED-CF-6F-92
	//public class NPC_Debug_Menu_Item {
		//character
	//};

	//A2-28-27-0E
	//public class FilterIsEnemyOfAllianceGroup {
		//alliance_group
	//};

	//AC-79-FA-EB
	//public class FilterHasWeaponEquipped {
		//weapon_type
	//};

	//91-93-4B-EC
	//public class FilterIsinInventory {
		//ItemName
	//};

	//59-68-22-E4
	//public class FilterIsInAlertnessState {
		//AlertState
	//};

	//AA-41-B0-B9
	//public class FilterIsInAGroup {
	//};

	//A1-7A-F5-FB
	//public class FilterIsInWeaponRange {
		//weapon_owner
	//};

	//6E-38-43-BE
	//public class FilterSmallestUsedDifficulty {
		//difficulty
	//};

	//8C-7D-53-BB
	//public class FilterHasPlayerCollectedIdTag {
		//tag_id
	//};

	//C9-6D-3A-B8
	//public class JobWithPosition {
	//};

	//5A-AF-05-B5
	//public class JOB_AreaSweepFlare {
	//};

	//4E-0A-FE-B7
	//public class JOB_SpottingPosition {
		//SpottingPosition
	//};

	//E3-42-20-82
	//public class JOB_Assault {
	//};

	//3B-AC-F8-AD
	//public class Job {
		//TaskPosition
		//filter
		//should_stop_moving_when_reached
		//should_orientate_when_reached
		//reached_distance_threshold
		//selection_priority
		//timeout
		//always_on_tracker
		//InitialTask
		//start_on_reset
	//};

	//4D-16-92-99
	//public class NPC_SetRateOfFire {
		//MinTimeBetweenShots
		//RandomRange
	//};

	//CA-51-D3-53
	//public class NPC_SetFiringRhythm {
		//MinShootingTime
		//RandomRangeShootingTime
		//MinNonShootingTime
		//RandomRangeNonShootingTime
		//MinCoverNonShootingTime
		//RandomRangeCoverNonShootingTime
	//};

	//92-BE-7B-F9
	//public class NPC_ResetFiringStats {
	//};

	//1A-73-0A-94
	//public class SoundPlaybackBaseClass {
		//on_finished
		//attached_sound_object
		//sound_event
		//is_occludable
		//argument_1
		//argument_2
		//argument_3
		//argument_4
		//argument_5
		//namespace
		//object_position
		//restore_on_checkpoint
	//};

	//A3-56-39-96
	//public class NPC_DynamicDialogueGlobalRange {
		//dialogue_range
	//};

	//02-88-BA-92
	//public class SoundSpline {
	//};

	//D7-59-40-C3
	//public class SoundEnvironmentZone {
		//reverb_name
		//priority
		//position
		//half_dimensions
	//};

	//20-D8-CC-57
	//public class RemoveFromInventory {
		//success
		//fail
		//items
	//};

	//70-F8-AA-C0
	//public class LimitItemUse {
		//enable_on_reset
		//items
	//};

	//F2-DE-C4-8F
	//public class PlayerHasItemEntity {
		//success
		//fail
		//items
	//};

	//90-98-28-C3
	//public class MultiplePickupSpawner {
		//pos
		//item_name
	//};

	//E2-1B-47-A2
	//public class QueryGCItemPool {
		//count
		//item_name
		//item_quantity
	//};

	//64-42-49-1E
	//public class RemoveFromGCItemPool {
		//on_success
		//on_failure
		//item_name
		//item_quantity
		//gcip_instances_to_remove
	//};

	//49-6A-20-07
	//public class DurangoVideoCapture {
		//clip_name
	//};

	//DF-BF-76-62
	//public class VideoCapture {
		//clip_name
		//only_in_capture_mode
	//};

	//42-A8-9A-43
	//public class EnableMotionTrackerPassiveAudio {
	//};

	//16-F9-CC-C7
	//public class FlashCallback {
		//callback
		//callback_name
	//};

	//DD-4D-63-35
	//public class UI_Attached {
		//closed
		//ui_icon
	//};

	//01-F5-84-66
	//public class UI_Keypad {
		//success
		//fail
		//code
		//exit_on_fail
	//};

	//F2-5E-AA-7B
	//public class StartNewChapter {
		//chapter
	//};

	//27-52-8B-23
	//public class UnlockLogEntry {
		//entry
	//};

	//E1-CF-30-B2
	//public class RewireTotalPowerResource {
		//total_power
	//};

	//64-6F-34-5A
	//public class SetMotionTrackerRange {
		//range
	//};

	//53-6C-AD-BB
	//public class SetGamepadAxes {
		//invert_x
		//invert_y
		//save_settings
	//};

	//B8-D1-F1-A6
	//public class SetGameplayTips {
		//tip_string_id
	//};

	//7D-4E-79-E5
	//public class GameplayTip {
		//string_id
	//};

	//F6-31-AA-A4
	//public class IsPlaylistTypeSingle {
		//single
	//};

	//F1-48-68-96
	//public class IsCurrentLevelAChallengeMap {
		//challenge_map
	//};

	//2D-FA-9A-8A
	//public class IsCurrentLevelAPreorderMap {
		//preorder_map
	//};

	//65-D3-CD-F0
	//public class SetObjectiveCompleted {
		//objective_id
	//};

	//A9-1E-C9-BD
	//public class CoverLine {
		//enable_on_reset
		//LinePath
		//low
		//resource
		//LinePathPosition
	//};

	//67-9B-2C-B1
	//public class TRAV_ContinuousPipe {
		//OnEnter
		//OnExit
		//enable_on_reset
		//LinePath
		//InUse
		//character_classes
	//};

	//5F-73-11-4F
	//public class TRAV_ContinuousLedge {
		//OnEnter
		//OnExit
		//enable_on_reset
		//LinePath
		//InUse
		//Dangling
		//Sidling
		//character_classes
	//};

	//40-11-33-00
	//public class TRAV_ContinuousClimbingWall {
		//OnEnter
		//OnExit
		//LinePath
		//InUse
		//Dangling
		//character_classes
	//};

	//3D-4D-E4-D1
	//public class TRAV_ContinuousCinematicSidle {
		//OnEnter
		//OnExit
		//enable_on_reset
		//LinePath
		//InUse
		//character_classes
	//};

	//F7-3F-1C-42
	//public class TRAV_ContinuousBalanceBeam {
		//OnEnter
		//OnExit
		//enable_on_reset
		//LinePath
		//InUse
		//character_classes
	//};

	//D9-E9-B2-AE
	//public class TRAV_ContinuousTightGap {
		//OnEnter
		//OnExit
		//enable_on_reset
		//LinePath
		//InUse
		//character_classes
	//};

	//0C-52-2C-D6
	//public class TRAV_1ShotVentEntrance {
		//OnEnter
		//Completed
		//enable_on_reset
		//LinePath
		//character_classes
		//resource
	//};

	//7D-23-8C-9E
	//public class TRAV_1ShotVentExit {
		//OnExit
		//Completed
		//enable_on_reset
		//LinePath
		//character_classes
		//resource
	//};

	//9D-A6-A3-5C
	//public class TRAV_1ShotFloorVentEntrance {
		//OnEnter
		//Completed
		//enable_on_reset
		//LinePath
		//character_classes
		//resource
	//};

	//45-D2-2B-A9
	//public class TRAV_1ShotFloorVentExit {
		//OnExit
		//Completed
		//enable_on_reset
		//LinePath
		//character_classes
		//resource
	//};

	//F6-AE-E0-A0
	//public class TRAV_1ShotClimbUnder {
		//OnEnter
		//OnExit
		//enable_on_reset
		//LinePath
		//InUse
		//character_classes
	//};

	//31-41-DC-BC
	//public class TRAV_1ShotLeap {
		//OnEnter
		//OnExit
		//OnSuccess
		//OnFailure
		//enable_on_reset
		//StartEdgeLinePath
		//EndEdgeLinePath
		//InUse
		//MissDistance
		//NearMissDistance
		//character_classes
	//};

	//0E-6F-16-B9
	//public class NavMeshArea {
		//position
		//half_dimensions
		//area_type
	//};

	//C9-B7-37-C9
	//public class SpottingExclusionArea {
		//position
		//half_dimensions
	//};

	//6E-B3-35-6C
	//public class NPC_SetChokePoint {
		//chokepoints
	//};

	//06-7C-BA-2D
	//public class PostprocessingSettings {
		//intensity
		//priority
		//blend_mode
	//};

	//2F-EE-8A-C0
	//public class HighSpecMotionBlurSettings {
		//contribution
		//camera_velocity_scalar
		//camera_velocity_min
		//camera_velocity_max
		//object_velocity_scalar
		//object_velocity_min
		//object_velocity_max
		//blur_range
	//};

	//C5-C4-4A-D2
	//public class SharpnessSettings {
		//local_contrast_factor
	//};

	//59-BF-3F-28
	//public class HableToneMappingSettings {
		//shoulder_strength
		//linear_strength
		//linear_angle
		//toe_strength
		//toe_numerator
		//toe_denominator
		//linear_white_point
	//};

	//02-D2-4D-F8
	//public class GetSplineLength {
		//spline
		//Result
	//};

	//BF-EF-74-3F
	//public class GetCentrePoint {
		//Positions
		//position_of_centre
	//};

	//95-14-66-0F
	//public class DistortionOverlay {
		//intensity
		//time
		//distortion_texture
		//alpha_threshold_enabled
		//threshold_texture
		//range
		//begin_start_time
		//begin_stop_time
		//end_start_time
		//end_stop_time
	//};

	//87-C8-13-55
	//public class ScreenFadeOutToWhiteTimed {
		//on_finished
		//time
	//};

	//2A-04-AB-4E
	//public class PhysicsApplyBuoyancy {
		//objects
		//water_height
		//water_density
		//water_viscosity
		//water_choppiness
	//};

	//7D-1D-1B-65
	//public class GetCharacterRotationSpeed {
		//character
		//speed
	//};

	//CA-A4-7C-4C
	//public class LevelCompletionTargets {
		//TargetTime
		//NumDeaths
		//TeamRespawnBonus
		//NoLocalRespawnBonus
		//NoRespawnBonus
		//GrappleBreakBonus
	//};

	//71-7E-BB-5E
	//public class Showlevel_Completed {
	//};

	//F3-56-BB-62
	//public class DebugGraph {
		//Inputs
		//scale
		//duration
		//samples_per_second
		//auto_scale
		//auto_scroll
	//};

	//2C-18-33-3E
	//public class ThrowingPointOfImpact {
		//show_point_of_impact
		//hide_point_of_impact
		//Location
		//Visible
	//};

	//E1-16-4A-8F
	//public class VisibilityMaster {
		//renderable
		//mastered_by_visibility
	//};

	//2B-64-5B-9E
	//public class PlayerLightProbe {
		//output
		//light_level_for_ai
		//dark_threshold
		//fully_lit_threshold
	//};
}
