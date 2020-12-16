﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pretune
{
    class Processor
    {
        string generatedDirectory;
        string outputsFile;
        IReadOnlyList<string> inputFiles;
        List<string> outputFiles;

        IFileProvider fileProvider;

        public Processor(IFileProvider fileProvider, string generatedDirectory, string outputsFile, IEnumerable<string> inputFiles)
        {
            this.fileProvider = fileProvider;

            this.generatedDirectory = generatedDirectory;
            this.outputsFile = outputsFile;
            this.inputFiles = inputFiles.ToList();

            this.outputFiles = new List<string>();
        }

        // 둘다 relative path
        static bool IsFileInOutputDirectory(string file, string directory)
        {
            return file.StartsWith(directory, StringComparison.CurrentCultureIgnoreCase);
        }
        
        public void GenerateStub()
        {
            var filePath = Path.Combine(generatedDirectory, "Stub.cs");

            var text = @"using System;

namespace Pretune
{
    class AutoConstructorAttribute : Attribute { }
    class ImplementINotifyPropertyChangedAttribute : Attribute { }
    class DependsOnAttribute : Attribute 
    {
        public DependsOnAttribute(params string[] names) { }
    }
}";

            if (fileProvider.FileExists(filePath))
            {
                var fileContents = fileProvider.ReadAllText(filePath);

                // 덮어씌우는 것으로
                // 내용이 똑같으면 덮어씌우지 않는다
                if (fileContents == text) return;
            }

            Save(filePath, text);            
        }

        public void Process()
        {
            // 일단 이 모드에서는 input files만 받는다, output directory는 generated
            // 1. input은 작업디렉토리에 영향을 받아야 하고, relative만 받는다.. Program.cs A\Sample.cs, FullyQualified면 에러
            GenerateStub();

            // generated 제외
            foreach (var inputFile in inputFiles)
            {
                VerifyInputFilePath(inputFile);

                string outputFile = MakeOutputFilePath(inputFile);

                Console.WriteLine($"Pretune: {inputFile} -> {outputFile}");

                var text = fileProvider.ReadAllText(inputFile);
                var unit = ParseCompilationUnit(text);

                var walker = new SyntaxWalker();
                walker.Visit(unit);

                if (walker.NeedSave)
                {
                    Debug.Assert(walker.CompilationUnitSyntax != null);
                    Save(outputFile, walker.CompilationUnitSyntax.ToString());
                }
            }

            fileProvider.WriteAllText(outputsFile, string.Join(Environment.NewLine, outputFiles));
        }

        void Save(string outputFile, string contents)
        {
            var directory = Path.GetDirectoryName(outputFile);
            if (directory != null)
                fileProvider.CreateDirectory(directory);

            fileProvider.WriteAllText(outputFile, contents);

            // collect outputFiles
            outputFiles.Add(outputFile);
        }

        void VerifyInputFilePath(string inputFile)
        {
            if (Path.IsPathFullyQualified(inputFile))
                throw new PretuneGeneralException($"input file path need to be relative: '{inputFile}'");
            else if (inputFile.Contains(".."))
                throw new PretuneGeneralException($"input file path should not contain '..': '{inputFile}'");

            if (IsFileInOutputDirectory(inputFile, generatedDirectory))
                throw new PretuneGeneralException($"input file is descendent of output directory: '{inputFile}'");
        }

        string MakeOutputFilePath(string inputFile)
        {
            var outputFile = Path.Combine(generatedDirectory, inputFile);
            var outputDirectoryName = Path.GetDirectoryName(outputFile);
            if (outputDirectoryName != null)
                outputFile = Path.Combine(outputDirectoryName, Path.GetFileNameWithoutExtension(outputFile) + ".g.cs");
            return outputFile;
        }
    }
}