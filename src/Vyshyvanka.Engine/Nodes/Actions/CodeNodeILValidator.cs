using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace Vyshyvanka.Engine.Nodes.Actions;

/// <summary>
/// Validates compiled C# script IL using an allowlist approach.
/// After Roslyn compiles user code, this validator inspects all type and member references
/// in the emitted assembly and rejects any that are not on the allowlist.
/// This is immune to source-level obfuscation (Unicode escapes, method groups, expression trees)
/// because it operates on the resolved compiled output.
/// </summary>
public static class CodeNodeIlValidator
{
    /// <summary>
    /// Namespaces where ALL types are permitted (except those in <see cref="BlockedTypes"/>).
    /// </summary>
    private static readonly HashSet<string> FullyAllowedNamespaces =
    [
        "System.Collections",
        "System.Collections.Generic",
        "System.Collections.Immutable",
        "System.Collections.ObjectModel",
        "System.Collections.Concurrent",
        "System.Globalization",
        "System.Linq",
        "System.Numerics",
        "System.Text",
        "System.Text.Json",
        "System.Text.Json.Nodes",
        "System.Text.Json.Serialization",
        "System.Text.Json.Serialization.Metadata",
        "System.Text.RegularExpressions",
        "System.Text.Unicode",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Diagnostics.CodeAnalysis"
    ];

    /// <summary>
    /// Namespace prefixes that are always allowed (script infrastructure, globals, etc.).
    /// </summary>
    private static readonly string[] AllowedNamespacePrefixes =
    [
        "Vyshyvanka.", // Globals type and engine types
        "Microsoft.CodeAnalysis.", // Roslyn script infrastructure
        "Microsoft.Extensions.Logging", // ILogger used in globals
        "Submission#", // Roslyn-generated script namespace
        "<global namespace>" // Top-level script code
    ];

    /// <summary>
    /// Types that the C# compiler automatically emits into every assembly.
    /// These are infrastructure attributes and are not controllable by user code.
    /// </summary>
    private static readonly HashSet<string> CompilerInfrastructureTypes =
    [
        // Assembly-level attributes emitted by Roslyn
        "System.Diagnostics.DebuggableAttribute",
        "System.Diagnostics.DebuggerHiddenAttribute",
        "System.Diagnostics.DebuggerBrowsableAttribute",
        "System.Diagnostics.DebuggerStepThroughAttribute",
        "System.Diagnostics.DebuggerDisplayAttribute",
        "System.Diagnostics.DebuggerNonUserCodeAttribute",

        // Security attributes emitted by compiler
        "System.Security.UnverifiableCodeAttribute",
        "System.Security.Permissions.SecurityAction",
        "System.Security.Permissions.SecurityPermissionAttribute",
        "System.Security.SecurityCriticalAttribute",
        "System.Security.SecuritySafeCriticalAttribute",

        // Reflection attributes emitted by compiler for assembly metadata
        "System.Reflection.AssemblyVersionAttribute",
        "System.Reflection.AssemblyCompanyAttribute",
        "System.Reflection.AssemblyConfigurationAttribute",
        "System.Reflection.AssemblyFileVersionAttribute",
        "System.Reflection.AssemblyInformationalVersionAttribute",
        "System.Reflection.AssemblyProductAttribute",
        "System.Reflection.AssemblyTitleAttribute",
        "System.Reflection.AssemblyTrademarkAttribute",
        "System.Reflection.AssemblyCopyrightAttribute",
        "System.Reflection.AssemblyDescriptionAttribute",
        "System.Reflection.AssemblyMetadataAttribute",
        "System.Reflection.DefaultMemberAttribute",

        // Runtime compiler services infrastructure
        "System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
        "System.Runtime.CompilerServices.RuntimeCompatibilityAttribute",
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
        "System.Runtime.CompilerServices.AsyncStateMachineAttribute",
        "System.Runtime.CompilerServices.AsyncMethodBuilderAttribute",
        "System.Runtime.CompilerServices.IteratorStateMachineAttribute",
        "System.Runtime.CompilerServices.IsExternalInit",
        "System.Runtime.CompilerServices.ExtensionAttribute",
        "System.Runtime.CompilerServices.TupleElementNamesAttribute",
        "System.Runtime.CompilerServices.NullableAttribute",
        "System.Runtime.CompilerServices.NullableContextAttribute",
        "System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute",
        "System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute",
        "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler",
        "System.Runtime.CompilerServices.CallerMemberNameAttribute",
        "System.Runtime.CompilerServices.CallerFilePathAttribute",
        "System.Runtime.CompilerServices.CallerLineNumberAttribute",
        "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute",
        "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
        "System.Runtime.CompilerServices.MethodImplAttribute",
        "System.Runtime.CompilerServices.MethodImplOptions",
        "System.Runtime.CompilerServices.TaskAwaiter",
        "System.Runtime.CompilerServices.TaskAwaiter`1",
        "System.Runtime.CompilerServices.ValueTaskAwaiter",
        "System.Runtime.CompilerServices.ValueTaskAwaiter`1",
        "System.Runtime.CompilerServices.ConfiguredTaskAwaitable",
        "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1",
        "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable",
        "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1",
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder",
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1",
        "System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder",
        "System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1",
        "System.Runtime.CompilerServices.IAsyncStateMachine",
        "System.Runtime.CompilerServices.ICriticalNotifyCompletion",
        "System.Runtime.CompilerServices.INotifyCompletion",
        "System.Runtime.CompilerServices.FormattableStringFactory",
        "System.Runtime.CompilerServices.SwitchExpressionException",
        "System.Runtime.CompilerServices.InlineArrayAttribute",
        "System.Runtime.CompilerServices.CollectionBuilderAttribute",
        "System.Runtime.CompilerServices.RequiredMemberAttribute",
        "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute",
        "System.Runtime.CompilerServices.SkipLocalsInitAttribute",
        "System.Runtime.CompilerServices.ModuleInitializerAttribute",

        // Runtime versioning
        "System.Runtime.Versioning.TargetFrameworkAttribute",

        // Serialization infrastructure (for records)
        "System.Runtime.Serialization.DataContractAttribute",
        "System.Runtime.Serialization.DataMemberAttribute"
    ];

    /// <summary>
    /// Types that are explicitly BLOCKED even if their namespace is otherwise allowed.
    /// These types provide dangerous capabilities (arbitrary memory access, reflection, etc.).
    /// </summary>
    private static readonly HashSet<string> BlockedTypes =
    [
        // Unsafe memory access — bypasses the entire type system
        "System.Runtime.CompilerServices.Unsafe",

        // RuntimeHelpers has GetUninitializedObject and other dangerous methods
        "System.Runtime.CompilerServices.RuntimeHelpers",

        // DependentHandle can be abused for GC manipulation
        "System.Runtime.CompilerServices.DependentHandle",

        // ConditionalWeakTable internals
        "System.Runtime.CompilerServices.ConditionalWeakTable`2",

        // Thread manipulation
        "System.Threading.Thread",
        "System.Threading.ThreadPool",
        "System.Threading.ThreadStart",
        "System.Threading.ParameterizedThreadStart",

        // Synchronization primitives that could cause deadlocks
        "System.Threading.Monitor",
        "System.Threading.Mutex",
        "System.Threading.Semaphore"
    ];

    /// <summary>
    /// Specific types allowed in the "System" namespace.
    /// Only these types from the root System namespace are permitted.
    /// </summary>
    private static readonly HashSet<string> AllowedSystemTypes =
    [
        // Primitive types and core structs
        "System.Object",
        "System.String",
        "System.Boolean",
        "System.Byte",
        "System.SByte",
        "System.Int16",
        "System.UInt16",
        "System.Int32",
        "System.UInt32",
        "System.Int64",
        "System.UInt64",
        "System.Int128",
        "System.UInt128",
        "System.Single",
        "System.Double",
        "System.Decimal",
        "System.Char",
        "System.IntPtr",
        "System.UIntPtr",
        "System.Half",
        "System.Void",
        "System.Enum",
        "System.ValueType",

        // Common structs
        "System.Guid",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.TimeSpan",
        "System.DateOnly",
        "System.TimeOnly",
        "System.Range",
        "System.Index",
        "System.Nullable`1",
        "System.Nullable",

        // Math and conversion
        "System.Math",
        "System.MathF",
        "System.Convert",
        "System.BitConverter",
        "System.Random",
        "System.HashCode",

        // String-related
        "System.StringComparison",
        "System.StringSplitOptions",
        "System.FormattableString",
        "System.IFormattable",
        "System.ISpanFormattable",

        // Enums
        "System.DayOfWeek",
        "System.DateTimeKind",
        "System.TypeCode",
        "System.MidpointRounding",
        "System.Base64FormattingOptions",
        "System.UriKind",

        // Interfaces
        "System.IDisposable",
        "System.IAsyncDisposable",
        "System.IComparable",
        "System.IComparable`1",
        "System.IEquatable`1",
        "System.IConvertible",
        "System.ICloneable",

        // Delegates
        "System.Action",
        "System.Action`1",
        "System.Action`2",
        "System.Action`3",
        "System.Action`4",
        "System.Action`5",
        "System.Action`6",
        "System.Action`7",
        "System.Action`8",
        "System.Func`1",
        "System.Func`2",
        "System.Func`3",
        "System.Func`4",
        "System.Func`5",
        "System.Func`6",
        "System.Func`7",
        "System.Func`8",
        "System.Func`9",
        "System.Predicate`1",
        "System.Comparison`1",
        "System.Converter`2",
        "System.EventHandler",
        "System.EventHandler`1",
        "System.AsyncCallback",
        "System.IAsyncResult",
        "System.MulticastDelegate",
        "System.Delegate",

        // Collections / Memory
        "System.Array",
        "System.ArraySegment`1",
        "System.Span`1",
        "System.ReadOnlySpan`1",
        "System.Memory`1",
        "System.ReadOnlyMemory`1",

        // Tuples
        "System.Tuple",
        "System.Tuple`1",
        "System.Tuple`2",
        "System.Tuple`3",
        "System.Tuple`4",
        "System.Tuple`5",
        "System.Tuple`6",
        "System.Tuple`7",
        "System.Tuple`8",
        "System.ValueTuple",
        "System.ValueTuple`1",
        "System.ValueTuple`2",
        "System.ValueTuple`3",
        "System.ValueTuple`4",
        "System.ValueTuple`5",
        "System.ValueTuple`6",
        "System.ValueTuple`7",
        "System.ValueTuple`8",
        "System.ITuple",

        // Exceptions (safe to throw/catch)
        "System.Exception",
        "System.SystemException",
        "System.InvalidOperationException",
        "System.ArgumentException",
        "System.ArgumentNullException",
        "System.ArgumentOutOfRangeException",
        "System.NotSupportedException",
        "System.NotImplementedException",
        "System.FormatException",
        "System.OverflowException",
        "System.IndexOutOfRangeException",
        "System.KeyNotFoundException",
        "System.NullReferenceException",
        "System.InvalidCastException",
        "System.ArithmeticException",
        "System.DivideByZeroException",
        "System.TimeoutException",
        "System.AggregateException",
        "System.OperationCanceledException",
        "System.TaskCanceledException",
        "System.ObjectDisposedException",
        "System.ApplicationException",
        "System.StackOverflowException",
        "System.OutOfMemoryException",

        // Lazy
        "System.Lazy`1",
        "System.Lazy`2",

        // Uri (read-only usage)
        "System.Uri",
        "System.UriBuilder",
        "System.Version",

        // Misc safe types
        "System.StringComparer",
        "System.IProgress`1",
        "System.Progress`1",
        "System.EventArgs",
        "System.Attribute",
        "System.ObsoleteAttribute",
        "System.FlagsAttribute",
        "System.ParamArrayAttribute"
    ];

    /// <summary>
    /// Validates the compiled assembly IL against the allowlist.
    /// Returns a list of violations (empty if the code is safe).
    /// </summary>
    /// <param name="compilation">The Roslyn compilation to validate.</param>
    /// <returns>List of violation descriptions.</returns>
    public static IReadOnlyList<string> Validate(Compilation compilation)
    {
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            // Compilation failed — will be caught by the script runner anyway
            return [];
        }

        ms.Position = 0;
        return ValidateAssembly(ms);
    }

    private static List<string> ValidateAssembly(Stream assemblyStream)
    {
        var violations = new List<string>();

        using var peReader = new PEReader(assemblyStream);
        var metadataReader = peReader.GetMetadataReader();

        // Check all type references (external types used by the script)
        foreach (var typeRefHandle in metadataReader.TypeReferences)
        {
            var typeRef = metadataReader.GetTypeReference(typeRefHandle);
            var ns = metadataReader.GetString(typeRef.Namespace);
            var name = metadataReader.GetString(typeRef.Name);

            // Handle nested types: if namespace is empty and ResolutionScope is a TypeReference,
            // this is a nested type. Resolve the parent to get the full context.
            if (string.IsNullOrEmpty(ns) && typeRef.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                var parentRef = metadataReader.GetTypeReference((TypeReferenceHandle)typeRef.ResolutionScope);
                var parentNs = metadataReader.GetString(parentRef.Namespace);
                var parentName = metadataReader.GetString(parentRef.Name);

                // Nested type inherits the parent's namespace for validation purposes
                // e.g., DebuggableAttribute.DebuggingModes → System.Diagnostics.DebuggableAttribute+DebuggingModes
                var parentFullName = string.IsNullOrEmpty(parentNs) ? parentName : $"{parentNs}.{parentName}";

                // If the parent type is allowed, the nested type is also allowed
                if (IsTypeAllowed(parentNs, parentName) || CompilerInfrastructureTypes.Contains(parentFullName))
                    continue;

                violations.Add($"Reference to nested type '{parentFullName}+{name}' is not permitted. " +
                               "Only allowlisted types may be used in Code node scripts.");
                continue;
            }

            if (!IsTypeAllowed(ns, name))
            {
                var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
                violations.Add($"Reference to type '{fullName}' is not permitted. " +
                               "Only allowlisted types may be used in Code node scripts.");
            }
        }

        return violations;
    }

    private static bool IsTypeAllowed(string ns, string typeName)
    {
        // Empty namespace — script-internal types (Roslyn-generated)
        if (string.IsNullOrEmpty(ns))
            return true;

        var fullName = $"{ns}.{typeName}";

        // Check blocked types FIRST — these are rejected regardless of namespace
        if (BlockedTypes.Contains(fullName))
            return false;

        // Check compiler infrastructure types (always allowed, compiler-emitted)
        if (CompilerInfrastructureTypes.Contains(fullName))
            return true;

        // Check fully-allowed namespaces
        if (FullyAllowedNamespaces.Contains(ns))
            return true;

        // Check allowed namespace prefixes (script infrastructure)
        foreach (var prefix in AllowedNamespacePrefixes)
        {
            if (ns.StartsWith(prefix, StringComparison.Ordinal) || ns == prefix.TrimEnd('.'))
                return true;
        }

        // For the "System" namespace specifically, check the type allowlist
        if (ns == "System")
            return AllowedSystemTypes.Contains(fullName);

        // Not in any allowed namespace
        return false;
    }
}
