using Pretune.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

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

        public void WriteAllTextOrSkip(string path, string contents)
        {
            try
            {
                // 파일이 있고, 내용이 똑같으면, 덮어쓰지 않고 건너뛴다
                if (File.Exists(path))
                {
                    var prevText = ReadAllText(path);
                    if (prevText == contents)
                    {
                        // Console.Error.WriteLine($"{path}: Pretune: skip, doesn't changed");
                        return;
                    }
                }

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

        public void RemoveFile(string path)
        {
            File.Delete(path);
        }

        public List<string> GetAllFiles(string path, string extension)
        {
            if (!Directory.Exists(path)) return new List<string>();

            var result = new List<string>();
            foreach (var file in Directory.GetFiles(path, "*" + extension, SearchOption.AllDirectories))
                result.Add(Path.GetRelativePath(path, file));
            return result;
        }
    }
}