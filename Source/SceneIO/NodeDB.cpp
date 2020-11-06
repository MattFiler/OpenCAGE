#include "NodeDB.h"

std::vector<std::pair<CathodeID, std::string>> NodeDB::node_types = std::vector<std::pair<CathodeID, std::string>>();
std::vector<std::pair<CathodeID, std::string>> NodeDB::node_friendly_names = std::vector<std::pair<CathodeID, std::string>>();

NodeDB::NodeDB()
{
	//Node type descriptions
	BinaryReader* reader = new BinaryReader("data/name_dbs/cathode_id_map.bin");
	while (reader->GetPosition() < reader->GetLength()) {
		std::pair<CathodeID, std::string> new_db_entry = std::pair<CathodeID, std::string>();
		new_db_entry.first = CathodeID(reader);
		new_db_entry.second = reader->ReadString();
		node_types.push_back(new_db_entry);
	}
	reader->Close();

	//Node individual names
	reader = new BinaryReader("data/name_dbs/node_friendly_names.bin");
	while (reader->GetPosition() < reader->GetLength()) {
		std::pair<CathodeID, std::string> new_db_entry = std::pair<CathodeID, std::string>();
		new_db_entry.first = CathodeID(reader);
		new_db_entry.second = reader->ReadString();
		node_friendly_names.push_back(new_db_entry);
	}
	reader->Close();
}
