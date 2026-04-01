import classNames from 'classnames';
import React from 'react';
import { NavLink } from 'react-router-dom';
import translate from 'Utilities/String/translate';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';
import styles from './LogsNavMenu.css';

function LogsNavMenu() {
  return (
    <nav className={styles.tabs} aria-label={translate('LogFiles')}>
      <NavLink
        end
        className={({ isActive }) =>
          classNames(styles.tab, isActive && styles.tabActive)
        }
        to={getPathWithUrlBase('/system/logs/files')}
      >
        {translate('LogFiles')}
      </NavLink>

      <NavLink
        className={({ isActive }) =>
          classNames(styles.tab, isActive && styles.tabActive)
        }
        to={getPathWithUrlBase('/system/logs/files/update')}
      >
        {translate('UpdaterLogFiles')}
      </NavLink>
    </nav>
  );
}

export default LogsNavMenu;
