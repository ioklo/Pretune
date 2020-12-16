using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
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
            Console.WriteLine("Usage: dotnet Pretune.dll");
            Console.WriteLine("       dotnet Pretune.dll @<response file containing arguments>");
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

                var processor = new Processor(fileProvider, switchInfo.GeneratedDirectory, switchInfo.OutputsFile, switchInfo.InputFiles);
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

