#include "CathodeID.h"

#include "BinaryReader.h"

CathodeID::CathodeID(BinaryReader* reader)
{
	byte_id = reader->ReadBytes(4);
}
