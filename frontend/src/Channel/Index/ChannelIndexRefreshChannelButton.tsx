import React, { useCallback, useMemo } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useSelect } from 'App/SelectContext';
import ClientSideCollectionAppState from 'App/State/ClientSideCollectionAppState';
import ChannelAppState, { ChannelIndexAppState } from 'App/State/ChannelAppState';
import { REFRESH_CHANNEL } from 'Commands/commandNames';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import { icons } from 'Helpers/Props';
import { executeCommand } from 'Store/Actions/commandActions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import createChannelClientSideCollectionItemsSelector from 'Store/Selectors/createChannelClientSideCollectionItemsSelector';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';

interface ChannelIndexRefreshChannelButtonProps {
  isSelectMode: boolean;
  selectedFilterKey: string;
}

function ChannelIndexRefreshChannelButton(
  props: ChannelIndexRefreshChannelButtonProps
) {
  const isRefreshing = useSelector(
    createCommandExecutingSelector(REFRESH_CHANNEL)
  );
  const {
    items,
    totalItems,
  }: ChannelAppState & ChannelIndexAppState & ClientSideCollectionAppState =
    useSelector(createChannelClientSideCollectionItemsSelector('channelIndex'));

  const dispatch = useDispatch();
  const { isSelectMode, selectedFilterKey } = props;
  const [selectState] = useSelect();
  const { selectedState } = selectState;

  const selectedChannelIds = useMemo(() => {
    return getSelectedIds(selectedState);
  }, [selectedState]);

  const channelIdsToRefresh =
    isSelectMode && selectedChannelIds.length > 0
      ? selectedChannelIds
      : items.map((m) => m.id);

  let refreshLabel = translate('UpdateAll');

  if (selectedChannelIds.length > 0) {
    refreshLabel = translate('UpdateSelected');
  } else if (selectedFilterKey !== 'all') {
    refreshLabel = translate('UpdateFiltered');
  }

  const onPress = useCallback(() => {
    dispatch(
      executeCommand({
        name: REFRESH_CHANNEL,
        channelIds: channelIdsToRefresh,
      })
    );
  }, [dispatch, channelIdsToRefresh]);

  return (
    <PageToolbarButton
      label={refreshLabel}
      isSpinning={isRefreshing}
      isDisabled={!totalItems}
      iconName={icons.REFRESH}
      onPress={onPress}
    />
  );
}

export default ChannelIndexRefreshChannelButton;
