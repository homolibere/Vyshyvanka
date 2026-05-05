using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Vyshyvanka.Engine.Nodes.Actions;

/// <summary>
/// Validates user-provided C# code for the Code node by walking the syntax tree
/// and rejecting dangerous patterns such as reflection, I/O, and process access.
/// This prevents sandbox escape via reflection-based attacks.
/// </summary>
public sealed class CodeNodeSyntaxValidator : CSharpSyntaxWalker
{
    private readonly List<string> _violations = [];

    /// <summary>
    /// Types that are forbidden in user scripts. These enable reflection-based sandbox escape
    /// or direct access to dangerous system capabilities.
    /// </summary>
    private static readonly HashSet<string> ForbiddenTypeNames =
    [
        // Reflection types
        "Type",
        "Assembly",
        "MethodInfo",
        "PropertyInfo",
        "FieldInfo",
        "ConstructorInfo",
        "MemberInfo",
        "EventInfo",
        "ParameterInfo",
        "Module",
        "TypeInfo",
        "CustomAttributeData",
        "Delegate",
        "MulticastDelegate",
        "BindingFlags",

        // Activation / dynamic loading
        "Activator",
        "AppDomain",
        "AssemblyLoadContext",

        // I/O types
        "File",
        "Directory",
        "Path",
        "FileInfo",
        "DirectoryInfo",
        "FileStream",
        "StreamReader",
        "StreamWriter",
        "DriveInfo",

        // Process / environment
        "Process",
        "ProcessStartInfo",
        "Environment",

        // Networking
        "Socket",
        "TcpClient",
        "TcpListener",
        "UdpClient",
        "HttpClient",
        "HttpMessageHandler",
        "WebClient",
        "WebRequest",
        "WebResponse",
        "HttpWebRequest",

        // Interop
        "Marshal",
        "DllImportAttribute",
        "GCHandle",

        // Unsafe code generation
        "DynamicMethod",
        "ILGenerator",
        "AssemblyBuilder",
        "ModuleBuilder",
        "TypeBuilder",
        "MethodBuilder",
        "Emit",

        // LINQ Expressions (can construct reflection calls dynamically)
        "Expression",
        "LambdaExpression",
        "MethodCallExpression",
        "MemberExpression",
        "UnaryExpression",
        "BinaryExpression",
        "ConstantExpression",
        "ParameterExpression",
        "NewExpression",
        "ExpressionVisitor"
    ];

    /// <summary>
    /// Namespace prefixes that are forbidden in member access expressions.
    /// </summary>
    private static readonly string[] ForbiddenNamespacePrefixes =
    [
        "System.Reflection",
        "System.IO",
        "System.Diagnostics",
        "System.Net",
        "System.Runtime.InteropServices",
        "System.Runtime.Loader",
        "System.Runtime.CompilerServices",
        "System.Reflection.Emit",
        "System.Security.Cryptography.X509Certificates",
        "System.Linq.Expressions"
    ];

    /// <summary>
    /// Method names that are forbidden when called on any object.
    /// </summary>
    private static readonly HashSet<string> ForbiddenMethodNames =
    [
        "GetType",
        "GetMethod",
        "GetMethods",
        "GetProperty",
        "GetProperties",
        "GetField",
        "GetFields",
        "GetMember",
        "GetMembers",
        "GetConstructor",
        "GetConstructors",
        "GetEvent",
        "GetEvents",
        "GetInterface",
        "GetInterfaces",
        "GetNestedType",
        "GetNestedTypes",
        "InvokeMember",
        "GetCustomAttribute",
        "GetCustomAttributes",
        "MakeGenericType",
        "MakeGenericMethod",
        "CreateDelegate",
        "CreateInstance",
        "DynamicInvoke",
        "LoadFrom",
        "LoadFile",
        "Load",
        "ReflectionOnlyLoad",
        "UnsafeLoadFrom",
        "GetAssembly",
        "GetCallingAssembly",
        "GetEntryAssembly",
        "GetExecutingAssembly"
    ];

    /// <summary>
    /// Property/field names that are forbidden in member access.
    /// </summary>
    private static readonly HashSet<string> ForbiddenMemberAccessNames =
    [
        "Assembly",
        "BaseType",
        "UnderlyingSystemType",
        "TypeHandle",
        "Module",
        "FullName",
        "AssemblyQualifiedName"
    ];

    /// <summary>
    /// Validates the given C# code and returns any security violations found.
    /// </summary>
    /// <param name="code">The user-provided C# code to validate.</param>
    /// <returns>A list of violation descriptions. Empty if the code is safe.</returns>
    public static IReadOnlyList<string> Validate(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script));
        var root = tree.GetRoot();

        var validator = new CodeNodeSyntaxValidator();
        validator.Visit(root);

        return validator._violations;
    }

    public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
    {
        _violations.Add(
            $"Usage of 'typeof' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
            "It can be used to access reflection APIs.");

        base.VisitTypeOfExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Check for forbidden method calls like obj.GetType(), Type.GetMethod(), etc.
        var methodName = GetInvokedMethodName(node);
        if (methodName is not null && ForbiddenMethodNames.Contains(methodName))
        {
            _violations.Add(
                $"Call to '{methodName}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                "Reflection and dynamic type loading are prohibited.");
        }

        base.VisitInvocationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var memberName = node.Name.Identifier.ValueText;

        // Check for forbidden member access like .Assembly, .BaseType, etc.
        if (ForbiddenMemberAccessNames.Contains(memberName))
        {
            _violations.Add(
                $"Access to member '{memberName}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                "Reflection-related member access is prohibited.");
        }

        // Check for method group references (e.g., var f = obj.GetType; without invocation)
        // This catches cases where a forbidden method is assigned as a delegate rather than called directly.
        if (ForbiddenMethodNames.Contains(memberName) && node.Parent is not InvocationExpressionSyntax)
        {
            _violations.Add(
                $"Reference to method '{memberName}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                "Reflection and dynamic type loading are prohibited, including method group references.");
        }

        // Check for forbidden namespace access like System.Reflection.X, System.IO.X
        var fullExpression = GetResolvedName(node);
        foreach (var ns in ForbiddenNamespacePrefixes)
        {
            if (fullExpression.StartsWith(ns, StringComparison.Ordinal))
            {
                _violations.Add(
                    $"Access to namespace '{ns}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                    "This namespace is restricted for security reasons.");
                break;
            }
        }

        base.VisitMemberAccessExpression(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var name = node.Identifier.ValueText;

        // Check for forbidden type names used as identifiers
        if (ForbiddenTypeNames.Contains(name))
        {
            // Avoid false positives: only flag if it looks like a type usage
            // (not a variable named "Path" in a lambda, etc.)
            var parent = node.Parent;
            if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
            {
                // Used as the target of a member access: e.g., File.ReadAllText(...)
                _violations.Add(
                    $"Reference to restricted type '{name}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                    "This type is blocked for security reasons.");
            }
            else if (parent is ObjectCreationExpressionSyntax
                     || parent is TypeOfExpressionSyntax
                     || parent is QualifiedNameSyntax
                     || parent is TypeArgumentListSyntax
                     || parent is VariableDeclarationSyntax
                     || parent is CastExpressionSyntax
                     || parent is BinaryExpressionSyntax
                     {
                         RawKind: (int)SyntaxKind.IsExpression or (int)SyntaxKind.AsExpression
                     })
            {
                _violations.Add(
                    $"Reference to restricted type '{name}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                    "This type is blocked for security reasons.");
            }
        }

        base.VisitIdentifierName(node);
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (ForbiddenTypeNames.Contains(name))
        {
            _violations.Add(
                $"Reference to restricted type '{name}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                "This type is blocked for security reasons.");
        }

        base.VisitGenericName(node);
    }

    public override void VisitQualifiedName(QualifiedNameSyntax node)
    {
        var fullName = GetResolvedName(node);
        foreach (var ns in ForbiddenNamespacePrefixes)
        {
            if (fullName.StartsWith(ns, StringComparison.Ordinal))
            {
                _violations.Add(
                    $"Reference to namespace '{ns}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                    "This namespace is restricted for security reasons.");
                break;
            }
        }

        base.VisitQualifiedName(node);
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var nameText = node.Name is not null ? GetResolvedName(node.Name) : null;
        if (nameText is not null)
        {
            foreach (var ns in ForbiddenNamespacePrefixes)
            {
                if (nameText.StartsWith(ns, StringComparison.Ordinal) || nameText == ns)
                {
                    _violations.Add(
                        $"Using directive for namespace '{nameText}' is not allowed (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}). " +
                        "This namespace is restricted for security reasons.");
                    break;
                }
            }
        }

        base.VisitUsingDirective(node);
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax node)
    {
        return node.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };
    }

    /// <summary>
    /// Builds the fully-resolved dotted name from a syntax node by using ValueText
    /// (which resolves Unicode escape sequences) instead of ToString() (which returns raw source).
    /// This prevents bypass via Unicode escapes like \u0047etType → GetType.
    /// </summary>
    private static string GetResolvedName(SyntaxNode node)
    {
        return node switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            QualifiedNameSyntax qualified => $"{GetResolvedName(qualified.Left)}.{GetResolvedName(qualified.Right)}",
            MemberAccessExpressionSyntax memberAccess =>
                $"{GetResolvedName(memberAccess.Expression)}.{memberAccess.Name.Identifier.ValueText}",
            AliasQualifiedNameSyntax alias => $"{alias.Alias.Identifier.ValueText}::{GetResolvedName(alias.Name)}",
            PredefinedTypeSyntax predefined => predefined.Keyword.ValueText,
            _ => node.ToString() // Fallback for unexpected node types
        };
    }
}
