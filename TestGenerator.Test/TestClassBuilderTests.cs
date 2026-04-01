using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestGenerator.Core.CodeGenerators.Builders;
using TestGenerator.Core.CodeGenerators.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TestGenerator.Test
{
    [TestFixture]
    public class TestClassBuilderTests
    {
        private static string Normalize(ClassDeclarationSyntax classDecl)
        {
            var compilationUnit = CompilationUnit()
                .AddMembers(classDecl)
                .NormalizeWhitespace();
            return compilationUnit.ToFullString().TrimStart();
        }

        private static ClassInfo CreateClassInfoWithMethodsAndDependencies()
        {
            var methods = new List<MethodInfo>
            {
                new MethodInfo(
                    Name: "Calculate",
                    ReturnType: "int",
                    Parameters: new List<ParameterInfo>
                    {
                        new ParameterInfo("int", "a"),
                        new ParameterInfo("int", "b")
                    },
                    IsStatic: false,
                    IsAsync: false)
            };

            var dependencies = new List<DependencyInfo>
            {
                new DependencyInfo(
                    InterfaceName: "ICalculator",
                    FieldName: "_calculator",
                    Methods: new List<MethodSignature>
                    {
                        new MethodSignature("Add", "int", new List<ParameterInfo>
                        {
                            new ParameterInfo("int", "x"),
                            new ParameterInfo("int", "y")
                        }),
                        new MethodSignature("Clear", "void", new List<ParameterInfo>())
                    })
            };

            return new ClassInfo(
                Name: "Calculator",
                Namespace: "MyApp",
                FilePath: "path.cs",
                Methods: methods,
                Dependencies: dependencies,
                Usings: new List<string>());
        }

        private static ClassInfo CreateClassInfoWithVoidMethod()
        {
            var methods = new List<MethodInfo>
            {
                new MethodInfo(
                    Name: "Process",
                    ReturnType: "void",
                    Parameters: new List<ParameterInfo>
                    {
                        new ParameterInfo("string", "input")
                    },
                    IsStatic: false,
                    IsAsync: false)
            };

            return new ClassInfo(
                Name: "Processor",
                Namespace: "MyApp",
                FilePath: "path.cs",
                Methods: methods,
                Dependencies: new List<DependencyInfo>(),
                Usings: new List<string>());
        }

        private static ClassInfo CreateClassInfoWithNoMethods()
        {
            return new ClassInfo(
                Name: "EmptyClass",
                Namespace: "MyApp",
                FilePath: "path.cs",
                Methods: new List<MethodInfo>(),
                Dependencies: new List<DependencyInfo>(),
                Usings: new List<string>());
        }

        [Test]
        public void Constructor_WithClassInfo_InitializesBuilder()
        {
            var classInfo = CreateClassInfoWithMethodsAndDependencies();
            var builder = new TestClassBuilder(classInfo);
            Assert.That(builder, Is.Not.Null);
        }

        [Test]
        public void Build_WithNoAdditionalSetup_ReturnsClassWithTestFixtureAttribute()
        {
            var classInfo = CreateClassInfoWithNoMethods();
            var builder = new TestClassBuilder(classInfo);
            var classDecl = builder.Build();

            string actual = Normalize(classDecl);
            string expected = """
                              [TestFixture]
                              public class EmptyClassTests
                              {
                              }
                              """;
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddClassUnderTestField_AddsPrivateField()
        {
            var classInfo = CreateClassInfoWithNoMethods();
            var builder = new TestClassBuilder(classInfo);
            builder.AddClassUnderTestField();
            var classDecl = builder.Build();

            string actual = Normalize(classDecl);
            string expected = """
                              [TestFixture]
                              public class EmptyClassTests
                              {
                                  private EmptyClass _myClassUnderTest;
                              }
                              """;
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddMockFields_WithDependencies_AddsMockFields()
        {
            var classInfo = CreateClassInfoWithMethodsAndDependencies();
            var builder = new TestClassBuilder(classInfo);
            builder.AddMockFields();
            var classDecl = builder.Build();

            string actual = Normalize(classDecl);
            string expected = """
                              [TestFixture]
                              public class CalculatorTests
                              {
                                  private Mock<ICalculator> _calculator;
                              }
                              """;
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddSetUpMethod_WithDependencies_GeneratesSetUpMethod()
        {
            var classInfo = CreateClassInfoWithMethodsAndDependencies();
            var builder = new TestClassBuilder(classInfo);
            builder.AddMockFields();
            builder.AddSetUpMethod();
            var classDecl = builder.Build();

            string actual = Normalize(classDecl);
            string expected = """
                              [TestFixture]
                              public class CalculatorTests
                              {
                                  private Mock<ICalculator> _calculator;
                                  [SetUp]
                                  public void SetUp()
                                  {
                                      _calculator = new Mock<ICalculator>();
                                      _calculator.Setup(x => x.Add(x, y)).Returns(0);
                                      _calculator.Setup(x => x.Clear());
                                      _myClassUnderTest = new Calculator(_calculator.Object);
                                  }
                              }
                              """;
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddSetUpMethod_WithClassUnderTestField_IncludesClassInstantiation()
        {
            var classInfo = CreateClassInfoWithVoidMethod();
            var builder = new TestClassBuilder(classInfo);
            builder.AddClassUnderTestField();
            builder.AddSetUpMethod();
            var classDecl = builder.Build();

            string actual = Normalize(classDecl);
            string expected = """
                              [TestFixture]
                              public class ProcessorTests
                              {
                                  private Processor _myClassUnderTest;
                                  [SetUp]
                                  public void SetUp()
                                  {
                                      _myClassUnderTest = new Processor();
                                  }
                              }
                              """;
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AddTestMethods_WithTestMethodBuilder_AddsTestMethods()
        {
            var classInfo = CreateClassInfoWithMethodsAndDependencies();
            var testMethodBuilder = new TestMethodBuilder();
            var builder = new TestClassBuilder(classInfo);
            builder.AddTestMethods(testMethodBuilder);
            var classDecl = builder.Build();

            string actual = Normalize(classDecl);
            string expected = """
                              [TestFixture]
                              public class CalculatorTests
                              {
                                  [Test]
                                  public void CalculateTest()
                                  {
                                      int a = 0;
                                      int b = 0;
                                      _calculator.Setup(x => x.Add(x, y)).Returns(0);
                                      _calculator.Setup(x => x.Clear());
                                      int actual = _myClassUnderTest.Calculate(a, b);
                                      int expected = 0;
                                      Assert.That(actual, Is.EqualTo(expected));
                                      _calculator.Verify(x => x.Add(x, y));
                                      _calculator.Verify(x => x.Clear());
                                      Assert.Fail("autogenerated");
                                  }
                              }
                              """;
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void FullBuild_WithAllFeatures_GeneratesCompleteTestClass()
        {
            var classInfo = CreateClassInfoWithMethodsAndDependencies();
            var testMethodBuilder = new TestMethodBuilder();
            var builder = new TestClassBuilder(classInfo);
            builder.AddClassUnderTestField();
            builder.AddMockFields();
            builder.AddSetUpMethod();
            builder.AddTestMethods(testMethodBuilder);
            var classDecl = builder.Build();

            string actual = Normalize(classDecl);
            string expected = """
                              [TestFixture]
                              public class CalculatorTests
                              {
                                  private Calculator _myClassUnderTest;
                                  private Mock<ICalculator> _calculator;
                                  [SetUp]
                                  public void SetUp()
                                  {
                                      _calculator = new Mock<ICalculator>();
                                      _calculator.Setup(x => x.Add(x, y)).Returns(0);
                                      _calculator.Setup(x => x.Clear());
                                      _myClassUnderTest = new Calculator(_calculator.Object);
                                  }
                              
                                  [Test]
                                  public void CalculateTest()
                                  {
                                      int a = 0;
                                      int b = 0;
                                      _calculator.Setup(x => x.Add(x, y)).Returns(0);
                                      _calculator.Setup(x => x.Clear());
                                      int actual = _myClassUnderTest.Calculate(a, b);
                                      int expected = 0;
                                      Assert.That(actual, Is.EqualTo(expected));
                                      _calculator.Verify(x => x.Add(x, y));
                                      _calculator.Verify(x => x.Clear());
                                      Assert.Fail("autogenerated");
                                  }
                              }
                              """;
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}