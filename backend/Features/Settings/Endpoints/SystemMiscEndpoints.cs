using Microsoft.AspNetCore.Builder;
using System.Text.Json;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static partial class SystemMiscEndpoints
{
	public static void Map(
		RouteGroupBuilder api,
		string preloadedUrlBase,
		Lazy<IReadOnlyDictionary<string, string>> englishStringsLazy)
	{
		api.MapGet("/system/status", async (TubeArrDbContext db) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);

			return Results.Json(new Dictionary<string, object?>
			{
				["version"] = "0.0.0-dev",
				["buildTime"] = "2026-01-01T00:00:00Z",
				["isDebug"] = true,
				["isProduction"] = false,
				["isAdmin"] = true,
				["appName"] = "TubeArr",
				["instanceName"] = serverSettings.InstanceName ?? "",
				["isWindows"] = OperatingSystem.IsWindows(),
				["mode"] = "console",
				["packageUpdateMechanism"] = "builtIn"
			});
		});

		api.MapGet("/system/routes", () => Results.Json(new Dictionary<string, object?>
		{
			["root"] = string.IsNullOrWhiteSpace(preloadedUrlBase) ? "/" : $"{preloadedUrlBase}/",
			["apiRoot"] = string.IsNullOrWhiteSpace(preloadedUrlBase) ? "/api/v1" : $"{preloadedUrlBase}/api/v1"
		}));

		api.MapGet("/system/task", () =>
		{
			var tasks = ScheduledTaskCatalog.GetScheduledTaskDtos();
			return Results.Json(tasks);
		});

		api.MapGet("/system/task/{id:int}", (int id) =>
		{
			var tasks = ScheduledTaskCatalog.GetScheduledTaskDtos();
			var task = tasks.FirstOrDefault(t => t.Id == id);
			return task is null ? Results.NotFound() : Results.Json(task);
		});

		MapRootFolderAndFilesystemEndpoints(api);
		MapLocalizationAndNotificationEndpoints(api, englishStringsLazy);
	}

	static partial void MapRootFolderAndFilesystemEndpoints(RouteGroupBuilder api);

	static partial void MapLocalizationAndNotificationEndpoints(
		RouteGroupBuilder api,
		Lazy<IReadOnlyDictionary<string, string>> englishStringsLazy);

	private static string BuildNotificationSchemaJson()
	{
		static Dictionary<string, object?> NotificationSchemaItem(string implementation, string implementationName, string infoLink, IList<Dictionary<string, object?>>? fields = null)
		{
			var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
			{
				["implementation"] = implementation,
				["implementationName"] = implementationName,
				["infoLink"] = infoLink,
				["presets"] = Array.Empty<object>(),
				["supportsOnGrab"] = true,
				["supportsOnDownload"] = true,
				["supportsOnUpgrade"] = true,
				["supportsOnImportComplete"] = true,
				["supportsOnRename"] = true,
				["supportsOnChannelAdd"] = true,
				["supportsOnChannelDelete"] = true,
				["supportsOnVideoFileDelete"] = true,
				["supportsOnVideoFileDeleteForUpgrade"] = true,
				["supportsOnHealthIssue"] = true,
				["supportsOnHealthRestored"] = true,
				["supportsOnApplicationUpdate"] = true,
				["supportsOnManualInteractionRequired"] = true
			};
			if (fields?.Count > 0)
				item["fields"] = fields;
			return item;
		}

		static Dictionary<string, object?> NotificationField(string name, string label, string type, object? value = null, string? helpText = null, object? selectOptions = null)
		{
			var field = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
			{
				["name"] = name,
				["label"] = label,
				["type"] = type,
				["value"] = value,
				["advanced"] = false
			};
			if (helpText != null)
				field["helpText"] = helpText;
			if (selectOptions != null)
				field["selectOptions"] = selectOptions;
			return field;
		}

		// Apprise: serverUrl, configurationKey, statelessUrls, notificationType, tags, authUsername, authPassword
		var appriseFields = new List<Dictionary<string, object?>>
		{
			NotificationField("serverUrl", "NotificationsSettingsAppriseServerUrl", "textbox", ""),
			NotificationField("configurationKey", "NotificationsSettingsAppriseConfigurationKey", "textbox", ""),
			NotificationField("statelessUrls", "NotificationsSettingsAppriseStatelessUrls", "textbox", ""),
			NotificationField("notificationType", "NotificationsSettingsAppriseNotificationType", "textbox", ""),
			NotificationField("tags", "NotificationsSettingsAppriseTags", "textbox", ""),
			NotificationField("authUsername", "NotificationsSettingsAppriseAuthUsername", "textbox", ""),
			NotificationField("authPassword", "NotificationsSettingsAppriseAuthPassword", "password", "")
		};

		// Custom Script: path, legacy hidden arguments
		var customScriptFields = new List<Dictionary<string, object?>>
		{
			NotificationField("path", "NotificationsSettingsCustomScriptPath", "textbox", "")
		};

		// Discord: webHookUrl, username, avatar, author, grabFields, importFields, manualInteractionFields
		var discordFields = new List<Dictionary<string, object?>>
		{
			NotificationField("webHookUrl", "NotificationsSettingsDiscordWebHookUrl", "textbox", ""),
			NotificationField("username", "NotificationsSettingsDiscordUsername", "textbox", ""),
			NotificationField("avatar", "NotificationsSettingsDiscordAvatar", "textbox", ""),
			NotificationField("author", "NotificationsSettingsDiscordAuthor", "textbox", ""),
			NotificationField("grabFields", "NotificationsSettingsDiscordGrabFields", "keyValueList", null),
			NotificationField("importFields", "NotificationsSettingsDiscordImportFields", "keyValueList", null),
			NotificationField("manualInteractionFields", "NotificationsSettingsDiscordManualInteractionFields", "keyValueList", null)
		};

		// Email: server, port, useEncryption, username, password, from, to, cc, bcc
		var emailFields = new List<Dictionary<string, object?>>
		{
			NotificationField("server", "NotificationsSettingsEmailServer", "textbox", ""),
			NotificationField("port", "NotificationsSettingsEmailPort", "number", 587),
			NotificationField("useEncryption", "NotificationsSettingsEmailUseEncryption", "checkbox", true),
			NotificationField("username", "NotificationsSettingsEmailUsername", "textbox", ""),
			NotificationField("password", "NotificationsSettingsEmailPassword", "password", ""),
			NotificationField("from", "NotificationsSettingsEmailFrom", "textbox", ""),
			NotificationField("to", "NotificationsSettingsEmailTo", "textbox", ""),
			NotificationField("cc", "NotificationsSettingsEmailCc", "textbox", ""),
			NotificationField("bcc", "NotificationsSettingsEmailBcc", "textbox", "")
		};

		// Gotify: server, appToken, priority, includeSeriesPoster, metadataLinks, preferredMetadataLink
		var gotifyFields = new List<Dictionary<string, object?>>
		{
			NotificationField("server", "NotificationsSettingsGotifyServer", "textbox", ""),
			NotificationField("appToken", "NotificationsSettingsGotifyAppToken", "password", ""),
			NotificationField("priority", "NotificationsSettingsGotifyPriority", "number", 0),
			NotificationField("includeSeriesPoster", "NotificationsSettingsGotifyIncludeSeriesPoster", "checkbox", false),
			NotificationField("metadataLinks", "NotificationsSettingsGotifyMetadataLinks", "textbox", ""),
			NotificationField("preferredMetadataLink", "NotificationsSettingsGotifyPreferredMetadataLink", "textbox", "")
		};

		// Join: apiKey, legacy deviceIds, deviceNames, priority
		var joinFields = new List<Dictionary<string, object?>>
		{
			NotificationField("apiKey", "NotificationsSettingsJoinApiKey", "password", ""),
			NotificationField("deviceNames", "NotificationsSettingsJoinDeviceNames", "textbox", ""),
			NotificationField("priority", "NotificationsSettingsJoinPriority", "number", 0)
		};

		// Mailgun: apiKey, useEuEndpoint, from, senderDomain, recipients
		var mailgunFields = new List<Dictionary<string, object?>>
		{
			NotificationField("apiKey", "NotificationsSettingsMailgunApiKey", "password", ""),
			NotificationField("useEuEndpoint", "NotificationsSettingsMailgunUseEuEndpoint", "checkbox", false),
			NotificationField("from", "NotificationsSettingsMailgunFrom", "textbox", ""),
			NotificationField("senderDomain", "NotificationsSettingsMailgunSenderDomain", "textbox", ""),
			NotificationField("recipients", "NotificationsSettingsMailgunRecipients", "textbox", "")
		};

		// Emby/Jellyfin (MediaBrowser): host, port, useSsl, urlBase, apiKey, notify, updateLibrary, mapFrom, mapTo
		var embyJellyfinFields = new List<Dictionary<string, object?>>
		{
			NotificationField("host", "NotificationsSettingsEmbyJellyfinHost", "textbox", ""),
			NotificationField("port", "NotificationsSettingsEmbyJellyfinPort", "number", 8096),
			NotificationField("useSsl", "NotificationsSettingsEmbyJellyfinUseSsl", "checkbox", false),
			NotificationField("urlBase", "NotificationsSettingsEmbyJellyfinUrlBase", "textbox", ""),
			NotificationField("apiKey", "NotificationsSettingsEmbyJellyfinApiKey", "password", ""),
			NotificationField("notify", "NotificationsSettingsEmbyJellyfinNotify", "checkbox", true),
			NotificationField("updateLibrary", "NotificationsSettingsEmbyJellyfinUpdateLibrary", "checkbox", true),
			NotificationField("mapFrom", "NotificationsSettingsEmbyJellyfinMapFrom", "textbox", ""),
			NotificationField("mapTo", "NotificationsSettingsEmbyJellyfinMapTo", "textbox", "")
		};

		// Notifiarr: apiKey
		var notifiarrFields = new List<Dictionary<string, object?>>
		{
			NotificationField("apiKey", "NotificationsSettingsNotifiarrApiKey", "password", "")
		};

		// ntfy: serverUrl, accessToken, userName, password, priority, topics, tags, clickUrl
		var ntfyFields = new List<Dictionary<string, object?>>
		{
			NotificationField("serverUrl", "NotificationsSettingsNtfyServerUrl", "textbox", ""),
			NotificationField("accessToken", "NotificationsSettingsNtfyAccessToken", "password", ""),
			NotificationField("userName", "NotificationsSettingsNtfyUserName", "textbox", ""),
			NotificationField("password", "NotificationsSettingsNtfyPassword", "password", ""),
			NotificationField("priority", "NotificationsSettingsNtfyPriority", "number", 0),
			NotificationField("topics", "NotificationsSettingsNtfyTopics", "textbox", ""),
			NotificationField("tags", "NotificationsSettingsNtfyTags", "textbox", ""),
			NotificationField("clickUrl", "NotificationsSettingsNtfyClickUrl", "textbox", "")
		};

		// Prowl: apiKey, priority
		var prowlFields = new List<Dictionary<string, object?>>
		{
			NotificationField("apiKey", "NotificationsSettingsProwlApiKey", "password", ""),
			NotificationField("priority", "NotificationsSettingsProwlPriority", "number", 0)
		};

		// Pushbullet: apiKey, deviceIds, channelTags, senderId
		var pushbulletFields = new List<Dictionary<string, object?>>
		{
			NotificationField("apiKey", "NotificationsSettingsPushbulletApiKey", "password", ""),
			NotificationField("deviceIds", "NotificationsSettingsPushbulletDeviceIds", "textbox", ""),
			NotificationField("channelTags", "NotificationsSettingsPushbulletChannelTags", "textbox", ""),
			NotificationField("senderId", "NotificationsSettingsPushbulletSenderId", "textbox", "")
		};

		// Pushcut: notificationName, apiKey, timeSensitive
		var pushcutFields = new List<Dictionary<string, object?>>
		{
			NotificationField("notificationName", "NotificationsSettingsPushcutNotificationName", "textbox", ""),
			NotificationField("apiKey", "NotificationsSettingsPushcutApiKey", "password", ""),
			NotificationField("timeSensitive", "NotificationsSettingsPushcutTimeSensitive", "checkbox", false)
		};

		// Pushover: apiKey, userKey, devices, priority, retry, expire, sound
		var pushoverFields = new List<Dictionary<string, object?>>
		{
			NotificationField("apiKey", "NotificationsSettingsPushoverApiKey", "password", ""),
			NotificationField("userKey", "NotificationsSettingsPushoverUserKey", "password", ""),
			NotificationField("devices", "NotificationsSettingsPushoverDevices", "textbox", ""),
			NotificationField("priority", "NotificationsSettingsPushoverPriority", "number", 0),
			NotificationField("retry", "NotificationsSettingsPushoverRetry", "number", 60),
			NotificationField("expire", "NotificationsSettingsPushoverExpire", "number", 3600),
			NotificationField("sound", "NotificationsSettingsPushoverSound", "textbox", "")
		};

		// SendGrid: baseUrl, apiKey, from, recipients
		var sendGridFields = new List<Dictionary<string, object?>>
		{
			NotificationField("baseUrl", "NotificationsSettingsSendGridBaseUrl", "textbox", ""),
			NotificationField("apiKey", "NotificationsSettingsSendGridApiKey", "password", ""),
			NotificationField("from", "NotificationsSettingsSendGridFrom", "textbox", ""),
			NotificationField("recipients", "NotificationsSettingsSendGridRecipients", "textbox", "")
		};

		// Signal: host, port, useSsl, senderNumber, receiverId, authUsername, authPassword
		var signalFields = new List<Dictionary<string, object?>>
		{
			NotificationField("host", "NotificationsSettingsSignalHost", "textbox", ""),
			NotificationField("port", "NotificationsSettingsSignalPort", "number", 8080),
			NotificationField("useSsl", "NotificationsSettingsSignalUseSsl", "checkbox", false),
			NotificationField("senderNumber", "NotificationsSettingsSignalSenderNumber", "textbox", ""),
			NotificationField("receiverId", "NotificationsSettingsSignalReceiverId", "textbox", ""),
			NotificationField("authUsername", "NotificationsSettingsSignalAuthUsername", "textbox", ""),
			NotificationField("authPassword", "NotificationsSettingsSignalAuthPassword", "password", "")
		};

		// Simplepush: key, event
		var simplepushFields = new List<Dictionary<string, object?>>
		{
			NotificationField("key", "NotificationsSettingsSimplepushKey", "password", ""),
			NotificationField("event", "NotificationsSettingsSimplepushEvent", "textbox", "")
		};

		// Slack: webHookUrl, username, icon, channel
		var slackFields = new List<Dictionary<string, object?>>
		{
			NotificationField("webHookUrl", "NotificationsSettingsSlackWebHookUrl", "textbox", ""),
			NotificationField("username", "NotificationsSettingsSlackUsername", "textbox", ""),
			NotificationField("icon", "NotificationsSettingsSlackIcon", "textbox", ""),
			NotificationField("channel", "NotificationsSettingsSlackChannel", "textbox", "")
		};

		// Telegram: botToken, chatId, topicId, sendSilently, includeAppNameInTitle, includeInstanceNameInTitle, metadataLinks
		var telegramFields = new List<Dictionary<string, object?>>
		{
			NotificationField("botToken", "NotificationsSettingsTelegramBotToken", "password", ""),
			NotificationField("chatId", "NotificationsSettingsTelegramChatId", "textbox", ""),
			NotificationField("topicId", "NotificationsSettingsTelegramTopicId", "textbox", ""),
			NotificationField("sendSilently", "NotificationsSettingsTelegramSendSilently", "checkbox", false),
			NotificationField("includeAppNameInTitle", "NotificationsSettingsTelegramIncludeAppNameInTitle", "checkbox", true),
			NotificationField("includeInstanceNameInTitle", "NotificationsSettingsTelegramIncludeInstanceNameInTitle", "checkbox", true),
			NotificationField("metadataLinks", "NotificationsSettingsTelegramMetadataLinks", "textbox", "")
		};

		// Trakt: accessToken, refreshToken, expires, authUser, signIn
		var traktFields = new List<Dictionary<string, object?>>
		{
			NotificationField("accessToken", "NotificationsSettingsTraktAccessToken", "password", ""),
			NotificationField("refreshToken", "NotificationsSettingsTraktRefreshToken", "password", ""),
			NotificationField("expires", "NotificationsSettingsTraktExpires", "textbox", ""),
			NotificationField("authUser", "NotificationsSettingsTraktAuthUser", "textbox", ""),
			NotificationField("signIn", "NotificationsSettingsTraktSignIn", "textbox", "")
		};

		// Twitter/X: consumerKey, consumerSecret, accessToken, accessTokenSecret, mention, directMessage, authorizeNotification
		var twitterFields = new List<Dictionary<string, object?>>
		{
			NotificationField("consumerKey", "NotificationsSettingsTwitterConsumerKey", "textbox", ""),
			NotificationField("consumerSecret", "NotificationsSettingsTwitterConsumerSecret", "password", ""),
			NotificationField("accessToken", "NotificationsSettingsTwitterAccessToken", "password", ""),
			NotificationField("accessTokenSecret", "NotificationsSettingsTwitterAccessTokenSecret", "password", ""),
			NotificationField("mention", "NotificationsSettingsTwitterMention", "textbox", ""),
			NotificationField("directMessage", "NotificationsSettingsTwitterDirectMessage", "checkbox", false),
			NotificationField("authorizeNotification", "NotificationsSettingsTwitterAuthorizeNotification", "textbox", "")
		};

		// Webhook: url, method, username, password, headers
		var webhookFields = new List<Dictionary<string, object?>>
		{
			NotificationField("url", "NotificationsSettingsWebhookUrl", "textbox", "", "NotificationsSettingsWebhookUrlHelpText"),
			NotificationField("method", "NotificationsSettingsWebhookMethod", "select", "POST", "NotificationsSettingsWebhookMethodHelpText", new[]
			{
				new { value = "POST", name = "POST" },
				new { value = "PUT", name = "PUT" },
				new { value = "GET", name = "GET" }
			}),
			NotificationField("username", "NotificationsSettingsWebhookUsername", "textbox", ""),
			NotificationField("password", "NotificationsSettingsWebhookPassword", "password", ""),
			NotificationField("headers", "NotificationsSettingsWebhookHeaders", "keyValueList", null, null)
		};

		// Kodi/XBMC: host, port, useSsl, urlBase, username, password, displayTime, notify, updateLibrary, cleanLibrary, alwaysUpdate
		var kodiFields = new List<Dictionary<string, object?>>
		{
			NotificationField("host", "NotificationsSettingsKodiHost", "textbox", ""),
			NotificationField("port", "NotificationsSettingsKodiPort", "number", 8080),
			NotificationField("useSsl", "NotificationsSettingsKodiUseSsl", "checkbox", false),
			NotificationField("urlBase", "NotificationsSettingsKodiUrlBase", "textbox", ""),
			NotificationField("username", "NotificationsSettingsKodiUsername", "textbox", ""),
			NotificationField("password", "NotificationsSettingsKodiPassword", "password", ""),
			NotificationField("displayTime", "NotificationsSettingsKodiDisplayTime", "number", 5),
			NotificationField("notify", "NotificationsSettingsKodiNotify", "checkbox", true),
			NotificationField("updateLibrary", "NotificationsSettingsKodiUpdateLibrary", "checkbox", true),
			NotificationField("cleanLibrary", "NotificationsSettingsKodiCleanLibrary", "checkbox", false),
			NotificationField("alwaysUpdate", "NotificationsSettingsKodiAlwaysUpdate", "checkbox", false)
		};

		var schema = new List<Dictionary<string, object?>>
		{
			NotificationSchemaItem("Apprise", "Apprise", "https://wiki.servarr.com/tubearr/settings#connections-apprise", appriseFields),
			NotificationSchemaItem("CustomScript", "Custom Script", "https://wiki.servarr.com/tubearr/settings#connections-custom-script", customScriptFields),
			NotificationSchemaItem("Discord", "Discord", "https://wiki.servarr.com/tubearr/settings#connections-discord", discordFields),
			NotificationSchemaItem("Email", "Email", "https://wiki.servarr.com/tubearr/settings#connections-email", emailFields),
			NotificationSchemaItem("EmbyJellyfin", "Emby / Jellyfin", "https://wiki.servarr.com/tubearr/settings#connections-emby-jellyfin", embyJellyfinFields),
			NotificationSchemaItem("Gotify", "Gotify", "https://wiki.servarr.com/tubearr/settings#connections-gotify", gotifyFields),
			NotificationSchemaItem("Join", "Join", "https://wiki.servarr.com/tubearr/settings#connections-join", joinFields),
			NotificationSchemaItem("Kodi", "Kodi", "https://wiki.servarr.com/tubearr/settings#connections-kodi", kodiFields),
			NotificationSchemaItem("Mailgun", "Mailgun", "https://wiki.servarr.com/tubearr/settings#connections-mailgun", mailgunFields),
			NotificationSchemaItem("Notifiarr", "Notifiarr", "https://wiki.servarr.com/tubearr/settings#connections-notifiarr", notifiarrFields),
			NotificationSchemaItem("Ntfy", "ntfy.sh", "https://wiki.servarr.com/tubearr/settings#connections-ntfy", ntfyFields),
			NotificationSchemaItem("PlexMediaServer", "Plex Media Server", "https://wiki.servarr.com/tubearr/settings#connections-plex"),
			NotificationSchemaItem("Prowl", "Prowl", "https://wiki.servarr.com/tubearr/settings#connections-prowl", prowlFields),
			NotificationSchemaItem("Pushbullet", "Pushbullet", "https://wiki.servarr.com/tubearr/settings#connections-pushbullet", pushbulletFields),
			NotificationSchemaItem("Pushcut", "Pushcut", "https://wiki.servarr.com/tubearr/settings#connections-pushcut", pushcutFields),
			NotificationSchemaItem("Pushover", "Pushover", "https://wiki.servarr.com/tubearr/settings#connections-pushover", pushoverFields),
			NotificationSchemaItem("SendGrid", "SendGrid", "https://wiki.servarr.com/tubearr/settings#connections-sendgrid", sendGridFields),
			NotificationSchemaItem("Signal", "Signal", "https://wiki.servarr.com/tubearr/settings#connections-signal", signalFields),
			NotificationSchemaItem("Simplepush", "Simplepush", "https://wiki.servarr.com/tubearr/settings#connections-simplepush", simplepushFields),
			NotificationSchemaItem("Slack", "Slack", "https://wiki.servarr.com/tubearr/settings#connections-slack", slackFields),
			NotificationSchemaItem("SynologyIndexer", "Synology Indexer", "https://wiki.servarr.com/tubearr/settings#connections-synology-indexer"),
			NotificationSchemaItem("Telegram", "Telegram", "https://wiki.servarr.com/tubearr/settings#connections-telegram", telegramFields),
			NotificationSchemaItem("Trakt", "Trakt", "https://wiki.servarr.com/tubearr/settings#connections-trakt", traktFields),
			NotificationSchemaItem("Twitter", "Twitter", "https://wiki.servarr.com/tubearr/settings#connections-twitter", twitterFields),
			NotificationSchemaItem("Webhook", "Webhook", "https://wiki.servarr.com/tubearr/settings#connections-webhook", webhookFields)
		};
		return JsonSerializer.Serialize(schema);
	}
}

