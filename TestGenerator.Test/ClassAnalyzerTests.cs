using TestGenerator.Core.CodeGenerators.Analyzer;
using TestGenerator.Core.CodeGenerators.Models;

namespace TestGenerator.Test
{
    [TestFixture]
    public class ClassAnalyzerTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        private string CreateFile(string content)
        {
            string path = Path.Combine(_tempDirectory, Guid.NewGuid() + ".cs");
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void Analyze_SimpleClass_NoMethodsNoDependencies()
        {
            string code = """

                          public class SimpleClass
                          {
                          }
                          """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Name, Is.EqualTo("SimpleClass"));
            Assert.That(result.Namespace, Is.EqualTo("SimpleClass.Tests"));
            Assert.That(result.FilePath, Is.EqualTo(filePath));
            Assert.That(result.Methods, Is.Empty);
            Assert.That(result.Dependencies, Is.Empty);
            Assert.That(result.Usings, Is.Empty);
        }

        [Test]
        public void Analyze_ClassWithPublicMethods_ExtractsMethods()
        {
            string code = """

                          public class TestClass
                          {
                              public void Method1() { }
                              public int Method2(string s) { return 0; }
                              private void PrivateMethod() { }
                          }
                          """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Methods.Count, Is.EqualTo(2));
            var method1 = result.Methods.FirstOrDefault(m => m.Name == "Method1");
            Assert.That(method1, Is.Not.Null);
            Assert.That(method1.ReturnType, Is.EqualTo("void"));
            Assert.That(method1.Parameters, Is.Empty);
            Assert.That(method1.IsStatic, Is.False);
            Assert.That(method1.IsAsync, Is.False);

            var method2 = result.Methods.FirstOrDefault(m => m.Name == "Method2");
            Assert.That(method2, Is.Not.Null);
            Assert.That(method2.ReturnType, Is.EqualTo("int"));
            Assert.That(method2.Parameters.Count, Is.EqualTo(1));
            Assert.That(method2.Parameters[0].Type, Is.EqualTo("string"));
            Assert.That(method2.Parameters[0].Name, Is.EqualTo("s"));
        }

        [Test]
        public void Analyze_ClassWithConstructorDependencies_ExtractsDependencies()
        {
            string code = """

                          public interface IService
                          {
                              void DoWork();
                          }
                          public class TestClass
                          {
                              private readonly IService _service;
                              public TestClass(IService service)
                              {
                                  _service = service;
                              }
                          }
                          """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Dependencies.Count, Is.EqualTo(1));
            var dep = result.Dependencies[0];
            Assert.Multiple(() =>
            {
                Assert.That(dep.InterfaceName, Is.EqualTo("IService"));
                Assert.That(dep.FieldName, Is.EqualTo("_service"));
                Assert.That(dep.Methods, Has.Count.EqualTo(1));
            });
            Assert.Multiple(() =>
            {
                Assert.That(dep.Methods[0].Name, Is.EqualTo("DoWork"));
                Assert.That(dep.Methods[0].ReturnType, Is.EqualTo("void"));
                Assert.That(dep.Methods[0].Parameters, Is.Empty);
            });
        }

        [Test]
        public void Analyze_ClassWithNonInterfaceConstructorParams_Ignores()
        {
            const string code = """

                                public class TestClass
                                {
                                    public TestClass(string name, int count) { }
                                }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Dependencies, Is.Empty);
        }

        [Test]
        public void Analyze_ClassWithNoPublicConstructor_NoDependencies()
        {
            const string code = """

                                public class TestClass
                                {
                                    private TestClass() { }
                                    public void Method() { }
                                }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Dependencies, Is.Empty);
        }

        [Test]
        public void Analyze_ClassWithUsingStatements_ExtractsUsings()
        {
            const string code = """

                                using System;
                                using System.Collections.Generic;
                                public class TestClass { }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Usings, Is.EquivalentTo(new[] { "System", "System.Collections.Generic" }));
        }

        [Test]
        public void Analyze_ClassWithInterfaceDefinedInSameFile_ExtractsInterfaceMethods()
        {
            const string code = """

                                public interface ICalculator
                                {
                                    int Add(int a, int b);
                                    void Clear();
                                }
                                public class TestClass
                                {
                                    public TestClass(ICalculator calc) { }
                                }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Dependencies, Has.Count.EqualTo(1));
            DependencyInfo dep = result.Dependencies[0];
            Assert.That(dep.Methods, Has.Count.EqualTo(2));
            MethodSignature? add = dep.Methods.FirstOrDefault(m => m.Name == "Add");
            Assert.That(add, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(add.ReturnType, Is.EqualTo("int"));
                Assert.That(add.Parameters, Has.Count.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(add.Parameters[0].Type, Is.EqualTo("int"));
                Assert.That(add.Parameters[0].Name, Is.EqualTo("a"));
            });
            MethodSignature? clear = dep.Methods.FirstOrDefault(m => m.Name == "Clear");
            Assert.That(clear, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(clear.ReturnType, Is.EqualTo("void"));
                Assert.That(clear.Parameters, Is.Empty);
            });
        }

        [Test]
        public void Analyze_ClassWithInterfaceNotDefinedInSameFile_NoInterfaceMethods()
        {
            const string code = """

                                public interface IExternalService
                                {
                                    void DoSomething();
                                }
                                public class TestClass
                                {
                                    public TestClass(IExternalService service) { }
                                }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Dependencies, Has.Count.EqualTo(1));
            Assert.That(result.Dependencies[0].Methods, Has.Count.EqualTo(1));
            
            const string codeWithoutInterface = """

                                                public class TestClass
                                                {
                                                    public TestClass(IExternalService service) { }
                                                }
                                                """;
            string filePath2 = CreateFile(codeWithoutInterface);
            ClassInfo result2 = ClassAnalyzer.Analyze(filePath2);
            
            Assert.That(result2.Dependencies, Has.Count.EqualTo(1));
            Assert.That(result2.Dependencies[0].Methods, Is.Empty);
        }

        [Test]
        public void Analyze_ClassWithStaticMethod_IsStaticTrue()
        {
            const string code = """

                                public class TestClass
                                {
                                    public static void StaticMethod() { }
                                }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            MethodInfo method = result.Methods.First();
            Assert.That(method.IsStatic, Is.True);
        }

        [Test]
        public void Analyze_ClassWithAsyncMethod_IsAsyncTrue()
        {
            const string code = """

                                using System.Threading.Tasks;
                                public class TestClass
                                {
                                    public async Task AsyncMethod() { await Task.CompletedTask; }
                                }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            MethodInfo method = result.Methods.First();
            Assert.That(method.IsAsync, Is.True);
        }

        [Test]
        public void Analyze_ClassWithMethodsWithParameters_ExtractsParameters()
        {
            const string code = """

                                public class TestClass
                                {
                                    public void MethodWithParams(int x, string y, bool z) { }
                                }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            MethodInfo method = result.Methods.First();
            Assert.That(method.Parameters, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(method.Parameters[0].Type, Is.EqualTo("int"));
                Assert.That(method.Parameters[0].Name, Is.EqualTo("x"));
                Assert.That(method.Parameters[1].Type, Is.EqualTo("string"));
                Assert.That(method.Parameters[1].Name, Is.EqualTo("y"));
                Assert.That(method.Parameters[2].Type, Is.EqualTo("bool"));
                Assert.That(method.Parameters[2].Name, Is.EqualTo("z"));
            });
        }

        [Test]
        public void Analyze_ClassWithMultipleMethods_AllExtracted()
        {
            const string code = """

                                public class TestClass
                                {
                                    public void MethodA() { }
                                    public int MethodB() { return 0; }
                                    public string MethodC(string s) { return s; }
                                }
                                """;
            string filePath = CreateFile(code);
            ClassInfo result = ClassAnalyzer.Analyze(filePath);

            Assert.That(result.Methods, Has.Count.EqualTo(3));
            Assert.That(result.Methods.Select(m => m.Name), Is.EquivalentTo(new[] { "MethodA", "MethodB", "MethodC" }));
        }
    }
}