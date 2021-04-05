using Pretune.Abstractions;
using Pretune.Generators;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Pretune.Test
{
    static class TestMisc
    {
        public static string SingleTextProcess(string input)
        {
            var identifierConverter = new CamelCaseIdentifierConverter();
            var generators = ImmutableArray.Create<IGenerator>(
                new ConstructorGenerator(identifierConverter),
                new INotifyPropertyChangedGenerator(identifierConverter),
                new IEquatableGenerator());

            var testFileProvider = new TestFileProvider();
            testFileProvider.WriteAllTextOrSkip("Program.cs", input);

            var refAssembly = typeof(object).Assembly.Location;
            var immutableAssembly = typeof(ImmutableArray<>).Assembly.Location;

            var processor = new Processor(testFileProvider, "Generated",
                ImmutableArray.Create("Program.cs"), 
                ImmutableArray.Create(refAssembly, immutableAssembly), generators);

            processor.Process();

            return testFileProvider.ReadAllText("Generated\\Program.g.cs");
        }

    }
}
