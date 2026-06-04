#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class LegacyDesktopParityGuardTests
{
    [TestMethod]
    public void Presentation_custom_activity_must_support_page_view_operations_without_not_implemented_fallbacks()
    {
        string repoRoot = FindRepoRoot();
        string customActivityPath = Path.Combine(repoRoot, "Chummer", "Backend", "Helpers", "Application Insights", "CustomActivity.cs");
        string source = File.ReadAllText(customActivityPath);

        StringAssert.Contains(source, "public PageViewTelemetry MyPageViewTelemetry { get; private set; }");
        StringAssert.Contains(source, "case OperationType.PageViewOperation:");
        StringAssert.Contains(source, "MyTelemetryClient.TrackPageView(MyPageViewTelemetry);");
        Assert.IsFalse(source.Contains("throw new NotImplementedException(\"Implement OperationType " + "\" + operationType);", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("throw new NotImplementedException(\"Implement OperationType " + "\" + parentActivity.MyOperationType);", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("throw new NotImplementedException(\"Implement OperationType " + "\" + OperationName);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Presentation_equipment_allow_paste_object_methods_must_not_throw_not_implemented()
    {
        string repoRoot = FindRepoRoot();
        string[] relativePaths =
        {
            Path.Combine("Chummer", "Backend", "Equipment", "Armor.cs"),
            Path.Combine("Chummer", "Backend", "Equipment", "ArmorMod.cs"),
            Path.Combine("Chummer", "Backend", "Equipment", "Gear.cs"),
            Path.Combine("Chummer", "Backend", "Equipment", "Vehicle.cs"),
            Path.Combine("Chummer", "Backend", "Equipment", "VehicleMod.cs"),
            Path.Combine("Chummer", "Backend", "Equipment", "Weapon.cs"),
            Path.Combine("Chummer", "Backend", "Equipment", "WeaponAccessory.cs"),
            Path.Combine("Chummer", "Backend", "Equipment", "WeaponMount.cs")
        };

        foreach (string relativePath in relativePaths)
        {
            string path = Path.Combine(repoRoot, relativePath);
            string source = File.ReadAllText(path);
            StringAssert.Contains(source, "AllowPasteObject");
            Assert.IsFalse(
                source.Contains("AllowPasteObject(object input, CancellationToken token = default)\n        {\n            token.ThrowIfCancellationRequested();\n            throw new NotImplementedException();", StringComparison.Ordinal),
                $"{relativePath} still contains a throw-only AllowPasteObject implementation.");
        }
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Could not locate repository root.");
        return string.Empty;
    }
}
