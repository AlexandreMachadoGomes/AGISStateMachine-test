// File: AGISRoslynCompiler.cs
// Folder: Assets/Scripts/AX State Machine/Authoring/
// Purpose: Runtime C# compiler using Microsoft.CodeAnalysis.CSharp.
//          Compiles source strings into in-memory assemblies at runtime.
//
// Setup (one-time):
//   1. Add define symbol AGIS_ROSLYN under Project Settings → Player → Scripting Define Symbols.
//   2. Place these DLLs in Assets/Plugins/Roslyn/ (obtain from NuGet or Unity's Roslyn package):
//        Microsoft.CodeAnalysis.CSharp.dll
//        Microsoft.CodeAnalysis.dll
//        System.Collections.Immutable.dll
//        System.Reflection.Metadata.dll
//   3. In the Inspector for each DLL: set Platform = Editor+Standalone, API Compat = .NET 4.x.
//   Without the DLLs / define, Compile() returns an explanatory error and is otherwise a no-op.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

#if AGIS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
#endif

namespace AGIS.ESM.Authoring
{
    /// <summary>
    /// Result of a Roslyn compile attempt.
    /// </summary>
    public sealed class AGISCompileResult
    {
        public readonly bool     Success;
        public readonly Assembly Assembly;   // non-null when Success == true
        public readonly string[] Errors;
        public readonly string[] Warnings;

        public AGISCompileResult(bool success, Assembly assembly, string[] errors, string[] warnings)
        {
            Success  = success;
            Assembly = assembly;
            Errors   = errors   ?? Array.Empty<string>();
            Warnings = warnings ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Stateless Roslyn compiler wrapper.  Thread-safe after first call (lazy ref init).
    /// </summary>
    public static class AGISRoslynCompiler
    {
#if AGIS_ROSLYN
        // Cached once; invalidated when new authored assemblies are loaded so future
        // compiles can reference previously compiled types.
        private static List<MetadataReference> _cachedRefs;
        private static readonly object _refsLock = new object();

        private static List<MetadataReference> GetReferences()
        {
            lock (_refsLock)
            {
                if (_cachedRefs != null) return _cachedRefs;
                RebuildReferenceCache();
                return _cachedRefs;
            }
        }

        /// <summary>
        /// Call after loading a new authored assembly so it is visible to the next compile.
        /// </summary>
        public static void InvalidateReferenceCache()
        {
            lock (_refsLock) { _cachedRefs = null; }
        }

        private static void RebuildReferenceCache()
        {
            var refs = new List<MetadataReference>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                string loc;
                try { loc = asm.Location; } catch { continue; }
                if (string.IsNullOrEmpty(loc)) continue;
                if (!File.Exists(loc)) continue;
                try { refs.Add(MetadataReference.CreateFromFile(loc)); }
                catch { /* skip unreadable assemblies */ }
            }
            _cachedRefs = refs;
        }
#endif

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Compile <paramref name="source"/> into an in-memory assembly.
        /// Returns a result with Success=true and non-null Assembly on success.
        /// </summary>
        public static AGISCompileResult Compile(string source, string assemblyName = null)
        {
            if (string.IsNullOrWhiteSpace(source))
                return Fail("Source code is empty.");

            assemblyName ??= "AGISAuthored_" + System.Guid.NewGuid().ToString("N");

#if AGIS_ROSLYN
            try
            {
                var syntaxTree  = CSharpSyntaxTree.ParseText(source);
                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    syntaxTrees: new[] { syntaxTree },
                    references:  GetReferences(),
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                        .WithOptimizationLevel(OptimizationLevel.Release)
                        .WithNullableContextOptions(NullableContextOptions.Disable)
                        .WithAllowUnsafe(false));

                using var ms = new MemoryStream();
                var emit = compilation.Emit(ms);

                var errors   = emit.Diagnostics
                                   .Where(d => d.Severity == DiagnosticSeverity.Error)
                                   .Select(d => d.GetMessage())
                                   .ToArray();
                var warnings = emit.Diagnostics
                                   .Where(d => d.Severity == DiagnosticSeverity.Warning)
                                   .Select(d => d.GetMessage())
                                   .ToArray();

                if (!emit.Success)
                    return new AGISCompileResult(false, null, errors, warnings);

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                // Invalidate reference cache so next compile can see this assembly
                InvalidateReferenceCache();

                return new AGISCompileResult(true, assembly, errors, warnings);
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
#else
            return Fail(
                "Roslyn compiler is not available. " +
                "Add AGIS_ROSLYN to Scripting Define Symbols and " +
                "place the required DLLs in Assets/Plugins/Roslyn/.");
#endif
        }

        private static AGISCompileResult Fail(string message)
            => new AGISCompileResult(false, null, new[] { message }, Array.Empty<string>());
    }
}
