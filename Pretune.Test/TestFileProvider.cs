using Pretune.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;

namespace Pretune.Test
{
    public class TestFileProvider : IFileProvider
    {
        HashSet<string> createdDirectories;
        Dictionary<string, string> fileContents;

        public TestFileProvider()
        {
            createdDirectories = new HashSet<string>();
            fileContents = new Dictionary<string, string>();
        }

        public void CreateDirectory(string path)
        {
            // Logging
            createdDirectories.Add(path);
        }

        public bool FileExists(string path)
        {
            return fileContents.ContainsKey(path);
        }

        public List<string> GetAllFiles(string directory, string extension)
        {
            var result = new List<string>();
            foreach(var path in fileContents.Keys)
            {
                if (path.StartsWith(directory) && path.EndsWith(extension))
                    result.Add(Path.GetRelativePath(directory, path));
            }

            return result;
        }

        public string ReadAllText(string path)
        {
            if (fileContents.TryGetValue(path, out var contents))
                return contents;

            throw new PretuneGeneralException();
        }

        public void RemoveFile(string generatedFile)
        {
            throw new NotImplementedException();
        }

        public void WriteAllTextOrSkip(string path, string contents)
        {
            fileContents[path] = contents;
        }
    }
}