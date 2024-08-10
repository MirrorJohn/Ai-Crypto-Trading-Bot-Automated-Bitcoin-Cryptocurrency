using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using log4net;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace TraderTools.Simulation
{
    public class CSharpLanguage
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static List<string> GetIncludeFilenames(string code)
        {
            var ret = new List<string>();
            foreach (var line in code.Split(Environment.NewLine))
            {
                if (line.StartsWith("//include:"))
                {
                    ret.Add(line.Replace("//include:", string.Empty));
                }
                else
                {
                    break;
                }
            }

            return ret;
        }

        private static void AddSyntaxTrees(List<string> csFilePaths, List<(SyntaxTree SyntaxTree, SourceText SourceTxt, string Path, string Code)> ret)
        {
            var encoding = Encoding.UTF8;
            var codeDirectory = Path.GetDirectoryName(csFilePaths.First());

            foreach (var path in csFilePaths)
            {
                var code = File.ReadAllText(path);

                var additionalCsFilePaths = GetIncludeFilenames(code).Select(f => Path.Combine(codeDirectory, f)).ToList();
                if (additionalCsFilePaths.Any()) AddSyntaxTrees(additionalCsFilePaths, ret);

                var buffer = encoding.GetBytes(code);
                var sourceText = SourceText.From(buffer, buffer.Length, encoding, canBeEmbedded: true);
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, new CSharpParseOptions(), path: path);
                var syntaxRootNode = syntaxTree.GetRoot() as CSharpSyntaxNode;
                ret.Add((CSharpSyntaxTree.Create(syntaxRootNode, null, path, encoding), sourceText, path, code));
            }
        }

        public static Assembly CreateAssemblyDefinition(string code, string sourceCodePath, params string[] references)
        {
            var assemblyName = Path.GetRandomFileName();
            var symbolsName = Path.ChangeExtension(assemblyName, "pdb");

            var compiled = new List<(SyntaxTree SyntaxTree, SourceText SourceTxt, string Path, string Code)>();
            AddSyntaxTrees(new List<string> { sourceCodePath } , compiled);

            var optimizationLevel = OptimizationLevel.Debug;

            var systemRefLocation = typeof(object).GetTypeInfo().Assembly.Location;
            var coreDir = Directory.GetParent(systemRefLocation);
            var metadataReferences = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Uri).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "mscorlib.dll"),
                MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "System.Runtime.dll"),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ImportAttribute).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Xml.XmlAttribute).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enum).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(coreDir.FullName, "System.Collections.dll")),
                MetadataReference.CreateFromFile(Path.Combine(coreDir.FullName, "System.IO.dll")),
                MetadataReference.CreateFromFile(typeof(File).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(coreDir.FullName, "netstandard.dll"))
            };

            metadataReferences.AddRange(references.Select(r => MetadataReference.CreateFromFile(r)));



            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: compiled.Select(x => x.SyntaxTree).ToArray(),
                references: metadataReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(optimizationLevel)
                    .WithPlatform(Platform.AnyCpu)
            );

            using (var assemblyStream = new MemoryStream())
            using (var symbolsStream = new MemoryStream())
            {
                var emitOptions = new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb,
                    pdbFilePath: symbolsName);

                var embeddedTexts = compiled.Select(x => EmbeddedText.FromSource(x.Path, x.SourceTxt)).ToList();

                EmitResult result = compilation.Emit(
                    peStream: assemblyStream,
                    pdbStream: symbolsStream,
                    embeddedTexts: embeddedTexts,
                    options: emitOptions);

                if (!result.Success)
                {
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (var d in failures)
                    {
                        Log.Error($"{d.Location} {d.Id}: {d.GetMessage()}");
                    }

                    return null;
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);
                symbolsStream?.Seek(0, SeekOrigin.Begin);

                var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream, symbolsStream);
                return assembly;
            }
        }
    }
}