using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace RazorSharpener
{
    public class RazorCompiler(ILoggerFactory loggerFactory)
    {
        private static readonly string _assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        private static readonly MetadataReference[] _references =
        [
            MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IComponent).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(RazorPage).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(RazorCompiledItemAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(_assemblyPath, "mscorlib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(_assemblyPath, "System.dll")),
            MetadataReference.CreateFromFile(Path.Combine(_assemblyPath, "netstandard.dll")),
            MetadataReference.CreateFromFile(Path.Combine(_assemblyPath, "System.Core.dll")),
            MetadataReference.CreateFromFile(Path.Combine(_assemblyPath, "System.Runtime.dll"))
        ];

        private static readonly RazorProjectFileSystem _projectFilesystem = RazorProjectFileSystem.Create(".");

        private static readonly CSharpCompilationOptions _compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithOverflowChecks(true)
            .WithOptimizationLevel(OptimizationLevel.Release);

        private static readonly IServiceProvider _serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        private readonly ILoggerFactory _loggerFactory = loggerFactory;

        public RazorCompiler() : this(_serviceProvider.GetRequiredService<ILoggerFactory>())
        {            
        }

        private Assembly Compile(RazorCodeDocument codeDocument, CSharpCompilationOptions options, params IReadOnlyCollection<Assembly> referenceAssemblies)
        {
            var csDocument = codeDocument.GetCSharpDocument();

            var logger = _loggerFactory.CreateLogger<RazorCompiler>();
            logger.LogInformation("Generated code is:\n{Code}", csDocument.GeneratedCode);

            var syntaxTree = CSharpSyntaxTree.ParseText(csDocument.GeneratedCode);

            var references = _references.Concat(referenceAssemblies.Select(asm => MetadataReference.CreateFromFile(asm.Location)));

            var assemblyName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());

            var compilation = CSharpCompilation.Create(assemblyName, [syntaxTree], references, options);

            using var assemblyStrean = new MemoryStream();

            var result = compilation.Emit(assemblyStrean);

            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => x.GetMessage())));
            }

            var assembly = Assembly.Load(assemblyStrean.ToArray());
            logger.LogInformation("Generated assembly is:\n{Assembly}", assembly.FullName);

            return assembly;
        }

        public Assembly CompileContent(string content, CSharpCompilationOptions options, params IReadOnlyCollection<Assembly> referenceAssemblies)
        {
            ArgumentException.ThrowIfNullOrEmpty(content);

            var assemblyName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            var document = RazorSourceDocument.Create(content, assemblyName + ".razor");
            
            var engine = RazorProjectEngine.Create(
                RazorConfiguration.Default,
                _projectFilesystem,
                builder =>
                {
                    builder.SetNamespace("Razor.Generated");
                    builder.ConfigureClass((doc, node) =>
                    {
                        node.BaseType = typeof(RazorPage).FullName;
                    });
                });

            var codeDocument = engine.ProcessDesignTime(document, null, [], []);

            return Compile(codeDocument, options, referenceAssemblies);
        }

        public Assembly CompileContent(string content, params IReadOnlyCollection<Assembly> referenceAssemblies)
        {
            return CompileContent(content, _compilationOptions, referenceAssemblies);
        }

        public Assembly CompileFile(string sourceFile, CSharpCompilationOptions options, params IReadOnlyCollection<Assembly> referenceAssemblies)
        {
            //see https://github.com/Merlin04/RazorEngineCore/blob/multiple-templates-in-assembly/RazorEngineCore/RazorEngine.cs
            ArgumentException.ThrowIfNullOrEmpty(sourceFile);

            var item = _projectFilesystem.GetItem(sourceFile);

            var className = Path.GetFileNameWithoutExtension(sourceFile);
            var assemblyName = className;

            var engine = RazorProjectEngine.Create(
                RazorConfiguration.Default,
                _projectFilesystem,
                builder =>
                {
                    builder.SetNamespace("Razor.Generated");
                    builder.ConfigureClass((doc, node) =>
                    {
                        node.ClassName = className;
                        node.BaseType = typeof(ComponentBase).FullName;
                    });
                });

            var codeDocument = engine.Process(item);

            return Compile(codeDocument, options, referenceAssemblies);
        }

        public Assembly CompileFile(string sourceFile, params IReadOnlyCollection<Assembly> referenceAssemblies)
        {
            return CompileFile(sourceFile, _compilationOptions, referenceAssemblies);
        }
    }
}
