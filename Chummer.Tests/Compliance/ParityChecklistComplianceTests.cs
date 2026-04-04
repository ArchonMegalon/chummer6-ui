#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class ParityChecklistComplianceTests
{
    [TestMethod]
    public void Parity_generator_fail_closes_missing_legacy_tabs_and_workspace_actions()
    {
        string parityGeneratorPath = FindPath("scripts", "generate-parity-checklist.sh");
        string parityGeneratorText = File.ReadAllText(parityGeneratorPath);

        StringAssert.Contains(parityGeneratorText, "fail_on_missing_required_legacy_ids");
        StringAssert.Contains(parityGeneratorText, "surface_label=\"tab\"");
        StringAssert.Contains(parityGeneratorText, "surface_label=\"workspace action\"");
        StringAssert.Contains(parityGeneratorText, "is missing required legacy");
    }

    private static string FindPath(params string[] parts)
    {
        foreach (string? root in CandidateRoots())
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            DirectoryInfo current = new(root);
            while (true)
            {
                string candidate = Path.Combine(new[] { current.FullName }.Concat(parts).ToArray());
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                if (current.Parent is null)
                {
                    break;
                }

                current = current.Parent;
            }
        }

        throw new FileNotFoundException("Could not locate file.", Path.Combine(parts));
    }

    private static IEnumerable<string?> CandidateRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }
}
