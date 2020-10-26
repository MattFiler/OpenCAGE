#pragma once

class BinaryReader;

class CathodeID {
public:
	CathodeID() = default;
	CathodeID(char* id) {
		byte_id = id;
	}
	CathodeID(BinaryReader* reader);
	~CathodeID() = default;

	char* Get() {
		return byte_id;
	}

	bool Equals(CathodeID id) {
		if (byte_id == nullptr) return (id.Get() == nullptr);

		for (int i = 0; i < 4; i++) {
			if (id.Get()[i] != byte_id[i]) return false;
		}
		return true;
	}

private:
	char* byte_id = nullptr;
};