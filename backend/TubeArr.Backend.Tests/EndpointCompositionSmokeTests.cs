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
			app.InitializeDatabaseWithLogging();
			app.MapInitializeEndpoints();
			MapApiEndpointsViaReflection(app);

			await app.StartAsync();
			var client = app.GetTestClient();

			await AssertStatusAsync(client, "/initialize.json", HttpStatusCode.OK);
			var initializeBody = await client.GetStringAsync("/initialize.json");
			using var initializeDoc = JsonDocument.Parse(initializeBody);
			Assert.False(initializeDoc.RootElement.TryGetProperty("apiKey", out _));
			await AssertStatusAsync(client, "/api/v1/update", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/command", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/channels", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/queue/status", HttpStatusCode.OK);
			var deleteMissingQueue = await client.DeleteAsync("/api/v1/queue/999999");
			Assert.Equal(HttpStatusCode.NotFound, deleteMissingQueue.StatusCode);
			await AssertStatusAsync(client, "/api/v1/channels/editor", HttpStatusCode.OK);

			// Sonarr-compat stub routes are intentionally not mounted (no fake indexer/client/list data).
			await AssertStatusAsync(client, "/api/v1/indexer", HttpStatusCode.NotFound);
			await AssertStatusAsync(client, "/api/v1/downloadClient", HttpStatusCode.NotFound);
			await AssertStatusAsync(client, "/api/v1/importList", HttpStatusCode.NotFound);
			await AssertStatusAsync(client, "/api/v1/marketplace/listIndexer", HttpStatusCode.NotFound);
			await AssertStatusAsync(client, "/api/v1/releaseProfile", HttpStatusCode.NotFound);
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
			app.InitializeDatabaseWithLogging();
			app.MapInitializeEndpoints();
			MapApiEndpointsViaReflection(app);

			await app.StartAsync();
			var client = app.GetTestClient();

			// Test 400: POST import-exclusion without youtubeChannelId
			var missingChannelPayload = new StringContent(
				System.Text.Json.JsonSerializer.Serialize(new { title = "x" }),
				System.Text.Encoding.UTF8,
				"application/json");
			var missingChannelResponse = await client.PostAsync("/api/v1/import-exclusions", missingChannelPayload);
			Assert.Equal(HttpStatusCode.BadRequest, missingChannelResponse.StatusCode);

			// Test 400: POST import-exclusion with empty youtubeChannelId
			var emptyChannelPayload = new StringContent(
				System.Text.Json.JsonSerializer.Serialize(new { youtubeChannelId = "   ", title = "x" }),
				System.Text.Encoding.UTF8,
				"application/json");
			var emptyChannelResponse = await client.PostAsync("/api/v1/import-exclusions", emptyChannelPayload);
			Assert.Equal(HttpStatusCode.BadRequest, emptyChannelResponse.StatusCode);
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
