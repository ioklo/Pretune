using System;
using System.Collections.Generic;
using System.IO;

namespace Pretune.Abstractions
{
    internal interface IFileProvider
    {
        string ReadAllText(string path);
        void WriteAllTextOrSkip(string path, string contents);
        void CreateDirectory(string path);
        bool FileExists(string path);        
        void RemoveFile(string generatedFile);
        List<string> GetAllFiles(string directory, string extension);
    }
}