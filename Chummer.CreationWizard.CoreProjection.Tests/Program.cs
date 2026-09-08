using Chummer.Tests.Presentation;

if (args.Length != 1)
    throw new ArgumentException("Supply one explicit absolute Core content root.");

await CoreCreationProjectionScenario.RunAsync(args[0], Console.WriteLine);
