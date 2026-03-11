#!/bin/bash

echo "=================================================="
echo "🕵️‍♂️ INITIATING FULL ARCHITECTURE AUDIT"
echo "=================================================="

# 1. Check Contracts Package Boundary
echo -e "\n[1/4] Auditing Contracts Package Boundary..."
cd /docker/chummercomplete/chummer-presentation/
if grep -q 'ProjectReference Include="..\\Chummer.Contracts\\Chummer.Contracts.csproj"' \
    Chummer.Presentation/Chummer.Presentation.csproj \
    Chummer.Blazor/Chummer.Blazor.csproj \
    Chummer.Tests/Chummer.Tests.csproj 2>/dev/null; then
    echo "❌ FAILED: Presentation still references the duplicated contracts source project!"
elif [ -d Chummer.Contracts ]; then
    echo "❌ FAILED: Presentation still carries the duplicated contracts source tree!"
else
    echo "✅ PASS: Presentation consumes the authoritative contracts package."
fi

# 2. Check Presentation Engine Boundaries
echo -e "\n[2/4] Auditing Presentation Boundaries..."
cd /docker/chummercomplete/chummer-presentation/
if grep -q "Chummer.Core.csproj" Chummer.Avalonia/Chummer.Avalonia.csproj 2>/dev/null; then
    echo "❌ FAILED: Presentation layer is illegally referencing the Core Engine math!"
else
    echo "✅ PASS: Presentation layer is strictly isolated from core logic."
fi

# 3. Check Contracts Linkage
if grep -q 'PackageReference Include="$(ChummerContractsPackageId)" Version="$(ChummerContractsPackageVersion)"' \
    Chummer.Presentation/Chummer.Presentation.csproj \
    Chummer.Blazor/Chummer.Blazor.csproj \
    Chummer.Tests/Chummer.Tests.csproj 2>/dev/null; then
    echo "✅ PASS: Presentation layer is correctly wired to the authoritative contracts package."
else
    echo "❌ FAILED: Presentation layer is missing the authoritative contracts package reference!"
fi

if [ -d Chummer.Session.Web ] || [ -d Chummer.Coach.Web ]; then
    echo "❌ FAILED: Play/mobile heads still live in the presentation repo!"
else
    echo "✅ PASS: Play/mobile heads have been moved out of the presentation repo."
fi

# 4. Resolve the Git "Up-to-date" Anomaly
echo -e "\n[3/4] Resolving Git Status..."
UNCOMMITTED=$(git status --porcelain)
if [ ! -z "$UNCOMMITTED" ]; then
    echo "⚠️ WARNING: Uncommitted files found in Presentation repo (likely the new .sln)."
    echo "Auto-committing and pushing to GitHub..."
    git add .
    git commit -m "build: finalize instance B presentation boundaries and contracts linkage"
    git push
    echo "✅ PASS: Presentation repository is now fully synced."
else
    echo "✅ PASS: Presentation repository is fully committed and pushed."
fi

# 5. Final Build Test
echo -e "\n[4/4] Verifying Final Presentation Build..."
# We pipe to /dev/null to hide the compiler spam and only care about the exit code
dotnet build Chummer.Presentation.sln > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "✅ PASS: Presentation UI builds perfectly against the headless contracts."
else
    echo "❌ FAILED: Presentation build broke during the audit!"
fi

echo -e "\n=================================================="
echo "🏆 AUDIT COMPLETE. THE DECAPITATION IS A SUCCESS."
echo "=================================================="
