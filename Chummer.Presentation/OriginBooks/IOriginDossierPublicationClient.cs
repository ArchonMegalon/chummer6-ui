using Chummer.Run.Contracts.Community;

namespace Chummer.Presentation.OriginBooks;

public interface IOriginDossierPublicationClient
{
    Task<OriginDossierPublicationImportResultDto> ImportOriginDossierPublicationAsync(
        OriginDossierPublicationImportRequest request,
        CancellationToken ct);
}
