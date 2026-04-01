using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TestGenerator.Core.CodeGenerators.Builders;

internal class NamespaceBuilder
{
    private static readonly Regex ValidNamespaceRegex = new(
        @"^[a-zA-Z]+(\.[a-zA-Z]+)*$",
        RegexOptions.Compiled
    );

    private static readonly TextInfo TextInfo = CultureInfo.CurrentCulture.TextInfo;

    private readonly string _namespaceName;
    private readonly bool _ensureTestsSuffix;
    private readonly List<MemberDeclarationSyntax> _members;
    private readonly List<UsingDirectiveSyntax> _usings;

    public NamespaceBuilder(string namespaceName)
    {
        ValidateNamespaceName(namespaceName);
        _namespaceName = namespaceName;
        _ensureTestsSuffix = true;
        _members = new List<MemberDeclarationSyntax>();
        _usings = new List<UsingDirectiveSyntax>();
    }
    
    public NamespaceBuilder WithTestsSuffix(bool ensureTestsSuffix = true)
    {
        return new NamespaceBuilder(_namespaceName, ensureTestsSuffix, _members, _usings);
    }

    public NamespaceBuilder AddMember(MemberDeclarationSyntax member)
    {
        _members.Add(member);
        
        return this;
    }

    public NamespaceBuilder AddMembers(IEnumerable<MemberDeclarationSyntax> members)
    {
        _members.AddRange(members);
        return this;
    }
    
    public NamespaceBuilder AddUsing(string namespaceName)
    {
        ValidateUsingNamespace(namespaceName);
        
        _usings.Add(CreateUsingDirective(namespaceName));
        
        return this;
    }
    
    public NamespaceBuilder AddUsings(IEnumerable<string> namespaces)
    {
        foreach (string ns in namespaces)
        {
            AddUsing(ns);
        }

        return this;
    }

    public NamespaceDeclarationSyntax Build()
    {
        string finalName = TransformNamespaceName(_namespaceName);
        
        NamespaceDeclarationSyntax namespaceDecl = CreateNamespaceDeclaration(finalName);
        
        namespaceDecl = AddUsingsToNamespace(namespaceDecl);
        
        namespaceDecl = AddMembersToNamespace(namespaceDecl);

        return namespaceDecl.NormalizeWhitespace();
    }
    
    private static void ValidateNamespaceName(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace name cannot be null or empty.", nameof(namespaceName));
        }

        if (!ValidNamespaceRegex.IsMatch(namespaceName))
        {
            throw new ArgumentException("Namespace name is not valid.", nameof(namespaceName));
        }
    }
    private static void ValidateUsingNamespace(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Using namespace cannot be null or empty.", nameof(namespaceName));
        }
    }

    private static UsingDirectiveSyntax CreateUsingDirective(string namespaceName)
    {
        return UsingDirective(IdentifierName(namespaceName));
    }
    private string TransformNamespaceName(string name)
    {
        string result = BuildTransformedNamespaceName(name);
        
        result = ApplyTestsSuffixIfNeeded(result);

        return result;
    }

    private static string BuildTransformedNamespaceName(string name)
    {
        IEnumerable<string> convertedToTitleCase = name.Split('.')
            .Select((string segment) => TextInfo.ToTitleCase(segment.ToLowerInvariant()));
        return string.Join(".", convertedToTitleCase);
    }

    private static NamespaceDeclarationSyntax CreateNamespaceDeclaration(string finalName)
    {
        return NamespaceDeclaration(IdentifierName(finalName));
    }
    
    private string ApplyTestsSuffixIfNeeded(string result)
    {
        if (_ensureTestsSuffix && !result.EndsWith(".Tests", StringComparison.Ordinal))
        {
            result += ".Tests";
        }

        return result;
    }
    private NamespaceBuilder(string namespaceName, bool ensureTestsSuffix, List<MemberDeclarationSyntax> members, List<UsingDirectiveSyntax> usings)
    {
        _namespaceName = namespaceName;
        _ensureTestsSuffix = ensureTestsSuffix;
        _members = members;
        _usings = usings;
    }

    private NamespaceDeclarationSyntax AddUsingsToNamespace(NamespaceDeclarationSyntax namespaceDecl)
    {
        return _usings.Count != 0 ? 
            namespaceDecl.AddUsings(_usings.ToArray()) : 
            namespaceDecl;
    }

    private NamespaceDeclarationSyntax AddMembersToNamespace(NamespaceDeclarationSyntax namespaceDecl)
    {
        return _members.Count != 0 ? 
            namespaceDecl.AddMembers(_members.ToArray()) : 
            namespaceDecl;
    }
}