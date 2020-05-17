using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    /*
     *
     * Generic PAK handler.
     * This can probably be depreciated when all handlers are as complete as the PAK2 implementation.
     * 
    */
    class PAK
    {
        AnyPAK PAKHandler;
        public PAKType Format = PAKType.UNRECOGNISED;

        /* Open a PAK archive */
        public PAKReturnType Open(string FilePath)
        {
            //Set PAKHandler to appropriate class depending on input filename
            switch (Path.GetFileName(FilePath).ToUpper())
            {
                case "GLOBAL_TEXTURES.ALL.PAK":
                case "LEVEL_TEXTURES.ALL.PAK":
                    PAKHandler = new TexturePAK(FilePath);
                    Format = PAKType.PAK_TEXTURES;
                    break;
                case "GLOBAL_MODELS.PAK":
                case "LEVEL_MODELS.PAK":
                    PAKHandler = new ModelPAK(FilePath);
                    Format = PAKType.PAK_MODELS;
                    break;
                case "MATERIAL_MAPPINGS.PAK":
                    PAKHandler = new MaterialMapPAK(FilePath);
                    Format = PAKType.PAK_MATERIALMAPS;
                    break;
                case "COMMANDS.PAK":
                    PAKHandler = new CommandPAK(FilePath);
                    Format = PAKType.PAK_SCRIPTS;
                    break;
                case "LEVEL_SHADERS_DX11.PAK":
                case "BESPOKESHADERS_DX11.PAK":
                case "DEFERREDSHADERS_DX11.PAK":
                case "POSTPROCESSINGSHADERS_DX11.PAK":
                case "REQUIREDSHADERS_DX11.PAK":
                    PAKHandler = new ShaderPAK(FilePath);
                    Format = PAKType.PAK_SHADERS;
                    break;
                default:
                    PAKHandler = new PAK2(FilePath);
                    Format = PAKType.PAK2;
                    break;
            }

            //Attempt to load, and if it fails, set format to UNRECOGNISED
            PAKReturnType LoadReturnInfo = PAKHandler.Load();
            switch (LoadReturnInfo)
            {
                case PAKReturnType.SUCCESS_WITH_WARNINGS:
                case PAKReturnType.SUCCESS:
                    return LoadReturnInfo;
                default:
                    Format = PAKType.UNRECOGNISED;
                    return LoadReturnInfo;
            }
        }

        /* Parse a PAK archive */
        public List<string> Parse()
        {
            return PAKHandler.GetFileNames();
        }

        /* Get the size of a file within the PAK archive */
        public int GetFileSize(string FileName)
        {
            return PAKHandler.GetFilesize(FileName);
        }

        /* Export from a PAK archive */
        public PAKReturnType ExportFile(string FileName, string ExportPath)
        {
            return PAKHandler.ExportFile(ExportPath, FileName);
        }

        /* Import to a PAK archive */
        public PAKReturnType ImportFile(string FileName, string ImportPath)
        {
            //Not all formats currently support the full Save() method functionality.
            if (Format == PAKType.PAK2 || Format == PAKType.PAK_MATERIALMAPS)
            {
                PAKReturnType ReplaceFileReturnCode = PAKHandler.ReplaceFile(ImportPath, FileName);
                if (ReplaceFileReturnCode == PAKReturnType.SUCCESS)
                {
                    return PAKHandler.Save();
                }
                return ReplaceFileReturnCode;
            }

            return PAKHandler.ReplaceFile(ImportPath, FileName);
        }

        /* Remove from a PAK archive */
        public PAKReturnType RemoveFile(string FileName)
        {
            if (Format != PAKType.PAK2) { return PAKReturnType.FAIL_FEATURE_IS_COMING_SOON; } //Currently only supported in PAK2
            PAKReturnType DeleteFilePAK2 = PAKHandler.DeleteFile(FileName);
            if (DeleteFilePAK2 == PAKReturnType.SUCCESS)
            {
                return PAKHandler.Save();
            }
            return DeleteFilePAK2;
        }

        /* Add to a PAK archive */
        public PAKReturnType AddNewFile(string NewFile)
        {
            if (Format != PAKType.PAK2) { return PAKReturnType.FAIL_FEATURE_IS_COMING_SOON; } //Currently only supported in PAK2
            PAKReturnType AddFilePAK2 = PAKHandler.AddFile(NewFile);
            if (AddFilePAK2 == PAKReturnType.SUCCESS)
            {
                return PAKHandler.Save();
            }
            return AddFilePAK2;
        }
    }
}
