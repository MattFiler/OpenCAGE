#include "LIVE_SETTINGS.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <clocale>
#include <cmath>
#include <cstdio>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds). Each run of
	numbers is found from where the run starts; the numbers inside a run sit the same way in every build.
*/
namespace
{
	// Where each file's numbers start in the game
	constexpr uintptr_t kRadiosity = 0x0124fea8;       // the six radiosity numbers, then the glow's pulse rate and power
	constexpr uintptr_t kRadiosityUnused = 0x01251368; // the radiosity file's last two numbers, which nothing uses
	constexpr uintptr_t kSkin = 0x0125159c;
	constexpr uintptr_t kHair = 0x0124f030;
	constexpr uintptr_t kOverlayColour = 0x0125082c;   // red, green, blue, alpha
	constexpr uintptr_t kGlobalConstants = 0x01241108;

	enum class Use { Live, Inert };

	struct Setting
	{
		uint8_t file;
		const char* key;
		uintptr_t run;     // where the run of numbers it is in starts (0: the game has no number for it)
		uint32_t offset;   // from the start of the run
		Use use;
	};

	constexpr Setting kSettings[] = {
		{ LIVE_SETTINGS::RADIOSITY, "gRadiosityEmissiveSurfaceScale", kRadiosity, 0x00, Use::Live },
		{ LIVE_SETTINGS::RADIOSITY, "gRadiosityFirstBounceScale", kRadiosity, 0x04, Use::Live },
		{ LIVE_SETTINGS::RADIOSITY, "gRadiosityMultiBounceScale", kRadiosity, 0x08, Use::Live },
		{ LIVE_SETTINGS::RADIOSITY, "gRadiosityAlbedoOverbrightAmount", kRadiosity, 0x0c, Use::Live },
		{ LIVE_SETTINGS::RADIOSITY, "gRadiosityAlbedoSaturationAmount", kRadiosity, 0x10, Use::Live },
		{ LIVE_SETTINGS::RADIOSITY, "gRadiositySpecularGlossScale", kRadiosity, 0x14, Use::Live },
		{ LIVE_SETTINGS::RADIOSITY, "gDeferredEmissiveSurfaceScale", kRadiosityUnused, 0x00, Use::Inert },
		{ LIVE_SETTINGS::RADIOSITY, "gDeferredEmissiveSurfaceExponent", kRadiosityUnused, 0x04, Use::Inert },

		{ LIVE_SETTINGS::SKIN, "scattering_radius", kSkin, 0x00, Use::Live },
		{ LIVE_SETTINGS::SKIN, "scattering_saturation", kSkin, 0x04, Use::Live },

		{ LIVE_SETTINGS::HAIR, "alpha_threshold", kHair, 0x00, Use::Live },
		{ LIVE_SETTINGS::HAIR, "alpha_threshold_shadow", kHair, 0x04, Use::Live },
		{ LIVE_SETTINGS::HAIR, "primary_spec_level", kHair, 0x08, Use::Live },
		{ LIVE_SETTINGS::HAIR, "secondary_spec_level", kHair, 0x0c, Use::Live },
		{ LIVE_SETTINGS::HAIR, "primary_spec_width", kHair, 0x10, Use::Live },
		{ LIVE_SETTINGS::HAIR, "secondary_spec_width", kHair, 0x14, Use::Live },
		{ LIVE_SETTINGS::HAIR, "spec_separation", kHair, 0x18, Use::Live },
		{ LIVE_SETTINGS::HAIR, "diffuse_level", kHair, 0x1c, Use::Live },
		{ LIVE_SETTINGS::HAIR, "base_absorption", kHair, 0x20, Use::Live },
		{ LIVE_SETTINGS::HAIR, "absorption_rate", kHair, 0x24, Use::Live },
		{ LIVE_SETTINGS::HAIR, "ao_absorption", kHair, 0x28, Use::Live },
		{ LIVE_SETTINGS::HAIR, "scatter_dist_rate", kHair, 0x2c, Use::Live },
		{ LIVE_SETTINGS::HAIR, "occlusion_rate", kHair, 0x30, Use::Live },
		{ LIVE_SETTINGS::HAIR, "occlusion_bias", kHair, 0x34, Use::Live },
		{ LIVE_SETTINGS::HAIR, "occlusion_ao_infl", kHair, 0x38, Use::Live },
		{ LIVE_SETTINGS::HAIR, "specular_occlusion", kHair, 0x3c, Use::Live },
		{ LIVE_SETTINGS::HAIR, "specular_ao", kHair, 0x40, Use::Live },
		{ LIVE_SETTINGS::HAIR, "softening_length", kHair, 0x44, Use::Live },
		{ LIVE_SETTINGS::HAIR, "softening_normal_bias", kHair, 0x48, Use::Live },
		{ LIVE_SETTINGS::HAIR, "softening_distance_rate", kHair, 0x4c, Use::Live },
		{ LIVE_SETTINGS::HAIR, "glint_intensity", 0, 0, Use::Inert },
		{ LIVE_SETTINGS::HAIR, "glint_width", 0, 0, Use::Inert },
		{ LIVE_SETTINGS::HAIR, "sub_strand_frequency", 0, 0, Use::Inert },
		{ LIVE_SETTINGS::HAIR, "sub_strand_spec_shift", 0, 0, Use::Inert },

		{ LIVE_SETTINGS::SELECTION_OVERLAY, "m_colour.red", kOverlayColour, 0x00, Use::Live },
		{ LIVE_SETTINGS::SELECTION_OVERLAY, "m_colour.green", kOverlayColour, 0x04, Use::Live },
		{ LIVE_SETTINGS::SELECTION_OVERLAY, "m_colour.blue", kOverlayColour, 0x08, Use::Live },
		{ LIVE_SETTINGS::SELECTION_OVERLAY, "m_colour.alpha", kOverlayColour, 0x0c, Use::Live },
		{ LIVE_SETTINGS::SELECTION_OVERLAY, "m_rate", kRadiosity, 0x18, Use::Live },
		{ LIVE_SETTINGS::SELECTION_OVERLAY, "m_power", kRadiosity, 0x1c, Use::Live },

		{ LIVE_SETTINGS::GLOBAL_CONSTANTS, "stealth_light_meter_full_dark_threshold", kGlobalConstants, 0xd0, Use::Live },
		{ LIVE_SETTINGS::GLOBAL_CONSTANTS, "stealth_light_meter_full_light_threshold", kGlobalConstants, 0xd4, Use::Live },
		{ LIVE_SETTINGS::GLOBAL_CONSTANTS, "stealth_light_meter_timeout_when_detected", kGlobalConstants, 0xd8, Use::Live },
		{ LIVE_SETTINGS::GLOBAL_CONSTANTS, "interaction_distance_threshold", kGlobalConstants, 0xdc, Use::Live },
		{ LIVE_SETTINGS::GLOBAL_CONSTANTS, "min_time_between_squad_shots_lower_bound", kGlobalConstants, 0xfc, Use::Live },
		{ LIVE_SETTINGS::GLOBAL_CONSTANTS, "min_time_between_squad_shots_upper_bound", kGlobalConstants, 0x100, Use::Live },
		{ LIVE_SETTINGS::GLOBAL_CONSTANTS, "min_time_suspicious_reaction_loop", kGlobalConstants, 0x104, Use::Live },
		{ LIVE_SETTINGS::GLOBAL_CONSTANTS, "max_time_suspicious_reaction_loop", kGlobalConstants, 0x108, Use::Live },
	};

	const Setting* Find(uint8_t file, const std::string& key)
	{
		for (const Setting& setting : kSettings)
			if (setting.file == file && key == setting.key)
				return &setting;
		return nullptr;
	}

	bool KnownFile(uint8_t file)
	{
		return file >= LIVE_SETTINGS::RADIOSITY && file <= LIVE_SETTINGS::GLOBAL_CONSTANTS;
	}

	// The game's number for a setting (null when this build has none)
	float* Where(const Setting& setting)
	{
		if (!setting.run)
			return nullptr;
		const uintptr_t run = DEVTOOLS_RELATIVE_ADDRESS(setting.run);
		return run ? reinterpret_cast<float*>(run + setting.offset) : nullptr;
	}

	bool Write(float* where, float value)
	{
		__try
		{
			*reinterpret_cast<volatile float*>(where) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool Read(const float* where, float& value)
	{
		__try
		{
			value = *reinterpret_cast<const volatile float*>(where);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	std::string Number(float value)
	{
		static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
		char text[32];
		_snprintf_s_l(text, sizeof(text), _TRUNCATE, "%.6g", invariant, value);
		return text;
	}
}

LIVE_LINK::Result LIVE_SETTINGS::Set(uint8_t file, const std::vector<Value>& values)
{
	if (!KnownFile(file))
		return LIVE_LINK::Result{ false, "Unknown settings file " + std::to_string(file) };

	std::string message;
	bool ok = true;
	for (const Value& value : values)
	{
		if (!message.empty())
			message += "\n";
		const Setting* setting = Find(file, value.key);
		if (!setting)
		{
			message += value.key + " unknown";
			ok = false;
			continue;
		}
		if (!std::isfinite(value.value))
		{
			message += value.key + " refused";
			ok = false;
			continue;
		}
		// A key the game never reads has nothing to write to, and one it reads but never uses is written all the same,
		// so that what the game holds matches the file
		float* where = Where(*setting);
		if (setting->run && (!where || !Write(where, value.value)))
		{
			message += value.key + " unavailable";
			ok = false;
			continue;
		}
		message += value.key + "=" + Number(value.value) + (setting->use == Use::Live ? " live" : " inert");
	}
	return LIVE_LINK::Result{ ok, message };
}

LIVE_LINK::Result LIVE_SETTINGS::Get(uint8_t file)
{
	if (!KnownFile(file))
		return LIVE_LINK::Result{ false, "Unknown settings file " + std::to_string(file) };

	std::string message;
	for (const Setting& setting : kSettings)
	{
		if (setting.file != file)
			continue;
		float* where = Where(setting);
		float value;
		if (!where || !Read(where, value))
			continue;
		if (!message.empty())
			message += "\n";
		message += std::string(setting.key) + "=" + Number(value);
	}
	return LIVE_LINK::Result{ true, message };
}
