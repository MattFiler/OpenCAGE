#include "LIVE_LINK_SERVER.h"
#include "LIVE_LINK.h"
#include "LIVE_CAMERA.h"
#include "CUBEMAP_CAPTURE.h"
#include "LIVE_ANIMATION.h"
#include "LIVE_TRACE.h"
#include "POSSESS.h"
#include "GAME_LEVEL_MANAGER.h"
#include "SCREENSHOT.h"
#include "DevTools.h"

#define WIN32_LEAN_AND_MEAN
#include <WinSock2.h>
#include <WS2tcpip.h>
#include <Windows.h>
#include <bcrypt.h>

#include <algorithm>
#include <atomic>
#include <condition_variable>
#include <cstring>
#include <deque>
#include <mutex>
#include <thread>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "bcrypt.lib")

namespace
{
	constexpr uint32_t kMagic = 0x4C4C434F; // "OCLL"
	constexpr uint16_t kVersion = 1;
	constexpr uint32_t kMaxMessage = 64 * 1024 * 1024;

	struct Request
	{
		uint16_t command = 0;
		uint32_t id = 0;
		bool reply = true;
		std::vector<uint8_t> payload;
		ULONGLONG waitingSince = 0; // when an edit was first held back while the level loads (0: never held)
		uint32_t connection = 0;    // which OpenCAGE connection sent it (g_connection when it came in)
		ULONGLONG arrived = 0;      // when it came in: a held edit is answered within kMaxEditWaitMs of this
	};

	// How long an edit or call waits for a loading level before it is refused (OpenCAGE gives a request 30 s)
	constexpr ULONGLONG kMaxEditWaitMs = 20000;

	std::mutex g_queueMutex;
	std::deque<Request> g_entityQueue;
	std::deque<Request> g_renderQueue;
	// Bumped when OpenCAGE connects and when it goes: requests from an earlier connection (queued, or held while the level
	// starts) are dropped rather than carried out and answered to whoever is connected now
	std::atomic<uint32_t> g_connection{ 0 };

	std::mutex g_sendMutex;
	SOCKET g_listener = INVALID_SOCKET;
	SOCKET g_client = INVALID_SOCKET;
	std::atomic<bool> g_running = false;
	std::atomic<bool> g_connected = false;
	std::thread g_thread;

	std::mutex g_activityMutex;
	std::string g_activity;
	ULONGLONG g_activityAt = 0;

	void SetActivity(const std::string& text)
	{
		std::lock_guard<std::mutex> lock(g_activityMutex);
		g_activity = text;
		g_activityAt = GetTickCount64();
	}

	// ---- Reading payloads ----
	struct Reader
	{
		const std::vector<uint8_t>& data;
		size_t position = 0;
		bool failed = false;

		explicit Reader(const std::vector<uint8_t>& bytes) : data(bytes) {}
		uint32_t U32()
		{
			if (position + 4 > data.size()) { failed = true; return 0; }
			uint32_t value;
			memcpy(&value, data.data() + position, 4);
			position += 4;
			return value;
		}
		uint8_t U8()
		{
			if (position + 1 > data.size()) { failed = true; return 0; }
			return data[position++];
		}
		float F32()
		{
			const uint32_t bits = U32();
			float value;
			memcpy(&value, &bits, 4);
			return value;
		}
		const uint8_t* Bytes(uint32_t count)
		{
			if (position + count > data.size() || position + count < position) { failed = true; return nullptr; }
			const uint8_t* bytes = data.data() + position;
			position += count;
			return bytes;
		}
		std::string String()
		{
			const uint32_t length = U32();
			const uint8_t* bytes = failed ? nullptr : Bytes(length);
			return bytes ? std::string(reinterpret_cast<const char*>(bytes), length) : std::string();
		}
	};

	// ---- WebSocket ----
	bool SendAll(SOCKET socket, const void* data, size_t size)
	{
		const char* bytes = static_cast<const char*>(data);
		while (size > 0)
		{
			const int sent = send(socket, bytes, static_cast<int>(size), 0);
			if (sent <= 0)
				return false;
			bytes += sent;
			size -= sent;
		}
		return true;
	}

	bool ReceiveAll(SOCKET socket, void* data, size_t size)
	{
		char* bytes = static_cast<char*>(data);
		while (size > 0)
		{
			const int received = recv(socket, bytes, static_cast<int>(size), 0);
			if (received <= 0)
				return false;
			bytes += received;
			size -= received;
		}
		return true;
	}

	bool SendFrame(uint8_t opcode, const void* data, size_t size)
	{
		std::lock_guard<std::mutex> lock(g_sendMutex);
		if (g_client == INVALID_SOCKET)
			return false;
		uint8_t header[10];
		size_t headerSize = 2;
		header[0] = 0x80 | opcode;
		if (size < 126)
		{
			header[1] = static_cast<uint8_t>(size);
		}
		else if (size <= 0xFFFF)
		{
			header[1] = 126;
			header[2] = static_cast<uint8_t>(size >> 8);
			header[3] = static_cast<uint8_t>(size);
			headerSize = 4;
		}
		else
		{
			header[1] = 127;
			for (int i = 0; i < 8; i++)
				header[2 + i] = static_cast<uint8_t>(static_cast<uint64_t>(size) >> (56 - 8 * i));
			headerSize = 10;
		}
		return SendAll(g_client, header, headerSize) && SendAll(g_client, data, size);
	}

	// ---- Sending replies, on a thread of its own ----
	std::mutex g_outMutex;
	std::condition_variable g_outReady;
	std::deque<std::vector<uint8_t>> g_outQueue;
	size_t g_outBytes = 0;
	constexpr size_t kMaxQueuedBytes = 64 * 1024 * 1024;

	void QueueReply(std::vector<uint8_t>&& message)
	{
		{
			std::lock_guard<std::mutex> lock(g_outMutex);
			if (g_outBytes + message.size() > kMaxQueuedBytes)
			{
				DevTools::Log("LiveLink: OpenCAGE is not reading its replies - one dropped");
				return;
			}
			g_outBytes += message.size();
			g_outQueue.push_back(std::move(message));
		}
		g_outReady.notify_one();
	}

	void DropQueuedReplies()
	{
		std::lock_guard<std::mutex> lock(g_outMutex);
		g_outQueue.clear();
		g_outBytes = 0;
	}

	void SenderThread()
	{
		while (g_running)
		{
			std::vector<uint8_t> message;
			{
				std::unique_lock<std::mutex> lock(g_outMutex);
				g_outReady.wait(lock, [] { return !g_outQueue.empty() || !g_running; });
				if (!g_running)
					return;
				message = std::move(g_outQueue.front());
				g_outQueue.pop_front();
				g_outBytes -= message.size();
			}
			if (!SendFrame(2, message.data(), message.size()))
			{
				// Timed out or gone: end the connection, the socket thread cleans up
				std::lock_guard<std::mutex> lock(g_sendMutex);
				if (g_client != INVALID_SOCKET)
					shutdown(g_client, SD_BOTH);
			}
		}
	}

	void Reply(const Request& request, bool ok, const std::string& message, const std::vector<uint8_t>& payload = {})
	{
		if (!request.reply || request.connection != g_connection)
			return; // no answer wanted, or its connection is gone
		std::vector<uint8_t> out(12 + 1 + 4 + message.size() + payload.size());
		uint8_t* p = out.data();
		const uint16_t command = request.command | 0x8000;
		const uint32_t length = static_cast<uint32_t>(message.size());
		memcpy(p, &kMagic, 4);
		memcpy(p + 4, &kVersion, 2);
		memcpy(p + 6, &command, 2);
		memcpy(p + 8, &request.id, 4);
		p[12] = ok ? 1 : 0;
		memcpy(p + 13, &length, 4);
		memcpy(p + 17, message.data(), message.size());
		if (!payload.empty())
			memcpy(p + 17 + message.size(), payload.data(), payload.size());
		QueueReply(std::move(out));
	}

	std::string Base64(const uint8_t* data, size_t size)
	{
		static const char* alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
		std::string out;
		for (size_t i = 0; i < size; i += 3)
		{
			const uint32_t chunk = (data[i] << 16) | ((i + 1 < size ? data[i + 1] : 0) << 8) | (i + 2 < size ? data[i + 2] : 0);
			out += alphabet[(chunk >> 18) & 63];
			out += alphabet[(chunk >> 12) & 63];
			out += i + 1 < size ? alphabet[(chunk >> 6) & 63] : '=';
			out += i + 2 < size ? alphabet[chunk & 63] : '=';
		}
		return out;
	}

	bool Sha1(const std::string& text, uint8_t digest[20])
	{
		BCRYPT_ALG_HANDLE algorithm = nullptr;
		if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA1_ALGORITHM, nullptr, 0) != 0)
			return false;
		const bool ok = BCryptHash(algorithm, nullptr, 0, reinterpret_cast<PUCHAR>(const_cast<char*>(text.data())), static_cast<ULONG>(text.size()), digest, 20) == 0;
		BCryptCloseAlgorithmProvider(algorithm, 0);
		return ok;
	}

	bool Handshake(SOCKET client)
	{
		std::string request;
		char buffer[1024];
		while (request.find("\r\n\r\n") == std::string::npos)
		{
			const int received = recv(client, buffer, sizeof(buffer), 0);
			if (received <= 0 || request.size() > 16384)
				return false;
			request.append(buffer, received);
		}

		std::string lower = request;
		for (char& c : lower)
			c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		if (lower.find("\norigin:") != std::string::npos)
		{
			DevTools::Log("LiveLink: refused a connection from a web page");
			return false;
		}
		const size_t keyAt = lower.find("sec-websocket-key:");
		if (keyAt == std::string::npos)
			return false;
		size_t start = keyAt + strlen("sec-websocket-key:");
		const size_t end = request.find("\r\n", start);
		while (start < end && request[start] == ' ')
			start++;
		std::string key = request.substr(start, end - start);
		while (!key.empty() && key.back() == ' ')
			key.pop_back();

		uint8_t digest[20];
		if (!Sha1(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11", digest))
			return false;
		const std::string response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + Base64(digest, 20) + "\r\n\r\n";
		return SendAll(client, response.data(), response.size());
	}

	// One whole message (reassembling fragments); answers pings itself. False when the connection is done.
	bool ReceiveMessage(SOCKET client, uint8_t& opcode, std::vector<uint8_t>& message)
	{
		message.clear();
		opcode = 0;
		for (;;)
		{
			uint8_t header[2];
			if (!ReceiveAll(client, header, 2))
				return false;
			const bool final = (header[0] & 0x80) != 0;
			const uint8_t frameOpcode = header[0] & 0x0F;
			const bool masked = (header[1] & 0x80) != 0;
			uint64_t length = header[1] & 0x7F;
			if (length == 126)
			{
				uint8_t extended[2];
				if (!ReceiveAll(client, extended, 2))
					return false;
				length = (extended[0] << 8) | extended[1];
			}
			else if (length == 127)
			{
				uint8_t extended[8];
				if (!ReceiveAll(client, extended, 8))
					return false;
				length = 0;
				for (int i = 0; i < 8; i++)
					length = (length << 8) | extended[i];
			}
			if (length > kMaxMessage || message.size() + length > kMaxMessage)
				return false;
			uint8_t mask[4] = {};
			if (masked && !ReceiveAll(client, mask, 4))
				return false;
			std::vector<uint8_t> payload(static_cast<size_t>(length));
			if (length && !ReceiveAll(client, payload.data(), payload.size()))
				return false;
			if (masked)
				for (size_t i = 0; i < payload.size(); i++)
					payload[i] ^= mask[i % 4];

			if (frameOpcode == 8)
			{
				SendFrame(8, payload.data(), payload.size() >= 2 ? 2 : 0);
				return false;
			}
			if (frameOpcode == 9)
			{
				SendFrame(10, payload.data(), payload.size());
				continue;
			}
			if (frameOpcode == 10)
				continue;
			if (frameOpcode != 0)
				opcode = frameOpcode;
			message.insert(message.end(), payload.begin(), payload.end());
			if (final)
				return true;
		}
	}

	// The older JSON packets: only load_level has ever been sent
	void QueueTextMessage(const std::vector<uint8_t>& message)
	{
		const std::string text(message.begin(), message.end());
		const size_t key = text.find("\"load_level\"");
		if (key == std::string::npos)
			return;
		const size_t open = text.find('"', text.find(':', key) + 1);
		const size_t close = open == std::string::npos ? std::string::npos : text.find('"', open + 1);
		if (close == std::string::npos || close == open + 1)
			return;

		Request request;
		request.command = LIVE_LINK_SERVER::LOAD_LEVEL;
		request.reply = false;
		request.connection = g_connection;
		const std::string level = text.substr(open + 1, close - open - 1);
		const uint32_t length = static_cast<uint32_t>(level.size());
		request.payload.resize(4 + level.size());
		memcpy(request.payload.data(), &length, 4);
		memcpy(request.payload.data() + 4, level.data(), level.size());
		std::lock_guard<std::mutex> lock(g_queueMutex);
		g_renderQueue.push_back(std::move(request));
	}

	// A camera pose is only stored here (the camera hook in LIVE_CAMERA.cpp applies it), so it is answered at once: it never
	// goes on the entity queue, whose edits are held while a level starts, so the camera follows OpenCAGE through loads,
	// cutscenes and the pause menu.
	void HandleCamera(const Request& request)
	{
		Reader reader(request.payload);
		const uint32_t root = reader.U32();
		const bool active = reader.U8() != 0;
		float position[3] = {}, forward[3] = {}, up[3] = {};
		float fov = 0.0f;
		if (active)
		{
			for (float& value : position) value = reader.F32();
			for (float& value : forward) value = reader.F32();
			for (float& value : up) value = reader.F32();
			fov = reader.F32();
		}
		if (reader.failed)
		{
			Reply(request, false, "Malformed request");
			return;
		}
		const LIVE_LINK::Result result = LIVE_CAMERA::SetPose(request.connection, root, active, position, forward, up, fov);
		Reply(request, result.ok, result.message);
	}

	// The game camera's own pose is read from what the camera hook kept of its last frame (see LIVE_CAMERA.cpp), so it is answered
	// at once too: OpenCAGE asks for it every frame while its viewport follows the game.
	void HandleCameraGet(const Request& request)
	{
		Reader reader(request.payload);
		const uint32_t root = reader.U32();
		if (reader.failed)
		{
			Reply(request, false, "Malformed request");
			return;
		}
		const LIVE_LINK::Result result = LIVE_CAMERA::GetPose(root);
		Reply(request, result.ok, result.message);
	}

	// An animation drive is only stored here (the entity thread carries it out every frame, see LIVE_ANIMATION.h), so it is
	// answered at once too: OpenCAGE sends a hold for every move of its playhead, and a release has to land whatever the
	// game is doing.
	void HandleAnimation(const Request& request)
	{
		Reader reader(request.payload);
		LIVE_ANIMATION::Drive drive;
		drive.connection = request.connection;
		drive.root = reader.U32();
		drive.mode = reader.U8();
		uint32_t pathCount = 0;
		// The rest is only there for a hold or play; an unknown mode, or a longer path, is refused without reading on
		if (drive.mode == LIVE_ANIMATION::Hold || drive.mode == LIVE_ANIMATION::Play)
		{
			drive.composite = reader.U32();
			drive.entity = reader.U32();
			pathCount = reader.U32();
			if (pathCount <= LIVE_ANIMATION::kMaxPath)
			{
				for (uint32_t i = 0; i < pathCount && !reader.failed; i++)
					drive.path.push_back(reader.U32());
				drive.time = reader.F32();
				drive.rate = reader.F32();
				drive.flags = reader.U8();
				drive.sequence = reader.U32();
			}
		}
		const LIVE_LINK::Result result = reader.failed ? LIVE_ANIMATION::Refuse("Malformed request") : LIVE_ANIMATION::SetDrive(drive, pathCount);
		Reply(request, result.ok, result.message);
	}

	// What the entity thread did with the drive is read from its last snapshot, so this is answered at once as well: OpenCAGE
	// asks for it every few frames while the game plays the animation, to follow it with its playhead.
	void HandleAnimationGet(const Request& request)
	{
		Reader reader(request.payload);
		const uint32_t root = reader.U32();
		if (reader.failed)
		{
			Reply(request, false, "Malformed request");
			return;
		}
		const LIVE_LINK::Result result = LIVE_ANIMATION::GetState(root);
		Reply(request, result.ok, result.message);
	}

	// What to trace is only stored here (the hooks on the game's own firing of outputs, reading and writing of parameters
	// and calling of methods gather the activity, see LIVE_TRACE.h), so it is answered at once too: OpenCAGE sends it
	// whenever the flowgraphs it shows change.
	void HandleTrace(const Request& request)
	{
		Reader reader(request.payload);
		const uint32_t root = reader.U32();
		const uint8_t on = reader.U8();
		bool malformed = reader.failed || on > 1;
		std::vector<LIVE_TRACE::Watch> watches;
		if (!malformed && on)
		{
			const uint32_t count = reader.U32();
			malformed = reader.failed || count == 0 || count > LIVE_TRACE::kMaxWatches;
			for (uint32_t i = 0; i < count && !malformed; i++)
			{
				LIVE_TRACE::Watch watch;
				watch.composite = reader.U32();
				const uint32_t pathCount = reader.U32();
				watch.any = pathCount == LIVE_TRACE::kAnyInstance;
				if (!watch.any && pathCount > LIVE_TRACE::kMaxPath)
				{
					malformed = true;
					break;
				}
				for (uint32_t step = 0; !watch.any && step < pathCount && !reader.failed; step++)
					watch.path.push_back(reader.U32());
				malformed = reader.failed;
				watches.push_back(std::move(watch));
			}
		}
		const LIVE_LINK::Result result = malformed ? LIVE_LINK::Result{ false, "Malformed request" } : LIVE_TRACE::Set(request.connection, root, on != 0, std::move(watches));
		Reply(request, result.ok, result.message);
	}

	// What was gathered is handed over at once as well: OpenCAGE asks for it several times a second while it shows activity.
	void HandleTraceGet(const Request& request)
	{
		Reader reader(request.payload);
		const uint32_t root = reader.U32();
		if (reader.failed)
		{
			Reply(request, false, "Malformed request");
			return;
		}
		std::vector<uint8_t> payload; // left empty when refused
		const LIVE_LINK::Result result = LIVE_TRACE::Take(request.connection, root, payload);
		Reply(request, result.ok, result.message, payload);
	}

	// What to possess, or the stick, is only stored here (the characters' behaviour updates act on it, see POSSESS.h), so it
	// is answered at once too: OpenCAGE sends a stick as often as it changes.
	void HandlePossess(const Request& request)
	{
		Reader reader(request.payload);
		const uint32_t root = reader.U32();
		const uint8_t mode = reader.U8();
		LIVE_LINK::Result result{ false, "Malformed request" };
		if (mode == 0 && !reader.failed)
			result = POSSESS::Release();
		else if (mode == 1)
		{
			const uint32_t length = reader.U32();
			const uint8_t* text = length <= 256 ? reader.Bytes(length) : nullptr;
			if (!reader.failed && (text || length == 0))
				result = POSSESS::Take(root, std::string(reinterpret_cast<const char*>(text), text ? length : 0));
		}
		else if (mode == 2)
		{
			const float x = reader.F32();
			const float y = reader.F32();
			const int speed = static_cast<int8_t>(reader.U8());
			const uint32_t milliseconds = reader.U32();
			if (!reader.failed)
				result = POSSESS::SetStick(request.connection, x, y, speed, milliseconds);
		}
		else if (mode == 3 || mode == 4)
		{
			const bool on = reader.U8() != 0;
			if (!reader.failed)
				result = mode == 3 ? POSSESS::SetCrouch(on) : POSSESS::SetMenu(on);
		}
		if (mode != 2)
			SetActivity(result.message);
		Reply(request, result.ok, result.message);
	}

	// A cubemap capture is only started here (the camera hook and the frame presentation carry it out over a few seconds,
	// see CUBEMAP_CAPTURE.h), and answered when it ends - from the render thread, with the six faces. A refusal is answered
	// at once.
	void HandleCaptureCubemap(const Request& request)
	{
		Reader reader(request.payload);
		CUBEMAP_CAPTURE::Request capture;
		capture.connection = request.connection;
		capture.root = reader.U32();
		for (float& value : capture.position)
			value = reader.F32();
		capture.size = reader.U32();
		capture.settleFrames = reader.U32();
		capture.streamTimeoutMs = reader.U32();
		capture.flags = reader.U32();
		const bool powerOfTwo = capture.size && (capture.size & (capture.size - 1)) == 0;
		if (reader.failed || !powerOfTwo || capture.size < 16 || capture.size > 1024 || capture.settleFrames < 4 || capture.settleFrames > 120 ||
			capture.streamTimeoutMs > 120000)
		{
			Reply(request, false, "Malformed request");
			return;
		}
		const LIVE_LINK::Result result = CUBEMAP_CAPTURE::Start(capture, [request](bool ok, const std::string& message, std::vector<uint8_t>&& payload)
		{
			Reply(request, ok, message, payload);
		});
		SetActivity(result.ok ? "Capturing a cubemap" : result.message);
		if (!result.ok)
			Reply(request, false, result.message);
	}

	void HandlePossessGet(const Request& request)
	{
		Reader reader(request.payload);
		const uint32_t root = reader.U32();
		if (reader.failed)
		{
			Reply(request, false, "Malformed request");
			return;
		}
		const LIVE_LINK::Result result = POSSESS::GetState(root);
		Reply(request, result.ok, result.message);
	}

	void QueueBinaryMessage(const std::vector<uint8_t>& message)
	{
		Request request;
		if (message.size() < 12)
			return;
		uint32_t magic;
		uint16_t version;
		memcpy(&magic, message.data(), 4);
		memcpy(&version, message.data() + 4, 2);
		memcpy(&request.command, message.data() + 6, 2);
		memcpy(&request.id, message.data() + 8, 4);
		request.connection = g_connection;
		request.arrived = GetTickCount64();
		request.payload.assign(message.begin() + 12, message.end());
		if (magic != kMagic || version != kVersion)
		{
			Reply(request, false, "Unsupported Live Link protocol (OpenCAGE and the game's OpenCAGE.RuntimeUtils.asi are different versions)");
			return;
		}
		if (request.command == LIVE_LINK_SERVER::CAMERA)
		{
			HandleCamera(request);
			return;
		}
		if (request.command == LIVE_LINK_SERVER::CAMERA_GET)
		{
			HandleCameraGet(request);
			return;
		}
		if (request.command == LIVE_LINK_SERVER::ANIMATION)
		{
			HandleAnimation(request);
			return;
		}
		if (request.command == LIVE_LINK_SERVER::ANIMATION_GET)
		{
			HandleAnimationGet(request);
			return;
		}
		if (request.command == LIVE_LINK_SERVER::TRACE)
		{
			HandleTrace(request);
			return;
		}
		if (request.command == LIVE_LINK_SERVER::TRACE_GET)
		{
			HandleTraceGet(request);
			return;
		}
		if (request.command == LIVE_LINK_SERVER::POSSESS)
		{
			HandlePossess(request);
			return;
		}
		if (request.command == LIVE_LINK_SERVER::POSSESS_GET)
		{
			HandlePossessGet(request);
			return;
		}
		if (request.command == LIVE_LINK_SERVER::CAPTURE_CUBEMAP)
		{
			HandleCaptureCubemap(request);
			return;
		}

		std::lock_guard<std::mutex> lock(g_queueMutex);
		switch (request.command)
		{
		case LIVE_LINK_SERVER::SCREENSHOT:
		case LIVE_LINK_SERVER::LOAD_LEVEL:
			g_renderQueue.push_back(std::move(request));
			break;
		default:
			g_entityQueue.push_back(std::move(request));
			break;
		}
	}

	// One connected client's messages, until it goes
	void ClientThread(SOCKET client)
	{
		uint8_t opcode;
		std::vector<uint8_t> message;
		while (g_running && ReceiveMessage(client, opcode, message))
		{
			if (opcode == 1)
				QueueTextMessage(message);
			else if (opcode == 2)
				QueueBinaryMessage(message);
		}

		{
			std::lock_guard<std::mutex> lock(g_sendMutex);
			g_client = INVALID_SOCKET;
		}
		closesocket(client);
		const uint32_t ended = g_connection++;
		// Script activity is traced for a connection only: it stops with it (before another can connect)
		LIVE_TRACE::ConnectionEnded(ended);
		DropQueuedReplies();
		g_connected = false;
		DevTools::Log("LiveLink: OpenCAGE disconnected");
		SetActivity("OpenCAGE disconnected");
	}

	void ServerThread(uint16_t port)
	{
		WSADATA wsa;
		if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0)
		{
			DevTools::Log("LiveLink: WSAStartup failed");
			return;
		}

		g_listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
		sockaddr_in address = {};
		address.sin_family = AF_INET;
		address.sin_port = htons(port);
		inet_pton(AF_INET, "127.0.0.1", &address.sin_addr);
		if (g_listener == INVALID_SOCKET || bind(g_listener, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0 || listen(g_listener, 1) != 0)
		{
			DevTools::Log("LiveLink: could not listen on 127.0.0.1:%u (error %d) - is another copy of the game running?", port, WSAGetLastError());
			if (g_listener != INVALID_SOCKET)
				closesocket(g_listener);
			g_listener = INVALID_SOCKET;
			WSACleanup();
			return;
		}
		DevTools::Log("LiveLink: listening on 127.0.0.1:%u", port);

		// Accepting carries on while a client is connected, so a second one is turned away at once rather than left
		// waiting in the backlog (where its handshake would hang until it timed out)
		while (g_running)
		{
			SOCKET client = accept(g_listener, nullptr, nullptr);
			if (client == INVALID_SOCKET)
				continue;
			DWORD timeout = 10000;
			setsockopt(client, SOL_SOCKET, SO_SNDTIMEO, reinterpret_cast<const char*>(&timeout), sizeof(timeout));
			setsockopt(client, SOL_SOCKET, SO_RCVTIMEO, reinterpret_cast<const char*>(&timeout), sizeof(timeout)); // for the handshake
			if (g_connected)
			{
				const char* busy = "HTTP/1.1 503 Service Unavailable\r\nConnection: close\r\n\r\n";
				SendAll(client, busy, strlen(busy));
				closesocket(client);
				DevTools::Log("LiveLink: turned away a second connection");
				continue;
			}
			if (!Handshake(client))
			{
				closesocket(client);
				continue;
			}
			timeout = 0; // messages arrive whenever OpenCAGE has something to send
			setsockopt(client, SOL_SOCKET, SO_RCVTIMEO, reinterpret_cast<const char*>(&timeout), sizeof(timeout));
			{
				std::lock_guard<std::mutex> lock(g_sendMutex);
				g_client = client;
			}
			g_connection++;
			g_connected = true;
			DevTools::Log("LiveLink: OpenCAGE connected");
			SetActivity("OpenCAGE connected");
			std::thread(ClientThread, client).detach();
		}

		WSACleanup();
	}

	bool PopRequest(std::deque<Request>& queue, Request& out)
	{
		std::lock_guard<std::mutex> lock(g_queueMutex);
		if (queue.empty())
			return false;
		out = std::move(queue.front());
		queue.pop_front();
		return true;
	}

	std::string Hex(uint32_t value)
	{
		char buffer[16];
		snprintf(buffer, sizeof(buffer), "%02X-%02X-%02X-%02X", value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF, value >> 24);
		return buffer;
	}
}

void LIVE_LINK_SERVER::Start(uint16_t port)
{
	if (g_running)
		return;
	g_running = true;
	g_thread = std::thread(ServerThread, port);
	g_thread.detach();
	std::thread(SenderThread).detach();
}

void LIVE_LINK_SERVER::Stop()
{
	g_running = false;
	g_outReady.notify_all();
	if (g_listener != INVALID_SOCKET)
		closesocket(g_listener);
	std::lock_guard<std::mutex> lock(g_sendMutex);
	if (g_client != INVALID_SOCKET)
		shutdown(g_client, SD_BOTH);
}

bool LIVE_LINK_SERVER::Connected()
{
	return g_connected;
}

std::string LIVE_LINK_SERVER::LastActivity(unsigned int withinMs)
{
	std::lock_guard<std::mutex> lock(g_activityMutex);
	return GetTickCount64() - g_activityAt <= withinMs ? g_activity : std::string();
}

uint32_t LIVE_LINK_SERVER::CurrentConnection()
{
	return g_connection;
}

void LIVE_LINK_SERVER::ShowActivity(const std::string& text)
{
	SetActivity(text);
}

namespace
{
	void HandleEntityRequest(Request& request);
}

// Edits and method calls wait (in order) while the level's scripts are not running (a level starting, the pause
// menu): pushed while a level started, they were found to be able to leave the game on a black loading screen for
// good. Reads are answered at once. Requests from an earlier connection are dropped unanswered.
void LIVE_LINK_SERVER::ProcessEntityRequests()
{
	std::deque<Request> pending, held;
	{
		std::lock_guard<std::mutex> lock(g_queueMutex);
		pending.swap(g_entityQueue);
	}
	const char* wait = nullptr;
	bool waitChecked = false;
	std::string refusing; // once one held edit is turned away, everything held behind it goes too (so none overtakes it)
	for (Request& request : pending)
	{
		if (request.connection != g_connection)
			continue; // OpenCAGE went away since; nobody is waiting for it
		const bool edit = request.command == APPLY_COMPOSITE || request.command == CALL_METHOD;
		if (edit)
		{
			// One for another level is turned away at once, as it would be when carried out: held, it would come back as
			// "still starting" or "paused", which OpenCAGE sends again. It changes nothing, so it cannot overtake anything.
			{
				Reader peek(request.payload); // both begin with the root composite
				const uint32_t root = peek.U32();
				if (!peek.failed && LIVE_LINK::LevelRunning() && !LIVE_LINK::RunningLevelIs(root))
				{
					Reply(request, false, "The game is running a different level - save, and load this one in the game");
					continue;
				}
			}
			if (!waitChecked)
			{
				wait = LIVE_LINK::EditsMustWait();
				waitChecked = true;
			}
			if (!refusing.empty())
			{
				Reply(request, false, refusing);
				continue;
			}
			if (!held.empty() || wait)
			{
				const ULONGLONG now = GetTickCount64();
				if (request.waitingSince == 0)
				{
					request.waitingSince = now;
					DevTools::Log("LiveLink: request %u held until the level's scripts run (%s)", request.command, wait ? wait : "behind another");
					SetActivity(std::string("Waiting for the game: ") + (wait ? wait : "queued"));
				}
				// Counted from when it came in, so the answer is always well inside OpenCAGE's 30 s
				if (wait && now - request.arrived >= kMaxEditWaitMs)
				{
					// The reason stays first: OpenCAGE recognises it by that and sends its edits again
					refusing = std::string(wait) + " - not carried out (edits and calls were held for 20 s, and the level is still not being played); "
						"send it again once it is (STATUS playing=1). OpenCAGE sends its own edits again by itself";
					DevTools::Log("LiveLink: request %u refused, with anything held behind it: %s", request.command, refusing.c_str());
					for (Request& before : held) // cannot happen in order, but keep the rule simple: nothing held survives
						Reply(before, false, refusing);
					held.clear();
					Reply(request, false, refusing);
					continue;
				}
				held.push_back(std::move(request));
				continue;
			}
		}
		HandleEntityRequest(request);
	}
	if (!held.empty())
	{
		std::lock_guard<std::mutex> lock(g_queueMutex);
		for (auto it = held.rbegin(); it != held.rend(); ++it)
			g_entityQueue.push_front(std::move(*it));
	}
}

namespace
{
using namespace LIVE_LINK_SERVER;

void HandleEntityRequest(Request& request)
{
	{
		Reader reader(request.payload);
		LIVE_LINK::Result result;
		switch (request.command)
		{
		case STATUS:
			result = LIVE_LINK::Status();
			break;
		case DESCRIBE:
		{
			const uint32_t composite = reader.U32();
			result = reader.failed ? LIVE_LINK::Result{ false, "Malformed request" } : LIVE_LINK::Describe(composite);
			break;
		}
		case CALL_METHOD:
		{
			const uint32_t root = reader.U32();
			const uint32_t composite = reader.U32();
			const uint32_t entity = reader.U32();
			const uint32_t method = reader.U32();
			const uint32_t pathCount = reader.U32();
			std::vector<uint32_t> path;
			for (uint32_t i = 0; i < pathCount && !reader.failed && i < 256; i++)
				path.push_back(reader.U32());
			if (reader.failed)
			{
				result.message = "Malformed request";
				break;
			}
			DevTools::Log("LiveLink: call %s on %s (composite %s, path of %u)", Hex(method).c_str(), Hex(entity).c_str(), Hex(composite).c_str(), pathCount);
			result = LIVE_LINK::CallMethod(root, composite, entity, method, path);
			SetActivity(result.message);
			break;
		}
		case APPLY_COMPOSITE:
		{
			const uint32_t root = reader.U32();
			const uint32_t composite = reader.U32();
			const uint32_t imageSize = reader.U32();
			const uint8_t* image = reader.Bytes(imageSize);
			const uint32_t relocationCount = reader.U32();
			const uint8_t* relocations = relocationCount <= kMaxMessage / 4 ? reader.Bytes(relocationCount * 4) : nullptr;
			if (reader.failed || !image || (relocationCount && !relocations))
			{
				result.message = "Malformed request";
				break;
			}
			std::vector<uint32_t> relocationList(relocationCount);
			if (relocationCount)
				memcpy(relocationList.data(), relocations, relocationCount * 4);
			// The data links' pins follow, from an OpenCAGE that sends them (one from before them ends here)
			std::vector<LIVE_LINK::DataPin> dataPins;
			if (reader.position < reader.data.size())
			{
				const uint32_t pinCount = reader.U32();
				const uint8_t* pins = pinCount <= kMaxMessage / sizeof(LIVE_LINK::DataPin) ? reader.Bytes(pinCount * sizeof(LIVE_LINK::DataPin)) : nullptr;
				if (reader.failed || (pinCount && !pins))
				{
					result.message = "Malformed request";
					break;
				}
				dataPins.resize(pinCount);
				if (pinCount)
					memcpy(dataPins.data(), pins, pinCount * sizeof(LIVE_LINK::DataPin));
			}
			result = LIVE_LINK::ApplyComposite(root, composite, image, imageSize, relocationList.data(), relocationCount, dataPins);
			SetActivity(result.message);
			break;
		}
		default:
			result.message = "Unknown request " + std::to_string(request.command);
			break;
		}
		if (!result.ok)
			DevTools::Log("LiveLink: request %u failed: %s", request.command, result.message.c_str());
		Reply(request, result.ok, result.message);
	}
}
}

void LIVE_LINK_SERVER::ProcessRenderRequests(IDXGISwapChain* swapChain)
{
	Request request;
	while (PopRequest(g_renderQueue, request))
	{
		if (request.connection != g_connection)
			continue; // from a connection that has gone
		Reader reader(request.payload);
		switch (request.command)
		{
		case SCREENSHOT:
		{
			const std::string path = reader.String();
			std::string error;
			std::string extension = path.size() > 4 ? path.substr(path.size() - 4) : "";
			for (char& c : extension)
				c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
			if (extension != ".bmp")
				error = "A screenshot path must end .bmp";
			const bool ok = !reader.failed && error.empty() && SCREENSHOT::Capture(swapChain, path, error);
			DevTools::Log("LiveLink: screenshot %s %s", path.c_str(), ok ? "written" : error.c_str());
			Reply(request, ok, ok ? "Written " + path : error);
			break;
		}
		case LOAD_LEVEL:
		{
			std::string level = reader.String();
			std::replace(level.begin(), level.end(), '/', '\\');
			if (level.find('\\') == std::string::npos && level.find('/') == std::string::npos)
				level = "Production\\" + level;
			bool ok = false;
			// The one the hooks saw, or else the game's own (from its globals), so this works whichever hooks are on
			GAME_LEVEL_MANAGER::Instance* manager = GAME_LEVEL_MANAGER::m_instance ? GAME_LEVEL_MANAGER::m_instance :
				static_cast<GAME_LEVEL_MANAGER::Instance*>(LIVE_LINK::LevelManager());
			if (manager)
			{
				const int index = GAME_LEVEL_MANAGER::get_level_from_name(manager, const_cast<char*>(level.c_str()));
				if (index != 0)
				{
					GAME_LEVEL_MANAGER::queue_level(manager, index);
					GAME_LEVEL_MANAGER::request_next_level(manager, false);
					ok = true;
				}
			}
			DevTools::Log("LiveLink: load level %s %s", level.c_str(), ok ? "requested" : "failed");
			Reply(request, ok, ok ? "Loading " + level : "Could not load " + level + " (the level manager has not been seen yet, or the level is unknown)");
			break;
		}
		}
	}
}
