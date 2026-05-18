namespace Chummer.Presentation.Overview;

public sealed record AttributeEditRequest(
    string AttributeName,
    string Bucket,
    int Value);
