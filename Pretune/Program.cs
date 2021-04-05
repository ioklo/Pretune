using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pretune.Abstractions;
using Pretune.Generators;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Pretune
{
    partial class Program
    {   
        static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet Pretune.dll @<line-separated file containing arguments>");
            Console.WriteLine("       dotnet Pretune.dll <generated directory> <source file1> <source file2> ... -r <reference assembly file1> <reference assembly file2>");
        }

        //static IEnumerable<string> EnumerateCSFiles(string inputDirectory, string outputDirectoryName)
        //{
        //    inputDirectory = Path.TrimEndingDirectorySeparator(inputDirectory);
        //    inputDirectory.Length

        //    foreach (var file in Directory.EnumerateFiles(inputDirectory, "*.cs").Select(f => f.Substring(inputDirectory.Length))
        //        yield return file;

        //    foreach (var dir in Directory.EnumerateDirectories(inputDirectory))
        //    {
        //        if (Path.GetFileName(dir).Equals(outputDirectoryName, StringComparison.CurrentCultureIgnoreCase))
        //            continue;

        //        foreach (var file in Directory.EnumerateFiles(inputDirectory, "*.cs", SearchOption.AllDirectories))
        //            yield return file;
        //    }
        //}
        
        public static int Main(string[] args)
        {
            try
            {
                var fileProvider = new FileProvider();
                var switchParser = new SwitchParser(fileProvider);
                var parseResult = switchParser.Parse(args); // 에러 처리는 어떻게 할 것인가                                

                SwitchInfo switchInfo = default;
                switch(parseResult)
                {
                    case SwitchParser.Result.Success successResult:
                        switchInfo = successResult.SwitchInfo;
                        break;

                    case SwitchParser.Result.NeedMoreArguments _:
                        PrintUsage();
                        return 1;
                }

                var identifierConverter = new CamelCaseIdentifierConverter();
                var generators = ImmutableArray.Create<IGenerator>(
                    new ConstructorGenerator(identifierConverter),
                    new INotifyPropertyChangedGenerator(identifierConverter),
                    new IEquatableGenerator());

                ImmutableArray<string> refAssemblyFiles = switchInfo.ReferenceAssemblyFiles;
                if (refAssemblyFiles.Length == 0)
                    refAssemblyFiles = ImmutableArray.Create(typeof(object).Assembly.Location);

                var processor = new Processor(fileProvider, switchInfo.GeneratedDirectory, switchInfo.InputFiles, refAssemblyFiles, generators);
                processor.Process();

                return 0;
            }
            catch(PretuneGeneralException e)
            {
                Console.WriteLine($"에러: {e.Message}");
                return 1;
            }
        }
    }
}

