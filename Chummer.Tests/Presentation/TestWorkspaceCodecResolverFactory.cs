using Chummer.Application.Characters;
using Chummer.Application.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;

namespace Chummer.Tests.Presentation;

internal static class TestWorkspaceCodecResolverFactory
{
    public static IRulesetWorkspaceCodecResolver Create()
    {
        CharacterFileService fileService = new();
        XmlCharacterFileQueries fileQueries = new(fileService);
        XmlCharacterSectionQueries sectionQueries = new(new CharacterSectionService());
        XmlCharacterMetadataCommands metadataCommands = new(fileService);

        return new RulesetWorkspaceCodecResolver(
        [
            new Sr4WorkspaceCodec(fileQueries, sectionQueries, metadataCommands),
            new Sr5WorkspaceCodec(fileQueries, sectionQueries, metadataCommands),
            new Sr6WorkspaceCodec(fileQueries, sectionQueries, metadataCommands)
        ]);
    }
}
