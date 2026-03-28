import { IconDefinition } from '@fortawesome/free-regular-svg-icons';
import React, { useMemo } from 'react';
import { useSelector } from 'react-redux';
import useMeasure from 'Helpers/Hooks/useMeasure';
import { icons } from 'Helpers/Props';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import dimensions from 'Styles/Variables/dimensions';
import QualityProfile from 'typings/QualityProfile';
import UiSettings from 'typings/Settings/UiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import ChannelIndexOverviewInfoRow from './ChannelIndexOverviewInfoRow';
import styles from './ChannelIndexOverviewInfo.css';

interface RowProps {
  name: string;
  showProp: string;
  valueProp: string;
}

interface RowInfoProps {
  title: string;
  iconName: IconDefinition;
  label: string;
}

interface ChannelIndexOverviewInfoProps {
  height: number;
  showNetwork: boolean;
  showMonitored: boolean;
  showQualityProfile: boolean;
  showPreviousAiring: boolean;
  showAdded: boolean;
  showPlaylistCount: boolean;
  showPath: boolean;
  showSizeOnDisk: boolean;
  monitored: boolean;
  nextAiring?: string;
  network?: string;
  qualityProfile?: QualityProfile;
  previousAiring?: string;
  added?: string;
  playlistCount: number;
  path: string;
  sizeOnDisk?: number;
  sortKey: string;
}

const infoRowHeight = parseInt(dimensions.channelIndexOverviewInfoRowHeight);

/** Metadata grid uses 2 columns when the container is at least this wide (px). */
const METADATA_TWO_COLUMN_MIN_WIDTH = 400;

const rows = [
  {
    name: 'monitored',
    showProp: 'showMonitored',
    valueProp: 'monitored',
  },
  {
    name: 'network',
    showProp: 'showNetwork',
    valueProp: 'network',
  },
  {
    name: 'qualityProfileId',
    showProp: 'showQualityProfile',
    valueProp: 'qualityProfile',
  },
  {
    name: 'previousAiring',
    showProp: 'showPreviousAiring',
    valueProp: 'previousAiring',
  },
  {
    name: 'added',
    showProp: 'showAdded',
    valueProp: 'added',
  },
  {
    name: 'playlistCount',
    showProp: 'showPlaylistCount',
    valueProp: 'playlistCount',
  },
  {
    name: 'path',
    showProp: 'showPath',
    valueProp: 'path',
  },
  {
    name: 'sizeOnDisk',
    showProp: 'showSizeOnDisk',
    valueProp: 'sizeOnDisk',
  },
];

function getInfoRowProps(
  row: RowProps,
  props: ChannelIndexOverviewInfoProps,
  uiSettings: UiSettings
): RowInfoProps | null {
  const { name } = row;

  if (name === 'monitored') {
    const monitoredText = props.monitored
      ? translate('Monitored')
      : translate('Unmonitored');

    return {
      title: monitoredText,
      iconName: props.monitored ? icons.MONITORED : icons.UNMONITORED,
      label: monitoredText,
    };
  }

  if (name === 'network') {
    return {
      title: translate('Network'),
      iconName: icons.NETWORK,
      label: props.network ?? '',
    };
  }

  if (name === 'qualityProfileId' && !!props.qualityProfile?.name) {
    return {
      title: translate('QualityProfile'),
      iconName: icons.PROFILE,
      label: props.qualityProfile.name,
    };
  }

  if (name === 'previousAiring') {
    const previousAiring = props.previousAiring;
    const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
      uiSettings;

    return {
      title: translate('PreviousAiringDate', {
        date: formatDateTime(previousAiring, longDateFormat, timeFormat),
      }),
      iconName: icons.CALENDAR,
      label: getRelativeDate({
        date: previousAiring,
        shortDateFormat,
        showRelativeDates,
        timeFormat,
        timeForToday: true,
      }),
    };
  }

  if (name === 'added') {
    const added = props.added;
    const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
      uiSettings;

    return {
      title: translate('AddedDate', {
        date: formatDateTime(added, longDateFormat, timeFormat),
      }),
      iconName: icons.ADD,
      label:
        getRelativeDate({
          date: added,
          shortDateFormat,
          showRelativeDates,
          timeFormat,
          timeForToday: true,
        }) ?? '',
    };
  }

  if (name === 'playlistCount') {
    const { playlistCount } = props;
    let playlists = translate('OnePlaylist');

    if (playlistCount === 0) {
      playlists = translate('NoPlaylists');
    } else if (playlistCount > 1) {
      playlists = translate('CountPlaylists', { count: playlistCount });
    }

    return {
      title: translate('PlaylistCount'),
      iconName: icons.CIRCLE,
      label: playlists,
    };
  }

  if (name === 'path') {
    return {
      title: translate('Path'),
      iconName: icons.FOLDER,
      label: props.path,
    };
  }

  if (name === 'sizeOnDisk') {
    const { sizeOnDisk = 0 } = props;

    return {
      title: translate('SizeOnDisk'),
      iconName: icons.DRIVE,
      label: formatBytes(sizeOnDisk),
    };
  }

  return null;
}

function ChannelIndexOverviewInfo(props: ChannelIndexOverviewInfoProps) {
  const { height, nextAiring } = props;

  const uiSettings = useSelector(createUISettingsSelector());

  const { shortDateFormat, showRelativeDates, longDateFormat, timeFormat } =
    uiSettings;

  const [measureRef, bounds] = useMeasure();
  const columnCount =
    bounds.width >= METADATA_TWO_COLUMN_MIN_WIDTH ? 2 : 1;

  const rowSlotHeight = infoRowHeight + 4;
  const reservedSlots = nextAiring ? 1 : 0;
  const maxRows = Math.max(
    0,
    Math.floor(height / rowSlotHeight) - reservedSlots
  );
  const maxItems = maxRows * columnCount;

  const rowInfo = useMemo(() => {
    return rows.map((row) => {
      const { name, showProp, valueProp } = row;

      const isVisible =
        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-ignore ts(7053)
        props[valueProp] != null && (props[showProp] || props.sortKey === name);

      return {
        ...row,
        isVisible,
      };
    });
  }, [props]);

  const renderedInfoRows: React.ReactNode[] = [];

  for (const row of rowInfo) {
    if (!row.isVisible) {
      continue;
    }

    const infoRowProps = getInfoRowProps(row, props, uiSettings);

    if (infoRowProps == null) {
      continue;
    }

    if (renderedInfoRows.length >= maxItems) {
      break;
    }

    renderedInfoRows.push(
      <ChannelIndexOverviewInfoRow key={row.name} {...infoRowProps} />
    );
  }

  return (
    <div ref={measureRef} className={styles.infosOuter}>
      <div
        className={styles.infos}
        data-columns={columnCount}
      >
        {!!nextAiring && (
          <div className={styles.fullWidth}>
            <ChannelIndexOverviewInfoRow
              title={translate('NextAiringDate', {
                date: formatDateTime(nextAiring, longDateFormat, timeFormat),
              })}
              iconName={icons.SCHEDULED}
              label={getRelativeDate({
                date: nextAiring,
                shortDateFormat,
                showRelativeDates,
                timeFormat,
                timeForToday: true,
              })}
            />
          </div>
        )}

        {renderedInfoRows}
      </div>
    </div>
  );
}

export default ChannelIndexOverviewInfo;
