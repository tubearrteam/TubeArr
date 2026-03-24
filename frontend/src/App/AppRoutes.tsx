import React, { Suspense, lazy } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';

const p = getPathWithUrlBase;

const ActivityPage = lazy(() => import('Activity/ActivityPage'));
const MetadataQueuePage = lazy(() => import('Activity/MetadataQueuePage'));
const AddNewChannelConnector = lazy(() =>
  import('AddChannel/AddNewChannel/AddNewChannelConnector')
);
const ImportChannel = lazy(() => import('AddChannel/ImportChannel/ImportChannel'));
const CalendarPage = lazy(() => import('Calendar/CalendarPage'));
const NotFound = lazy(() => import('Components/NotFound'));
const ChannelDetailsPageConnector = lazy(() =>
  import('Channel/Details/ChannelDetailsPageConnector')
);
const ChannelIndex = lazy(() => import('Channel/Index/ChannelIndex'));
const HistoryPageConnector = lazy(() => import('History/HistoryPageConnector'));
const QueuePageConnector = lazy(() => import('Queue/QueuePageConnector'));
const GeneralSettingsConnector = lazy(() =>
  import('Settings/General/GeneralSettingsConnector')
);
const MediaManagementConnector = lazy(() =>
  import('Settings/MediaManagement/MediaManagementConnector')
);
const NotificationSettings = lazy(() =>
  import('Settings/Notifications/NotificationSettings')
);
const Profiles = lazy(() => import('Settings/Profiles/Profiles'));
const Settings = lazy(() => import('Settings/Settings'));
const TagSettings = lazy(() => import('Settings/Tags/TagSettings'));
const UISettingsConnector = lazy(() => import('Settings/UI/UISettingsConnector'));
const YtDlpSettingsConnector = lazy(() =>
  import('Settings/Tools/YtDlp/YtDlpSettingsConnector')
);
const FFmpegSettingsConnector = lazy(() =>
  import('Settings/Tools/FFmpeg/FFmpegSettingsConnector')
);
const ToolsSettings = lazy(() => import('Settings/Tools/ToolsSettings'));
const YouTubeSettingsConnector = lazy(() =>
  import('Settings/YouTube/YouTubeSettingsConnector')
);
const BackupsConnector = lazy(() => import('System/Backup/BackupsConnector'));
const LogsTableConnector = lazy(() => import('System/Events/LogsTableConnector'));
const Logs = lazy(() => import('System/Logs/Logs'));
const Status = lazy(() => import('System/Status/Status'));
const Tasks = lazy(() => import('System/Tasks/Tasks'));
const Updates = lazy(() => import('System/Updates/Updates'));

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

        <Route path={p('/add/import')} element={<ImportChannel />} />

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
