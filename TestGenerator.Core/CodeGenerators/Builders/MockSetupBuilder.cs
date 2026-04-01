using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestGenerator.Core.CodeGenerators.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TestGenerator.Core.CodeGenerators.Builders;

internal class MockSetupBuilder
{
    private readonly List<StatementSyntax> _setupStatements;

    public MockSetupBuilder()
    {
        _setupStatements = new List<StatementSyntax>();
    }

    public MockSetupBuilder AddSetup(DependencyInfo dependency, MethodSignature method)
    {
        InvocationExpressionSyntax setupInvocation = BuildSetupInvocation(dependency, method);

        if (method.ReturnType != "void")
        {
            ExpressionSyntax defaultValue = GetDefaultValue(method.ReturnType);
            setupInvocation = WrapWithReturns(setupInvocation, defaultValue);
        }

        StatementSyntax statement = CreateExpressionStatement(setupInvocation);
        _setupStatements.Add(statement);

        return this;
    }

    public MockSetupBuilder AddSetupWithReturnValue(DependencyInfo dependency, MethodSignature method, ExpressionSyntax returnValue)
    {
        InvocationExpressionSyntax setupInvocation = BuildSetupInvocation(dependency, method);
        InvocationExpressionSyntax wrappedInvocation = WrapWithReturns(setupInvocation, returnValue);
        StatementSyntax statement = CreateExpressionStatement(wrappedInvocation);
        _setupStatements.Add(statement);

        return this;
    }

    public MockSetupBuilder AddVerify(DependencyInfo dependency, MethodSignature method)
    {
        InvocationExpressionSyntax verifyInvocation = BuildVerifyInvocation(dependency, method);
        StatementSyntax statement = CreateExpressionStatement(verifyInvocation);
        _setupStatements.Add(statement);

        return this;
    }

    public List<StatementSyntax> Build()
    {
        return _setupStatements.ToList();
    }

    private InvocationExpressionSyntax BuildSetupInvocation(DependencyInfo dependency, MethodSignature method)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = BuildMethodArguments(method);
        SimpleLambdaExpressionSyntax lambdaExpression = BuildLambdaExpression(method, arguments);

        return CreateInvocationExpression(dependency.FieldName, "Setup", lambdaExpression);
    }

    private InvocationExpressionSyntax BuildVerifyInvocation(DependencyInfo dependency, MethodSignature method)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = BuildMethodArguments(method);
        SimpleLambdaExpressionSyntax lambdaExpression = BuildLambdaExpression(method, arguments);

        return CreateInvocationExpression(dependency.FieldName, "Verify", lambdaExpression);
    }

    private SeparatedSyntaxList<ArgumentSyntax> BuildMethodArguments(MethodSignature method)
    {
        if (method.Parameters.Count == 0)
        {
            return SeparatedList<ArgumentSyntax>();
        }

        return CreateSeparatedArgumentList(method);
    }

    private SeparatedSyntaxList<ArgumentSyntax> CreateSeparatedArgumentList(MethodSignature method)
    {
        return SeparatedList<ArgumentSyntax>(
            method.Parameters.Select((ParameterInfo p) => Argument(IdentifierName(p.Name)))
        );
    }

    private SimpleLambdaExpressionSyntax BuildLambdaExpression(MethodSignature method, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        InvocationExpressionSyntax methodInvocation = CreateMethodInvocation(method, arguments);

        return SimpleLambdaExpression(
            Parameter(Identifier("x"))
        ).WithExpressionBody(methodInvocation);
    }

    private static InvocationExpressionSyntax CreateMethodInvocation(MethodSignature method, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("x"),
                IdentifierName(method.Name)
            )
        ).WithArgumentList(ArgumentList(arguments));
    }

    private InvocationExpressionSyntax CreateInvocationExpression(string fieldName, string methodName, ExpressionSyntax argument)
    {
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(fieldName),
                IdentifierName(methodName)
            )
        ).WithArgumentList(ArgumentList(
            SingletonSeparatedList(Argument(argument))
        ));
    }

    private InvocationExpressionSyntax WrapWithReturns(InvocationExpressionSyntax setupInvocation, ExpressionSyntax returnValue)
    {
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                setupInvocation,
                IdentifierName("Returns")
            )
        ).WithArgumentList(ArgumentList(
            SingletonSeparatedList(Argument(returnValue))
        ));
    }

    private static ExpressionStatementSyntax CreateExpressionStatement(ExpressionSyntax expression)
    {
        return ExpressionStatement(expression);
    }

    private static LiteralExpressionSyntax GetDefaultValue(string type)
    {
        return type switch
        {
            "int" => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)),
            "long" => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0L)),
            "double" => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0.0)),
            "float" => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0.0f)),
            "string" => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("")),
            "bool" => LiteralExpression(SyntaxKind.FalseLiteralExpression),
            _ => LiteralExpression(SyntaxKind.NullLiteralExpression)
        };
    }
}