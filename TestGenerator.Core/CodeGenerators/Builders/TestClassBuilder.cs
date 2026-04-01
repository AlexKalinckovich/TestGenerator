using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestGenerator.Core.CodeGenerators.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TestGenerator.Core.CodeGenerators.Builders;

public class TestClassBuilder
{
    private readonly ClassInfo _classInfo;
    private readonly List<MethodDeclarationSyntax> _testMethods;
    private readonly List<FieldDeclarationSyntax> _fields;
    private MethodDeclarationSyntax? _setUpMethod;
    private readonly MockSetupBuilder _mockSetupBuilder;

    public TestClassBuilder(ClassInfo classInfo)
    {
        _classInfo = classInfo;
        _testMethods = new List<MethodDeclarationSyntax>();
        _fields = new List<FieldDeclarationSyntax>();
        _mockSetupBuilder = new MockSetupBuilder();
    }

    public ClassDeclarationSyntax Build()
    {
        string testClassName = BuildTestClassName();
        ClassDeclarationSyntax classDecl = CreateClassDeclaration(testClassName);
        classDecl = AddFieldsToClass(classDecl);
        classDecl = AddSetUpMethodToClass(classDecl);
        classDecl = AddTestMethodsToClass(classDecl);

        return classDecl;
    }

    private string BuildTestClassName()
    {
        return $"{_classInfo.Name}Tests";
    }

    private ClassDeclarationSyntax CreateClassDeclaration(string testClassName)
    {
        return ClassDeclaration(testClassName)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAttributeLists(AttributeList(SingletonSeparatedList(
                Attribute(IdentifierName("TestFixture"))
            )));
    }

    private ClassDeclarationSyntax AddFieldsToClass(ClassDeclarationSyntax classDecl)
    {
        if (_fields.Count == 0)
        {
            return classDecl;
        }

        IEnumerable<MemberDeclarationSyntax> fieldMembers = ConvertFieldsToMembers(_fields);
        return classDecl.AddMembers(fieldMembers.ToArray());
    }

    private IEnumerable<MemberDeclarationSyntax> ConvertFieldsToMembers(List<FieldDeclarationSyntax> fields)
    {
        return fields;
    }

    private ClassDeclarationSyntax AddSetUpMethodToClass(ClassDeclarationSyntax classDecl)
    {
        if (_setUpMethod == null)
        {
            return classDecl;
        }

        return classDecl.AddMembers(_setUpMethod);
    }

    private ClassDeclarationSyntax AddTestMethodsToClass(ClassDeclarationSyntax classDecl)
    {
        if (_testMethods.Count == 0)
        {
            return classDecl;
        }

        IEnumerable<MemberDeclarationSyntax> methodMembers = ConvertTestMethodsToMembers(_testMethods);
        
        return classDecl.AddMembers(methodMembers.ToArray());
    }

    private IEnumerable<MemberDeclarationSyntax> ConvertTestMethodsToMembers(List<MethodDeclarationSyntax> methods)
    {
        return methods;
    }

    public TestClassBuilder AddClassUnderTestField()
    {
        FieldDeclarationSyntax field = CreateClassUnderTestField();
        _fields.Add(field);
        return this;
    }

    private FieldDeclarationSyntax CreateClassUnderTestField()
    {
        return FieldDeclaration(
            VariableDeclaration(IdentifierName(_classInfo.Name))
                .AddVariables(VariableDeclarator(Identifier("_myClassUnderTest")))
        )
        .AddModifiers(Token(SyntaxKind.PrivateKeyword));
    }

    public TestClassBuilder AddMockFields()
    {
        foreach (DependencyInfo dependency in _classInfo.Dependencies)
        {
            FieldDeclarationSyntax field = CreateMockField(dependency);
            _fields.Add(field);
        }

        return this;
    }

    private FieldDeclarationSyntax CreateMockField(DependencyInfo dependency)
    {
        return FieldDeclaration(
            VariableDeclaration(
                GenericName(Identifier("Mock"))
                    .WithTypeArgumentList(TypeArgumentList(
                        SingletonSeparatedList<TypeSyntax>(IdentifierName(dependency.InterfaceName))
                    ))
            )
            .AddVariables(VariableDeclarator(Identifier(dependency.FieldName)))
        )
        .AddModifiers(Token(SyntaxKind.PrivateKeyword));
    }

    public TestClassBuilder AddSetUpMethod()
    {
        List<StatementSyntax> statements = CreateSetUpStatements();
        _setUpMethod = CreateSetUpMethodDeclaration(statements);
        return this;
    }

    private List<StatementSyntax> CreateSetUpStatements()
    {
        var statements = new List<StatementSyntax>();
        List<StatementSyntax> mockInitializations = CreateMockInitializations();
        statements.AddRange(mockInitializations);

        List<StatementSyntax> mockSetups = CreateMockSetups();
        statements.AddRange(mockSetups);

        StatementSyntax classInitialization = CreateClassInitialization();
        statements.Add(classInitialization);

        return statements;
    }

    private List<StatementSyntax> CreateMockInitializations()
    {
        var statements = new List<StatementSyntax>();

        foreach (DependencyInfo dependency in _classInfo.Dependencies)
        {
            StatementSyntax initialization = CreateMockInitialization(dependency);
            statements.Add(initialization);
        }

        return statements;
    }

    private StatementSyntax CreateMockInitialization(DependencyInfo dependency)
    {
        return ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(dependency.FieldName),
                ObjectCreationExpression(
                    GenericName(Identifier("Mock"))
                        .WithTypeArgumentList(TypeArgumentList(
                            SingletonSeparatedList<TypeSyntax>(IdentifierName(dependency.InterfaceName))
                        ))
                )
                .WithArgumentList(ArgumentList())
            )
        );
    }

    private List<StatementSyntax> CreateMockSetups()
    {
        foreach (DependencyInfo dependency in _classInfo.Dependencies)
        {
            AddMockSetupsForDependency(dependency);
        }

        return _mockSetupBuilder.Build();
    }

    private void AddMockSetupsForDependency(DependencyInfo dependency)
    {
        foreach (MethodSignature method in dependency.Methods)
        {
            _mockSetupBuilder.AddSetup(dependency, method);
        }
    }

    private StatementSyntax CreateClassInitialization()
    {
        ArgumentSyntax[] constructorArgs = BuildConstructorArguments();

        return ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("_myClassUnderTest"),
                ObjectCreationExpression(IdentifierName(_classInfo.Name))
                    .WithArgumentList(ArgumentList(SeparatedList(constructorArgs)))
            )
        );
    }

    private ArgumentSyntax[] BuildConstructorArguments()
    {
        if (_classInfo.Dependencies.Count == 0)
        {
            return [];
        }

        return _classInfo.Dependencies
            .Select(CreateConstructorArgument)
            .ToArray();
    }

    private ArgumentSyntax CreateConstructorArgument(DependencyInfo dependency)
    {
        return Argument(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(dependency.FieldName),
                IdentifierName("Object")
            )
        );
    }

    private static MethodDeclarationSyntax CreateSetUpMethodDeclaration(List<StatementSyntax> statements)
    {
        return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "SetUp")
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAttributeLists(AttributeList(SingletonSeparatedList(
                Attribute(IdentifierName("SetUp"))
            )))
            .WithBody(Block(statements));
    }

    public TestClassBuilder AddTestMethods(TestMethodBuilder testMethodBuilder)
    {
        foreach (MethodInfo method in _classInfo.Methods)
        {
            MethodDeclarationSyntax testMethod = BuildTestMethod(testMethodBuilder, method);
            _testMethods.Add(testMethod);
        }

        return this;
    }

    private MethodDeclarationSyntax BuildTestMethod(TestMethodBuilder testMethodBuilder, MethodInfo method)
    {
        return testMethodBuilder.Build(method, _classInfo.Dependencies);
    }
}