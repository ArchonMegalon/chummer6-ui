#!/bin/bash
cd /docker/chummercomplete/chummer-presentation/

# Load API keys
if [ -f "/docker/EA/.env" ]; then
    export $(grep -v '^#' /docker/EA/.env | xargs)
fi

MODEL="gemini/gemini-3.1-pro-preview"

echo "=================================================="
echo "⚡ TICKET B4: Wiring the UI to the Live Blazor Portal"
echo "=================================================="

# Let Aider find the Blazor files and inject the UI
aider --model $MODEL \
  --yes \
  --message "Find the main landing page in the Chummer.Blazor project (e.g., Pages/Index.razor or Components/Pages/Home.razor). Replace its content with a dark-themed 'Build Lab Dashboard'. Add a text input for 'Parameters', a 'Simulate Build' button, and a preformatted text area to display the result. Create a 'Chummer.Blazor/Services/EngineClient.cs' class with a mocked 'ExecuteBuildAsync' method using Task.Delay (return a success string). Register this EngineClient as a scoped service in the Blazor Program.cs, and inject it into the Razor page so the button works. Do not touch any .csproj files."

echo "=================================================="
echo "🏗️ Rebuilding the LIVE chummer5a Blazor container..."
echo "=================================================="
docker compose -p chummer5a build --no-cache chummer-blazor
docker compose -p chummer5a up -d chummer-blazor

echo "=================================================="
echo "✅ Blazor UI deployed! Do a Hard Refresh on your domain."
echo "=================================================="
