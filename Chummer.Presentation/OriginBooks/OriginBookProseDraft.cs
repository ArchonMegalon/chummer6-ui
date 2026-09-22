using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.LifeModules;

namespace Chummer.Presentation.OriginBooks;

/// <summary>
/// A reading-edition proposal, not a Core chapter, rule change or canon approval.
/// The authenticated job transport owns provider provenance; this class only
/// checks the exact chapter/language/content binding before showing its text.
/// </summary>
public sealed record OriginBookProseDraft(
    string ChapterId, string ChapterDigest, string AcceptedDecisionId, string Locale,
    string JobId, string ProviderReceiptDigest, string Text, string DraftDigest)
{
    public const int MaximumTextBytes = 64 * 1024;
    public bool AffectsMechanics => false;
    public bool RequiresReaderReview => true;

    public static OriginBookProseDraft Create(OriginNarrativeChapterProjection chapter, string locale,
        string jobId, string providerReceiptDigest, string text)
    {
        var draft = new OriginBookProseDraft(chapter.ChapterId, chapter.ChapterDigest,
            chapter.ThroughAcceptedDecisionId, locale, jobId, providerReceiptDigest, text, "");
        if (!draft.HasValidFields()) throw new InvalidDataException("The chapter draft is incomplete or oversized.");
        return draft with { DraftDigest = draft.ComputeDigest() };
    }

    public bool Matches(OriginNarrativeChapterProjection chapter, string locale)
        => IsValid() && ChapterId == chapter.ChapterId && ChapterDigest == chapter.ChapterDigest
            && AcceptedDecisionId == chapter.ThroughAcceptedDecisionId && Locale == locale;

    public bool IsValid() => HasValidFields() && DraftDigest == ComputeDigest();

    private bool HasValidFields()
        => Exact(ChapterId, 256) && Hex(ChapterDigest) && Exact(AcceptedDecisionId, 256)
            && Exact(Locale, 32) && OriginDossierNarrativeLocalePolicy.PrimaryLanguage(Locale) is "de" or "en" or "es"
            && Exact(JobId, 256) && Hex(ProviderReceiptDigest) && !string.IsNullOrWhiteSpace(Text)
            && Encoding.UTF8.GetByteCount(Text) <= MaximumTextBytes;

    private string ComputeDigest() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        new[] { ChapterId, ChapterDigest, AcceptedDecisionId, Locale, JobId, ProviderReceiptDigest, Text }))).ToLowerInvariant();
    private static bool Exact(string? value, int limit) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= limit && value == value.Trim();
    private static bool Hex(string? value) => value is { Length: 64 }
        && value.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
