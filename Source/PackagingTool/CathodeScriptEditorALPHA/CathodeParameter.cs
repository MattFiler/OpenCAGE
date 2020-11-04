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

        POSITION,
        FLOAT,
        STRING,
        SPLINE_DATA,
        ENUM,
        SHORT_GUID,
        FILEPATH,
        BOOL,
        DIRECTION,
        INTEGER,

        OBJECT,
        UNKNOWN_7,
        ZONE_LINK_PTR,
        ZONE_PTR,
        MARKER,
        CHARACTER,
        CAMERA
    }

    /* Resource reference types */
    public enum CathodeResourceReferenceType
    {
        RENDERABLE_INSTANCE,             //This one references an entry in the REnDerable elementS (REDS.BIN) database
        COLLISION_MAPPING,               //This one seems to be called in another script block that I'm not currently parsing 
        TRAVERSAL_SEGMENT,               //This just seems to be two -1 32-bit integers
        NAV_MESH_BARRIER_RESOURCE,       //This just seems to be two -1 32-bit integers (same as above)
        EXCLUSIVE_MASTER_STATE_RESOURCE, //This just seems to be two -1 32-bit integers (same as above)
        DYNAMIC_PHYSICS_SYSTEM,          //This is a count (usually small) and then a -1 32-bit int
        ANIMATED_MODEL,                  //This is a count (usually small) and then a -1 32-bit int (same as above)
    }

    /* A parameter compiled in COMMANDS.PAK */
    public class CathodeParameter
    {
        public int offset;
        public CathodeDataType dataType;

        public byte[] unknownContent; //This contains any byte data not yet understood for children types
    }
    public class CathodeTransform : CathodeParameter
    {
        public Vec3 position = new Vec3();
        public Vec3 rotation = new Vec3();
    }
    public class CathodeInteger : CathodeParameter
    {
        public int value = 0;
    }
    public class CathodeString : CathodeParameter
    {
        public int initial_length = 0; //Added for parameter editing, when the format is fully known, this will not be a limitation
        public string value = "";
        public byte[] unk0;
        public byte[] unk1;
    }
    public class CathodeBool : CathodeParameter
    {
        public bool value = false;
    }
    public class CathodeFloat : CathodeParameter
    {
        public float value = 0.0f;
    }
    public class CathodeResource : CathodeParameter
    {
        public byte[] resourceID = { };
    }
    public class CathodeVector3 : CathodeParameter
    {
        public Vec3 value = new Vec3();
    }
    public class CathodeEnum : CathodeParameter
    {
        public byte[] enumID = { };
        public int enumIndex = 0;
    }
    public class CathodeSpline : CathodeParameter
    {
        public List<CathodeTransform> splinePoints = new List<CathodeTransform>();
    }

    /* Vector 2/3 */
    public class Vec2
    {
        public Vec2()
        {

        }
        public Vec2(float _x, float _y)
        {
            x = _x;
            y = _y;
        }
        public float x = 0.0f;
        public float y = 0.0f;
    }
    public class Vec3
    {
        public Vec3()
        {

        }
        public Vec3(float _x, float _y, float _z)
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
