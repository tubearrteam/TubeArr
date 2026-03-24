import translate from 'Utilities/String/translate';

/** Standard monitoring options used across Add Channel, Edit Channel, and Monitoring modals. No specials/playlist-only options. */
const monitorOptions = [
  {
    key: 'all',
    get value() {
      return translate('MonitorAllVideos');
    },
  },
  {
    key: 'future',
    get value() {
      return translate('MonitorFutureVideos');
    },
  },
  {
    key: 'missing',
    get value() {
      return translate('MonitorMissingVideos');
    },
  },
  {
    key: 'existing',
    get value() {
      return translate('MonitorExistingVideos');
    },
  },
  {
    key: 'recent',
    get value() {
      return translate('MonitorRecentVideos');
    },
  },
  {
    key: 'roundRobin',
    get value() {
      return translate('MonitorRoundRobinVideos');
    },
  },
  {
    key: 'none',
    get value() {
      return translate('MonitorNoVideos');
    },
  },
];

export default monitorOptions;
