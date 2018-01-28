// Alien Isolation (Binary XML converter)
// Written by WRS (xentax.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;

namespace PackagingTool
{
    class AlienConverter
    {
        static string version_string = "0.03";

        private void ShowInfo()
        {
            //Console.WriteLine("Usage:");
            //Console.WriteLine("\tAlienBML.exe source_file <target_file>");
            //Console.WriteLine("\t source_file   A BML or XML filename");
            //Console.WriteLine("\t target_file   A BML or XML filename (optional)");
        }

        // basic extension check
        static bool CheckExtension(string filename, string lower_ext = "bml")
        {
            int li = filename.LastIndexOf('.');

            if (li != -1)
            {
                filename = filename.Substring(li + 1);
            }

            return filename.ToLower() == lower_ext;
        }

        // switch between extensions
        static string ConvertFileName(string filename)
        {
            // strip extension
            int li = filename.LastIndexOf('.');
            string fn = (li != -1) ? filename.Substring(0, li) : filename;

            if (CheckExtension(filename))
            {
                return fn + ".xml";
            }
            else
            {
                return fn + ".bml";
            }            
        }

        string filename_src, filename_dst;

        bool ConvertFile(ref BML bml)
        {
            bool result = true;

            //Console.WriteLine("Exporting to {0}...", filename_dst);

            if (File.Exists(filename_dst))
            {
                //Console.WriteLine("Warning: destination file will be replaced");
                File.Delete(filename_dst);
            }

            BinaryWriter bw = new BinaryWriter(File.OpenWrite(filename_dst));

            if (CheckExtension(filename_dst, "xml") )
            {
                string data = "";
                bml.ExportXML(ref data);

                // writing raw data otherwise we get the string encoding header
                bw.Write(Encoding.Default.GetBytes(data), 0, data.Length);
            }
            else if( CheckExtension(filename_dst) )
            {
                bml.ExportBML(bw);
            }
            else
            {
                result = false;
                //Console.WriteLine("Unexpected destination file type \"{0}\"", filename_dst);
            }

            bw.Close();

            return result;
        }

        bool ProcessFile()
        {
            FileStream strm;

            try
            {
                strm = File.OpenRead(filename_src);
            }
            catch
            {
                //Console.WriteLine("Unable to open \"{0}\"", filename_src);
                return false;
            }

            if (!strm.CanRead)
            {
                //Console.WriteLine("Unable to read \"{0}\"", filename_src);
                return false;
            }
                
            BinaryReader br = new BinaryReader(strm);
            BML bml = null;
            bool valid = true;

            if (CheckExtension(filename_src))
            {
                bml = new BML();
                //Console.WriteLine("Reading as BML...");
                valid = bml.ReadBML(br);
                
                if( !valid )
                {
                    //Console.WriteLine("Reading BML failed");
                }
            }
            else if (CheckExtension(filename_src, "xml"))
            {
                bml = new BML();
                //Console.WriteLine("Reading as XML...");
                valid = bml.ReadXML(br);

                if( !valid )
                {
                    //Console.WriteLine("Reading XML failed");
                }
            }
            else
            {
                valid = false;
               // Console.WriteLine("Unexpected source file type \"{0}\"", filename_src);
            }

            strm.Close();

            if( valid )
            {
                return ConvertFile(ref bml);
            }

            return valid;
        }

        public AlienConverter(string filenameSource, string filenameDestination)
        {
            filename_src = filenameSource;
            filename_dst = filenameDestination;  
        }

        public bool Run()
        {
            //Console.WriteLine("Alien Isolation BML Converter v{0}", version_string);
            //Console.WriteLine("Written by WRS (xentax.com)");

            if (filename_src == null)
            {
                ShowInfo();
                return true;
            }
            else
            {
                if( filename_dst == null )
                {
                    filename_dst = ConvertFileName(filename_src);
                }

                return ProcessFile();
            }
        }
    }
}
