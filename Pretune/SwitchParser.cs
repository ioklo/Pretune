using Pretune.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pretune
{
    struct SwitchInfo
    {
        public string GeneratedDirectory { get; }
        public string OutputsFile { get; }
        public IReadOnlyList<string> InputFiles { get; }

        public SwitchInfo(string generatedDirectory, string outputsFile, IEnumerable<string> inputFiles)
        {
            GeneratedDirectory = generatedDirectory;
            OutputsFile = outputsFile;
            InputFiles = inputFiles.ToList();
        }
    }

    internal partial class SwitchParser
    {
        public abstract class Result
        {
            public class Success : Result
            {
                public SwitchInfo SwitchInfo { get; }
                public Success(SwitchInfo switchInfo)
                {
                    SwitchInfo = switchInfo;
                }

            }

            public class NeedMoreArguments : Result
            {
            }
        }        
    }

    partial class SwitchParser
    {
        IFileProvider fileProvider;

        public SwitchParser(IFileProvider fileProvider)
        {
            this.fileProvider = fileProvider;
        }

        string[] HandleResponseFile(string[] args)
        {   
            var finalArgs = new List<string>();
            // var regex = new Regex(@"^([\s\r\n]+((?<Arg>\w+)|(""(?<Arg>([^\""]|\"")*)"")))*[\s\r\n]*$");

            foreach (var arg in args)
            {
                if (arg.StartsWith("@"))
                {
                    var fileName = arg.Substring(1);
                    var text = fileProvider.ReadAllText(arg.Substring(1));

                    // Parse
                    //var match = regex.Match(text);
                    //if (!match.Success)
                    //    throw new InvalidOperationException("invalid response file format");

                    foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        // \" -> "
                        finalArgs.Add(line);
                    }
                }
                else
                {
                    finalArgs.Add(arg);
                }
            }

            return finalArgs.ToArray();
        }

        public Result Parse(string[] args)
        {
            var handledArgs = HandleResponseFile(args);

            if (handledArgs.Length < 2)
                return new Result.NeedMoreArguments();

            string generatedDirectory = handledArgs[0];
            string outputsFile = handledArgs[1];

            return new Result.Success(new SwitchInfo(generatedDirectory, outputsFile, handledArgs.Skip(2)));
        }
    }
}
