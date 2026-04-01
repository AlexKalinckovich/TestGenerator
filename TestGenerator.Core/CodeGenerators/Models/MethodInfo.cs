namespace TestGenerator.Core.CodeGenerators.Models;

public record MethodInfo(
    string Name,
    string ReturnType,
    List<ParameterInfo> Parameters,
    bool IsStatic,
    bool IsAsync
);