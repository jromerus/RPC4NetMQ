using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDemo
{
    public interface IFileManager
    {
        bool SetFileToPath(string path, byte[] content);
    }
}
