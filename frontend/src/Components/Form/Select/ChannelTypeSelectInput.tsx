import React, { useMemo } from 'react';
import * as channelTypes from 'Utilities/Channel/channelTypes';
import translate from 'Utilities/String/translate';
import EnhancedSelectInput, {
  EnhancedSelectInputProps,
  EnhancedSelectInputValue,
} from './EnhancedSelectInput';
import ChannelTypeSelectInputOption from './ChannelTypeSelectInputOption';
import ChannelTypeSelectInputSelectedValue from './ChannelTypeSelectInputSelectedValue';

interface ChannelTypeSelectInputProps
  extends EnhancedSelectInputProps<EnhancedSelectInputValue<string>, string> {
  includeNoChange: boolean;
  includeNoChangeDisabled?: boolean;
  includeMixed: boolean;
}

export interface IChannelTypeOption {
  key: string;
  value: string;
  format?: string;
  isDisabled?: boolean;
}

const channelTypeOptions: IChannelTypeOption[] = [
  {
    key: channelTypes.STANDARD,
    value: 'Standard',
    get format() {
      return translate('StandardVideoTypeFormat', { format: '2026-03-14' });
    },
  },
  {
    key: channelTypes.EPISODIC,
    value: 'Episodic / Ordered',
    get format() {
      return translate('EpisodicVideoTypeFormat', { format: 'S01E05' });
    },
  },
  {
    key: channelTypes.DAILY,
    value: 'Daily',
    get format() {
      return translate('DailyVideoTypeFormat', { format: '2026-03-14' });
    },
  },
  {
    key: channelTypes.STREAMING,
    value: 'Streaming / VOD',
    get format() {
      return translate('StreamingVideoTypeFormat', { format: 'Livestream VOD' });
    },
  },
];

function ChannelTypeSelectInput(props: ChannelTypeSelectInputProps) {
  const {
    includeNoChange = false,
    includeNoChangeDisabled = true,
    includeMixed = false,
  } = props;

  const values = useMemo(() => {
    const result = [...channelTypeOptions];

    if (includeNoChange) {
      result.unshift({
        key: 'noChange',
        value: translate('NoChange'),
        isDisabled: includeNoChangeDisabled,
      });
    }

    if (includeMixed) {
      result.unshift({
        key: 'mixed',
        value: `(${translate('Mixed')})`,
        isDisabled: true,
      });
    }

    return result;
  }, [includeNoChange, includeNoChangeDisabled, includeMixed]);

  return (
    <EnhancedSelectInput
      {...props}
      values={values}
      optionComponent={ChannelTypeSelectInputOption}
      selectedValueComponent={ChannelTypeSelectInputSelectedValue}
    />
  );
}

export default ChannelTypeSelectInput;
