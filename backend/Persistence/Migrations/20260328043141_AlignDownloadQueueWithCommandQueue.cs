using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations;

/// <inheritdoc />
public partial class AlignDownloadQueueWithCommandQueue : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// SQLite: rebuild DownloadQueue — rename timestamp/error columns and map Status int → same strings as CommandQueueJobs.
		migrationBuilder.Sql(
			"""
			CREATE TABLE IF NOT EXISTS DownloadQueue_new (
				Id INTEGER NOT NULL CONSTRAINT PK_DownloadQueue PRIMARY KEY AUTOINCREMENT,
				VideoId INTEGER NOT NULL,
				ChannelId INTEGER NOT NULL,
				Status TEXT NOT NULL,
				Progress REAL NULL,
				EstimatedSecondsRemaining INTEGER NULL,
				OutputPath TEXT NULL,
				LastError TEXT NULL,
				QueuedAtUtc TEXT NOT NULL,
				StartedAtUtc TEXT NULL,
				EndedAtUtc TEXT NULL
			);

			INSERT INTO DownloadQueue_new (Id, VideoId, ChannelId, Status, Progress, EstimatedSecondsRemaining, OutputPath, LastError, QueuedAtUtc, StartedAtUtc, EndedAtUtc)
			SELECT
				Id, VideoId, ChannelId,
				CASE Status WHEN 0 THEN 'queued' WHEN 1 THEN 'running' WHEN 2 THEN 'completed' WHEN 3 THEN 'failed' ELSE 'failed' END,
				Progress, EstimatedSecondsRemaining, OutputPath, ErrorMessage, QueuedAt, StartedAt, CompletedAt
			FROM DownloadQueue;

			DROP TABLE DownloadQueue;
			ALTER TABLE DownloadQueue_new RENAME TO DownloadQueue;

			CREATE INDEX IX_DownloadQueue_ChannelId ON DownloadQueue (ChannelId);
			CREATE INDEX IX_DownloadQueue_Status ON DownloadQueue (Status);
			""");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			CREATE TABLE IF NOT EXISTS DownloadQueue_old (
				Id INTEGER NOT NULL CONSTRAINT PK_DownloadQueue PRIMARY KEY AUTOINCREMENT,
				VideoId INTEGER NOT NULL,
				ChannelId INTEGER NOT NULL,
				Status INTEGER NOT NULL,
				Progress REAL NULL,
				EstimatedSecondsRemaining INTEGER NULL,
				OutputPath TEXT NULL,
				ErrorMessage TEXT NULL,
				QueuedAt TEXT NOT NULL,
				StartedAt TEXT NULL,
				CompletedAt TEXT NULL
			);

			INSERT INTO DownloadQueue_old (Id, VideoId, ChannelId, Status, Progress, EstimatedSecondsRemaining, OutputPath, ErrorMessage, QueuedAt, StartedAt, CompletedAt)
			SELECT
				Id, VideoId, ChannelId,
				CASE Status WHEN 'queued' THEN 0 WHEN 'running' THEN 1 WHEN 'completed' THEN 2 WHEN 'failed' THEN 3 ELSE 3 END,
				Progress, EstimatedSecondsRemaining, OutputPath, LastError, QueuedAtUtc, StartedAtUtc, EndedAtUtc
			FROM DownloadQueue;

			DROP TABLE DownloadQueue;
			ALTER TABLE DownloadQueue_old RENAME TO DownloadQueue;

			CREATE INDEX IX_DownloadQueue_ChannelId ON DownloadQueue (ChannelId);
			CREATE INDEX IX_DownloadQueue_Status ON DownloadQueue (Status);
			""");
	}
}
