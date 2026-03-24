import translate from 'Utilities/String/translate';

/** Options for "Monitor New Items" (all new videos vs none). Used in Edit Channel and Import List modals. */
const monitorNewItemsOptions = [
  {
    key: 'all',
    get value() {
      return translate('MonitorAllNewVideos');
    },
  },
  {
    key: 'none',
    get value() {
      return translate('MonitorNoNewVideos');
    },
  },
];

export default monitorNewItemsOptions;
