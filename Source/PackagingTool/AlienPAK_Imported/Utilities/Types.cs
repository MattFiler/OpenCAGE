using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    public enum PAKType {
        PAK2,
        PAK_TEXTURES,
        PAK_MODELS,
        PAK_SCRIPTS,
        PAK_MATERIALMAPS,
        PAK_SHADERS,
        UNRECOGNISED
    };
    public enum PAKReturnType {
        FAIL_COULD_NOT_ACCESS_FILE,
        FAIL_TRIED_TO_LOAD_VIRTUAL_ARCHIVE,
        FAIL_GENERAL_LOGIC_ERROR,
        FAIL_ARCHIVE_IS_NOT_EXCPETED_TYPE,
        FAIL_REQUEST_IS_UNSUPPORTED,
        FAIL_FEATURE_IS_COMING_SOON,
        FAIL_UNKNOWN,
        SUCCESS,
        SUCCESS_WITH_WARNINGS
    };
}
