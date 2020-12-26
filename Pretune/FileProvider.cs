using Pretune.Abstractions;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Pretune
{
    class FileProvider : IFileProvider
    {
        public FileProvider()
        {

        }
        
        void HandleException(FileNotFoundException e)
        {
            throw new PretuneGeneralException($"지정된 파일({e.FileName})을 찾을 수 없습니다");
        }

        public string ReadAllText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch(FileNotFoundException e)
            {
                HandleException(e);
                return string.Empty;
            }
        }

        public void WriteAllText(string path, string contents)
        {
            try
            {
                File.WriteAllText(path, contents);
            }
            catch(FileNotFoundException e)
            {
                HandleException(e);
            }
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }
    }
}