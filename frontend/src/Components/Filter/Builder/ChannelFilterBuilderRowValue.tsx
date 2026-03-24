import React from 'react';
import { useSelector } from 'react-redux';
import Channel from 'Channel/Channel';
import createAllChannelSelector from 'Store/Selectors/createAllChannelSelector';
import sortByProp from 'Utilities/Array/sortByProp';
import FilterBuilderRowValue from './FilterBuilderRowValue';
import FilterBuilderRowValueProps from './FilterBuilderRowValueProps';

function ChannelFilterBuilderRowValue(props: FilterBuilderRowValueProps) {
  const allChannels: Channel[] = useSelector(createAllChannelSelector());

  const tagList = allChannels
    .map((channel) => ({ id: channel.id, name: channel.title }))
    .sort(sortByProp('name'));

  return <FilterBuilderRowValue {...props} tagList={tagList} />;
}

export default ChannelFilterBuilderRowValue;
