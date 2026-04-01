using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestGenerator.Core.CodeGenerators.Builders;
using TestGenerator.Core.CodeGenerators.Models;

namespace TestGenerator.Test
{
    [TestFixture]
    public class MockSetupBuilderTests
    {
        private static string Normalize(string code)
        {
            return SyntaxFactory.ParseCompilationUnit(code).NormalizeWhitespace().ToFullString();
        }

        private static string GetStatementText(StatementSyntax statement)
        {
            return Normalize(statement.ToFullString());
        }

        [Test]
        public void AddSetup_ForVoidMethod_GeneratesSetupWithNoReturns()
        {
            var dependency = new DependencyInfo(
                InterfaceName: "IService",
                FieldName: "_service",
                Methods: new List<MethodSignature>()
            );
            var method = new MethodSignature("DoWork", "void", new List<ParameterInfo>());

            var builder = new MockSetupBuilder();
            builder.AddSetup(dependency, method);
            var statements = builder.Build();

            Assert.That(statements.Count, Is.EqualTo(1));
            string actual = GetStatementText(statements[0]);
            string expected = Normalize("_service.Setup(x => x.DoWork());");
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddSetup_ForNonVoidMethod_GeneratesSetupWithReturnsDefault()
        {
            DependencyInfo dependency = new DependencyInfo(
                InterfaceName: "IService",
                FieldName: "_service",
                Methods: new List<MethodSignature>()
            );
            var method = new MethodSignature("GetValue", "int", new List<ParameterInfo>());

            var builder = new MockSetupBuilder();
            builder.AddSetup(dependency, method);
            var statements = builder.Build();

            Assert.That(statements.Count, Is.EqualTo(1));
            string actual = GetStatementText(statements[0]);
            string expected = Normalize("_service.Setup(x => x.GetValue()).Returns(0);");
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddSetup_ForMethodWithParameters_GeneratesSetupWithParameters()
        {
            var dependency = new DependencyInfo(
                InterfaceName: "IService",
                FieldName: "_service",
                Methods: new List<MethodSignature>()
            );
            var parameters = new List<ParameterInfo>
            {
                new ParameterInfo("int", "a"),
                new ParameterInfo("string", "b")
            };
            var method = new MethodSignature("Method", "void", parameters);

            var builder = new MockSetupBuilder();
            builder.AddSetup(dependency, method);
            var statements = builder.Build();

            string actual = GetStatementText(statements[0]);
            string expected = Normalize("_service.Setup(x => x.Method(a, b));");
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddSetup_ForMethodWithNoParameters_GeneratesSetupWithEmptyArgs()
        {
            var dependency = new DependencyInfo(
                InterfaceName: "IService",
                FieldName: "_service",
                Methods: new List<MethodSignature>()
            );
            MethodSignature method = new MethodSignature("Method", "void", new List<ParameterInfo>());

            MockSetupBuilder builder = new MockSetupBuilder();
            builder.AddSetup(dependency, method);
            
            List<StatementSyntax> statements = builder.Build();

            string actual = GetStatementText(statements[0]);
            string expected = Normalize("_service.Setup(x => x.Method());");
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddSetupWithReturnValue_ForNonVoidMethod_GeneratesSetupWithReturnsCustomValue()
        {
            DependencyInfo dependency = new DependencyInfo(
                InterfaceName: "IService",
                FieldName: "_service",
                Methods: new List<MethodSignature>()
            );
            MethodSignature method = new MethodSignature("GetValue", "int", new List<ParameterInfo>());
            LiteralExpressionSyntax returnValue = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(42));

            var builder = new MockSetupBuilder();
            builder.AddSetupWithReturnValue(dependency, method, returnValue);
            
            List<StatementSyntax> statements = builder.Build();

            string actual = GetStatementText(statements[0]);
            string expected = Normalize("_service.Setup(x => x.GetValue()).Returns(42);");
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddVerify_ForMethod_GeneratesVerifyInvocation()
        {
            DependencyInfo dependency = new DependencyInfo(
                InterfaceName: "IService",
                FieldName: "_service",
                Methods: new List<MethodSignature>()
            );
            MethodSignature method = new MethodSignature("DoWork", "void", new List<ParameterInfo>());

            MockSetupBuilder builder = new MockSetupBuilder();
            builder.AddVerify(dependency, method);
            List<StatementSyntax> statements = builder.Build();

            string actual = GetStatementText(statements[0]);
            string expected = Normalize("_service.Verify(x => x.DoWork());");
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void MultipleAdds_GeneratesMultipleStatements()
        {
            DependencyInfo dependency = new DependencyInfo(
                InterfaceName: "IService",
                FieldName: "_service",
                Methods: new List<MethodSignature>()
            );
            MethodSignature method1 = new MethodSignature("Method1", "void", new List<ParameterInfo>());
            MethodSignature method2 = new MethodSignature("Method2", "int", new List<ParameterInfo>());

            MockSetupBuilder builder = new MockSetupBuilder();
            builder.AddSetup(dependency, method1);
            builder.AddSetup(dependency, method2);
            builder.AddVerify(dependency, method1);
            List<StatementSyntax> statements = builder.Build();

            Assert.That(statements, Has.Count.EqualTo(3));
            string actual1 = GetStatementText(statements[0]);
            string actual2 = GetStatementText(statements[1]);
            string actual3 = GetStatementText(statements[2]);

            Assert.That(actual1, Is.EqualTo(Normalize("_service.Setup(x => x.Method1());")));
            Assert.That(actual2, Is.EqualTo(Normalize("_service.Setup(x => x.Method2()).Returns(0);")));
            Assert.That(actual3, Is.EqualTo(Normalize("_service.Verify(x => x.Method1());")));
        }

        [Test]
        public void Build_WhenNoAdds_ReturnsEmptyList()
        {
            var builder = new MockSetupBuilder();
            var statements = builder.Build();
            Assert.That(statements, Is.Empty);
        }
    }
}