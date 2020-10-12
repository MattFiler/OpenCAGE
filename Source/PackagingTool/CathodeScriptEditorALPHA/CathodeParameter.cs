using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    /* Data types in the CATHODE scripting system */
    public enum CathodeDataType
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
    }

    /* A parameter compiled in COMMANDS.PAK */
    class CathodeParameter
    {
        public int offset;
        public CathodeDataType dataType;

        public byte[] unknownContent; //This contains any byte data not yet understood for children types
    }
    class CathodeTransform : CathodeParameter
    {
        public Vector3 position = new Vector3();
        public Vector3 rotation = new Vector3();
    }
    class CathodeInteger : CathodeParameter
    {
        public int value = 0;
    }
    class CathodeString : CathodeParameter
    {
        public string value = "";
        public byte[] unk0;
        public byte[] unk1;
    }
    class CathodeBool : CathodeParameter
    {
        public bool value = false;
    }
    class CathodeFloat : CathodeParameter
    {
        public float value = 0.0f;
    }
    class CathodeResource : CathodeParameter
    {
        public byte[] resourceID = { };
    }
    class CathodeVector3 : CathodeParameter
    {
        public Vector3 value = new Vector3();
    }
    class CathodeEnum : CathodeParameter
    {
        public byte[] enumID = { };
        public int enumIndex = 0;
    }

    /* Vector 2/3 */
    class Vector2
    {
        public Vector2()
        {

        }
        public Vector2(float _x, float _y)
        {
            x = _x;
            y = _y;
        }
        public float x = 0.0f;
        public float y = 0.0f;
    }
    class Vector3
    {
        public Vector3()
        {

        }
        public Vector3(float _x, float _y, float _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }
        public float x = 0.0f;
        public float y = 0.0f;
        public float z = 0.0f;
    }
}
