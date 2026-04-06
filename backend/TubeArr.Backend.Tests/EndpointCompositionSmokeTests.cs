using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class EndpointCompositionSmokeTests
{
	[Fact]
	public async Task ComposedEndpoints_respond_for_core_routes()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var builder = WebApplication.CreateBuilder(new string[0]);
			builder.WebHost.UseTestServer();
			builder.Services.AddTubeArrServices($"Data Source={dbPath}");

			await using var app = builder.Build();
			TubeArrAppPaths.ContentRoot = Path.GetDirectoryName(dbPath) ?? Path.GetTempPath();
			app.InitializeDatabaseWithLogging();
			app.MapInitializeEndpoints();
			MapApiEndpointsViaReflection(app);

			await app.StartAsync();
			var client = app.GetTestClient();

			await AssertStatusAsync(client, "/initialize.json", HttpStatusCode.OK);
			var initializeBody = await client.GetStringAsync("/initialize.json");
			using var initializeDoc = JsonDocument.Parse(initializeBody);
			Assert.True(initializeDoc.RootElement.TryGetProperty("apiKeyRequired", out var apiKeyRequiredEl));
			Assert.False(apiKeyRequiredEl.GetBoolean());
			Assert.True(initializeDoc.RootElement.TryGetProperty("apiKey", out var apiKeyEl));
			Assert.False(string.IsNullOrEmpty(apiKeyEl.GetString()));
			await AssertStatusAsync(client, "/api/v1/update", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/command", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/channels", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/queue/status", HttpStatusCode.OK);
			var deleteMissingQueue = await client.DeleteAsync("/api/v1/queue/999999");
			Assert.Equal(HttpStatusCode.NotFound, deleteMissingQueue.StatusCode);
			await AssertStatusAsync(client, "/api/v1/channels/editor", HttpStatusCode.OK);

			// Sonarr-compat stub routes are intentionally not mounted (no fake indexer/client data).
			await AssertStatusAsync(client, "/api/v1/indexer", HttpStatusCode.NotFound);
			await AssertStatusAsync(client, "/api/v1/downloadClient", HttpStatusCode.NotFound);
			await AssertStatusAsync(client, "/api/v1/tag", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/marketplace/listIndexer", HttpStatusCode.NotFound);
			await AssertStatusAsync(client, "/api/v1/releaseProfile", HttpStatusCode.NotFound);

			await AssertStatusAsync(client, "/api/v1/health", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/notification", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/system/task/history", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/debug/plex/match-traces", HttpStatusCode.OK);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	[Fact]
	public async Task ComposedEndpoints_return_errors_for_invalid_requests()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var builder = WebApplication.CreateBuilder(new string[0]);
			builder.WebHost.UseTestServer();
			builder.Services.AddTubeArrServices($"Data Source={dbPath}");

			await using var app = builder.Build();
			TubeArrAppPaths.ContentRoot = Path.GetDirectoryName(dbPath) ?? Path.GetTempPath();
			app.InitializeDatabaseWithLogging();
			app.MapInitializeEndpoints();
			MapApiEndpointsViaReflection(app);

			await app.StartAsync();
			var client = app.GetTestClient();

			// Test 400: POST tag with empty label
			var emptyTagPayload = new StringContent(
				JsonSerializer.Serialize(new { label = "   " }),
				System.Text.Encoding.UTF8,
				"application/json");
			var emptyTagResponse = await client.PostAsync("/api/v1/tag", emptyTagPayload);
			Assert.Equal(HttpStatusCode.BadRequest, emptyTagResponse.StatusCode);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	static void MapApiEndpointsViaReflection(WebApplication app)
	{
		var englishStringsLazy = new Lazy<IReadOnlyDictionary<string, string>>(() => new Dictionary<string, string>());

		var composerType = typeof(InitializeEndpoints).Assembly.GetType("TubeArr.Backend.ApiEndpointComposer");
		Assert.NotNull(composerType);

		var mapMethod = composerType!.GetMethod("MapTubeArrApiEndpoints", BindingFlags.Static | BindingFlags.NonPublic);
		Assert.NotNull(mapMethod);

		mapMethod!.Invoke(null,
		[
			app,
			"",
			englishStringsLazy
		]);
	}

	static async Task AssertStatusAsync(System.Net.Http.HttpClient client, string path, HttpStatusCode expected)
	{
		var response = await client.GetAsync(path);
		Assert.Equal(expected, response.StatusCode);
	}

	static string CreateTempDbPath()
	{
		var root = Path.Combine(Path.GetTempPath(), "TubeArrTests");
		Directory.CreateDirectory(root);
		return Path.Combine(root, $"endpoint-smoke-{Guid.NewGuid():N}.sqlite");
	}

	static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch
		{
			// Best-effort cleanup.
		}
	}
}
