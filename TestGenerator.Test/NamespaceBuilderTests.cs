using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestGenerator.Core.CodeGenerators.Builders;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TestGenerator.Test;

[TestFixture]
public class NamespaceBuilderTests
{
    private static string Normalize(string code)
    {
        return ParseCompilationUnit(code).NormalizeWhitespace().ToFullString();
    }

    [Test]
    public void Constructor_WithValidNamespace_CreatesBuilder()
    {
        var builder = new NamespaceBuilder("Mynamespace");
        Assert.That(builder, Is.Not.Null);
    }

    [Test]
    public void Constructor_WithNullNamespace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new NamespaceBuilder(null));
    }

    [Test]
    public void Constructor_WithEmptyNamespace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new NamespaceBuilder(""));
    }

    [Test]
    public void Constructor_WithInvalidNamespaceFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new NamespaceBuilder("Invalid!Name"));
        Assert.Throws<ArgumentException>(() => new NamespaceBuilder("1Invalid"));
    }

    [Test]
    public void Build_WithoutMembersOrUsings_ReturnsValidNamespace()
    {
        var builder = new NamespaceBuilder("Mynamespace");
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("namespace Mynamespace.Tests\r\n{\r\n}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Build_WithTestsSuffixEnabled_AddsSuffix()
    {
        var builder = new NamespaceBuilder("Mynamespace").WithTestsSuffix(true);
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("namespace Mynamespace.Tests\r\n{\r\n}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Build_WithTestsSuffixDisabled_DoesNotAddSuffix()
    {
        var builder = new NamespaceBuilder("Mynamespace").WithTestsSuffix(false);
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("namespace Mynamespace\r\n{\r\n}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Build_WhenNamespaceAlreadyHasTestsSuffix_DoesNotAddDuplicate()
    {
        var builder = new NamespaceBuilder("Mynamespace.Tests");
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("namespace Mynamespace.Tests\r\n{\r\n}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Build_WithMultipleSegments_PreservesOriginalCase()
    {
        var builder = new NamespaceBuilder("Myapp.data.models");
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("namespace Myapp.Data.Models.Tests\r\n{\r\n}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Build_WithMixedCaseSegments_PreservesOriginalCase()
    {
        var builder = new NamespaceBuilder("Myapp.Data.Models");
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("namespace Myapp.Data.Models.Tests\r\n{\r\n}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void AddUsing_AddsUsingDirective()
    {
        var builder = new NamespaceBuilder("Mynamespace")
            .AddUsing("System.Collections.Generic");
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("""
                                    namespace Mynamespace.Tests
                                    {
                                        using System.Collections.Generic;
                                    }
                                    """);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void AddUsing_WithMultipleCalls_AddsAll()
    {
        var builder = new NamespaceBuilder("Mynamespace")
            .AddUsing("System")
            .AddUsing("System.Linq");
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("""
                                    namespace Mynamespace.Tests
                                    {
                                        using System;
                                        using System.Linq;
                                    }
                                    """);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void AddUsings_AddsMultipleUsings()
    {
        var usings = new[] { "System", "System.Collections.Generic", "System.Linq" };
        var builder = new NamespaceBuilder("Mynamespace")
            .AddUsings(usings);
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("""
                                    namespace Mynamespace.Tests
                                    {
                                        using System;
                                        using System.Collections.Generic;
                                        using System.Linq;
                                    }
                                    """);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void AddUsing_WithEmptyNamespace_ThrowsArgumentException()
    {
        var builder = new NamespaceBuilder("Mynamespace");
        Assert.Throws<ArgumentException>(() => builder.AddUsing(""));
    }

    [Test]
    public void AddUsing_WithNullNamespace_ThrowsArgumentException()
    {
        var builder = new NamespaceBuilder("Mynamespace");
        Assert.Throws<ArgumentException>(() => builder.AddUsing(null));
    }

    [Test]
    public void AddMember_AddsMemberToNamespace()
    {
        var builder = new NamespaceBuilder("Mynamespace");
        var classDecl = ClassDeclaration("TestClass").NormalizeWhitespace();
        builder.AddMember(classDecl);
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("""
                                    namespace Mynamespace.Tests
                                    {
                                        class TestClass { }
                                    }
                                    """);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void AddMembers_AddsMultipleMembers()
    {
        var builder = new NamespaceBuilder("Mynamespace");
        var class1 = ClassDeclaration("Class1").NormalizeWhitespace();
        var class2 = ClassDeclaration("Class2").NormalizeWhitespace();
        builder.AddMembers(new[] { class1, class2 });
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("""

                                    namespace Mynamespace.Tests
                                    {
                                        class Class1 { }
                                        class Class2 { }
                                    }
                                    """);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void AddMembers_WithEmptyList_DoesNothing()
    {
        var builder = new NamespaceBuilder("Mynamespace");
        builder.AddMembers(Enumerable.Empty<MemberDeclarationSyntax>());
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("namespace Mynamespace.Tests\r\n{\r\n}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Build_AfterWithTestsSuffix_RetainsOriginalMembersAndUsings()
    {
        var builder = new NamespaceBuilder("Mynamespace")
            .AddUsing("System")
            .AddMember(ClassDeclaration("TestClass").NormalizeWhitespace())
            .WithTestsSuffix(false);
        var result = builder.Build();

        string actual = Normalize(result.ToFullString());
        string expected = Normalize("""
                                    namespace Mynamespace
                                    {
                                        using System;
                                        class TestClass { }
                                    }
                                    """);
        Assert.That(actual, Is.EqualTo(expected));
    }
}