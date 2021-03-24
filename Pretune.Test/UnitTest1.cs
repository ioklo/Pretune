using Pretune.Abstractions;
using Pretune.Generators;
using System;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Pretune.Test
{
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

        // ImmutableArray<> 인 경우 C.Equals를 사용
        [CustomEquatable(typeof(ImmutableArray<>))]
        static class C
        {
            public bool Equals<T>(ImmutableArray<T> x, ImmutableArray<T> y)
            {

            }

            public int GetHashCode<T>(ImmutableArray<T> x)
            {

            }
        }

        [Fact]
        public void Processor_InputCauseGeneratingFiles_GenerateOutputsFile()
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
        public void Processor_NestedClass_PlaceNestedClassRight()
        {
            var input = @"
namespace N
{
    [AutoConstructor]
    partial class X
    {
        [AutoConstructor]
        public partial class Sample<T>
        {
            public int X { get; set; }
            public int Y { get => 1; } // no generation
            T Params;
        }
    }
}";
            var output = SingleTextProcess(input);

            var expected = @"#nullable enable

namespace N
{
    partial class X
    {
        public partial class Sample<T>
        {
            public Sample(int x, T @params)
            {
                this.X = x;
                this.Params = @params;
            }
        }

        public X()
        {
        }
    }
}";

            Assert.Equal(expected, output);

        }

        [Fact]
        public void AutoConstructor_SimpleInput_GenerateConstuctor()
        {
            var input = @"
namespace N
{
    [AutoConstructor]
    public partial class Sample<T>
    {
        public int X { get; set; }
        public int Y { get => 1; } // no generation
        T Params;
    }
}";
            var output = SingleTextProcess(input);

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
            
            Assert.Equal(expected, output);
        }

        [Fact]
        public void AutoConstructor_ContainsStaticVariable_IgnoreStaticVariable()
        {
            var input = @"
namespace N
{
    [AutoConstructor]
    public partial class Sample<T>
    {
        public static readonly int X; // no generation
        public static int Y { get; }
    }
}";
            var output = SingleTextProcess(input);

            var expected = @"#nullable enable

namespace N
{
    public partial class Sample<T>
    {
        public Sample()
        {
        }
    }
}";

            Assert.Equal(expected, output);
        }



        [Fact]
        public void ImplementINotifyPropertyChanged_SimpleInput_ImplemnetINotifyPropertyChanged()
        {
            var input = @"
namespace N
{
    [ImplementINotifyPropertyChanged]
    public partial class Sample<T>
    {
        string firstName;
        T lastName;
    }
}";
            var output = SingleTextProcess(input);

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

            Assert.Equal(expected, output);
        }

        string SingleTextProcess(string input)
        {
            var identifierConverter = new CamelCaseIdentifierConverter();
            var generators = ImmutableArray.Create<IGenerator>(
                new ConstructorGenerator(identifierConverter),
                new INotifyPropertyChangedGenerator(identifierConverter),
                new IEquatableGenerator());

            var testFileProvider = new TestFileProvider();
            testFileProvider.WriteAllText("Program.cs", input);
            var processor = new Processor(testFileProvider, "Generated", "obj/Debug/Pretune.outputs", new[] { "Program.cs" }, generators);

            processor.Process();

            return testFileProvider.ReadAllText("Generated\\Program.g.cs");
        }

        [Fact]
        public void ImplementINotifyPropertyChanged_HasDependsOnAttributes_AddExtraNotifications()
        {
            var input = @"using Pretune;

namespace N
{
    [ImplementINotifyPropertyChanged]
    public partial class Sample<T>
    {
        string firstName;
        T lastName;

        [DependsOn(nameof(firstName), nameof(lastName))]
        public string FamilyName { get => @$""{firstName} {lastName}""; }
    }
}";
            var output = SingleTextProcess(input);
            
            var expected = @"#nullable enable

using Pretune;

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
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(""FamilyName""));
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
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(""FamilyName""));
                }
            }
        }
    }
}";
            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementINotifyPropertyChanged_PrivateFieldHasDependsOnAttribute_IgnoreAttribute()
        {
            var input = @"using Pretune;

namespace N
{
    [ImplementINotifyPropertyChanged]
    public partial class Sample<T>
    {
        string firstName;

        [DependsOn(nameof(firstName))]
        public string P1 { get; } // Warning, auto property, no generation

        [DependsOn(nameof(firstName))]
        private string P2 { get => firstName; } // Warning, private property, no generation
    }
}";
            var output = SingleTextProcess(input);

            var expected = @"#nullable enable

using Pretune;

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
    }
}";
            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementIEquatable_SimpleClass_ImplementIEquatable()
        {
            var input = @"
namespace N
{
    [ImplementIEquatable]
    public partial class Script
    {
        int x;
        public string Y { get; }
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

namespace N
{
    public partial class Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => Equals(obj as Script);
        public bool Equals(Script? other)
        {
            if (other != null)
                return false;
            if (!x.Equals(other.x))
                return false;
            if (!Y.Equals(other.Y))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(this.x.GetHashCode());
            hashCode.Add(this.Y.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}";

            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementIEquatable_SimpleStruct_ImplementIEquatable()
        {
            var input = @"
namespace N
{
    [ImplementIEquatable]
    public partial struct Script
    {
        int x;
        public string Y { get; }
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

namespace N
{
    public partial struct Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => obj is Script other && Equals(other);
        public bool Equals(Script other)
        {
            return x.Equals(other.x) && Y.Equals(other.Y);
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(this.x.GetHashCode());
            hashCode.Add(this.Y.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}";

            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementIEquatable_HasNullableStructMember_ImplementIEquatable()
        {
            var input = @"
namespace N
{
    [ImplementIEquatable]
    public partial class Script
    {
        int? nx;
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

namespace N
{
    public partial class Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => Equals(obj as Script);
        
        public bool Equals(Script? other)
        {
            if (other != null) return false;

            if (nx != null && other.nx != null)
            {
                if (!nx.Value.Equals(other.nx.Value)) return false;
            }
            else if (nx != null || other.nx != null) return false;

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(this.nx == null ? 0 : this.nx.Value.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}";

            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementIEquatable_HasNullableClass_ImplementIEquatable()
        {
            var input = @"
namespace N
{
    [ImplementIEquatable]
    public partial class Script
    {
        string? ns;
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

namespace N
{
    public partial class Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => Equals(obj as Script);
        public bool Equals(Script? other)
        {
            if (other != null)
                return false;
            if (ns != null)
            {
                if (!ns.Equals(other.ns))
                    return false;
            }
            else if (other.ns != null)
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(this.ns == null ? 0 : this.ns.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}";
            Assert.Equal(expected, output);
        }
    }
}