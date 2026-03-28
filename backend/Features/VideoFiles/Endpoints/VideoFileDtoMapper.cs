using System.Text.Json;
using System.Text.Json.Serialization;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class VideoFileDtoMapper
{
	static readonly JsonSerializerOptions ProbeJsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	internal static VideoFileDto ToDto(
		VideoFileEntity vf,
		int playlistNumber,
		VideoFileQualityWrapper defaultQuality)
	{
		var payload = TryParsePayload(vf.MediaInfoJson);
		object[] customFormats = Array.Empty<object>();
		if (payload?.CustomFormats is { Length: > 0 })
		{
			customFormats = new object[payload.CustomFormats.Length];
			for (var i = 0; i < payload.CustomFormats.Length; i++)
			{
				var f = payload.CustomFormats[i];
				customFormats[i] = new { f.Id, f.Name };
			}
		}

		return new VideoFileDto(
			Id: vf.Id,
			ChannelId: vf.ChannelId,
			PlaylistNumber: playlistNumber,
			Path: vf.Path,
			RelativePath: vf.RelativePath,
			Size: vf.Size,
			FileDurationSeconds: payload?.DurationSeconds,
			DateAdded: vf.DateAdded,
			ReleaseGroup: "",
			Languages: Array.Empty<object>(),
			Quality: defaultQuality,
			CustomFormats: customFormats,
			CustomFormatScore: 0,
			IndexerFlags: 0,
			ReleaseType: "",
			MediaInfo: payload?.MediaInfo,
			QualityCutoffNotMet: false
		);
	}

	internal static VideoFileMediaProbePayload? TryParsePayload(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return null;
		try
		{
			return JsonSerializer.Deserialize<VideoFileMediaProbePayload>(json, ProbeJsonOptions);
		}
		catch
		{
			return null;
		}
	}
}
