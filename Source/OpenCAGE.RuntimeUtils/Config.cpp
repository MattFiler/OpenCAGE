#include "Config.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>

namespace
{
	struct NamedKey { const char* name; int virtualKey; };
	const NamedKey kNamedKeys[] = {
		{ "INSERT", VK_INSERT }, { "DELETE", VK_DELETE }, { "HOME", VK_HOME }, { "END", VK_END },
		{ "PAGEUP", VK_PRIOR }, { "PAGEDOWN", VK_NEXT }, { "PAUSE", VK_PAUSE }, { "SCROLLLOCK", VK_SCROLL },
		{ "BACKSPACE", VK_BACK }, { "TAB", VK_TAB }, { "SPACE", VK_SPACE },
		{ "F1", VK_F1 }, { "F2", VK_F2 }, { "F3", VK_F3 }, { "F4", VK_F4 }, { "F5", VK_F5 }, { "F6", VK_F6 },
		{ "F7", VK_F7 }, { "F8", VK_F8 }, { "F9", VK_F9 }, { "F10", VK_F10 }, { "F11", VK_F11 }, { "F12", VK_F12 },
		{ "NUMPAD0", VK_NUMPAD0 }, { "NUMPAD1", VK_NUMPAD1 }, { "NUMPAD2", VK_NUMPAD2 }, { "NUMPAD3", VK_NUMPAD3 },
		{ "NUMPAD4", VK_NUMPAD4 }, { "NUMPAD5", VK_NUMPAD5 }, { "NUMPAD6", VK_NUMPAD6 }, { "NUMPAD7", VK_NUMPAD7 },
		{ "NUMPAD8", VK_NUMPAD8 }, { "NUMPAD9", VK_NUMPAD9 },
	};

	// Accepts a key name from the table, a single letter/digit, or a virtual-key code (decimal or 0x-prefixed hex).
	int ParseKey(const std::string& text, int fallback)
	{
		std::string value;
		for (char c : text)
			if (!isspace(static_cast<unsigned char>(c)))
				value += static_cast<char>(toupper(static_cast<unsigned char>(c)));
		if (value.empty())
			return fallback;

		for (const NamedKey& key : kNamedKeys)
			if (value == key.name)
				return key.virtualKey;

		if (value.size() == 1 && ((value[0] >= 'A' && value[0] <= 'Z') || (value[0] >= '0' && value[0] <= '9')))
			return value[0];

		char* end = nullptr;
		const long code = strtol(value.c_str(), &end, 0);
		if (end && *end == '\0' && code > 0 && code < 256)
			return static_cast<int>(code);

		return fallback;
	}

	std::string IniPath()
	{
		char exePath[MAX_PATH] = {};
		GetModuleFileNameA(nullptr, exePath, MAX_PATH);
		std::string path = exePath;
		const size_t slash = path.find_last_of("\\/");
		path = (slash == std::string::npos) ? "" : path.substr(0, slash + 1);
		return path + "OpenCAGE.RuntimeUtils.ini";
	}

	Config::Settings Load()
	{
		Config::Settings settings;
		const std::string ini = IniPath();
		const char* section = "RuntimeUtils";

		auto flag = [&](const char* name, bool fallback) { return GetPrivateProfileIntA(section, name, fallback ? 1 : 0, ini.c_str()) != 0; };
		settings.hotReload = flag("HotReload", settings.hotReload);
		settings.unlockMouse = flag("UnlockMouse", settings.unlockMouse);
		settings.debugText = flag("DebugText", settings.debugText);
		settings.debugTextStacking = flag("DebugTextStacking", settings.debugTextStacking);
		settings.debugEnvironmentMarker = flag("DebugEnvironmentMarker", settings.debugEnvironmentMarker);
		settings.debugPositionMarker = flag("DebugPositionMarker", settings.debugPositionMarker);
		settings.loadAllZones = flag("LoadAllZones", settings.loadAllZones);
		settings.liveLink = flag("LiveLink", settings.liveLink);
		settings.liveLinkCamera = flag("LiveLinkCamera", settings.liveLinkCamera);
		const int port = GetPrivateProfileIntA(section, "LiveLinkPort", settings.liveLinkPort, ini.c_str());
		if (port > 0 && port < 65536)
			settings.liveLinkPort = port;
		const int capFps = static_cast<int>(GetPrivateProfileIntA(section, "CapFps", settings.capFps, ini.c_str()));
		if (capFps > 0 && capFps <= 1000)
			settings.capFps = capFps;

		char key[64] = {};
		GetPrivateProfileStringA(section, "HotReloadKey", "", key, sizeof(key), ini.c_str());
		settings.hotReloadKey = ParseKey(key, settings.hotReloadKey);
		GetPrivateProfileStringA(section, "UnlockMouseKey", "", key, sizeof(key), ini.c_str());
		settings.unlockMouseKey = ParseKey(key, settings.unlockMouseKey);

		return settings;
	}
}

const Config::Settings& Config::Get()
{
	static const Settings settings = Load();
	return settings;
}

bool Config::AnyDebug()
{
	const Settings& settings = Get();
	return settings.debugText || settings.debugTextStacking || settings.debugEnvironmentMarker || settings.debugPositionMarker;
}

const char* Config::KeyName(int virtualKey)
{
	for (const NamedKey& key : kNamedKeys)
		if (key.virtualKey == virtualKey)
			return key.name;

	static char buffer[8];
	if ((virtualKey >= 'A' && virtualKey <= 'Z') || (virtualKey >= '0' && virtualKey <= '9'))
	{
		buffer[0] = static_cast<char>(virtualKey);
		buffer[1] = '\0';
	}
	else
	{
		snprintf(buffer, sizeof(buffer), "0x%02X", virtualKey);
	}
	return buffer;
}
