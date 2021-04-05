using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Pretune.UpdateVersion
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("UpdateVersion [pretune project directory]");
                return;
            }

            var projDir = args[0];
            var versionFile = Path.Combine(projDir, "version.txt");
            var nuspecFile = Path.Combine(projDir, "Pretune.nuspec");
            var propsFile = Path.Combine(projDir, "packaging", "build", "Pretune.props");

            if (!File.Exists(versionFile))
            {
                Console.WriteLine($"{versionFile} not exists");
                return;
            }

            if (!File.Exists(nuspecFile))
            {
                Console.WriteLine($"{nuspecFile} not exists");
                return;
            }

            if (!File.Exists(propsFile))
            {
                Console.WriteLine($"{propsFile} not exists");
                return;
            }

            var versionText = File.ReadAllText(versionFile);
            var match = Regex.Match(versionText, @"(\d+)\.(\d+)\.(\d+)");
            if (!match.Success)
            {
                Console.WriteLine($"version text is not 0.0.0 format");
                return;
            }

            // 세번째만 올리면 된다
            int rev = int.Parse(match.Groups[3].Value);
            rev++;

            var newVersionText = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{rev}";
            File.WriteAllText(versionFile, newVersionText);

            var nuspecText = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>Pretune</id>
    <version>{newVersionText}</version>
    <authors>IOKLO</authors>
    <description>Syntax-based C# preprocessor generating boilerplate code before compile.</description>
    <license type=""expression"">Apache-2.0</license>
    <projectUrl>https://github.com/ioklo/Pretune</projectUrl>
    <copyright>Copyright © IOKLO 2020</copyright>
  </metadata>
  <files>
    <file src=""$projectdir$packaging\build\*.*"" target=""build"" />
    <file src=""$projectdir$packaging\lib\*"" target=""lib"" />
    <file src=""$publishdir$**\*"" target=""tools/"" />
  </files>
</package>
";
            File.WriteAllText(nuspecFile, nuspecText);

            var propsText = $@"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <PretuneVersion>{newVersionText}</PretuneVersion>
  </PropertyGroup>
</Project>";
            File.WriteAllText(propsFile, propsText);

        }
    }
}
