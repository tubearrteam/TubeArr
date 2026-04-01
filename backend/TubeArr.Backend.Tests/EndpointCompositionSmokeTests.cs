using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;
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
			await AssertStatusAsync(client, "/api/v1/update", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/command", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/channels", HttpStatusCode.OK);
			await AssertStatusAsync(client, "/api/v1/queue/status", HttpStatusCode.OK);
			var deleteMissingQueue = await client.DeleteAsync("/api/v1/queue/999999");
			Assert.Equal(HttpStatusCode.NotFound, deleteMissingQueue.StatusCode);
			await AssertStatusAsync(client, "/api/v1/channels/editor", HttpStatusCode.OK);
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

			// Test 400: POST import-exclusion with empty title
			var badImportExclusionPayload = new StringContent(
				System.Text.Json.JsonSerializer.Serialize(new { title = "" }),
				System.Text.Encoding.UTF8,
				"application/json");
			var badImportResponse = await client.PostAsync("/api/v1/import-exclusions", badImportExclusionPayload);
			Assert.Equal(HttpStatusCode.BadRequest, badImportResponse.StatusCode);

			// Test 400: POST import-exclusion with missing title entirely
			var missingTitlePayload = new StringContent(
				System.Text.Json.JsonSerializer.Serialize(new { }),
				System.Text.Encoding.UTF8,
				"application/json");
			var missingTitleResponse = await client.PostAsync("/api/v1/import-exclusions", missingTitlePayload);
			Assert.Equal(HttpStatusCode.BadRequest, missingTitleResponse.StatusCode);
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
