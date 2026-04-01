using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestGenerator.Core.CodeGenerators.Analyzer;
using TestGenerator.Core.CodeGenerators.Builders;
using TestGenerator.Core.CodeGenerators.Models;

namespace TestGenerator.Core.CodeGenerators.Orchestrator;

public class TestGeneratorOrchestrator
{
    private readonly TestMethodBuilder _methodBuilder;

    public TestGeneratorOrchestrator()
    {
        _methodBuilder = new TestMethodBuilder();
    }
    
    public void Generate(string sourceFilePath, string outputFilePath)
    {
            
        CompilationUnitSyntax compilationUnit = GetCompilationUnitSyntax(sourceFilePath);


        string? directory = Path.GetDirectoryName(outputFilePath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputFilePath, compilationUnit.ToFullString());
    }

    public string GenerateToString(string sourceFilePath)
    {
        CompilationUnitSyntax compilationUnit = GetCompilationUnitSyntax(sourceFilePath);
        
        return compilationUnit.ToFullString();
    }
    
    private CompilationUnitSyntax GetCompilationUnitSyntax(string sourceFilePath)
    {
        var classInfo = ClassAnalyzer.Analyze(sourceFilePath);

            
        NamespaceBuilder namespaceBuilder = new NamespaceBuilder(classInfo.Namespace)
            .WithTestsSuffix(true)
            .AddUsing("System")
            .AddUsing("System.Collections.Generic")
            .AddUsing("System.Linq")
            .AddUsing("System.Text")
            .AddUsing("NUnit.Framework")
            .AddUsing("Moq")
            .AddUsing(classInfo.Namespace);

            
        TestClassBuilder classBuilder = new TestClassBuilder(classInfo)
            .AddClassUnderTestField()
            .AddMockFields()
            .AddSetUpMethod()
            .AddTestMethods(_methodBuilder);

            
        ClassDeclarationSyntax testClass = classBuilder.Build();
            
            
        NamespaceDeclarationSyntax namespaceDecl = namespaceBuilder.AddMember(testClass).Build();
            
        CompilationUnitSyntax compilationUnit = Microsoft.CodeAnalysis.CSharp.SyntaxFactory
            .CompilationUnit()
            .AddMembers(namespaceDecl)
            .NormalizeWhitespace();
        return compilationUnit;
    }
}