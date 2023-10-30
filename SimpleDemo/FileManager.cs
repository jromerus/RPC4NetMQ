using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDemo
{
    public class FileManager : IFileManager
    {        
        public bool SetFileToPath(string path, byte[] content)
        {
            if (path == null) return false;
            //logger.LogDebug("Escribiendo en path -> " + path);
            try
            {
                if (content == null) File.Delete(path);
                else
                {
                    string? folder = Path.GetDirectoryName(path);
                    if (folder != null && !Directory.Exists(folder.Trim()))
                    {
                        Directory.CreateDirectory(folder);
                    }
                    File.WriteAllBytes(path, content);
                }
                return true;
            }
            catch (IOException e)
            {
                //logger.LogError(e.Message + "\r\n" + e.StackTrace);
                return false;
            }
        }
    }
}
