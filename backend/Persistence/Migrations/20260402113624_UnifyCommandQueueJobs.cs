using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations;

/// <inheritdoc />
public partial class UnifyCommandQueueJobs : Migration
{
	const string JobCatSql = @"
CASE JobType
	WHEN 'RefreshChannel' THEN 'metadata'
	WHEN 'GetVideoDetails' THEN 'metadata'
	WHEN 'GetChannelPlaylists' THEN 'metadata'
	WHEN 'RssSync' THEN 'metadata'
	WHEN 'RenameFiles' THEN 'fileops'
	WHEN 'RenameChannel' THEN 'fileops'
	WHEN 'MapUnmappedVideoFiles' THEN 'fileops'
	WHEN 'SyncCustomNfos' THEN 'dbops'
	WHEN 'RepairLibraryNfosAndArtwork' THEN 'dbops'
	ELSE NULL
END";

	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(
			name: "Category",
			table: "CommandQueueJobs",
			type: "TEXT",
			nullable: true);

		migrationBuilder.AddColumn<int>(
			name: "ChannelId",
			table: "CommandQueueJobs",
			type: "INTEGER",
			nullable: true);

		migrationBuilder.CreateIndex(
			name: "IX_CommandQueueJobs_Category",
			table: "CommandQueueJobs",
			column: "Category");

		migrationBuilder.CreateIndex(
			name: "IX_CommandQueueJobs_ChannelId",
			table: "CommandQueueJobs",
			column: "ChannelId");

		// Backfill Category + ChannelId from shadow queue tables (1:1 with CommandQueueJobs).
		migrationBuilder.Sql("""
			UPDATE CommandQueueJobs
			SET Category = 'metadata',
			    ChannelId = COALESCE(ChannelId, (SELECT mq.ChannelId FROM MetadataQueue mq WHERE mq.CommandQueueJobId = CommandQueueJobs.Id))
			WHERE Id IN (SELECT CommandQueueJobId FROM MetadataQueue);
			""");

		migrationBuilder.Sql("""
			UPDATE CommandQueueJobs
			SET Category = 'fileops',
			    ChannelId = COALESCE(ChannelId, (SELECT fq.ChannelId FROM FileOpsQueue fq WHERE fq.CommandQueueJobId = CommandQueueJobs.Id))
			WHERE Id IN (SELECT CommandQueueJobId FROM FileOpsQueue);
			""");

		migrationBuilder.Sql("""
			UPDATE CommandQueueJobs
			SET Category = 'dbops',
			    ChannelId = COALESCE(ChannelId, (SELECT dq.ChannelId FROM DbOpsQueue dq WHERE dq.CommandQueueJobId = CommandQueueJobs.Id))
			WHERE Id IN (SELECT CommandQueueJobId FROM DbOpsQueue);
			""");

		// Jobs cancelled before this release: history row exists but CommandQueueJobs row was deleted — restore from history.
		migrationBuilder.Sql($"""
			INSERT INTO CommandQueueJobs (Id, CommandId, Name, JobType, Category, ChannelId, PayloadJson, Status, QueuedAtUtc, StartedAtUtc, EndedAtUtc, LastError, AcquisitionMethodsJson)
			SELECT
				h.CommandQueueJobId,
				h.CommandId,
				h.Name,
				h.JobType,
				{JobCatSql.Replace("JobType", "h.JobType")},
				h.ChannelId,
				h.PayloadJson,
				h.ResultStatus,
				h.QueuedAtUtc,
				h.StartedAtUtc,
				h.EndedAtUtc,
				h.Message,
				h.AcquisitionMethodsJson
			FROM MetadataHistory h
			WHERE h.CommandQueueJobId IS NOT NULL
			  AND NOT EXISTS (SELECT 1 FROM CommandQueueJobs c WHERE c.Id = h.CommandQueueJobId);
			""");

		migrationBuilder.Sql($"""
			INSERT INTO CommandQueueJobs (Id, CommandId, Name, JobType, Category, ChannelId, PayloadJson, Status, QueuedAtUtc, StartedAtUtc, EndedAtUtc, LastError, AcquisitionMethodsJson)
			SELECT
				h.CommandQueueJobId,
				h.CommandId,
				h.Name,
				h.JobType,
				{JobCatSql.Replace("JobType", "h.JobType")},
				h.ChannelId,
				h.PayloadJson,
				h.ResultStatus,
				h.QueuedAtUtc,
				h.StartedAtUtc,
				h.EndedAtUtc,
				h.Message,
				h.AcquisitionMethodsJson
			FROM FileOpsHistory h
			WHERE h.CommandQueueJobId IS NOT NULL
			  AND NOT EXISTS (SELECT 1 FROM CommandQueueJobs c WHERE c.Id = h.CommandQueueJobId);
			""");

		migrationBuilder.Sql($"""
			INSERT INTO CommandQueueJobs (Id, CommandId, Name, JobType, Category, ChannelId, PayloadJson, Status, QueuedAtUtc, StartedAtUtc, EndedAtUtc, LastError, AcquisitionMethodsJson)
			SELECT
				h.CommandQueueJobId,
				h.CommandId,
				h.Name,
				h.JobType,
				{JobCatSql.Replace("JobType", "h.JobType")},
				h.ChannelId,
				h.PayloadJson,
				h.ResultStatus,
				h.QueuedAtUtc,
				h.StartedAtUtc,
				h.EndedAtUtc,
				h.Message,
				h.AcquisitionMethodsJson
			FROM DbOpsHistory h
			WHERE h.CommandQueueJobId IS NOT NULL
			  AND NOT EXISTS (SELECT 1 FROM CommandQueueJobs c WHERE c.Id = h.CommandQueueJobId);
			""");

		// History rows with no CommandQueueJobId (legacy samples): new CommandQueueJobs rows.
		migrationBuilder.Sql($"""
			INSERT INTO CommandQueueJobs (CommandId, Name, JobType, Category, ChannelId, PayloadJson, Status, QueuedAtUtc, StartedAtUtc, EndedAtUtc, LastError, AcquisitionMethodsJson)
			SELECT
				h.CommandId,
				h.Name,
				h.JobType,
				{JobCatSql.Replace("JobType", "h.JobType")},
				h.ChannelId,
				h.PayloadJson,
				h.ResultStatus,
				h.QueuedAtUtc,
				h.StartedAtUtc,
				h.EndedAtUtc,
				h.Message,
				h.AcquisitionMethodsJson
			FROM MetadataHistory h
			WHERE h.CommandQueueJobId IS NULL;
			""");

		migrationBuilder.Sql($"""
			INSERT INTO CommandQueueJobs (CommandId, Name, JobType, Category, ChannelId, PayloadJson, Status, QueuedAtUtc, StartedAtUtc, EndedAtUtc, LastError, AcquisitionMethodsJson)
			SELECT
				h.CommandId,
				h.Name,
				h.JobType,
				{JobCatSql.Replace("JobType", "h.JobType")},
				h.ChannelId,
				h.PayloadJson,
				h.ResultStatus,
				h.QueuedAtUtc,
				h.StartedAtUtc,
				h.EndedAtUtc,
				h.Message,
				h.AcquisitionMethodsJson
			FROM FileOpsHistory h
			WHERE h.CommandQueueJobId IS NULL;
			""");

		migrationBuilder.Sql($"""
			INSERT INTO CommandQueueJobs (CommandId, Name, JobType, Category, ChannelId, PayloadJson, Status, QueuedAtUtc, StartedAtUtc, EndedAtUtc, LastError, AcquisitionMethodsJson)
			SELECT
				h.CommandId,
				h.Name,
				h.JobType,
				{JobCatSql.Replace("JobType", "h.JobType")},
				h.ChannelId,
				h.PayloadJson,
				h.ResultStatus,
				h.QueuedAtUtc,
				h.StartedAtUtc,
				h.EndedAtUtc,
				h.Message,
				h.AcquisitionMethodsJson
			FROM DbOpsHistory h
			WHERE h.CommandQueueJobId IS NULL;
			""");

		// Terminal jobs that still lack Category (e.g. completed job + redundant history): derive from JobType.
		migrationBuilder.Sql($"""
			UPDATE CommandQueueJobs
			SET Category = {JobCatSql}
			WHERE Category IS NULL
			  AND ({JobCatSql}) IS NOT NULL
			  AND Status IN ('completed', 'failed', 'aborted');
			""");

		migrationBuilder.DropTable(name: "DbOpsQueue");
		migrationBuilder.DropTable(name: "FileOpsQueue");
		migrationBuilder.DropTable(name: "MetadataQueue");
		migrationBuilder.DropTable(name: "DbOpsHistory");
		migrationBuilder.DropTable(name: "FileOpsHistory");
		migrationBuilder.DropTable(name: "MetadataHistory");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		throw new System.NotSupportedException("UnifyCommandQueueJobs cannot be reverted automatically.");
	}
}
