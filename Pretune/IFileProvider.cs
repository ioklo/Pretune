using System;

namespace Pretune
{
    internal interface IFileProvider
    {
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void CreateDirectory(string path);
        bool FileExists(string path);
    }
}