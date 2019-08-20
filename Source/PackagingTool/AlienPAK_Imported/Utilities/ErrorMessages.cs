using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    class ErrorMessages
    {
        public string ErrorMessageBody(PAKReturnType ReturnType)
        {
            switch (ReturnType)
            {
                case PAKReturnType.FAIL_ARCHIVE_IS_NOT_EXCPETED_TYPE:
                    //This error should be fixed soon once everything has moved to the new class system.
                    //Archives can be loaded and tested to see what format they are, without relying on filename.
                    return "The loaded archive was not of an expected type.\nPlease check the name of the archive file.";
                case PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE:
                    //General file I/O issue - the game is probably open.
                    return "Failed to load the requested file.\nIs Alien: Isolation open?";
                case PAKReturnType.FAIL_FEATURE_IS_COMING_SOON:
                    //The requested feature is a WIP and not ready to ship yet.
                    return "This functionality is coming soon!";
                case PAKReturnType.FAIL_GENERAL_LOGIC_ERROR:
                    //General logic issue, the request could not be performed as expected.
                    return "The requested action was unable to be performed.\nIf this is incorrect, please open an issue on GitHub.";
                case PAKReturnType.FAIL_REQUEST_IS_UNSUPPORTED:
                    //The request is unsupported - likely a V1/V2 texture issue.
                    return "The requested action is unsupported in the current context.\nIf this is incorrect, please open an issue on GitHub.";
                case PAKReturnType.FAIL_TRIED_TO_LOAD_VIRTUAL_ARCHIVE:
                    //Tried to load a virtual archive that doesn't exist in the filesystem.
                    //Don't think this error should ever be shown to the user really, as it would be an underlying logic issue.
                    return "Tried to load an archive which doesn't exist!\nThe archive is likely virtual - this is a logic error!\nPlease log this issue to GitHub.";
                case PAKReturnType.FAIL_UNKNOWN:
                    //General unknown error.
                    return "The requested action failed for an unknown reason.\nPlease log this to AlienPAK's GitHub issues.";
                case PAKReturnType.SUCCESS:
                    //General success.
                    return "Process complete!";
                case PAKReturnType.SUCCESS_WITH_WARNINGS:
                    //General success with warnings.
                    return "Process complete.";
            }
            return "An unknown error occured.\nPlease log this to AlienPAK's GitHub issues!";
        }
        public string ErrorMessageTitle(PAKReturnType ReturnType)
        {
            switch (ReturnType)
            {
                case PAKReturnType.SUCCESS:
                case PAKReturnType.SUCCESS_WITH_WARNINGS:
                    return "Process completed...";
                default:
                    return "The process encountered an error...";
            }
        }
    }
}
