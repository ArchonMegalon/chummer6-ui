using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chummer.Presentation.CharacterStatistics;

public interface ICharacterStatisticsProjectionService
{
    ValueTask<CharacterStatisticsProjection> ProjectAsync(
        CharacterStatisticsSnapshot snapshot,
        CharacterStatisticsCalculationOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class CharacterStatisticsProjectionService : ICharacterStatisticsProjectionService
{
    private readonly ICharacterStatisticsCalculator _calculator;

    public CharacterStatisticsProjectionService(ICharacterStatisticsCalculator calculator)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    public async ValueTask<CharacterStatisticsProjection> ProjectAsync(
        CharacterStatisticsSnapshot snapshot,
        CharacterStatisticsCalculationOptions options,
        CancellationToken cancellationToken = default)
    {
        CharacterStatisticsResult result = await _calculator.CalculateAsync(snapshot, options, cancellationToken);
        return CharacterStatisticsProjection.FromResult(result);
    }
}
