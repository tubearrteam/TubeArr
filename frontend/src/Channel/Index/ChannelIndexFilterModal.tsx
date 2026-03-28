import React from 'react';
import AppState from 'App/State/AppState';
import FilterModal from 'Components/Filter/FilterModal';
import { useSectionFilterModalState } from 'Components/Filter/useSectionFilterModalState';
import { setChannelFilter } from 'Store/Actions/channelIndexActions';

const selectChannelsItems = (state: AppState) => state.channels.items;
const selectChannelIndexFilterBuilderProps = (state: AppState) =>
	state.channelIndex.filterBuilderProps;

export interface ChannelIndexFilterModalProps {
	isOpen: boolean;
	selectedFilterKey: string | number;
	filters: object[];
	customFilters: object[];
	onFilterSelect: (filterName: string) => unknown;
	onModalClose: () => void;
}

export default function ChannelIndexFilterModal({
	isOpen,
	selectedFilterKey,
	filters,
	customFilters,
	onFilterSelect,
	onModalClose
}: ChannelIndexFilterModalProps) {
	const { sectionItems, filterBuilderProps, dispatchSetFilter } =
		useSectionFilterModalState(
			selectChannelsItems,
			selectChannelIndexFilterBuilderProps,
			setChannelFilter
		);

	return (
		<FilterModal
			isOpen={isOpen}
			selectedFilterKey={selectedFilterKey}
			filters={filters}
			customFilters={customFilters}
			onFilterSelect={onFilterSelect}
			onModalClose={onModalClose}
			sectionItems={sectionItems}
			filterBuilderProps={filterBuilderProps}
			customFilterType="channels"
			dispatchSetFilter={dispatchSetFilter}
		/>
	);
}
