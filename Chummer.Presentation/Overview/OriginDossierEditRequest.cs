namespace Chummer.Presentation.Overview;

public sealed record OriginDossierEditRequest(
    string Name,
    string Alias,
    string PlayerName,
    string Sex,
    string Age,
    string Height,
    string Weight,
    string Hair,
    string Eyes,
    string Skin,
    string Concept,
    string Description,
    string Background);
