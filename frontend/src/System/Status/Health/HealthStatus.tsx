import React, { useEffect, useMemo } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import PageSidebarStatus from 'Components/Page/Sidebar/PageSidebarStatus';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { fetchHealth } from 'Store/Actions/systemActions';
import createHealthSelector from './createHealthSelector';

function HealthStatus() {
  const dispatch = useDispatch();
  const { isConnected, isReconnecting } = useSelector(
    (state: AppState) => state.app
  );
  const { isPopulated, items } = useSelector(createHealthSelector());

  const wasReconnecting = usePrevious(isReconnecting);

  const { count, errors, warnings } = useMemo(() => {
    let errors = false;
    let warnings = false;

    items.forEach((item) => {
      const severity = (item.status ?? item.type).toLowerCase();

      if (severity === 'error') {
        errors = true;
      }

      if (severity === 'warn' || severity === 'warning') {
        warnings = true;
      }
    });

    return {
      count: items.length,
      errors,
      warnings,
    };
  }, [items]);

  useEffect(() => {
    if (!isPopulated) {
      dispatch(fetchHealth());
    }
  }, [isPopulated, dispatch]);

  useEffect(() => {
    if (isConnected && wasReconnecting) {
      dispatch(fetchHealth());
    }
  }, [isConnected, wasReconnecting, dispatch]);

  return (
    <PageSidebarStatus count={count} errors={errors} warnings={warnings} />
  );
}

export default HealthStatus;
