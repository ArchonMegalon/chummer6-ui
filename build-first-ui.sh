#!/bin/bash
cd /docker/chummercomplete/chummer-presentation/

# Load API keys
if [ -f "/docker/EA/.env" ]; then
    export $(grep -v '^#' /docker/EA/.env | xargs)
fi

MODEL="gemini/gemini-3.1-pro-preview"
BUILD_CMD="dotnet build Chummer.Presentation.sln"

echo "=================================================="
echo "🎨 TICKET B3: Building the Build Lab UI"
echo "=================================================="

# We target the main UI files in the Avalonia project
aider --model $MODEL \
  Chummer.Avalonia/Views/MainWindow.axaml \
  Chummer.Avalonia/Views/MainWindow.axaml.cs \
  Chummer.Avalonia/Services/EngineClient.cs \
  --yes \
  --test-cmd "$BUILD_CMD" \
  --message "Turn this empty shell into a real application. Update MainWindow.axaml to create a 'Build Lab' dashboard. Use a modern, dark-themed UI. Add a 'Simulate Build' button. When clicked, it should call 'EngineClient.ExecuteBuildAsync' and display the returned string in a TextBlock below it. Ensure the EngineClient is properly instantiated or injected in MainWindow.axaml.cs. Make sure the code passes the build."

echo "✅ UI Scaffold complete. Push the code to see it live."
