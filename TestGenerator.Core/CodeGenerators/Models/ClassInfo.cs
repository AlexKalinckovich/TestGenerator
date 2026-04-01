namespace TestGenerator.Core.CodeGenerators.Models;

public record ClassInfo(
    string Name,
    string Namespace,
    string FilePath,
    List<MethodInfo> Methods,
    List<DependencyInfo> Dependencies,
    List<string> Usings
);