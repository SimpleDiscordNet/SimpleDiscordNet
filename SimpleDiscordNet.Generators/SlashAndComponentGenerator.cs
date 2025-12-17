using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimpleDiscordNet.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class SlashAndComponentGenerator : IIncrementalGenerator
{
    private const string SlashAttr = "SimpleDiscordNet.Commands.SlashCommandAttribute";
    private const string SlashGroupAttr = "SimpleDiscordNet.Commands.SlashCommandGroupAttribute";
    private const string ComponentAttr = "SimpleDiscordNet.Commands.ComponentHandlerAttribute";
    private const string AutoDeferAttr = "SimpleDiscordNet.Commands.AutoDeferAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodsWithAttrs = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                                  static (ctx, ct) => GetCandidate(ctx))
            .Where(static c => c is not null)!
            .Select(static (c, _) => c!);

        var compilationAndCandidates = context.CompilationProvider.Combine(methodsWithAttrs.Collect());

        context.RegisterSourceOutput(compilationAndCandidates, static (spc, tuple) =>
        {
            var (compilation, items) = tuple;
            Emit(spc, compilation.AssemblyName ?? "Assembly", items);
        });
    }

    private static Candidate? GetCandidate(GeneratorSyntaxContext ctx)
    {
        var methodSyntax = (MethodDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol ms)
            return null;

        bool isSlash = false;
        bool isComponent = false;
        string? slashName = null;
        string? slashDescription = null;
        bool autoDefer = true; // default true per runtime
        string? componentId = null;
        bool componentPrefix = false;

        foreach (var ad in ms.GetAttributes())
        {
            var name = ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
            if (name == SlashAttr)
            {
                isSlash = true;
                if (ad.ConstructorArguments.Length >= 1)
                    slashName = ad.ConstructorArguments[0].Value as string;
                if (ad.ConstructorArguments.Length >= 2)
                    slashDescription = ad.ConstructorArguments[1].Value as string;
            }
            else if (name == ComponentAttr)
            {
                isComponent = true;
                if (ad.ConstructorArguments.Length >= 1)
                    componentId = ad.ConstructorArguments[0].Value as string;
                if (ad.ConstructorArguments.Length >= 2 && ad.ConstructorArguments[1].Value is bool b)
                    componentPrefix = b;
            }
            else if (name == AutoDeferAttr)
            {
                if (ad.ConstructorArguments.Length == 1 && ad.ConstructorArguments[0].Value is bool b)
                    autoDefer = b;
            }
        }

        if (!isSlash && !isComponent) return null;

        // Group attribute on containing type
        string? groupName = null;
        string? groupDescription = null;
        if (ms.ContainingType is { } ct)
        {
            foreach (var ad in ct.GetAttributes())
            {
                var name = ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
                if (name == SlashGroupAttr)
                {
                    if (ad.ConstructorArguments.Length >= 1)
                        groupName = ad.ConstructorArguments[0].Value as string;
                    if (ad.ConstructorArguments.Length >= 2)
                        groupDescription = ad.ConstructorArguments[1].Value as string;
                    break;
                }
            }
        }

        bool hasContext = ms.Parameters.Length > 0 && ms.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "SimpleDiscordNet.Commands.InteractionContext";

        // Only parameterless instance or any static methods are supported
        bool isStatic = ms.IsStatic;
        bool hasDefaultCtor = false;
        if (!isStatic)
        {
            foreach (var c in ms.ContainingType.InstanceConstructors)
            {
                if (c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0)
                {
                    hasDefaultCtor = true;
                    break;
                }
            }
        }

        return new Candidate
        {
            Namespace = GetNamespace(ms.ContainingType),
            TypeName = ms.ContainingType.Name,
            ContainingType = ms.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':'),
            MethodName = ms.Name,
            IsStatic = isStatic,
            HasDefaultCtor = hasDefaultCtor,
            HasContext = hasContext,
            IsSlash = isSlash,
            SlashName = slashName,
            SlashDescription = slashDescription,
            GroupName = groupName,
            GroupDescription = groupDescription,
            AutoDefer = autoDefer,
            IsComponent = isComponent,
            ComponentId = componentId,
            ComponentPrefix = componentPrefix
        };
    }

    private static string GetNamespace(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace;
        if (ns is null || ns.IsGlobalNamespace) return string.Empty;
        return ns.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
    }

    private static void Emit(SourceProductionContext spc, string assemblyName, IReadOnlyList<Candidate> candidates)
    {
        var commands = candidates.Where(c => c.IsSlash && !string.IsNullOrWhiteSpace(c.SlashName)).ToList();
        var components = candidates.Where(c => c.IsComponent && !string.IsNullOrWhiteSpace(c.ComponentId)).ToList();

        // Organize slash commands
        var ungrouped = new List<Candidate>();
        var grouped = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);

        foreach (var c in commands)
        {
            var normName = NormalizeName(c.SlashName!);
            c.SlashName = normName;
            if (!string.IsNullOrWhiteSpace(c.GroupName))
            {
                var grp = NormalizeName(c.GroupName!);
                c.GroupName = grp;
                if (!grouped.TryGetValue(grp, out var list))
                {
                    list = new List<Candidate>();
                    grouped[grp] = list;
                }
                list.Add(c);
            }
            else
            {
                ungrouped.Add(c);
            }
        }

        // Build definitions array source
        var defsBuilder = new StringBuilder();
        defsBuilder.AppendLine("new global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] {");
        foreach (var g in grouped)
        {
            defsBuilder.AppendLine("    new global::SimpleDiscordNet.Models.ApplicationCommandDefinition { ");
            defsBuilder.AppendLine($"        name = \"{g.Key}\", ");
            defsBuilder.AppendLine("        type = 1,");
            var gdesc = string.IsNullOrWhiteSpace(g.Value.FirstOrDefault()?.GroupDescription) ? "group" : g.Value.First().GroupDescription!.Replace("\"", "\\\"");
            defsBuilder.AppendLine($"        description = \"{gdesc}\",");
            defsBuilder.AppendLine("        options = new global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] {");
            foreach (var sc in g.Value.GroupBy(x => (x.SlashName!, x.SlashDescription ?? "command")).Select(x => x.First()))
            {
                var desc = string.IsNullOrWhiteSpace(sc.SlashDescription) ? "command" : sc.SlashDescription!.Replace("\"", "\\\"");
                defsBuilder.AppendLine("            new global::SimpleDiscordNet.Models.ApplicationCommandDefinition { name = \"" + sc.SlashName + "\", type = 1, description = \"" + desc + "\" },");
            }
            defsBuilder.AppendLine("        }");
            defsBuilder.AppendLine("    },");
        }
        foreach (var u in ungrouped.GroupBy(x => (x.SlashName!, x.SlashDescription ?? "command")).Select(x => x.First()))
        {
            var desc = string.IsNullOrWhiteSpace(u.SlashDescription) ? "command" : u.SlashDescription!.Replace("\"", "\\\"");
            defsBuilder.AppendLine("    new global::SimpleDiscordNet.Models.ApplicationCommandDefinition { name = \"" + u.SlashName + "\", type = 1, description = \"" + desc + "\" },");
        }
        defsBuilder.AppendLine("}");

        // Build handler dictionaries
        string BuildHandlerFactory(Candidate c)
        {
            var targetExpr = c.IsStatic ? c.ContainingType : $"__InstHolder_{SanitizeId(c.ContainingType)}.Value";
            // In case method returns Task, await it; if void, just complete
            var call = c.HasContext
                ? $"var _res = {targetExpr}.{c.MethodName}(ctx); if (_res is System.Threading.Tasks.Task t) await t.ConfigureAwait(false);"
                : $"var _res = {targetExpr}.{c.MethodName}(); if (_res is System.Threading.Tasks.Task t) await t.ConfigureAwait(false);";
            return $"new global::SimpleDiscordNet.Commands.CommandHandler(HasContext: {(c.HasContext ? "true" : "false")}, AutoDefer: {(c.AutoDefer ? "true" : "false")}, Invoke: static async (ctx, ct) => {{ {call} }})";
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("namespace SimpleDiscordNet.Generated");
        sb.AppendLine("{");

        // Emit optional singletons for instance types
        foreach (var typeName in commands.Where(c => !c.IsStatic).Select(c => c.ContainingType).Concat(components.Where(c => !c.IsStatic).Select(c => c.ContainingType)).Distinct())
        {
            var any = candidates.First(c => c.ContainingType == typeName);
            if (!any.HasDefaultCtor) continue; // will diagnose below
            sb.AppendLine($"    internal static class __InstHolder_{SanitizeId(typeName)} {{ internal static readonly {typeName} Value = new {typeName}(); }}");
        }

        // Generated manifest/provider
        sb.AppendLine("    internal sealed class __GeneratedManifest : global::SimpleDiscordNet.Commands.IGeneratedManifest");
        sb.AppendLine("    {");
        // Ungrouped
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler> Ungrouped { get; } = new global::System.Collections.Generic.Dictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var u in ungrouped)
        {
            var instExpr = u.IsStatic ? null : (u.HasDefaultCtor ? $"__InstHolder_{SanitizeId(u.ContainingType)}.Value" : null);
            if (!u.IsStatic && instExpr is null) continue;
            sb.AppendLine($"            [\"{u.SlashName}\"] = {BuildHandlerFactory(u)},");
        }
        sb.AppendLine("        };");

        // Grouped
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>> Grouped { get; } = new global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var g in grouped)
        {
            sb.AppendLine($"            [\"{g.Key}\"] = new global::System.Collections.Generic.Dictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>(System.StringComparer.Ordinal)");
            sb.AppendLine("            {");
            foreach (var sc in g.Value)
            {
                var instExpr = sc.IsStatic ? null : (sc.HasDefaultCtor ? $"__InstHolder_{SanitizeId(sc.ContainingType)}.Value" : null);
                if (!sc.IsStatic && instExpr is null) continue;
                sb.AppendLine($"                [\"{sc.SlashName}\"] = {BuildHandlerFactory(sc)},");
            }
            sb.AppendLine("            },");
        }
        sb.AppendLine("        };");

        // Components
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::SimpleDiscordNet.Commands.ComponentHandler> Components { get; } = new global::System.Collections.Generic.List<global::SimpleDiscordNet.Commands.ComponentHandler>");
        sb.AppendLine("        {");
        foreach (var c in components)
        {
            var instExpr = c.IsStatic ? null : (c.HasDefaultCtor ? $"__InstHolder_{SanitizeId(c.ContainingType)}.Value" : null);
            if (!c.IsStatic && instExpr is null) continue;
            string invoker = c.HasContext ? (c.IsStatic ? $"static async (ctx, ct) => {{ var _r = {c.ContainingType}.{c.MethodName}(ctx); if (_r is System.Threading.Tasks.Task t) await t.ConfigureAwait(false); }}" : $"async (ctx, ct) => {{ var _r = __InstHolder_{SanitizeId(c.ContainingType)}.Value.{c.MethodName}(ctx); if (_r is System.Threading.Tasks.Task t) await t.ConfigureAwait(false); }}")
                                          : (c.IsStatic ? $"static async (ctx, ct) => {{ var _r = {c.ContainingType}.{c.MethodName}(); if (_r is System.Threading.Tasks.Task t) await t.ConfigureAwait(false); }}" : $"async (ctx, ct) => {{ var _r = __InstHolder_{SanitizeId(c.ContainingType)}.Value.{c.MethodName}(); if (_r is System.Threading.Tasks.Task t) await t.ConfigureAwait(false); }}");
            sb.AppendLine($"            new global::SimpleDiscordNet.Commands.ComponentHandler(\"{c.ComponentId}\", {c.ComponentPrefix.ToString().ToLowerInvariant()}, {c.HasContext.ToString().ToLowerInvariant()}, {c.AutoDefer.ToString().ToLowerInvariant()}, {invoker}),");
        }
        sb.AppendLine("        };");

        // Definitions
        sb.AppendLine($"        public global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] Definitions {{ get; }} = {defsBuilder};");

        // Help index (basic)
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, string> HelpIndex { get; } = new global::System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var u in ungrouped)
        {
            var desc = string.IsNullOrWhiteSpace(u.SlashDescription) ? "command" : u.SlashDescription!.Replace("\"", "\\\"");
            sb.AppendLine($"            [\"/{u.SlashName}\"] = \"{desc}\",");
        }
        foreach (var g in grouped)
        {
            foreach (var sc in g.Value)
            {
                var desc = string.IsNullOrWhiteSpace(sc.SlashDescription) ? "command" : sc.SlashDescription!.Replace("\"", "\\\"");
                sb.AppendLine($"            [\"/{g.Key} {sc.SlashName}\"] = \"{desc}\",");
            }
        }
        sb.AppendLine("        };");

        sb.AppendLine("    }");

        sb.AppendLine("    internal sealed class __GeneratedProvider : global::SimpleDiscordNet.Commands.IGeneratedManifestProvider");
        sb.AppendLine("    {");
        sb.AppendLine("        public global::SimpleDiscordNet.Commands.IGeneratedManifest CreateManifest() => new __GeneratedManifest();");
        sb.AppendLine("    }");

        sb.AppendLine("    internal static class __GeneratedModuleInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        public static void Init() => global::SimpleDiscordNet.Commands.GeneratedRegistry.Register(new __GeneratedProvider());");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        spc.AddSource($"{SanitizeId(assemblyName)}_SimpleDiscordNet_GeneratedManifest.g.cs", sb.ToString());

        // Report diagnostics for unsupported patterns
        foreach (var c in commands.Concat(components))
        {
            if (!c.IsStatic && !c.HasDefaultCtor)
            {
                spc.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    id: "SDN001",
                    title: "Handler type must have a public parameterless constructor or the method must be static",
                    messageFormat: "Type '{0}' must have a public parameterless constructor or the method '{1}' must be static for source-generated handlers.",
                    category: "SimpleDiscordNet",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                    Location.None,
                    c.ContainingType, c.MethodName));
            }
        }
    }

    private static string NormalizeName(string name)
    {
        var s = (name ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '-');
        var filtered = new string(s.Where(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_').ToArray());
        if (filtered.Length == 0) filtered = "cmd";
        if (filtered.Length > 32) filtered = filtered.Substring(0, 32);
        return filtered;
    }

    private static string SanitizeId(string id)
        => new string(id.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

    private sealed class Candidate
    {
        public string Namespace { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string ContainingType { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public bool IsStatic { get; set; }
        public bool HasDefaultCtor { get; set; }
        public bool HasContext { get; set; }
        public bool IsSlash { get; set; }
        public string? SlashName { get; set; }
        public string? SlashDescription { get; set; }
        public string? GroupName { get; set; }
        public string? GroupDescription { get; set; }
        public bool AutoDefer { get; set; }
        public bool IsComponent { get; set; }
        public string? ComponentId { get; set; }
        public bool ComponentPrefix { get; set; }
    }
}
