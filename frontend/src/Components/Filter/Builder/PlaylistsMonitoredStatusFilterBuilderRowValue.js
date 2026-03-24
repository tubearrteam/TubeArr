import React from 'react';
import translate from 'Utilities/String/translate';
import FilterBuilderRowValue from './FilterBuilderRowValue';

const playlistsMonitoredStatusList = [
  {
    id: 'all',
    get name() {
      return translate('PlaylistsMonitoredAll');
    }
  },
  {
    id: 'partial',
    get name() {
      return translate('PlaylistsMonitoredPartial');
    }
  },
  {
    id: 'none',
    get name() {
      return translate('PlaylistsMonitoredNone');
    }
  }
];

function PlaylistsMonitoredStatusFilterBuilderRowValue(props) {
  return (
    <FilterBuilderRowValue
      tagList={playlistsMonitoredStatusList}
      {...props}
    />
  );
}

export default PlaylistsMonitoredStatusFilterBuilderRowValue;
