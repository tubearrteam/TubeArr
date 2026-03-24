import React from 'react';
import translate from 'Utilities/String/translate';
import FilterBuilderRowValue from './FilterBuilderRowValue';

const channelTypeList = [
  {
    id: 'standard',
    get name() {
      return translate('Standard');
    }
  },
  {
    id: 'episodic',
    get name() {
      return translate('Episodic');
    }
  },
  {
    id: 'daily',
    get name() {
      return translate('Daily');
    }
  },
  {
    id: 'streaming',
    get name() {
      return translate('Streaming');
    }
  }
];

function ChannelTypeFilterBuilderRowValue(props) {
  return (
    <FilterBuilderRowValue
      tagList={channelTypeList}
      {...props}
    />
  );
}

export default ChannelTypeFilterBuilderRowValue;
