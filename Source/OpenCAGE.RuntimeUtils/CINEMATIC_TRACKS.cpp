#include "CINEMATIC_TRACKS.h"
#include "CINEMATIC_VISUALS.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <DirectXMath.h>
#include <algorithm>
#include <atomic>
#include <clocale>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The track player is Cinematic Tools' (by Hattiwatti), kept as it worked: a Catmull-Rom spline through the nodes for
	the pose, and a second one through the nodes' times (sampled a hundred times a node) that eases the pace so the camera
	keeps to the times given. Rotations are made to agree in sign before they are blended (a rotation and its negative are
	the same rotation, and blending the two would swing the camera round the long way).
*/
namespace
{
	using namespace DirectX;

	struct Node
	{
		float time = 0.0f;
		CINEMATIC_CAMERA::Pose pose;
		CINEMATIC_VISUALS::Dof dof;
	};

	struct SmoothNode
	{
		float time;
		float value;
	};

	struct Track
	{
		std::string name;
		std::string level;       // the level it was made in (shown; tracks are not tied to it)
		std::vector<Node> nodes;
		std::vector<SmoothNode> smooth;
		std::vector<XMFLOAT3> path; // the path drawn over the game: every tenth of a second
	};

	std::atomic<bool> g_loop = false;         // cinematic.track_loop
	std::atomic<bool> g_lockRotation = true;  // cinematic.track_lock_rotation
	std::atomic<bool> g_lockFov = false;      // cinematic.track_lock_fov
	std::atomic<bool> g_lockDof = false;      // cinematic.track_lock_dof
	std::atomic<bool> g_manual = false;       // cinematic.track_manual
	std::atomic<bool> g_show = false;         // cinematic.track_show
	std::atomic<float> g_gap = 3.0f;          // cinematic.track_gap: seconds between a new node and the one before
	std::atomic<float> g_speed = 1.0f;        // cinematic.track_speed: playback speed

	// Everything below under g_mutex (the camera task plays, the render thread draws and edits, commands edit). Never held
	// while asking the camera for anything (the camera asks here while holding its own).
	std::mutex g_mutex;
	std::vector<Track> g_tracks;
	int g_selected = 0;
	int g_nextNumber = 2;
	bool g_playing = false;
	float g_time = 0.0f;
	unsigned g_node = 0, g_smoothNode = 0;
	std::string g_levelName;                  // the running level's, as last seen by the render thread
	std::string g_note;

	void SetNote(const std::string& text)
	{
		// Called under g_mutex
		g_note = text;
		DevTools::Log("Debug menu: cinematic tracks: %s", text.c_str());
	}

	Track& Selected()
	{
		if (g_tracks.empty())
			g_tracks.push_back(Track{ "Track 1" });
		if (g_selected < 0 || g_selected >= static_cast<int>(g_tracks.size()))
			g_selected = 0;
		return g_tracks[g_selected];
	}

	float Duration(const Track& track)
	{
		return track.nodes.empty() ? 0.0f : track.nodes.back().time;
	}

	float CatmullRom(float y0, float y1, float y2, float y3, float mu)
	{
		const float mu2 = mu * mu;
		const float a0 = -0.5f * y0 + 1.5f * y1 - 1.5f * y2 + 0.5f * y3;
		const float a1 = y0 - 2.5f * y1 + 2.0f * y2 - 0.5f * y3;
		const float a2 = -0.5f * y0 + 0.5f * y2;
		return a0 * mu * mu2 + a1 * mu2 + a2 * mu + y1;
	}

	// The eased pace: for times through the track, how far along the nodes the camera is (Cinematic Tools' smoothing)
	void Smooth(Track& track)
	{
		track.smooth.clear();
		const std::vector<Node>& nodes = track.nodes;
		if (nodes.size() < 2)
			return;
		const int stepsPerNode = 100;
		const int last = static_cast<int>(nodes.size()) - 1;
		for (int step = 0; step <= last * stepsPerNode; step++)
		{
			const float at = step / static_cast<float>(stepsPerNode);
			const int current = (std::min)(step / stepsPerNode, last - 1);
			const int n1 = current, n2 = current + 1;
			const int n0 = n1 > 0 ? n1 - 1 : n1;
			const int n3 = n2 < last ? n2 + 1 : n2;
			SmoothNode node;
			node.time = CatmullRom(nodes[n0].time, nodes[n1].time, nodes[n2].time, nodes[n3].time, at - n1);
			node.value = at;
			// The time has to keep going up for the playback's search
			if (!track.smooth.empty() && node.time < track.smooth.back().time)
				node.time = track.smooth.back().time;
			track.smooth.push_back(node);
		}
		track.smooth.back().time = nodes[last].time;
		track.smooth.back().value = static_cast<float>(last);
	}

	// The pose at a time on the track (clamped to it)
	Node Evaluate(const Track& track, float time)
	{
		const std::vector<Node>& nodes = track.nodes;
		if (nodes.empty())
			return Node();
		if (nodes.size() == 1 || track.smooth.size() < 2 || time >= nodes.back().time)
			return time >= nodes.back().time ? nodes.back() : nodes.front();
		if (time <= 0.0f)
			return nodes.front();

		// Where along the nodes the eased pace has the camera at this time
		const std::vector<SmoothNode>& smooth = track.smooth;
		const auto after = std::upper_bound(smooth.begin(), smooth.end(), time, [](float t, const SmoothNode& node) { return t < node.time; });
		size_t s = after == smooth.begin() ? 0 : static_cast<size_t>(after - smooth.begin()) - 1;
		s = (std::min)(s, smooth.size() - 2);
		const float span = smooth[s + 1].time - smooth[s].time;
		float t = span > 0.0f ? (time - smooth[s].time) / span : 0.0f;
		t = (std::max)(0.0f, (std::min)(1.0f, t));
		const float along = smooth[s].value + (smooth[s + 1].value - smooth[s].value) * t;
		int n1 = static_cast<int>(floorf(along));
		n1 = (std::max)(0, (std::min)(n1, static_cast<int>(nodes.size()) - 2));
		const float mu = (std::max)(0.0f, (std::min)(1.0f, along - n1));
		const int n2 = n1 + 1;
		const int n0 = n1 > 0 ? n1 - 1 : n1;
		const int n3 = n2 + 1 < static_cast<int>(nodes.size()) ? n2 + 1 : n2;
		const Node* n[4] = { &nodes[n0], &nodes[n1], &nodes[n2], &nodes[n3] };

		Node result;
		result.time = time;
		XMVECTOR q[4], p[4];
		for (int i = 0; i < 4; i++)
		{
			q[i] = XMLoadFloat4(reinterpret_cast<const XMFLOAT4*>(n[i]->pose.rotation));
			p[i] = XMLoadFloat3(reinterpret_cast<const XMFLOAT3*>(n[i]->pose.position));
		}
		// Each rotation the same sign as the one the segment starts from
		for (int i = 0; i < 4; i++)
			if (i != 1 && XMVectorGetX(XMVector4Dot(q[i], q[1])) < 0.0f)
				q[i] = XMVectorNegate(q[i]);
		XMStoreFloat4(reinterpret_cast<XMFLOAT4*>(result.pose.rotation), XMQuaternionNormalize(XMVectorCatmullRom(q[0], q[1], q[2], q[3], mu)));
		XMStoreFloat3(reinterpret_cast<XMFLOAT3*>(result.pose.position), XMVectorCatmullRom(p[0], p[1], p[2], p[3], mu));
		result.pose.fov = CatmullRom(n[0]->pose.fov, n[1]->pose.fov, n[2]->pose.fov, n[3]->pose.fov, mu);
		result.dof.focalLength = (std::max)(0.0f, CatmullRom(n[0]->dof.focalLength, n[1]->dof.focalLength, n[2]->dof.focalLength, n[3]->dof.focalLength, mu));
		result.dof.focus = (std::max)(0.01f, CatmullRom(n[0]->dof.focus, n[1]->dof.focus, n[2]->dof.focus, n[3]->dof.focus, mu));
		result.dof.fnumber = (std::max)(0.01f, CatmullRom(n[0]->dof.fnumber, n[1]->dof.fnumber, n[2]->dof.fnumber, n[3]->dof.fnumber, mu));
		return result;
	}

	// The track's path drawn over the game, sampled every tenth of a second
	void Rebuild(Track& track)
	{
		Smooth(track);
		track.path.clear();
		const float duration = Duration(track);
		if (track.nodes.size() < 2 || duration <= 0.0f)
			return;
		const int samples = (std::min)(4000, static_cast<int>(duration / 0.1f) + 1);
		for (int i = 0; i <= samples; i++)
		{
			const Node node = Evaluate(track, duration * i / samples);
			track.path.push_back(XMFLOAT3(node.pose.position[0], node.pose.position[1], node.pose.position[2]));
		}
	}

	// ---- Editing (under g_mutex) ----

	bool AddNode(const CINEMATIC_CAMERA::Pose& pose, const CINEMATIC_VISUALS::Dof& dof)
	{
		if (g_playing)
		{
			SetNote("stop the track first");
			return false;
		}
		Track& track = Selected();
		Node node;
		node.pose = pose;
		node.dof = dof;
		node.time = track.nodes.empty() ? 0.0f : track.nodes.back().time + (std::max)(0.1f, g_gap.load());
		track.nodes.push_back(node);
		if (track.level.empty())
			track.level = g_levelName;
		Rebuild(track);
		SetNote("node " + std::to_string(track.nodes.size()) + " added to " + track.name);
		return true;
	}

	// Removes a node, closing the gap it leaves (the first node's removal moves the track back to start at 0)
	bool DeleteNode(size_t index)
	{
		if (g_playing)
		{
			SetNote("stop the track first");
			return false;
		}
		Track& track = Selected();
		std::vector<Node>& nodes = track.nodes;
		if (index >= nodes.size())
			return false;
		float shift = 0.0f;
		if (index == 0 && nodes.size() > 1)
			shift = nodes[1].time;
		else if (index > 0)
			shift = nodes[index].time - nodes[index - 1].time;
		nodes.erase(nodes.begin() + index);
		for (size_t i = index; i < nodes.size(); i++)
			nodes[i].time -= shift;
		Rebuild(track);
		g_time = (std::min)(g_time, Duration(track));
		SetNote("node " + std::to_string(index + 1) + " deleted");
		return true;
	}

	// A node moved in time: the first stays at 0, the others keep their order a tenth of a second apart at least
	bool SetNodeTime(size_t index, float time)
	{
		Track& track = Selected();
		std::vector<Node>& nodes = track.nodes;
		if (g_playing || index >= nodes.size() || !std::isfinite(time))
			return false;
		const float gap = 0.1f;
		if (index == 0)
			time = 0.0f;
		else
		{
			time = (std::max)(time, nodes[index - 1].time + gap);
			if (index + 1 < nodes.size())
				time = (std::min)(time, nodes[index + 1].time - gap);
		}
		nodes[index].time = time;
		Rebuild(track);
		return true;
	}

	bool Play()
	{
		Track& track = Selected();
		if (track.nodes.size() < 2)
		{
			SetNote("a track needs two nodes at least");
			return false;
		}
		// From the timeline's time, unless that is the end
		if (g_time >= Duration(track))
			g_time = 0.0f;
		g_playing = true;
		SetNote("playing " + track.name);
		return true;
	}

	void Stop()
	{
		if (g_playing)
			SetNote("stopped");
		g_playing = false;
	}

	// ---- Saving and loading ----

	std::string GameFolder()
	{
		char exePath[MAX_PATH] = {};
		GetModuleFileNameA(nullptr, exePath, MAX_PATH);
		std::string path = exePath;
		const size_t slash = path.find_last_of("\\/");
		return slash == std::string::npos ? std::string() : path.substr(0, slash + 1);
	}

	// A file named in the game folder (a plain name only, no folders), OpenCAGE.CinematicTracks.txt by default
	bool TrackFile(const std::string& given, std::string& path)
	{
		std::string name = given;
		const size_t start = name.find_first_not_of(" \t\""), end = name.find_last_not_of(" \t\"\r\n");
		name = start == std::string::npos ? std::string() : name.substr(start, end - start + 1);
		if (name.empty())
			name = "OpenCAGE.CinematicTracks.txt";
		if (name.find_first_of("\\/:*?<>|") != std::string::npos || name.find("..") != std::string::npos)
			return false;
		// Always a .txt: a name typed without it never lands on the game's own files (its executable, its libraries, the ini)
		if (name.size() < 4 || _stricmp(name.c_str() + name.size() - 4, ".txt") != 0)
			name += ".txt";
		path = GameFolder() + name;
		return true;
	}

	// Whether a file there is one these tracks can be saved over: none yet, or a tracks file (another text file is kept)
	bool MaySaveOver(const std::string& path)
	{
		FILE* file = nullptr;
		if (fopen_s(&file, path.c_str(), "rb") != 0 || !file)
			return GetFileAttributesA(path.c_str()) == INVALID_FILE_ATTRIBUTES;
		char line[64] = {};
		const bool tracks = fgets(line, sizeof(line), file) && strncmp(line, "OpenCAGE cinematic camera tracks", 32) == 0;
		fclose(file);
		return tracks;
	}

	_locale_t Invariant()
	{
		static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
		return invariant;
	}

	bool Save(const std::string& path, std::string& message)
	{
		if (!MaySaveOver(path))
		{
			message = path + " is there and is not a cinematic tracks file: give another name";
			return false;
		}
		FILE* file = nullptr;
		if (fopen_s(&file, path.c_str(), "wb") != 0 || !file)
		{
			message = "could not write " + path;
			return false;
		}
		fputs("OpenCAGE cinematic camera tracks 1\r\n", file);
		size_t nodes = 0;
		for (const Track& track : g_tracks)
		{
			fprintf(file, "track %s\r\n", track.name.c_str());
			fprintf(file, "level %s\r\n", track.level.c_str());
			for (const Node& node : track.nodes)
			{
				char line[512];
				_snprintf_s_l(line, sizeof(line), _TRUNCATE, "node %.4f %.5f %.5f %.5f %.6f %.6f %.6f %.6f %.3f %.5f %.4f %.4f\r\n", Invariant(),
					node.time, node.pose.position[0], node.pose.position[1], node.pose.position[2],
					node.pose.rotation[0], node.pose.rotation[1], node.pose.rotation[2], node.pose.rotation[3], node.pose.fov,
					node.dof.focalLength, node.dof.focus, node.dof.fnumber);
				fputs(line, file);
				nodes++;
			}
			fputs("end\r\n", file);
		}
		const bool ok = fclose(file) == 0;
		message = ok ? "saved " + std::to_string(g_tracks.size()) + " track(s), " + std::to_string(nodes) + " node(s) to " + path : "could not finish writing " + path;
		return ok;
	}

	bool Load(const std::string& path, std::string& message)
	{
		FILE* file = nullptr;
		if (fopen_s(&file, path.c_str(), "rb") != 0 || !file)
		{
			message = "could not read " + path;
			return false;
		}
		std::vector<Track> tracks;
		char line[1024];
		bool header = false, bad = false;
		while (fgets(line, sizeof(line), file))
		{
			std::string text = line;
			while (!text.empty() && (text.back() == '\r' || text.back() == '\n'))
				text.pop_back();
			if (!header)
			{
				header = text.rfind("OpenCAGE cinematic camera tracks", 0) == 0;
				if (!header)
				{
					bad = true;
					break;
				}
				continue;
			}
			if (text.rfind("track ", 0) == 0)
				tracks.push_back(Track{ text.substr(6) });
			else if (text.rfind("level ", 0) == 0 && !tracks.empty())
				tracks.back().level = text.substr(6);
			else if (text.rfind("node ", 0) == 0 && !tracks.empty())
			{
				float values[12];
				const char* at = text.c_str() + 5;
				int count = 0;
				for (; count < 12; count++)
				{
					char* end = nullptr;
					const double value = _strtod_l(at, &end, Invariant());
					if (end == at || !std::isfinite(value))
						break;
					values[count] = static_cast<float>(value);
					at = end;
				}
				if (count != 12)
				{
					bad = true;
					break;
				}
				// The rotation made unit again (one that can't be is a damaged file)
				const float length = sqrtf(values[4] * values[4] + values[5] * values[5] + values[6] * values[6] + values[7] * values[7]);
				if (!(length > 1e-4f) || !std::isfinite(length))
				{
					bad = true;
					break;
				}
				for (int i = 4; i < 8; i++)
					values[i] /= length;
				Node node;
				node.time = values[0];
				memcpy(node.pose.position, values + 1, sizeof(node.pose.position));
				memcpy(node.pose.rotation, values + 4, sizeof(node.pose.rotation));
				node.pose.fov = values[8];
				node.dof.focalLength = values[9];
				node.dof.focus = values[10];
				node.dof.fnumber = values[11];
				tracks.back().nodes.push_back(node);
			}
		}
		fclose(file);
		if (bad || !header)
		{
			message = path + " is not a cinematic tracks file (or is damaged): nothing loaded";
			return false;
		}
		if (tracks.empty())
		{
			message = path + " has no tracks: nothing loaded";
			return false;
		}
		size_t nodes = 0;
		for (Track& track : tracks)
		{
			std::sort(track.nodes.begin(), track.nodes.end(), [](const Node& a, const Node& b) { return a.time < b.time; });
			Rebuild(track);
			nodes += track.nodes.size();
		}
		g_playing = false;
		g_time = 0.0f;
		g_tracks.swap(tracks);
		g_selected = 0;
		g_nextNumber = static_cast<int>(g_tracks.size()) + 1;
		message = "loaded " + std::to_string(g_tracks.size()) + " track(s), " + std::to_string(nodes) + " node(s) from " + path;
		return true;
	}

	std::string Number(float value, int decimals = 3)
	{
		char text[48];
		_snprintf_s_l(text, sizeof(text), _TRUNCATE, "%.*f", Invariant(), decimals, value);
		return text;
	}

	// ---- Going to a node or a time (outside g_mutex: it moves the camera) ----

	void GoTo(const Node& node)
	{
		CINEMATIC_CAMERA::Pose pose = node.pose;
		if (!g_lockFov)
			pose.fov = CINEMATIC_CAMERA::Fov();
		if (!g_lockRotation)
		{
			CINEMATIC_CAMERA::Pose current;
			if (CINEMATIC_CAMERA::Current(current))
				memcpy(pose.rotation, current.rotation, sizeof(pose.rotation));
		}
		if (g_lockDof)
			CINEMATIC_VISUALS::SetDof(node.dof);
		CINEMATIC_CAMERA::Place(pose);
	}

	bool NodeAt(int index, Node& node)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		Track& track = Selected();
		if (index < 0 || index >= static_cast<int>(track.nodes.size()))
			return false;
		node = track.nodes[index];
		if (!g_playing)
			g_time = node.time;
		return true;
	}

	bool PoseAt(float time, Node& node)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		Track& track = Selected();
		if (track.nodes.empty() || g_playing)
			return false;
		g_time = (std::max)(0.0f, (std::min)(time, Duration(track)));
		node = Evaluate(track, g_time);
		return true;
	}

	bool AddHere(std::string& message)
	{
		CINEMATIC_CAMERA::Pose pose;
		if (!CINEMATIC_CAMERA::Current(pose))
		{
			message = "turn the cinematic camera on first (a node is where it is)";
			return false;
		}
		const CINEMATIC_VISUALS::Dof dof = CINEMATIC_VISUALS::GetDof();
		std::lock_guard<std::mutex> lock(g_mutex);
		const bool added = AddNode(pose, dof);
		message = g_note;
		return added;
	}

	bool UpdateHere(int index, std::string& message)
	{
		CINEMATIC_CAMERA::Pose pose;
		if (!CINEMATIC_CAMERA::Current(pose))
		{
			message = "turn the cinematic camera on first";
			return false;
		}
		const CINEMATIC_VISUALS::Dof dof = CINEMATIC_VISUALS::GetDof();
		std::lock_guard<std::mutex> lock(g_mutex);
		Track& track = Selected();
		if (g_playing || index < 0 || index >= static_cast<int>(track.nodes.size()))
		{
			message = g_playing ? "stop the track first" : "no node " + std::to_string(index + 1);
			return false;
		}
		track.nodes[index].pose = pose;
		track.nodes[index].dof = dof;
		Rebuild(track);
		SetNote("node " + std::to_string(index + 1) + " updated");
		message = g_note;
		return true;
	}

	// A track by number (1 = the first) or name
	int FindTrack(const std::string& text)
	{
		char* end = nullptr;
		const long number = strtol(text.c_str(), &end, 10);
		if (end && *end == '\0' && !text.empty())
			return number >= 1 && number <= static_cast<long>(g_tracks.size()) ? static_cast<int>(number - 1) : -1;
		for (size_t i = 0; i < g_tracks.size(); i++)
			if (g_tracks[i].name == text)
				return static_cast<int>(i);
		return -1;
	}

	std::string Trim(const std::string& text)
	{
		const size_t start = text.find_first_not_of(" \t\r\n"), end = text.find_last_not_of(" \t\r\n");
		return start == std::string::npos ? std::string() : text.substr(start, end - start + 1);
	}

	// A node number from a command (1 = the first; empty or "last" = the last), as an index; -1 when there is none
	int NodeIndex(const std::string& text)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		const int count = static_cast<int>(Selected().nodes.size());
		const std::string value = Trim(text);
		if (value.empty() || value == "last")
			return count - 1;
		char* end = nullptr;
		const long number = strtol(value.c_str(), &end, 10);
		return end && *end == '\0' && number >= 1 && number <= count ? static_cast<int>(number - 1) : -1;
	}

	DEBUG_MENU::CommandResult Result(bool ok, const std::string& message)
	{
		return DEBUG_MENU::CommandResult{ ok, message };
	}

	void Name()
	{
		DEBUG_MENU::AddSwitch("cinematic.track_loop", &g_loop);
		DEBUG_MENU::AddSwitch("cinematic.track_lock_rotation", &g_lockRotation);
		DEBUG_MENU::AddSwitch("cinematic.track_lock_fov", &g_lockFov);
		DEBUG_MENU::AddSwitch("cinematic.track_lock_dof", &g_lockDof);
		DEBUG_MENU::AddSwitch("cinematic.track_manual", &g_manual);
		DEBUG_MENU::AddSwitch("cinematic.track_show", &g_show);
		DEBUG_MENU::AddValue("cinematic.track_gap", &g_gap, 0.1f, 60.0f);
		DEBUG_MENU::AddValue("cinematic.track_speed", &g_speed, 0.05f, 10.0f);

		DEBUG_MENU::AddCommand("cinematic.track_add", "adds a node to the chosen track where the cinematic camera is", [](const std::string&)
		{
			std::string message;
			const bool ok = AddHere(message);
			return Result(ok, message);
		});
		DEBUG_MENU::AddCommand("cinematic.track_delete", "\"node number\" (empty: the last): deletes a node of the chosen track", [](const std::string& argument)
		{
			const int index = NodeIndex(argument);
			std::lock_guard<std::mutex> lock(g_mutex);
			if (index < 0)
				return Result(false, "no such node");
			const bool ok = DeleteNode(static_cast<size_t>(index));
			return Result(ok, g_note);
		});
		DEBUG_MENU::AddCommand("cinematic.track_update", "\"node number\": puts that node where the cinematic camera is", [](const std::string& argument)
		{
			const int index = NodeIndex(argument);
			std::string message;
			const bool ok = index >= 0 && UpdateHere(index, message);
			return Result(ok, index < 0 ? "no such node" : message);
		});
		DEBUG_MENU::AddCommand("cinematic.track_goto", "\"node number\": the cinematic camera to that node (turns it on)", [](const std::string& argument)
		{
			Node node;
			const int index = NodeIndex(argument);
			if (index < 0 || !NodeAt(index, node))
				return Result(false, "no such node");
			GoTo(node);
			return Result(true, "at node " + std::to_string(index + 1));
		});
		DEBUG_MENU::AddCommand("cinematic.track_time", "\"node number, seconds\": moves a node in time", [](const std::string& argument)
		{
			const size_t comma = argument.find(',');
			if (comma == std::string::npos)
				return Result(false, "give node number, seconds");
			const int index = NodeIndex(argument.substr(0, comma));
			const float seconds = static_cast<float>(_strtod_l(argument.c_str() + comma + 1, nullptr, Invariant()));
			std::lock_guard<std::mutex> lock(g_mutex);
			if (index < 0 || !SetNodeTime(static_cast<size_t>(index), seconds))
				return Result(false, g_playing ? "stop the track first" : "no such node");
			return Result(true, "node " + std::to_string(index + 1) + " at " + Number(Selected().nodes[index].time, 2) + " s");
		});
		DEBUG_MENU::AddCommand("cinematic.track_play", "plays the chosen track (turns the cinematic camera on)", [](const std::string&)
		{
			bool ok;
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				ok = Play();
				if (!ok)
					return Result(false, g_note);
			}
			DEBUG_MENU::SetSwitch("cinematic.camera", true);
			return Result(true, "playing");
		});
		DEBUG_MENU::AddCommand("cinematic.track_stop", "stops the track playing", [](const std::string&)
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			Stop();
			return Result(true, "stopped at " + Number(g_time, 2) + " s");
		});
		DEBUG_MENU::AddCommand("cinematic.track_seek", "\"seconds\": the cinematic camera to that time on the track (not while playing)", [](const std::string& argument)
		{
			Node node;
			const float seconds = static_cast<float>(_strtod_l(argument.c_str(), nullptr, Invariant()));
			if (!std::isfinite(seconds) || !PoseAt(seconds, node))
				return Result(false, "no track to seek on, or it is playing");
			CINEMATIC_CAMERA::Pose pose = node.pose;
			if (!g_lockFov)
				pose.fov = CINEMATIC_CAMERA::Fov();
			if (g_lockDof)
				CINEMATIC_VISUALS::SetDof(node.dof);
			CINEMATIC_CAMERA::Place(pose);
			return Result(true, "at " + Number(node.time, 2) + " s");
		});
		DEBUG_MENU::AddCommand("cinematic.track_new", "\"name\" (optional): a new empty track, chosen", [](const std::string& argument)
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			if (g_playing)
				return Result(false, "stop the track first");
			Selected();
			std::string name = Trim(argument);
			for (char& c : name)
				if (static_cast<unsigned char>(c) < 32)
					c = ' ';
			if (name.size() > 60)
				name.resize(60);
			if (name.empty())
				name = "Track " + std::to_string(g_nextNumber++);
			g_tracks.push_back(Track{ name, g_levelName });
			g_selected = static_cast<int>(g_tracks.size()) - 1;
			g_time = 0.0f;
			return Result(true, "track " + std::to_string(g_selected + 1) + ": " + name);
		});
		DEBUG_MENU::AddCommand("cinematic.track_remove", "deletes the chosen track (the last one is emptied instead)", [](const std::string&)
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			if (g_playing)
				return Result(false, "stop the track first");
			Selected();
			if (g_tracks.size() <= 1)
			{
				g_tracks[0].nodes.clear();
				Rebuild(g_tracks[0]);
			}
			else
				g_tracks.erase(g_tracks.begin() + g_selected);
			g_selected = (std::min)(g_selected, static_cast<int>(g_tracks.size()) - 1);
			g_time = 0.0f;
			return Result(true, "removed");
		});
		DEBUG_MENU::AddCommand("cinematic.track_select", "\"number or name\": chooses a track", [](const std::string& argument)
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			if (g_playing)
				return Result(false, "stop the track first");
			Selected();
			const int index = FindTrack(Trim(argument));
			if (index < 0)
				return Result(false, "no such track");
			g_selected = index;
			g_time = 0.0f;
			return Result(true, "chose " + g_tracks[index].name);
		});
		DEBUG_MENU::AddCommand("cinematic.list_tracks", "the tracks: number, name, nodes, length, level (* = chosen)", [](const std::string&)
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			Selected();
			std::string text;
			for (size_t i = 0; i < g_tracks.size(); i++)
				text += (static_cast<int>(i) == g_selected ? "* " : "  ") + std::to_string(i + 1) + " " + g_tracks[i].name + " nodes=" + std::to_string(g_tracks[i].nodes.size()) +
					" length=" + Number(Duration(g_tracks[i]), 2) + " level=" + g_tracks[i].level + "\n";
			text += std::string("playing=") + (g_playing ? "1" : "0") + " time=" + Number(g_time, 2);
			return Result(true, text);
		});
		DEBUG_MENU::AddCommand("cinematic.list_nodes", "the chosen track's nodes: number, time, position, rotation, fov, depth of field", [](const std::string&)
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			const Track& track = Selected();
			std::string text;
			for (size_t i = 0; i < track.nodes.size(); i++)
			{
				const Node& node = track.nodes[i];
				text += std::to_string(i + 1) + " time=" + Number(node.time, 2) + " position=" + Number(node.pose.position[0]) + "," + Number(node.pose.position[1]) + "," + Number(node.pose.position[2]) +
					" rotation=" + Number(node.pose.rotation[0], 4) + "," + Number(node.pose.rotation[1], 4) + "," + Number(node.pose.rotation[2], 4) + "," + Number(node.pose.rotation[3], 4) +
					" fov=" + Number(node.pose.fov, 1) + " dof=" + Number(node.dof.focalLength, 4) + "," + Number(node.dof.focus, 2) + "," + Number(node.dof.fnumber, 2) + "\n";
			}
			return Result(true, text);
		});
		DEBUG_MENU::AddCommand("cinematic.track_save", "\"file\" (optional, in the game folder): saves every track (OpenCAGE.CinematicTracks.txt)", [](const std::string& argument)
		{
			std::string path, message;
			if (!TrackFile(argument, path))
				return Result(false, "give a plain file name (it goes in the game folder)");
			std::lock_guard<std::mutex> lock(g_mutex);
			const bool ok = Save(path, message);
			SetNote(message);
			return Result(ok, message);
		});
		DEBUG_MENU::AddCommand("cinematic.track_load", "\"file\" (optional, in the game folder): loads tracks, replacing those kept", [](const std::string& argument)
		{
			std::string path, message;
			if (!TrackFile(argument, path))
				return Result(false, "give a plain file name (it is read from the game folder)");
			std::lock_guard<std::mutex> lock(g_mutex);
			const bool ok = Load(path, message);
			SetNote(message);
			return Result(ok, message);
		});
	}

	const bool g_named = (Name(), true);

	// ---- The tab ----

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 28.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	bool Checkbox(const char* label, std::atomic<bool>& value)
	{
		bool v = value;
		if (ImGui::Checkbox(label, &v))
		{
			value = v;
			return true;
		}
		return false;
	}

	int g_selectedNode = 0;
	char g_fileName[128] = "OpenCAGE.CinematicTracks.txt";
	char g_trackName[64] = "";

	void RunAndNote(const char* name, const std::string& argument)
	{
		DEBUG_MENU::CommandResult result;
		if (DEBUG_MENU::RunCommand(name, argument, result))
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_note = result.message;
		}
	}

	void DrawTab()
	{
		const bool cameraOn = CINEMATIC_CAMERA::Active();
		// A copy of what is drawn, taken under the lock (the buttons act through the commands, outside it)
		std::vector<std::string> names;
		std::vector<Node> nodes;
		int selected;
		bool playing;
		float time, duration;
		std::string note, level;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			Track& track = Selected();
			for (const Track& each : g_tracks)
				names.push_back(each.name);
			nodes = track.nodes;
			selected = g_selected;
			playing = g_playing;
			time = g_time;
			duration = Duration(track);
			note = g_note;
			level = track.level;
		}
		const int nodeCount = static_cast<int>(nodes.size());
		if (g_selectedNode >= nodeCount)
			g_selectedNode = nodeCount > 0 ? nodeCount - 1 : 0;

		ImGui::SeparatorText("Tracks");
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::BeginDisabled(playing);
		if (ImGui::BeginCombo("##track", names[selected].c_str()))
		{
			for (int i = 0; i < static_cast<int>(names.size()); i++)
				if (ImGui::Selectable((names[i] + "##" + std::to_string(i)).c_str(), i == selected))
					RunAndNote("cinematic.track_select", std::to_string(i + 1));
			ImGui::EndCombo();
		}
		ImGui::SameLine();
		if (ImGui::Button("New"))
			RunAndNote("cinematic.track_new", g_trackName);
		ImGui::SameLine();
		ImGui::SetNextItemWidth(140.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##trackname", "name (optional)", g_trackName, sizeof(g_trackName));
		ImGui::SameLine();
		if (ImGui::Button("Delete track"))
			RunAndNote("cinematic.track_remove", "");
		ImGui::EndDisabled();
		if (!level.empty())
			ImGui::TextDisabled("Made in %s", level.c_str());

		ImGui::SeparatorText("Nodes");
		if (nodeCount == 0)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("No nodes yet: turn the camera on, fly to the first shot and add one (F1 while flying).");
			ImGui::PopTextWrapPos();
		}
		else if (ImGui::BeginListBox("##nodes", ImVec2(260.0f * DEBUG_MENU::Scale(), ImGui::GetTextLineHeightWithSpacing() * (std::min)(8, nodeCount + 1))))
		{
			for (int i = 0; i < nodeCount; i++)
			{
				char label[64];
				sprintf_s(label, "Node %d   %.2f s", i + 1, nodes[i].time);
				if (ImGui::Selectable(label, i == g_selectedNode))
					g_selectedNode = i;
			}
			ImGui::EndListBox();
		}
		ImGui::BeginDisabled(playing);
		ImGui::BeginDisabled(!cameraOn);
		if (ImGui::Button("Add node"))
		{
			RunAndNote("cinematic.track_add", "");
			g_selectedNode = nodeCount;
		}
		Tooltip("A node where the camera is, the node gap after the last (F1 while flying).");
		ImGui::SameLine();
		ImGui::BeginDisabled(nodeCount == 0);
		if (ImGui::Button("Update node"))
			RunAndNote("cinematic.track_update", std::to_string(g_selectedNode + 1));
		Tooltip("The chosen node moved to where the camera is (its time kept).");
		ImGui::EndDisabled();
		ImGui::EndDisabled();
		ImGui::SameLine();
		ImGui::BeginDisabled(nodeCount == 0);
		if (ImGui::Button("Delete node"))
			RunAndNote("cinematic.track_delete", std::to_string(g_selectedNode + 1));
		ImGui::SameLine();
		if (ImGui::Button("Go to node"))
			RunAndNote("cinematic.track_goto", std::to_string(g_selectedNode + 1));
		Tooltip("Turns the camera on at the chosen node.");
		// (the node just added is chosen from the next frame, when it is in this copy)
		if (g_selectedNode >= 0 && g_selectedNode < nodeCount)
		{
			float nodeTime = nodes[g_selectedNode].time;
			ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
			if (ImGui::InputFloat("Node time (s)", &nodeTime, 0.1f, 1.0f, "%.2f", ImGuiInputTextFlags_EnterReturnsTrue))
				RunAndNote("cinematic.track_time", std::to_string(g_selectedNode + 1) + "," + Number(nodeTime, 3));
			Tooltip("Kept between its neighbours, a tenth of a second from each; the first node stays at 0.");
		}
		ImGui::EndDisabled();
		ImGui::EndDisabled();
		float gap = g_gap;
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		if (ImGui::InputFloat("Node gap (s)", &gap, 0.1f, 1.0f, "%.2f"))
			g_gap = (std::max)(0.1f, (std::min)(60.0f, gap));
		Tooltip("The time between a new node and the one before (Cinematic Tools' \"time between previous node\").");

		ImGui::SeparatorText("Playback");
		const bool canPlay = nodeCount > 1;
		ImGui::BeginDisabled(!canPlay && !playing);
		if (ImGui::Button(playing ? "Stop" : "Play"))
			RunAndNote(playing ? "cinematic.track_stop" : "cinematic.track_play", "");
		ImGui::EndDisabled();
		Tooltip("Plays the chosen track (turning the camera on); F3 while flying.");
		ImGui::SameLine();
		ImGui::BeginDisabled(nodeCount == 0 || playing);
		if (ImGui::Button("To start"))
			RunAndNote("cinematic.track_seek", "0");
		ImGui::EndDisabled();
		ImGui::SameLine();
		float speed = g_speed;
		ImGui::SetNextItemWidth(140.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Speed", &speed, 0.05f, 10.0f, "%.2fx", ImGuiSliderFlags_Logarithmic | ImGuiSliderFlags_AlwaysClamp))
			g_speed = speed;
		Tooltip("Playback speed: the track's times divided by this.");

		// The timeline: dragging it moves the camera along the track while it isn't playing. It shows the time and the
		// track's length (the only place either is shown)
		ImGui::SetNextItemWidth(-1.0f);
		float scrubbed = time;
		char timelineFormat[64]; // room for any float's length (a node's time can be typed as anything)
		sprintf_s(timelineFormat, "%%.2f / %.2f s", duration);
		ImGui::BeginDisabled(nodeCount < 2 || playing);
		if (ImGui::SliderFloat("##timeline", &scrubbed, 0.0f, duration > 0.0f ? duration : 1.0f, timelineFormat))
			RunAndNote("cinematic.track_seek", Number(scrubbed, 3));
		ImGui::EndDisabled();
		const ImVec2 sliderMin = ImGui::GetItemRectMin(), sliderMax = ImGui::GetItemRectMax();
		ImDrawList* drawList = ImGui::GetWindowDrawList();
		for (int i = 0; i < nodeCount && duration > 0.0f; i++)
		{
			const float x = sliderMin.x + (sliderMax.x - sliderMin.x) * (nodes[i].time / duration);
			drawList->AddLine(ImVec2(x, sliderMax.y + 2.0f), ImVec2(x, sliderMax.y + 8.0f), i == g_selectedNode ? IM_COL32(229, 121, 28, 255) : IM_COL32(255, 255, 255, 200), 2.0f);
		}
		// (room for the node marks under it)
		ImGui::Dummy(ImVec2(0.0f, 6.0f));

		Checkbox("Loop", g_loop);
		ImGui::SameLine();
		Checkbox("Play manually", g_manual);
		Tooltip("Up and down (Space/Ctrl, the triggers) move the camera along the track instead of time.");
		ImGui::SameLine();
		Checkbox("Show the track", g_show);
		Tooltip("The path and the nodes drawn over the game while the track isn't playing.");
		Checkbox("Lock rotation", g_lockRotation);
		Tooltip("The camera turns as the nodes do. Off, it only moves along the track and can be turned freely.");
		ImGui::SameLine();
		Checkbox("Lock field of view", g_lockFov);
		ImGui::SameLine();
		Checkbox("Lock depth of field", g_lockDof);
		Tooltip("The depth of field's focus follows the nodes' (with the Visuals tab's \"Override the level's focus\" on).");
		if (!cameraOn)
			ImGui::TextDisabled("The camera is off: playing or going to a node turns it on.");

		ImGui::SeparatorText("File");
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		ImGui::InputText("##file", g_fileName, sizeof(g_fileName));
		ImGui::SameLine();
		ImGui::BeginDisabled(playing);
		if (ImGui::Button("Save"))
			RunAndNote("cinematic.track_save", g_fileName);
		ImGui::SameLine();
		if (ImGui::Button("Load"))
			RunAndNote("cinematic.track_load", g_fileName);
		ImGui::EndDisabled();
		Tooltip("Every track, in the game folder next to OpenCAGE.RuntimeUtils.ini. Loading replaces the tracks kept.");

		if (!note.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Last: %s", note.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	// The chosen track drawn over the game while it isn't playing: the path, and each node with the way it looks
	void DrawOverlay(ImDrawList* drawList)
	{
		const std::string level = LIVE_LINK::RunningLevelName();
		std::vector<XMFLOAT3> path;
		std::vector<Node> nodes;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_levelName = level;
			if (!g_show || g_playing)
				return;
			const Track& track = Selected();
			path = track.path;
			nodes = track.nodes;
		}
		const ImU32 colour = IM_COL32(230, 60, 50, 220);
		for (size_t i = 1; i < path.size(); i++)
			DEBUG_MENU::WorldLine(drawList, &path[i - 1].x, &path[i].x, colour, 2.0f);
		for (size_t i = 0; i < nodes.size(); i++)
		{
			const Node& node = nodes[i];
			XMFLOAT4X4 rows;
			XMStoreFloat4x4(&rows, XMMatrixRotationQuaternion(XMLoadFloat4(reinterpret_cast<const XMFLOAT4*>(node.pose.rotation))));
			const float* at = node.pose.position;
			const float ahead[3] = { at[0] + rows._31 * 0.6f, at[1] + rows._32 * 0.6f, at[2] + rows._33 * 0.6f };
			DEBUG_MENU::WorldCross(drawList, at, 0.15f, IM_COL32(255, 255, 255, 220));
			DEBUG_MENU::WorldArrow(drawList, at, ahead, colour, 2.0f);
			char label[16];
			sprintf_s(label, "%u", static_cast<unsigned>(i + 1));
			DEBUG_MENU::WorldText(drawList, at, label, IM_COL32(255, 255, 255, 230), -14.0f);
		}
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Tracks", 91, DrawTab, "Cinematic");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(91, DrawOverlay);
}

bool CINEMATIC_TRACKS::Advance(float dt, float scrub, CINEMATIC_CAMERA::Pose& pose)
{
	CINEMATIC_VISUALS::Dof dof;
	bool setDof = false;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		if (!g_playing)
			return false;
		Track& track = Selected();
		const float duration = Duration(track);
		if (track.nodes.size() < 2 || duration <= 0.0f)
		{
			g_playing = false;
			return false;
		}
		const float speed = g_speed;
		g_time += g_manual ? dt * speed * scrub : dt * speed;
		if (g_time >= duration)
		{
			if (g_loop && !g_manual)
				g_time = fmodf(g_time, duration);
			else
			{
				g_time = duration;
				if (!g_manual)
				{
					g_playing = false;
					g_note = "finished " + track.name;
				}
			}
		}
		if (g_time < 0.0f)
			g_time = 0.0f;
		const Node node = Evaluate(track, g_time);
		memcpy(pose.position, node.pose.position, sizeof(pose.position));
		if (g_lockRotation)
			memcpy(pose.rotation, node.pose.rotation, sizeof(pose.rotation));
		if (g_lockFov)
			pose.fov = node.pose.fov;
		if (g_lockDof)
		{
			dof = node.dof;
			setDof = true;
		}
	}
	if (setDof)
		CINEMATIC_VISUALS::SetDof(dof);
	return true;
}

bool CINEMATIC_TRACKS::Playing()
{
	std::lock_guard<std::mutex> lock(g_mutex);
	return g_playing;
}

void CINEMATIC_TRACKS::AddNodeHere()
{
	std::string message;
	AddHere(message);
}

void CINEMATIC_TRACKS::DeleteLastNode()
{
	std::lock_guard<std::mutex> lock(g_mutex);
	const size_t count = Selected().nodes.size();
	if (count)
		DeleteNode(count - 1);
}

void CINEMATIC_TRACKS::TogglePlay()
{
	std::lock_guard<std::mutex> lock(g_mutex);
	if (g_playing)
		Stop();
	else
		Play();
}
