using Pretune.Abstractions;
using System.Collections.Generic;

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

        public string ReadAllText(string path)
        {
            if (fileContents.TryGetValue(path, out var contents))
                return contents;

            throw new PretuneGeneralException();
        }

        public void WriteAllText(string path, string contents)
        {
            fileContents[path] = contents;
        }
    }
}