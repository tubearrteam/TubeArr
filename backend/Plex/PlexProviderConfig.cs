using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Plex;

internal sealed record PlexProviderConfig(
	bool Enabled,
	string BasePath,
	bool ExposeArtworkUrls,
	bool IncludeChildrenByDefault,
	bool VerboseRequestLogging)
{
	internal static async Task<PlexProviderConfig> GetAsync(TubeArrDbContext db, CancellationToken ct)
	{
		var row = await db.PlexProviderConfig.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (row is null)
		{
			row = new PlexProviderConfigEntity { Id = 1 };
			db.PlexProviderConfig.Add(row);
			await db.SaveChangesAsync(ct);
		}

		return new PlexProviderConfig(
			Enabled: row.Enabled,
			BasePath: (row.BasePath ?? "").Trim(),
			ExposeArtworkUrls: row.ExposeArtworkUrls,
			IncludeChildrenByDefault: row.IncludeChildrenByDefault,
			VerboseRequestLogging: row.VerboseRequestLogging);
	}
}

