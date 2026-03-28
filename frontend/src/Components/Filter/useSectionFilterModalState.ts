import { useCallback, useMemo } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

export function useSectionFilterModalState(
	selectItems: (state: AppState) => unknown,
	selectFilterBuilderProps: (state: AppState) => unknown,
	setFilter: (payload: unknown) => unknown
) {
	const itemsSelector = useMemo(
		() => createSelector(selectItems, (x) => x),
		[selectItems]
	);
	const builderSelector = useMemo(
		() => createSelector(selectFilterBuilderProps, (x) => x),
		[selectFilterBuilderProps]
	);
	const sectionItems = useSelector(itemsSelector);
	const filterBuilderProps = useSelector(builderSelector);
	const dispatch = useDispatch();
	const dispatchSetFilter = useCallback(
		(payload: unknown) => {
			dispatch(setFilter(payload));
		},
		[dispatch, setFilter]
	);
	return { sectionItems, filterBuilderProps, dispatchSetFilter };
}
