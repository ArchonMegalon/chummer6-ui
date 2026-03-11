using System;
using System.Threading.Tasks;

namespace AvaloniaProject
{
    /// <summary>
    /// Contract for the Build Lab Engine.
    /// </summary>
    public interface IBuildLabEngine
    {
        Task<string> ExecuteBuildAsync(string buildParameters);
    }

    /// <summary>
    /// Contract for the Derive Initiative Capability.
    /// </summary>
    public interface IDeriveInitiativeCapability
    {
        Task<bool> DeriveInitiativeAsync(string initiativeId);
    }

    /// <summary>
    /// Client that implements the contracts over a mocked asynchronous boundary.
    /// </summary>
    public class EngineClient : IBuildLabEngine, IDeriveInitiativeCapability
    {
        // Simulated network/processing delay
        private const int MockDelayMilliseconds = 500;

        public async Task<string> ExecuteBuildAsync(string buildParameters)
        {
            // Mock asynchronous boundary
            await Task.Delay(MockDelayMilliseconds);
            
            return $"Successfully executed build with parameters: {buildParameters}";
        }

        public async Task<bool> DeriveInitiativeAsync(string initiativeId)
        {
            // Mock asynchronous boundary
            await Task.Delay(MockDelayMilliseconds);
            
            // Simulate a successful derivation
            return !string.IsNullOrWhiteSpace(initiativeId);
        }
    }
}
