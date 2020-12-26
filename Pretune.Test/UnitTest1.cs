using Pretune.Abstractions;
using Pretune.Generators;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        // UnitOfWorkName_ScenarioName_ExpectedBehavior

        [Fact]
        public void ParseSwitch_FileListAsArguments_DistinguishGeneratedDirectoryOutputsFileInputFiles()
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
        public void Process_InputFileNeedGenerateFile_GenerateOutputsFile()
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
            var identifierConverter = new CamelCaseIdentifierConverter();
            var generators = ImmutableArray.Create<IGenerator>(
                new ConstructorGenerator(identifierConverter),
                new INotifyPropertyChangedGenerator(identifierConverter));

            var processor = new Processor(testFileProvider, "Generated", "obj/Debug/Pretune.outputs", new[] { "Program.cs" }, generators);
            processor.Process();

            var text = testFileProvider.ReadAllText("obj/Debug/Pretune.outputs");
            Assert.Equal(string.Join(Environment.NewLine, new[] { "Generated\\Stub.cs", "Generated\\Program.g.cs" }), text);
        }

        [Fact]
        public void AutoConstructor_SimpleInput_GenerateConstuctor()
        {
            var testFileProvider = new TestFileProvider();
            testFileProvider.WriteAllText("Program.cs", @"
namespace N
{
    [AutoConstructor]
    public partial class Sample<T>
    {
        public int X { get; set; }
        public int Y { get => 1; } // no generation
        T Params;
    }
}");
            var identifierConverter = new CamelCaseIdentifierConverter();
            var generators = ImmutableArray.Create<IGenerator>(
                new ConstructorGenerator(identifierConverter),
                new INotifyPropertyChangedGenerator(identifierConverter));
            var processor = new Processor(testFileProvider, "Generated", "obj/Debug/Pretune.outputs", new[] { "Program.cs" }, generators);

            processor.Process();

            var text = testFileProvider.ReadAllText("Generated\\Program.g.cs");

            var expected = @"#nullable enable

namespace N
{
    public partial class Sample<T>
    {
        public Sample(int x, T @params)
        {
            this.X = x;
            this.Params = @params;
        }
    }
}";
            
            Assert.Equal(expected, text);
        }

        [Fact]
        public void ImplementINotifyPropertyChanged_SimpleInput_ImplmenetINotifyPropertyChanged()
        {
            var testFileProvider = new TestFileProvider();
            testFileProvider.WriteAllText("Program.cs", @"
namespace N
{
    [ImplementINotifyPropertyChanged]
    public partial class Sample<T>
    {
        string firstName;
        T lastName;
    }
}");
            var identifierConverter = new CamelCaseIdentifierConverter();
            var generators = ImmutableArray.Create<IGenerator>(
                new ConstructorGenerator(identifierConverter),
                new INotifyPropertyChangedGenerator(identifierConverter));
            var processor = new Processor(testFileProvider, "Generated", "obj/Debug/Pretune.outputs", new[] { "Program.cs" }, generators);

            processor.Process();

            var text = testFileProvider.ReadAllText("Generated\\Program.g.cs");

            var expected = @"#nullable enable

namespace N
{
    public partial class Sample<T> : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public string FirstName
        {
            get => firstName;
            set
            {
                if (!System.Collections.Generic.EqualityComparer<string>.Default.Equals(firstName, value))
                {
                    firstName = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(""FirstName""));
                }
            }
        }

        public T LastName
        {
            get => lastName;
            set
            {
                if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(lastName, value))
                {
                    lastName = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(""LastName""));
                }
            }
        }
    }
}";

            Assert.Equal(expected, text);
        }

        [Fact]
        public void ImplementIEquatable_SimpleInput_ImplementIEquatable()
        {
            var testFileProvider = new TestFileProvider();
            testFileProvider.WriteAllText("Program.cs", @"
namespace N
{
    [ImplementIEquatable]
    public partial class Script
    {
        int x;
        public string Y { get; }
    }
}");
            var identifierConverter = new CamelCaseIdentifierConverter();
            var generators = ImmutableArray.Create<IGenerator>(
                new ConstructorGenerator(identifierConverter),
                new INotifyPropertyChangedGenerator(identifierConverter),
                new IEquatableGenerator());

            var processor = new Processor(testFileProvider, "PretuneGenerated", "obj/Debug/Pretune.outputs", new[] { "Program.cs" }, generators);
            processor.Process();            

            var text = testFileProvider.ReadAllText("PretuneGenerated\\Program.g.cs");
            var expected = @"#nullable enable

namespace N
{
    public partial class Script : System.IEquatable<Script?>
    {
        public override bool Equals(object? obj) => Equals(obj as Script);
        public bool Equals(Script? other)
        {
            return other != null && System.Collections.Generic.EqualityComparer<int>.Default.Equals(x, other.x) && System.Collections.Generic.EqualityComparer<string>.Default.Equals(Y, other.Y);
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(x);
            hashCode.Add(Y);
            return hashCode.ToHashCode();
        }
    }
}";

            Assert.Equal(expected, text);
        }
    }
}