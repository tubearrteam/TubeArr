import React, { Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';
import lazyWithChunkReload from 'Utilities/lazyWithChunkReload';

const p = getPathWithUrlBase;

const ActivityPage = lazyWithChunkReload(() => import('Activity/ActivityPage'));
const MetadataQueuePage = lazyWithChunkReload(() => import('Activity/MetadataQueuePage'));
const AddNewChannelConnector = lazyWithChunkReload(() =>
  import('AddChannel/AddNewChannel/AddNewChannelConnector')
);
const ImportChannel = lazyWithChunkReload(() => import('AddChannel/ImportChannel/ImportChannel'));
const CalendarPage = lazyWithChunkReload(() => import('Calendar/CalendarPage'));
const NotFound = lazyWithChunkReload(() => import('Components/NotFound'));
const ChannelDetailsPageConnector = lazyWithChunkReload(() =>
  import('Channel/Details/ChannelDetailsPageConnector')
);
const ChannelIndex = lazyWithChunkReload(() => import('Channel/Index/ChannelIndex'));
const HistoryPageConnector = lazyWithChunkReload(() => import('History/HistoryPageConnector'));
const QueuePageConnector = lazyWithChunkReload(() => import('Queue/QueuePageConnector'));
const GeneralSettingsConnector = lazyWithChunkReload(() =>
  import('Settings/General/GeneralSettingsConnector')
);
const MediaManagementConnector = lazyWithChunkReload(() =>
  import('Settings/MediaManagement/MediaManagementConnector')
);
const NotificationSettings = lazyWithChunkReload(() =>
  import('Settings/Notifications/NotificationSettings')
);
const Profiles = lazyWithChunkReload(() => import('Settings/Profiles/Profiles'));
const Settings = lazyWithChunkReload(() => import('Settings/Settings'));
const TagSettings = lazyWithChunkReload(() => import('Settings/Tags/TagSettings'));
const UISettingsConnector = lazyWithChunkReload(() => import('Settings/UI/UISettingsConnector'));
const YtDlpSettingsConnector = lazyWithChunkReload(() =>
  import('Settings/Tools/YtDlp/YtDlpSettingsConnector')
);
const FFmpegSettingsConnector = lazyWithChunkReload(() =>
  import('Settings/Tools/FFmpeg/FFmpegSettingsConnector')
);
const ToolsSettings = lazyWithChunkReload(() => import('Settings/Tools/ToolsSettings'));
const YouTubeSettingsConnector = lazyWithChunkReload(() =>
  import('Settings/YouTube/YouTubeSettingsConnector')
);
const BackupsConnector = lazyWithChunkReload(() => import('System/Backup/BackupsConnector'));
const LogsTableConnector = lazyWithChunkReload(() => import('System/Events/LogsTableConnector'));
const Logs = lazyWithChunkReload(() => import('System/Logs/Logs'));
const Status = lazyWithChunkReload(() => import('System/Status/Status'));
const Tasks = lazyWithChunkReload(() => import('System/Tasks/Tasks'));
const Updates = lazyWithChunkReload(() => import('System/Updates/Updates'));

function AppRoutes() {
  return (
    <Suspense fallback={<LoadingIndicator />}>
      <Routes>
        <Route path={p('/')} element={<ChannelIndex />} />

        <Route path={p('/channels')} element={<ChannelIndex />} />

        {window.TubeArr.urlBase ? (
          <Route
            path="/"
            element={<Navigate to={p('/')} replace />}
          />
        ) : null}

        <Route path={p('/add/new')} element={<AddNewChannelConnector />} />

        <Route path={p('/add/import/*')} element={<ImportChannel />} />

        <Route
          path={p('/channels/:titleSlug')}
          element={<ChannelDetailsPageConnector />}
        />

        <Route path={p('/activity')} element={<ActivityPage />} />
        <Route path={p('/activity/download-queue')} element={<QueuePageConnector />} />
        <Route path={p('/activity/metadata-queue')} element={<MetadataQueuePage />} />
        <Route path={p('/activity/history')} element={<HistoryPageConnector />} />

        <Route
          path={p('/queue')}
          element={<Navigate to={p('/activity/download-queue')} replace />}
        />

        <Route
          path={p('/history')}
          element={<Navigate to={p('/activity/history')} replace />}
        />

        <Route path={p('/calendar')} element={<CalendarPage />} />

        <Route path={p('/settings')} element={<Settings />} />

        <Route
          path={p('/settings/mediamanagement')}
          element={<MediaManagementConnector />}
        />

        <Route path={p('/settings/profiles')} element={<Profiles />} />

        <Route path={p('/settings/connect')} element={<NotificationSettings />} />

        <Route path={p('/settings/tags')} element={<TagSettings />} />

        <Route path={p('/settings/general')} element={<GeneralSettingsConnector />} />

        <Route path={p('/settings/youtube')} element={<YouTubeSettingsConnector />} />

        <Route path={p('/settings/tools')} element={<ToolsSettings />} />

        <Route path={p('/settings/tools/ytdlp')} element={<YtDlpSettingsConnector />} />

        <Route path={p('/settings/tools/ffmpeg')} element={<FFmpegSettingsConnector />} />

        <Route path={p('/settings/ui')} element={<UISettingsConnector />} />

        <Route path={p('/system/status')} element={<Status />} />

        <Route path={p('/system/tasks')} element={<Tasks />} />

        <Route path={p('/system/backup')} element={<BackupsConnector />} />

        <Route path={p('/system/updates')} element={<Updates />} />

        <Route path={p('/system/events')} element={<LogsTableConnector />} />

        <Route path={p('/system/logs/files')} element={<Logs />} />

        <Route path="*" element={<NotFound />} />
      </Routes>
    </Suspense>
  );
}

export default AppRoutes;
