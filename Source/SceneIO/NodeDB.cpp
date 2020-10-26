#include "NodeDB.h"

std::vector<std::pair<CathodeID, std::string>> NodeDB::param_names = std::vector<std::pair<CathodeID, std::string>>();
std::vector<std::pair<std::pair<CathodeID, std::string>, bool>> NodeDB::node_types = std::vector<std::pair<std::pair<CathodeID, std::string>, bool>>();
std::vector<std::string> NodeDB::node_types_name_only = std::vector<std::string>();
std::vector<std::pair<CathodeID, std::string>> NodeDB::node_friendly_names = std::vector<std::pair<CathodeID, std::string>>();

NodeDB::NodeDB()
{
	//Node type descriptions
	BinaryReader* reader = new BinaryReader("data/name_dbs/node_desc.bin");
	while (reader->GetPosition() < reader->GetLength()) {
		std::pair<std::pair<CathodeID, std::string>, bool> new_db_entry = std::pair<std::pair<CathodeID, std::string>, bool>();
		new_db_entry.first.first = CathodeID(reader);
		new_db_entry.first.second = reader->ReadString();
		new_db_entry.second = false; //false means we're shown in 3d editor at the start
		node_types.push_back(new_db_entry);
		node_types_name_only.push_back(new_db_entry.first.second);
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

	//Parameter names
	reader = new BinaryReader("data/name_dbs/param_desc.bin");
	while (reader->GetPosition() < reader->GetLength()) {
		std::pair<CathodeID, std::string> new_db_entry = std::pair<CathodeID, std::string>();
		new_db_entry.first = CathodeID(reader);
		new_db_entry.second = reader->ReadString();
		param_names.push_back(new_db_entry);
	}
	reader->Close();
}
