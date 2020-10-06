#pragma once

#include "CathodeID.h"
#include "CommonMisc.h"
#include <string>

/* Data types in the CATHODE scripting system */
enum class CathodeDataType
{
    NONE = -1,

    TRANSFORM,
    FLOAT,
    STRING,
    UNKNOWN_2, //related to splines?
    ENUM,
    RESOURCE_ID,
    BOOL,
    VECTOR3,
    INTEGER,

    //Ones below here are potentially event-based
    UNKNOWN_6,
    UNKNOWN_7,
    UNKNOWN_8,
    UNKNOWN_9,
    UNKNOWN_10,
    UNKNOWN_11,
    UNKNOWN_12
};

/* A parameter compiled in COMMANDS.PAK */
class CathodeParameter
{
public:
    CathodeParameter() = default;
    ~CathodeParameter() = default;

    int offset;
    CathodeDataType data_type;

    char* unk_content = nullptr;
    int unk_content_length = 0;
};
class CathodeTransform : public CathodeParameter
{
public:
    Vector3 position = Vector3();
    Vector3 rotation = Vector3();
};
class CathodeInteger : public CathodeParameter
{
public:
    int value = 0;
};
class CathodeString : public CathodeParameter
{
public:
    std::string value = "";
    CathodeID unk0;
    CathodeID unk1;
};
class CathodeBool : public CathodeParameter
{
public:
    bool value = false;
};
class CathodeFloat : public CathodeParameter
{
public:
    float value = 0.0f;
};
class CathodeResource : public CathodeParameter
{
public:
    CathodeID resourceID;
};
class CathodeVector3 : public CathodeParameter
{
public:
    Vector3 value = Vector3();
};
class CathodeEnum : public CathodeParameter
{
public:
    CathodeID enumID;
    int enumIndex = 0;
};