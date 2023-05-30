using Pretune.Abstractions;
using Pretune.Generators;
using System;
using System.Collections.Immutable;
using System.Linq;
using Xunit;
using static Pretune.Test.TestMisc;

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
                "Program.cs",
                "Sample/A.cs",
                "-r",
                "MyAssembly1.dll",
                "MyAssembly2.dll",
            };

            var testFileProvider = new TestFileProvider();
            var switchParser = new SwitchParser(testFileProvider);
            var successResult = switchParser.Parse(args) as SwitchParser.Result.Success;

            var switchInfo = successResult.SwitchInfo;
            Assert.Equal("Generated", switchInfo.GeneratedDirectory);
            Assert.Equal("Program.cs", switchInfo.InputFiles[0]);
            Assert.Equal("Sample/A.cs", switchInfo.InputFiles[1]);
            Assert.Equal("MyAssembly1.dll", switchInfo.ReferenceAssemblyFiles[0]);
            Assert.Equal("MyAssembly2.dll", switchInfo.ReferenceAssemblyFiles[1]);
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
        public void FileScopedNamespace_SimpleInput_WorksProperly()
        {
            var input = @"
namespace N.N2;

extern alias A;
using B;

[AutoConstructor]
public partial class Sample
{
    public int X;
}
";
            var output = SingleTextProcess(input);

            var expected = @"#nullable enable

namespace N.N2;
extern alias A;

using B;

public partial class Sample
{
    public Sample(int x)
    {
        this.X = x;
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
        public void AutoConstructor_UsingKeywordAsMemberName_PutAtSignBothMemberAndParameter()
        {
            var input = @"
[AutoConstructor]
public partial class C
{
    public int @namespace;
}";
            var output = SingleTextProcess(input);

            var expected = @"#nullable enable

public partial class C
{
    public C(int @namespace)
    {
        this.@namespace = @namespace;
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
    }
}