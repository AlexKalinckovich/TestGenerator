using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestGenerator.Core.CodeGenerators.Models;

namespace TestGenerator.Core.CodeGenerators.Analyzer;

internal static class ClassAnalyzer
{
    public static ClassInfo Analyze(string filePath)
    {
        string sourceCode = ReadSourceCode(filePath);
        
        SyntaxTree tree = ParseSyntaxTree(sourceCode);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        ClassDeclarationSyntax classDecl = FindClassDeclaration(root);
        

        string className = ExtractClassName(classDecl);
        string namespaceName = BuildTestNamespace(className);
        
        List<string> usings = ExtractUsings(root);
        List<MethodInfo> methods = ExtractMethods(classDecl);
        List<DependencyInfo> dependencies = ExtractDependencies(classDecl, root);

        return new ClassInfo(
            Name: className,
            Namespace: namespaceName,
            FilePath: filePath,
            Methods: methods,
            Dependencies: dependencies,
            Usings: usings
        );
    }

    private static string ReadSourceCode(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    private static SyntaxTree ParseSyntaxTree(string sourceCode)
    {
        return CSharpSyntaxTree.ParseText(sourceCode);
    }

    private static ClassDeclarationSyntax FindClassDeclaration(CompilationUnitSyntax root)
    {
        ClassDeclarationSyntax? classDeclarationSyntax =  root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();
        
        ArgumentNullException.ThrowIfNull(classDeclarationSyntax);
        
        return classDeclarationSyntax;
    }
    

    private static string ExtractClassName(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Identifier.Text;
    }

    private static string BuildTestNamespace(string className)
    {
        return $"{className}.Tests";
    }

    private static List<string> ExtractUsings(CompilationUnitSyntax root)
    {
        return root.Usings
            .Select((UsingDirectiveSyntax u) => u.Name?.ToString() ?? string.Empty)
            .Where((string u) => !string.IsNullOrEmpty(u))
            .ToList();
    }

    private static List<MethodInfo> ExtractMethods(ClassDeclarationSyntax classDecl)
    {
        IEnumerable<MethodDeclarationSyntax> methodDeclarations = GetPublicMethodDeclarations(classDecl);
        return ConvertMethodsToInfo(methodDeclarations, classDecl);
    }

    private static IEnumerable<MethodDeclarationSyntax> GetPublicMethodDeclarations(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => IsPublicMethod(m) && !IsConstructor(m, classDecl));
    }

    private static bool IsPublicMethod(MethodDeclarationSyntax methodDecl)
    {
        return methodDecl.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.PublicKeyword));
    }

    private static bool IsConstructor(MethodDeclarationSyntax methodDecl, ClassDeclarationSyntax classDecl)
    {
        return methodDecl.Identifier.Text == classDecl.Identifier.Text;
    }

    private static List<MethodInfo> ConvertMethodsToInfo(IEnumerable<MethodDeclarationSyntax> methodDeclarations, ClassDeclarationSyntax classDecl)
    {
        return methodDeclarations
            .Select((MethodDeclarationSyntax m) => ConvertMethodToInfo(m, classDecl))
            .Where((MethodInfo? m) => m != null)
            .Cast<MethodInfo>()
            .ToList();
    }

    private static MethodInfo? ConvertMethodToInfo(MethodDeclarationSyntax methodDecl, ClassDeclarationSyntax classDecl)
    {
        if (IsConstructor(methodDecl, classDecl))
        {
            return null;
        }

        List<ParameterInfo> parameters = ExtractParameters(methodDecl);

        return new MethodInfo(
            Name: methodDecl.Identifier.Text,
            ReturnType: methodDecl.ReturnType.ToString(),
            Parameters: parameters,
            IsStatic: IsStaticMethod(methodDecl),
            IsAsync: IsAsyncMethod(methodDecl)
        );
    }

    private static List<ParameterInfo> ExtractParameters(MethodDeclarationSyntax methodDecl)
    {
        return methodDecl.ParameterList.Parameters
            .Select((ParameterSyntax p) => new ParameterInfo(
                Type: p.Type?.ToString() ?? "object",
                Name: p.Identifier.Text
            ))
            .ToList();
    }

    private static bool IsStaticMethod(MethodDeclarationSyntax methodDecl)
    {
        return methodDecl.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.StaticKeyword));
    }

    private static bool IsAsyncMethod(MethodDeclarationSyntax methodDecl)
    {
        return methodDecl.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.AsyncKeyword));
    }

    private static List<DependencyInfo> ExtractDependencies(ClassDeclarationSyntax classDecl, CompilationUnitSyntax root)
    {
        ConstructorDeclarationSyntax? constructor = GetPublicConstructor(classDecl);

        return constructor != null ? 
            ExtractDependenciesFromConstructor(constructor, root) : 
            new List<DependencyInfo>();
    }

    private static ConstructorDeclarationSyntax? GetPublicConstructor(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));
    }

    private static List<DependencyInfo> ExtractDependenciesFromConstructor(ConstructorDeclarationSyntax constructor, CompilationUnitSyntax root)
    {
        return constructor.ParameterList.Parameters
            .Select((ParameterSyntax p) => ProcessParameter(p, root))
            .Where((DependencyInfo? d) => d != null)
            .Cast<DependencyInfo>()
            .ToList();
    }

    private static DependencyInfo? ProcessParameter(ParameterSyntax parameter, CompilationUnitSyntax root)
    {
        string typeString = parameter.Type?.ToString() ?? string.Empty;

        return IsInterfaceType(typeString) ? 
            CreateDependencyInfo(typeString, root) : null;
    }

    private static bool IsInterfaceType(string typeString)
    {
        return typeString.StartsWith("I") && typeString.Length > 1;
    }

    private static DependencyInfo CreateDependencyInfo(string interfaceName, CompilationUnitSyntax root)
    {
        List<MethodSignature> interfaceMethods = ExtractInterfaceMethods(interfaceName, root);

        return new DependencyInfo(
            InterfaceName: interfaceName,
            FieldName: $"_{CharToLower(interfaceName.TrimStart('I'))}",
            Methods: interfaceMethods
        );
    }

    private static List<MethodSignature> ExtractInterfaceMethods(string interfaceName, CompilationUnitSyntax root)
    {
        InterfaceDeclarationSyntax? interfaceDecl = FindInterfaceDeclaration(interfaceName, root);

        return interfaceDecl != null ? 
            ConvertInterfaceMethodsToSignatures(interfaceDecl) : 
            new List<MethodSignature>();
    }

    private static InterfaceDeclarationSyntax? FindInterfaceDeclaration(string interfaceName, CompilationUnitSyntax root)
    {
        return root.DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .FirstOrDefault((InterfaceDeclarationSyntax i) => i.Identifier.Text == interfaceName);
    }

    private static List<MethodSignature> ConvertInterfaceMethodsToSignatures(InterfaceDeclarationSyntax interfaceDecl)
    {
        return interfaceDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Select(BuildMethodSignature)
            .ToList();
    }

    private static MethodSignature BuildMethodSignature(MethodDeclarationSyntax methodDecl)
    {
        List<ParameterInfo> parameters = methodDecl.ParameterList.Parameters
            .Select(p => new ParameterInfo(
                Type: p.Type?.ToString() ?? "object",
                Name: p.Identifier.Text
            ))
            .ToList();

        return new MethodSignature(
            Name: methodDecl.Identifier.Text,
            ReturnType: methodDecl.ReturnType.ToString(),
            Parameters: parameters
        );
    }

    private static string CharToLower(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }

        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}