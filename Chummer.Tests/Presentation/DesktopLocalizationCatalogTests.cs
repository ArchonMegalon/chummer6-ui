using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Chummer.Tests.Presentation;

[TestClass]
public class DesktopLocalizationCatalogTests
{
    [TestMethod]
    public void NormalizeOrDefault_uses_shipping_language_and_falls_back_to_english()
    {
        Assert.AreEqual("de-de", DesktopLocalizationCatalog.NormalizeOrDefault("de-DE"));
        Assert.AreEqual(DesktopLocalizationCatalog.DefaultLanguage, DesktopLocalizationCatalog.NormalizeOrDefault("es-es"));
        Assert.AreEqual(DesktopLocalizationCatalog.DefaultLanguage, DesktopLocalizationCatalog.NormalizeOrDefault(null));
    }

    [TestMethod]
    public void ShippingLanguages_match_locked_desktop_wave()
    {
        CollectionAssert.AreEqual(
            new[] { "en-us", "de-de", "fr-fr", "ja-jp", "pt-br", "zh-cn" },
            DesktopLocalizationCatalog.ShippingLanguages.Select(language => language.Code).ToArray());
    }

    [TestMethod]
    public void RequiredTrustSurfaceKeys_resolve_for_every_shipping_language()
    {
        foreach (string key in DesktopLocalizationCatalog.RequiredTrustSurfaceKeys())
        {
            foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages.Select(language => language.Code))
            {
                string value = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Expected trust-surface localization for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Non_default_locales_never_return_unmarked_english_values_on_seeded_keys()
    {
        string[] seededKeys =
        [
            "desktop.shell.menu.file",
            "desktop.shell.tool.desktop_home",
            "desktop.shell.tool.horizons",
            "desktop.home.section.install_support",
            "desktop.home.title",
            "desktop.support.title"
        ];

        foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages
                     .Select(language => language.Code)
                     .Where(language => !string.Equals(language, DesktopLocalizationCatalog.DefaultLanguage, StringComparison.Ordinal)))
        {
            foreach (string key in seededKeys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                string enValue = DesktopLocalizationCatalog.GetRequiredString(key, DesktopLocalizationCatalog.DefaultLanguage);
                Assert.AreNotEqual(enValue, localizedValue, $"Expected locale-distinct value for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Non_default_locales_cover_remaining_trust_surface_seed_keys_without_fallback_markers()
    {
        string[] seedKeys =
        [
            "desktop.install_link.summary",
            "desktop.update.heading",
            "desktop.report.bug.intro",
            "desktop.report.heading",
            "desktop.crash.heading"
        ];

        foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages
                     .Select(language => language.Code)
                     .Where(language => !string.Equals(language, DesktopLocalizationCatalog.DefaultLanguage, StringComparison.Ordinal)))
        {
            foreach (string key in seedKeys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                string enValue = DesktopLocalizationCatalog.GetRequiredString(key, DesktopLocalizationCatalog.DefaultLanguage);
                Assert.IsFalse(localizedValue.Contains("[en-US fallback]", StringComparison.Ordinal), $"Expected localized value without fallback marker for {key} / {languageCode}.");
                Assert.AreNotEqual(enValue, localizedValue, $"Expected locale-distinct localized value for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Release_critical_localized_seed_keys_cover_menu_support_update_and_home_surfaces_without_fallback()
    {
        string[] releaseCriticalSeedKeys =
        [
            "desktop.shell.menu.file",
            "desktop.shell.tool.horizons",
            "desktop.shell.tool.update_status",
            "desktop.shell.tool.open_support",
            "desktop.shell.tool.report_issue",
            "desktop.home.title",
            "desktop.home.section.install_support",
            "desktop.home.section.update_posture",
            "desktop.support.title"
        ];

        foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages
                     .Select(language => language.Code)
                     .Where(language => !string.Equals(language, DesktopLocalizationCatalog.DefaultLanguage, StringComparison.Ordinal)))
        {
            foreach (string key in releaseCriticalSeedKeys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                Assert.IsFalse(localizedValue.Contains("[en-US fallback]", StringComparison.Ordinal), $"Expected fully localized seeded key for {key} / {languageCode}.");
                Assert.AreNotEqual(
                    DesktopLocalizationCatalog.GetRequiredString(key, DesktopLocalizationCatalog.DefaultLanguage),
                    localizedValue,
                    $"Expected locale-distinct seeded key for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Required_trust_surface_keys_cover_flagship_localization_domains()
    {
        string[] requiredPrefixes =
        [
            "desktop.shell.menu.",
            "desktop.shell.tool.",
            "desktop.home.",
            "desktop.install_link.",
            "desktop.update.",
            "desktop.support.",
            "desktop.support_case.",
            "desktop.crash.",
            "desktop.report.",
            "desktop.dialog.global_settings.",
            "desktop.dialog.translator.",
            "desktop.shell.notice.export_"
        ];

        IReadOnlyList<string> keys = DesktopLocalizationCatalog.RequiredTrustSurfaceKeys();
        foreach (string prefix in requiredPrefixes)
        {
            Assert.IsTrue(
                keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)),
                $"Expected flagship localization trust-surface coverage for prefix '{prefix}'.");
        }
    }

    [TestMethod]
    public void Product_area_home_seed_keys_are_localized_for_every_shipping_language_without_fallback_markers()
    {
        string[] keys =
        [
            "desktop.home.section.horizons",
            "desktop.home.horizons.summary",
            "desktop.home.button.open_horizons_public"
        ];

        foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages.Select(language => language.Code))
        {
            foreach (string key in keys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                Assert.IsFalse(string.IsNullOrWhiteSpace(localizedValue), $"Expected localized value for {key} / {languageCode}.");
                Assert.IsFalse(localizedValue.Contains("[en-US fallback]", StringComparison.Ordinal), $"Expected non-fallback horizon-home value for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Character_settings_notice_uses_dossier_language_in_primary_locales()
    {
        Assert.AreEqual(
            "Dossier settings updated.",
            DesktopLocalizationCatalog.GetRequiredString("desktop.dialog.character_settings.notice.updated", DesktopLocalizationCatalog.DefaultLanguage));
        Assert.AreEqual(
            "Dossier-Einstellungen wurden aktualisiert.",
            DesktopLocalizationCatalog.GetRequiredString("desktop.dialog.character_settings.notice.updated", "de-de"));
    }

    [TestMethod]
    public void Close_feedback_uses_dossier_language_across_shipping_locales()
    {
        Dictionary<string, string> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = "State: no active dossier to close.",
            ["de-de"] = "Status: Kein aktives Dossier zum Schließen.",
            ["fr-fr"] = "Etat: aucun dossier actif a fermer.",
            ["ja-jp"] = "状態: 閉じるアクティブなドシエはありません。",
            ["pt-br"] = "Status: nenhum dossie ativo para fechar.",
            ["zh-cn"] = "状态: 没有可关闭的活动档案。"
        };

        foreach ((string languageCode, string expectedValue) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedValue,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.feedback.no_active_workspace", languageCode),
                $"Expected dossier-focused close feedback for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_shell_actions_use_dossier_language_across_shipping_locales()
    {
        Dictionary<string, (string Save, string Close)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "Save Dossier",
                "Close Active Dossier"),
            ["de-de"] = (
                "Dossier speichern",
                "Aktives Dossier schließen"),
            ["fr-fr"] = (
                "Enregistrer le dossier",
                "Fermer le dossier actif"),
            ["ja-jp"] = (
                "ドシエを保存",
                "アクティブなドシエを閉じる"),
            ["pt-br"] = (
                "Salvar dossie",
                "Fechar dossie ativo"),
            ["zh-cn"] = (
                "保存档案",
                "关闭当前档案")
        };

        foreach ((string languageCode, (string expectedSave, string expectedClose)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedSave,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.save_workspace", languageCode),
                $"Expected dossier-focused save label for {languageCode}.");
            Assert.AreEqual(
                expectedClose,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.close_active_workspace", languageCode),
                $"Expected dossier-focused close label for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_shell_workspace_strip_uses_dossier_language_across_shipping_locales()
    {
        Dictionary<string, (string Heading, string Summary, string Empty)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "Current Dossier",
                "Dossier: {0} (open: {1}, {2})",
                "Dossier: none"),
            ["de-de"] = (
                "Aktuelles Dossier",
                "Dossier: {0} (offen: {1}, {2})",
                "Dossier: keines"),
            ["fr-fr"] = (
                "Dossier actuel",
                "Dossier: {0} (ouverts: {1}, {2})",
                "Dossier: aucun"),
            ["ja-jp"] = (
                "現在のドシエ",
                "ドシエ: {0} (オープン: {1}, {2})",
                "ドシエ: なし"),
            ["pt-br"] = (
                "Dossie atual",
                "Dossie: {0} (abertos: {1}, {2})",
                "Dossie: nenhum"),
            ["zh-cn"] = (
                "当前档案",
                "档案: {0} (已打开: {1}, {2})",
                "档案: 无")
        };

        foreach ((string languageCode, (string expectedHeading, string expectedSummary, string expectedEmpty)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedHeading,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.workspace_strip.heading", languageCode),
                $"Expected dossier-focused workspace-strip heading for {languageCode}.");
            Assert.AreEqual(
                expectedSummary,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.workspace_strip.summary", languageCode),
                $"Expected dossier-focused workspace-strip summary for {languageCode}.");
            Assert.AreEqual(
                expectedEmpty,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.workspace_strip.empty", languageCode),
                $"Expected dossier-focused workspace-strip empty state for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_shell_banner_and_snapshot_use_dossier_language_across_shipping_locales()
    {
        Dictionary<string, (string Banner, string Snapshot)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "Dossier Workbench",
                "State: {0}, dossier={1}, open={2}, saved={3}, last-command={4}"),
            ["de-de"] = (
                "Dossier Workbench",
                "Status: {0}, dossier={1}, offen={2}, gespeichert={3}, letzter-befehl={4}"),
            ["fr-fr"] = (
                "Dossier Workbench",
                "Etat: {0}, dossier={1}, ouvert={2}, sauvegarde={3}, derniere-commande={4}"),
            ["ja-jp"] = (
                "Dossier Workbench",
                "状態: {0}, ドシエ={1}, オープン={2}, 保存={3}, 前回コマンド={4}"),
            ["pt-br"] = (
                "Dossier Workbench",
                "Status: {0}, dossie={1}, aberto={2}, salvo={3}, ultimo-comando={4}"),
            ["zh-cn"] = (
                "Dossier Workbench",
                "状态: {0}, 档案={1}, 已打开={2}, 保存={3}, 上一命令={4}")
        };

        foreach ((string languageCode, (string expectedBanner, string expectedSnapshot)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedBanner,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.banner", languageCode),
                $"Expected dossier-focused shell banner for {languageCode}.");
            Assert.AreEqual(
                expectedSnapshot,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.snapshot", languageCode),
                $"Expected dossier-focused shell snapshot format for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_campaign_shell_labels_use_campaign_language_across_shipping_locales()
    {
        Dictionary<string, (string Tool, string Reviewed, string Title, string Heading)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "Campaign",
                "State: campaign reviewed.",
                "Campaign",
                "Campaign"),
            ["de-de"] = (
                "Kampagne",
                "Status: Kampagne geprüft.",
                "Kampagne",
                "Kampagne"),
            ["fr-fr"] = (
                "Campagne",
                "Etat: campagne verifiee.",
                "Campagne",
                "Campagne"),
            ["ja-jp"] = (
                "キャンペーン",
                "状態: キャンペーンを確認しました。",
                "キャンペーン",
                "キャンペーン"),
            ["pt-br"] = (
                "Campanha",
                "Status: campanha revisada.",
                "Campanha",
                "Campanha"),
            ["zh-cn"] = (
                "战役",
                "状态: 已查看战役。",
                "战役",
                "战役")
        };

        foreach ((string languageCode, (string expectedTool, string expectedReviewed, string expectedTitle, string expectedHeading)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedTool,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.campaign_workspace", languageCode),
                $"Expected campaign shell tool label for {languageCode}.");
            Assert.AreEqual(
                expectedReviewed,
                DesktopLocalizationCatalog.GetRequiredString("desktop.shell.feedback.campaign_workspace_reviewed", languageCode),
                $"Expected campaign reviewed feedback for {languageCode}.");
            Assert.AreEqual(
                expectedTitle,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.title", languageCode),
                $"Expected campaign title for {languageCode}.");
            Assert.AreEqual(
                expectedHeading,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.heading", languageCode),
                $"Expected campaign heading for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_campaign_intro_copy_uses_campaign_language_across_shipping_locales()
    {
        Dictionary<string, (string Guest, string LocalFallback, string Watchouts, string Ready)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "This campaign is still local-only. Claim this copy before you rely on restore, device status, or support.",
                "The campaign service is unavailable, so this view is showing the best local campaign summary and restore status available on this desktop.",
                "This campaign is ready to continue, but a few things still need review before you jump back into live session work.",
                "This campaign can reopen session context, runboard context, and support status from the desktop."),
            ["de-de"] = (
                "Diese Kampagne ist noch nur lokal. Beanspruche diese Kopie, bevor du Wiederherstellung, Gerätestatus oder Support nutzt.",
                "Der Kampagnendienst ist nicht verfügbar, daher zeigt diese Ansicht die beste lokale Kampagnenzusammenfassung und Wiederherstellung auf diesem Desktop.",
                "Diese Kampagne ist fast bereit, aber vor der Fortsetzung der Live-Sitzung müssen noch ein paar Punkte geprüft werden.",
                "Diese Kampagne kann Sitzungsstand, Runboard-Stand und Support-Status vom Desktop wieder öffnen."),
            ["fr-fr"] = (
                "Cette campagne est encore locale. Reclamez cette copie avant de compter sur la restauration, l'etat de l'appareil ou le support.",
                "Le service de campagne est indisponible, donc cette vue affiche le meilleur resume de campagne local et le meilleur statut de restauration disponible sur ce desktop.",
                "Cette campagne est prete a continuer, mais quelques points doivent encore etre verifies avant de reprendre le travail de session en direct.",
                "Cette campagne peut rouvrir le contexte de session, le contexte du runboard et le statut du support depuis le desktop."),
            ["ja-jp"] = (
                "このキャンペーンはまだローカル専用です。復元、デバイス状態、サポートを利用する前に、このコピーをリンクしてください。",
                "キャンペーンサービスを利用できないため、このビューではこのデスクトップで利用できる最善のローカルキャンペーン概要と復元状態を表示しています。",
                "このキャンペーンは継続可能ですが、ライブセッション作業に戻る前にいくつか確認事項があります。",
                "このキャンペーンは、デスクトップからセッション状況、ランボード状況、サポート状態を再開できます。"),
            ["pt-br"] = (
                "Esta campanha ainda esta apenas local. Reivindique esta copia antes de confiar em restauracao, status do dispositivo ou suporte.",
                "O servico de campanha esta indisponivel, entao esta tela mostra o melhor resumo local da campanha e o melhor status de restauracao disponivel neste desktop.",
                "Esta campanha esta pronta para continuar, mas alguns pontos ainda precisam ser revisados antes de retomar o trabalho de sessao ao vivo.",
                "Esta campanha pode reabrir o contexto da sessao, o contexto do runboard e o status de suporte a partir do desktop."),
            ["zh-cn"] = (
                "该战役仍仅限本地。请先绑定此副本，再依赖恢复、设备状态或支持。",
                "战役服务当前不可用，因此此视图会显示此桌面上可用的最佳本地战役摘要与恢复状态。",
                "该战役已可继续，但在恢复实时会话工作前仍有一些事项需要检查。",
                "该战役可从桌面重新打开会话上下文、运行看板上下文与支持状态。")
        };

        foreach ((string languageCode, (string expectedGuest, string expectedLocalFallback, string expectedWatchouts, string expectedReady)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedGuest,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.intro.guest", languageCode),
                $"Expected campaign guest intro for {languageCode}.");
            Assert.AreEqual(
                expectedLocalFallback,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.intro.local_fallback", languageCode),
                $"Expected campaign local fallback intro for {languageCode}.");
            Assert.AreEqual(
                expectedWatchouts,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.intro.watchouts", languageCode),
                $"Expected campaign watchouts intro for {languageCode}.");
            Assert.AreEqual(
                expectedReady,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.intro.ready", languageCode),
                $"Expected campaign ready intro for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_campaign_status_copy_uses_campaign_language_across_shipping_locales()
    {
        Dictionary<string, (string LocalFallback, string ServerGenerated, string RefreshFailed)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "Campaign status: local campaign data is shown because the live campaign service is unavailable.",
                "Campaign status: live service updated {0} UTC.",
                "Campaign status: refresh failed, so the last good local state is still shown."),
            ["de-de"] = (
                "Kampagnenstatus: Lokale Kampagnendaten werden angezeigt, weil der Live-Kampagnendienst nicht verfügbar ist.",
                "Kampagnenstatus: Live-Dienst aktualisiert um {0} UTC.",
                "Kampagnenstatus: Aktualisierung fehlgeschlagen, daher wird weiter der letzte gute lokale Stand angezeigt."),
            ["fr-fr"] = (
                "Statut campagne: des donnees de campagne locales sont affichees car le service de campagne en direct est indisponible.",
                "Statut campagne: le service en direct a ete mis a jour a {0} UTC.",
                "Statut campagne: le rafraichissement a echoue, donc le dernier bon etat local reste affiche."),
            ["ja-jp"] = (
                "キャンペーン状態: ライブのキャンペーンサービスを利用できないため、ローカルのキャンペーンデータを表示しています。",
                "キャンペーン状態: ライブサービスは {0} UTC に更新されました。",
                "キャンペーン状態: 更新に失敗したため、最後の正常なローカル状態を表示しています。"),
            ["pt-br"] = (
                "Status da campanha: os dados locais da campanha estao sendo exibidos porque o servico de campanha ao vivo esta indisponivel.",
                "Status da campanha: o servico ao vivo foi atualizado em {0} UTC.",
                "Status da campanha: a atualizacao falhou, entao o ultimo bom estado local continua exibido."),
            ["zh-cn"] = (
                "战役状态: 因实时战役服务不可用，当前显示本地战役数据。",
                "战役状态: 实时服务已于 {0} UTC 更新。",
                "战役状态: 刷新失败，因此仍显示最近一次有效的本地状态。")
        };

        foreach ((string languageCode, (string expectedLocalFallback, string expectedServerGenerated, string expectedRefreshFailed)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedLocalFallback,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.status.local_fallback", languageCode),
                $"Expected campaign local-fallback status for {languageCode}.");
            Assert.AreEqual(
                expectedServerGenerated,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.status.server_generated", languageCode),
                $"Expected campaign server-generated status for {languageCode}.");
            Assert.AreEqual(
                expectedRefreshFailed,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.status.refresh_failed", languageCode),
                $"Expected campaign refresh-failed status for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_home_recent_workspace_copy_uses_current_dossier_language_across_shipping_locales()
    {
        Dictionary<string, (string Intro, string Empty)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "This copy is linked, current enough to continue, and ready to reopen recent dossiers.",
                "No recent dossiers were restored yet. Import or create a dossier to get started."),
            ["de-de"] = (
                "Diese Kopie ist verknüpft, aktuell genug und bereit, zuletzt geöffnete Dossiers wieder zu öffnen.",
                "Es wurden noch keine zuletzt geöffneten Dossiers wiederhergestellt. Importiere oder erstelle ein Dossier, um zu beginnen."),
            ["fr-fr"] = (
                "Cette copie est liee, assez a jour pour continuer, et prete a rouvrir les dossiers recents.",
                "Aucun dossier recent n'a encore ete restaure. Importez ou creez un dossier pour commencer."),
            ["ja-jp"] = (
                "このコピーはリンク済みで継続可能で、最近のドシエを再度開く準備ができています。",
                "最近のドシエはまだ復元されていません。開始するにはドシエをインポートするか作成してください。"),
            ["pt-br"] = (
                "Esta copia esta vinculada, atual o suficiente para continuar e pronta para reabrir dossies recentes.",
                "Nenhum dossie recente foi restaurado ainda. Importe ou crie um dossie para comecar."),
            ["zh-cn"] = (
                "此副本已绑定且足够新，可以重新打开最近的档案。",
                "尚未恢复任何最近的档案。请导入或创建档案以开始。")
        };

        foreach ((string languageCode, (string expectedIntro, string expectedEmpty)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedIntro,
                DesktopLocalizationCatalog.GetRequiredString("desktop.home.intro.ready_recent_workspaces", languageCode),
                $"Expected dossier-focused intro copy for {languageCode}.");
            Assert.AreEqual(
                expectedEmpty,
                DesktopLocalizationCatalog.GetRequiredString("desktop.home.workspace_summary.empty", languageCode),
                $"Expected dossier-focused empty-state copy for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_home_action_labels_use_dossier_language_across_shipping_locales()
    {
        Dictionary<string, (string Recent, string Open, string Support, string Followthrough)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "Recent dossiers",
                "Open Dossier",
                "Get dossier help",
                "Open Dossier Help"),
            ["de-de"] = (
                "Letzte Dossiers",
                "Dossier öffnen",
                "Dossier-Hilfe öffnen",
                "Dossier-Hilfe öffnen"),
            ["fr-fr"] = (
                "Dossiers récents",
                "Ouvrir le dossier",
                "Obtenir de l'aide pour le dossier",
                "Ouvrir l'aide du dossier"),
            ["ja-jp"] = (
                "最近のドシエ",
                "ドシエを開く",
                "ドシエのサポートを開く",
                "ドシエのヘルプを開く"),
            ["pt-br"] = (
                "Dossies recentes",
                "Abrir dossie",
                "Obter ajuda do dossie",
                "Abrir ajuda do dossie"),
            ["zh-cn"] = (
                "最近的档案",
                "打开档案",
                "获取档案帮助",
                "打开档案帮助")
        };

        foreach ((string languageCode, (string expectedRecent, string expectedOpen, string expectedSupport, string expectedFollowthrough)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedRecent,
                DesktopLocalizationCatalog.GetRequiredString("desktop.home.section.recent_workspaces", languageCode),
                $"Expected dossier-focused recent section label for {languageCode}.");
            Assert.AreEqual(
                expectedOpen,
                DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_current_workspace", languageCode),
                $"Expected dossier-focused open action label for {languageCode}.");
            Assert.AreEqual(
                expectedSupport,
                DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_work_support", languageCode),
                $"Expected dossier-focused support label for {languageCode}.");
            Assert.AreEqual(
                expectedFollowthrough,
                DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_workspace_followthrough", languageCode),
                $"Expected dossier-focused follow-through label for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_campaign_restore_and_reopen_copy_uses_campaign_and_dossier_language_across_shipping_locales()
    {
        Dictionary<string, (string HomeIntro, string HomeOpen, string Recent, string Latest, string Empty)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "This copy is linked, current enough to continue, and ready to reopen the current campaign.",
                "Open Campaign",
                "Recent dossiers",
                "Latest local dossier: {0}. {1} UTC",
                "No local dossier is pinned yet, so restore status is limited to the campaign summary and linked-copy state."),
            ["de-de"] = (
                "Diese Kopie ist verknüpft, aktuell genug und bereit, die aktuelle Kampagne wieder zu öffnen.",
                "Kampagne öffnen",
                "Letzte Dossiers",
                "Letztes lokales Dossier: {0}. {1} UTC",
                "Noch ist kein lokales Dossier angeheftet, daher bleibt die Wiederherstellung auf Kampagnen-Zusammenfassung und verknüpfte Kopien begrenzt."),
            ["fr-fr"] = (
                "Cette copie est liee, assez a jour pour continuer, et prete a rouvrir la campagne actuelle.",
                "Ouvrir la campagne",
                "Dossiers récents",
                "Dernier dossier local: {0}. {1} UTC",
                "Aucun dossier local n'est epingle pour l'instant, donc le statut de restauration reste borne au digest de campagne et a la verite des appareils lies."),
            ["ja-jp"] = (
                "このコピーはリンク済みで継続可能で、現在のキャンペーンを再開できます。",
                "キャンペーンを開く",
                "最近のドシエ",
                "最新のローカルドシエ: {0}. {1} UTC",
                "ローカルドシエはまだ固定されていないため、復元姿勢はキャンペーンダイジェストと認証済みデバイス真実に制限されます。"),
            ["pt-br"] = (
                "Esta copia esta vinculada, atual o suficiente para continuar e pronta para reabrir a campanha atual.",
                "Abrir campanha",
                "Dossies recentes",
                "Dossie local mais recente: {0}. {1} UTC",
                "Nenhum dossie local foi fixado ainda, entao a postura de restauracao permanece limitada ao resumo da campanha e a verdade do dispositivo vinculado."),
            ["zh-cn"] = (
                "此副本已绑定且足够新，可以重新打开当前战役。",
                "打开战役",
                "最近的档案",
                "最近的本地档案: {0}. {1} UTC",
                "尚未固定任何本地档案，因此恢复姿态仍受限于战役摘要与已认领设备真相。")
        };

        foreach ((string languageCode, (string expectedHomeIntro, string expectedHomeOpen, string expectedRecent, string expectedLatest, string expectedEmpty)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedHomeIntro,
                DesktopLocalizationCatalog.GetRequiredString("desktop.home.intro.ready_current_campaign_workspace", languageCode),
                $"Expected campaign-focused reopen intro for {languageCode}.");
            Assert.AreEqual(
                expectedHomeOpen,
                DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_current_campaign_workspace", languageCode),
                $"Expected campaign-focused home action label for {languageCode}.");
            Assert.AreEqual(
                expectedRecent,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.section.recent_workspaces", languageCode),
                $"Expected dossier-focused campaign recents label for {languageCode}.");
            Assert.AreEqual(
                expectedLatest,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.restore.latest_workspace", languageCode),
                $"Expected dossier-focused campaign restore summary for {languageCode}.");
            Assert.AreEqual(
                expectedEmpty,
                DesktopLocalizationCatalog.GetRequiredString("desktop.campaign.restore.no_workspace", languageCode),
                $"Expected dossier-focused campaign restore empty-state copy for {languageCode}.");
        }
    }

    [TestMethod]
    public void Desktop_install_link_and_devices_copy_uses_dossier_language_across_shipping_locales()
    {
        Dictionary<string, (string Open, string Opened, string Unable, string Next, string Access)> expectedByLanguage = new(StringComparer.Ordinal)
        {
            [DesktopLocalizationCatalog.DefaultLanguage] = (
                "Open Linked Dossier",
                "Opened your dossier.",
                "Unable to open the dossier from this host.",
                "Next step: open your dossier.",
                "Support, updates, and dossier recovery are attached to this linked install."),
            ["de-de"] = (
                "Verknüpftes Dossier öffnen",
                "Dein Dossier wurde geöffnet.",
                "Das Dossier kann auf diesem Host nicht geöffnet werden.",
                "Nächster Schritt: Öffne dein Dossier.",
                "Support, Updates und Dossier-Wiederherstellung sind mit dieser verknüpften Installation verbunden."),
            ["fr-fr"] = (
                "Ouvrir le dossier lie",
                "Le dossier a ete ouvert.",
                "Impossible d'ouvrir le dossier depuis cet hote.",
                "Prochaine action sure: ouvrez votre dossier.",
                "Le support, les mises a jour et la restauration du dossier sont attaches a cette installation liee."),
            ["ja-jp"] = (
                "リンク済みドシエを開く",
                "ドシエを開きました。",
                "このホストではドシエを開けません。",
                "次のステップ: ドシエを開きます。",
                "サポート、更新、ドシエの復元はこのリンク済みインストールに関連付けられています。"),
            ["pt-br"] = (
                "Abrir dossie vinculado",
                "O dossie foi aberto.",
                "Nao foi possivel abrir o dossie neste host.",
                "Proximo passo: abrir o dossie.",
                "Suporte, atualizacoes e recuperacao do dossie estao associados a esta instalacao vinculada."),
            ["zh-cn"] = (
                "打开已关联档案",
                "已打开档案。",
                "此主机无法打开档案。",
                "下一步: 打开档案。",
                "支持、更新和档案恢复已附加到此已绑定安装。")
        };

        foreach ((string languageCode, (string expectedOpen, string expectedOpened, string expectedUnable, string expectedNext, string expectedAccess)) in expectedByLanguage)
        {
            Assert.AreEqual(
                expectedOpen,
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_work", languageCode),
                $"Expected dossier-focused install-link open label for {languageCode}.");
            Assert.AreEqual(
                expectedOpened,
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_work_route", languageCode),
                $"Expected dossier-focused install-link opened status for {languageCode}.");
            Assert.AreEqual(
                expectedUnable,
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.unable_open_work_route", languageCode),
                $"Expected dossier-focused install-link error status for {languageCode}.");
            Assert.AreEqual(
                expectedNext,
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.next_safe_action_claimed", languageCode),
                $"Expected dossier-focused install-link next step for {languageCode}.");
            Assert.AreEqual(
                expectedAccess,
                DesktopLocalizationCatalog.GetRequiredString("desktop.devices.context.access_claimed", languageCode),
                $"Expected dossier-focused linked-install access summary for {languageCode}.");
        }
    }

    [TestMethod]
    public void Product_area_hub_copy_does_not_leak_internal_horizon_or_lane_language()
    {
        string[] keys =
        [
            "desktop.shell.tool.horizons",
            "desktop.home.section.horizons",
            "desktop.home.horizons.summary",
            "desktop.horizons.title",
            "desktop.horizons.heading",
            "desktop.horizons.intro",
            "desktop.home.button.open_horizons_public"
        ];
        string[] blockedFragments =
        [
            "horizon",
            "lane",
            "レーン",
            "线路",
            "视界"
        ];

        foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages.Select(language => language.Code))
        {
            foreach (string key in keys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                foreach (string blockedFragment in blockedFragments)
                {
                    Assert.IsFalse(
                        localizedValue.Contains(blockedFragment, StringComparison.OrdinalIgnoreCase),
                        $"Expected user-facing product-area copy for {key} / {languageCode}, but found '{blockedFragment}' in '{localizedValue}'.");
                }
            }
        }
    }

    [TestMethod]
    public void Primary_desktop_copy_stays_human_facing_without_internal_release_or_support_jargon()
    {
        string[] primaryLanguages = [DesktopLocalizationCatalog.DefaultLanguage, "de-de"];
        string[] blockedFragments =
        [
            "proof",
            "evidence",
            "truth",
            "provider",
            "artifact",
            "posture",
            "lane",
            "rail",
            "verification",
            "validation",
            "audit",
            "smoke",
            "synthetic",
            "generated",
            "Beleg",
            "Wahrheit",
            "Verifikation"
        ];

        foreach (string languageCode in primaryLanguages)
        {
            foreach (string key in DesktopLocalizationCatalog.RequiredTrustSurfaceKeys())
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                foreach (string blockedFragment in blockedFragments)
                {
                    Assert.IsFalse(
                        ContainsBlockedTerm(localizedValue, blockedFragment),
                        $"Expected minimal human-facing copy for {key} / {languageCode}, but found '{blockedFragment}' in '{localizedValue}'.");
                }
            }
        }
    }

    [TestMethod]
    public void Support_and_update_copy_uses_plain_next_step_language()
    {
        string[] languages = [DesktopLocalizationCatalog.DefaultLanguage, "de-de"];
        string[] keys =
        [
            "desktop.update.section.follow_through",
            "desktop.support.section.follow_through",
            "desktop.support.section.diagnostics",
            "desktop.support.intro.action_needed",
            "desktop.support.follow_through.claimed",
            "desktop.support.follow_through.attention",
            "desktop.support_case.section.follow_through",
            "desktop.support_case.section.diagnostics",
            "desktop.support_case.intro.preview",
            "desktop.support_case.intro.action_needed",
            "desktop.support_case.follow_through.attention",
            "desktop.support_case.follow_through.verify"
        ];
        string[] blockedFragments =
        [
            "follow-through",
            "closure",
            "reporter-ready",
            "signed-in support",
            "flagship",
            "Nachverfolgung",
            "Abschluss",
            "reporter-bereit",
            "angemeldeten Support",
            "Flagship"
        ];

        foreach (string languageCode in languages)
        {
            foreach (string key in keys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                foreach (string blockedFragment in blockedFragments)
                {
                    Assert.IsFalse(
                        ContainsBlockedTerm(localizedValue, blockedFragment),
                        $"Expected plain support/update copy for {key} / {languageCode}, but found '{blockedFragment}' in '{localizedValue}'.");
                }
            }
        }
    }

    private static bool ContainsBlockedTerm(string value, string blockedFragment)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(blockedFragment))
        {
            return false;
        }

        string pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(blockedFragment)}(?![\p{{L}}\p{{N}}])";
        return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
