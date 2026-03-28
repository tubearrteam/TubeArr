import React from 'react';
import AppState from 'App/State/AppState';
import FilterModal from 'Components/Filter/FilterModal';
import { useSectionFilterModalState } from 'Components/Filter/useSectionFilterModalState';
import { setCalendarFilter } from 'Store/Actions/calendarActions';

const selectCalendarItems = (state: AppState) => state.calendar.items;
const selectCalendarFilterBuilderProps = (state: AppState) =>
	state.calendar.filterBuilderProps;

export interface CalendarFilterModalProps {
	isOpen: boolean;
	selectedFilterKey: string | number;
	filters: object[];
	customFilters: object[];
	onFilterSelect: (filterName: string) => unknown;
	onModalClose: () => void;
}

export default function CalendarFilterModal({
	isOpen,
	selectedFilterKey,
	filters,
	customFilters,
	onFilterSelect,
	onModalClose
}: CalendarFilterModalProps) {
	const { sectionItems, filterBuilderProps, dispatchSetFilter } =
		useSectionFilterModalState(
			selectCalendarItems,
			selectCalendarFilterBuilderProps,
			setCalendarFilter
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
			customFilterType="calendar"
			dispatchSetFilter={dispatchSetFilter}
		/>
	);
}
