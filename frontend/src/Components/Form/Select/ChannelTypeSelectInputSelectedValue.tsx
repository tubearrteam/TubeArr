import React from 'react';
import HintedSelectInputSelectedValue from './HintedSelectInputSelectedValue';
import { IChannelTypeOption } from './ChannelTypeSelectInput';

interface ChannelTypeSelectInputOptionProps {
  selectedValue: string;
  values: IChannelTypeOption[];
  format: string;
}
function ChannelTypeSelectInputSelectedValue(
  props: ChannelTypeSelectInputOptionProps
) {
  const { selectedValue, values, ...otherProps } = props;
  const format = values.find((v) => v.key === selectedValue)?.format;

  return (
    <HintedSelectInputSelectedValue
      {...otherProps}
      selectedValue={selectedValue}
      values={values}
      hint={format}
    />
  );
}

export default ChannelTypeSelectInputSelectedValue;
