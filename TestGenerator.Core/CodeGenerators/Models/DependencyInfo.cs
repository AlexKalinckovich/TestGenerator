
namespace TestGenerator.Core.CodeGenerators.Models;

public record DependencyInfo(
    string InterfaceName,
    string FieldName,
    List<MethodSignature> Methods
);

public record MethodSignature(
    string Name,
    string ReturnType,
    List<ParameterInfo> Parameters
);