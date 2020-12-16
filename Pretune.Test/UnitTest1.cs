using System;
using System.Collections.Generic;
using Xunit;

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

    public class UnitTest1
    {
        [Fact]
        public void SwitchTest()
        {
            var args = new[]
            {
                "Generated",
                "obj/Debug/Pretune.outputs",
                "Program.cs",
                "Sample/A.cs"
            };

            var testFileProvider = new TestFileProvider();
            var switchParser = new SwitchParser(testFileProvider);
            var successResult = switchParser.Parse(args) as SwitchParser.Result.Success;

            var switchInfo = successResult.SwitchInfo;
            Assert.Equal("Generated", switchInfo.GeneratedDirectory);
            Assert.Equal("obj/Debug/Pretune.outputs", switchInfo.OutputsFile);
            Assert.Equal("Program.cs", switchInfo.InputFiles[0]);
            Assert.Equal("Sample/A.cs", switchInfo.InputFiles[1]);
        }

        [Fact]
        public void ProcessorTest_GenerateOutputsFile()
        {
            var testFileProvider = new TestFileProvider();
            testFileProvider.WriteAllText("Program.cs", @"
[AutoConstructor]
public partial class Sample<T>
{
    public int X { get; set; }
    public int Y { get => 1; } // no generation
    T Param;
}");

            var processor = new Processor(testFileProvider, "Generated", "obj/Debug/Pretune.outputs", new[] { "Program.cs" });
            processor.Process();

            var text = testFileProvider.ReadAllText("obj/Debug/Pretune.outputs");
            Assert.Equal(string.Join(Environment.NewLine, new[] { "Generated\\Stub.cs", "Generated\\Program.g.cs" }), text);
        }
    }
}
