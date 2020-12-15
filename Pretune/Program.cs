using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pretune
{

    class Program
    {
        class Frame
        {
            // using과 namespace를 보존한다
            public SyntaxList<MemberDeclarationSyntax> Members { get; private set; }

            public Frame()
            {
                Members = List<MemberDeclarationSyntax>();
            }

            public void AddMember(MemberDeclarationSyntax member)
            {
                Members = Members.Add(member);
            }
        }

        class SyntaxWalker : CSharpSyntaxWalker
        {
            public CompilationUnitSyntax CompilationUnitSyntax { get; private set; }
            Frame frame;
            public bool NeedSave { get; private set; }

            public SyntaxWalker()
            {
                frame = new Frame();
                NeedSave = false;
            }

            public override void VisitCompilationUnit(CompilationUnitSyntax node)
            {
                // CompilationUnitSyntax = CompilationUnit(frame.)
                base.VisitCompilationUnit(node); // collect members

                CompilationUnitSyntax = CompilationUnit(
                    node.Externs, node.Usings, node.AttributeLists, frame.Members).NormalizeWhitespace();
            }

            public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
            {
                var prevFrame = frame;

                frame = new Frame();
                base.VisitNamespaceDeclaration(node);
                var member = NamespaceDeclaration(node.Name, node.Externs, node.Usings, frame.Members);

                frame = prevFrame;
                frame.AddMember(member);
            }

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {                
                bool bAutoConstructor = false, bImplementINotifyPropertyChanged = false;
                foreach(var attrList in node.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        var name = attr.Name.ToString();

                        if (name == "AutoConstructor")
                            bAutoConstructor = true;
                        else if (name == "ImplementINotifyPropertyChanged")
                            bImplementINotifyPropertyChanged = true;
                    }
                }

                if (!bAutoConstructor && !bImplementINotifyPropertyChanged) return;
                NeedSave = true;

                // 1. body없는 get; set; property 목록 얻어오기
                // 2. fieldDeclaration
                var members = new List<(TypeSyntax, SyntaxToken)>();
                foreach(var member in node.Members)
                {
                    switch(member)
                    {
                        case PropertyDeclarationSyntax propertyMember:
                            if (propertyMember.AccessorList != null)
                            {
                                if (propertyMember.AccessorList.Accessors.All(accessor => accessor.Body == null && accessor.ExpressionBody == null))
                                {
                                    members.Add((propertyMember.Type, propertyMember.Identifier));
                                }
                            }
                            break;

                        case FieldDeclarationSyntax fieldMember:
                            foreach (var variable in fieldMember.Declaration.Variables)
                                members.Add((fieldMember.Declaration.Type, variable.Identifier));
                            break;
                    }
                }

                var memberDecls = new List<MemberDeclarationSyntax>();                

                if (bAutoConstructor)
                {
                    // parameters
                    var parameters = new List<SyntaxNodeOrToken>();
                    var statements = new List<StatementSyntax>();

                    foreach (var member in members)
                    {
                        var memberName = member.Item2.ToString();

                        string paramName;
                        if (0 < memberName.Length)
                            paramName = "@" + char.ToLower(memberName[0]) + memberName.Substring(1);
                        else
                            paramName = "@" + memberName;

                        if (parameters.Count != 0)
                            parameters.Add(Token(SyntaxKind.CommaToken));

                        var parameter = Parameter(Identifier(paramName)).WithType(member.Item1);
                        parameters.Add(parameter);

                        var statement = ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ThisExpression(),
                                    IdentifierName(memberName)
                                ),
                                IdentifierName(paramName)
                            )
                        );

                        statements.Add(statement);
                    }

                    var constructorDecl = ConstructorDeclaration(node.Identifier)
                        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                        .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(parameters)))
                        .WithBody(Block(statements));

                    memberDecls.Add(constructorDecl);
                }

                if (bImplementINotifyPropertyChanged)
                {
                    var eventDecl = EventFieldDeclaration(
                        VariableDeclaration(
                            QualifiedName(
                                QualifiedName(
                                    IdentifierName("System"),
                                    IdentifierName("ComponentModel")),
                                IdentifierName("PropertyChangedEventHandler")))
                            .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier("PropertyChanged")))))
                            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));

                    memberDecls.Add(eventDecl);

                }

                var classDecl = ClassDeclaration(node.Identifier)
                    .WithTypeParameterList(node.TypeParameterList)
                    .WithModifiers(node.Modifiers)
                    .WithMembers(List(memberDecls));

                frame.AddMember(classDecl);
                
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet Pretune.dll");
            Console.WriteLine("       dotnet Pretune.dll @<response file containing arguments>");
        }

        static string[] HandleResponseFile(string[] args)
        {
            try
            {
                var finalArgs = new List<string>();
                // var regex = new Regex(@"^([\s\r\n]+((?<Arg>\w+)|(""(?<Arg>([^\""]|\"")*)"")))*[\s\r\n]*$");

                foreach (var arg in args)
                {
                    if (arg.StartsWith("@"))
                    {
                        var fileName = arg.Substring(1);
                        var text = File.ReadAllText(arg.Substring(1));

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
            catch (FileNotFoundException e)
            {
                throw new InvalidOperationException($"지정된 파일({e.FileName})을 찾을 수 없습니다");
            }
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

        // 둘다 relative path
        static bool IsFileInOutputDirectory(string file, string directory)
        {
            return file.StartsWith(directory, StringComparison.CurrentCultureIgnoreCase);
        }

        // 이번 실행에서 Stub을 만들었는가
        static bool StubGenerated = false;

        // stub은 매번 생성하는것이 맞는가
        static void EnsureGenerateStub(string outputDirectory)
        {
            if (StubGenerated) return;

            var filePath = Path.Combine(outputDirectory, "Stub.cs");

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

            if (File.Exists(filePath))
            {
                var fileContents = File.ReadAllText(filePath);
                
                // 덮어씌우는 것으로
                // 내용이 똑같으면 덮어씌우지 않는다
                if (fileContents == text)
                {
                    StubGenerated = true;
                    return;
                }
            }

            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(filePath, text);
            StubGenerated = true;
            return;
        }

        public static int Main(string[] argsWithResponseFile)
        {
            try
            {
                // expand response file
                var args = HandleResponseFile(argsWithResponseFile);

                Console.WriteLine(args.Length);

                if (args.Length < 1)
                {
                    PrintUsage();
                    return 1;
                }

                // 일단 이 모드에서는 input files만 받는다, output directory는 generated
                // 1. input은 작업디렉토리에 영향을 받아야 하고, relative만 받는다.. Program.cs A\Sample.cs, FullyQualified면 에러

                var outputDirectory = "Generated";
                EnsureGenerateStub(outputDirectory);

                // generated 제외
                foreach (var inputFile in args)
                {
                    if (Path.IsPathFullyQualified(inputFile))
                        throw new InvalidOperationException($"input file path need to be relative: '{inputFile}'");
                    else if (inputFile.Contains(".."))
                        throw new InvalidOperationException($"input file path should not contain '..': '{inputFile}'");
                    
                    if (IsFileInOutputDirectory(inputFile, outputDirectory))
                        throw new InvalidOperationException($"input file is descendent of output directory: '{inputFile}'");

                    var outputFile = Path.Combine(outputDirectory, inputFile);
                    outputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + ".g.cs");


                    Console.WriteLine($"Pretune: {inputFile} -> {outputFile}");

                    var text = File.ReadAllText(inputFile);
                    var syntax = CSharpSyntaxTree.ParseText(text);

                    var root = syntax.GetCompilationUnitRoot();

                    var walker = new SyntaxWalker();
                    walker.Visit(root);

                    if (walker.NeedSave)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                        File.WriteAllText(outputFile, walker.CompilationUnitSyntax.ToString());
                    }
                }

                return 0;
            }
            catch(InvalidOperationException e)
            {
                Console.WriteLine($"에러: {e.Message}");
                return 1;
            }
        }
    }
}

