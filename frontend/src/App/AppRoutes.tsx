import React, { Suspense, lazy } from 'react';
import { Redirect, Route } from 'react-router-dom';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Switch from 'Components/Router/Switch';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';

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

function RedirectWithUrlBase() {
  return <Redirect to={getPathWithUrlBase('/')} />;
}

function RedirectToDownloadQueue() {
  return <Redirect to={getPathWithUrlBase('/activity/download-queue')} />;
}

function RedirectToActivityHistory() {
  return <Redirect to={getPathWithUrlBase('/activity/history')} />;
}

function AppRoutes() {
  return (
    <Suspense fallback={<LoadingIndicator />}>
      <Switch>
      {/*
        Channels
      */}

      <Route exact={true} path="/" component={ChannelIndex} />

      <Route exact={true} path="/channels" component={ChannelIndex} />

      {window.TubeArr.urlBase && (
        <Route
          exact={true}
          path="/"
          // eslint-disable-next-line @typescript-eslint/ban-ts-comment
          // @ts-ignore
          addUrlBase={false}
          render={RedirectWithUrlBase}
        />
      )}

      <Route path="/add/new" component={AddNewChannelConnector} />

      <Route path="/add/import" component={ImportChannel} />

      <Route
        path="/channels/:titleSlug"
        component={ChannelDetailsPageConnector}
      />

      {/*
        Activity
      */}

      <Route exact={true} path="/activity" component={ActivityPage} />
      <Route path="/activity/download-queue" component={QueuePageConnector} />
      <Route path="/activity/metadata-queue" component={MetadataQueuePage} />
      <Route path="/activity/history" component={HistoryPageConnector} />

      {/*
        Queue
      */}

      <Route exact={true} path="/queue" render={RedirectToDownloadQueue} />

      {/*
        History
      */}

      <Route exact={true} path="/history" render={RedirectToActivityHistory} />

      {/*
        Calendar
      */}

      <Route path="/calendar" component={CalendarPage} />

      {/*
        Settings
      */}

      <Route exact={true} path="/settings" component={Settings} />

      <Route
        path="/settings/mediamanagement"
        component={MediaManagementConnector}
      />

      <Route path="/settings/profiles" component={Profiles} />

      {/* Quality: uncomment here + import above + Settings.js link + PageSidebar.js child */}
      {/* <Route path="/settings/quality" component={QualityConnector} /> */}

      {/* CustomFormats: uncomment here + import above + Settings.js link + PageSidebar.js child */}
      {/* <Route path="/settings/customformats" component={CustomFormatSettingsPage} /> */}

      {/* ImportLists: uncomment here + import above + Settings.js link + PageSidebar.js child */}
      {/* <Route path="/settings/importlists" component={ImportListSettingsConnector} /> */}

      <Route path="/settings/connect" component={NotificationSettings} />

      {/* Metadata: uncomment here + import above + Settings.js link + PageSidebar.js child */}
      {/* <Route path="/settings/metadata" component={MetadataSettings} /> */}

      {/* MetadataSource: uncomment here + import above + Settings.js link + PageSidebar.js child */}
      {/* <Route path="/settings/metadatasource" component={MetadataSourceSettings} /> */}

      <Route path="/settings/tags" component={TagSettings} />

      <Route path="/settings/general" component={GeneralSettingsConnector} />

      <Route path="/settings/youtube" component={YouTubeSettingsConnector} />

      <Route exact path="/settings/tools" component={ToolsSettings} />

      <Route path="/settings/tools/ytdlp" component={YtDlpSettingsConnector} />

      <Route path="/settings/tools/ffmpeg" component={FFmpegSettingsConnector} />

      <Route path="/settings/ui" component={UISettingsConnector} />

      {/*
        System
      */}

      <Route path="/system/status" component={Status} />

      <Route path="/system/tasks" component={Tasks} />

      <Route path="/system/backup" component={BackupsConnector} />

      <Route path="/system/updates" component={Updates} />

      <Route path="/system/events" component={LogsTableConnector} />

      <Route path="/system/logs/files" component={Logs} />

      {/*
        Not Found
      */}

        <Route path="*" component={NotFound} />
      </Switch>
    </Suspense>
  );
}

export default AppRoutes;
