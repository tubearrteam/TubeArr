import React from 'react';
import { CustomFilter } from 'App/State/AppState';
import FilterMenu from 'Components/Menu/FilterMenu';
import { align } from 'Helpers/Props';
import ChannelIndexFilterModal from 'Channel/Index/ChannelIndexFilterModal';

interface ChannelIndexFilterMenuProps {
  selectedFilterKey: string | number;
  filters: object[];
  customFilters: CustomFilter[];
  isDisabled: boolean;
  onFilterSelect(filterName: string): unknown;
}

function ChannelIndexFilterMenu(props: ChannelIndexFilterMenuProps) {
  const {
    selectedFilterKey,
    filters,
    customFilters,
    isDisabled,
    onFilterSelect,
  } = props;

  return (
    <FilterMenu
      alignMenu={align.RIGHT}
      isDisabled={isDisabled}
      selectedFilterKey={selectedFilterKey}
      filters={filters}
      customFilters={customFilters}
      filterModalConnectorComponent={ChannelIndexFilterModal}
      onFilterSelect={onFilterSelect}
    />
  );
}

ChannelIndexFilterMenu.defaultProps = {
  showCustomFilters: false,
};

export default ChannelIndexFilterMenu;
