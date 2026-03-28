import classNames from 'classnames';
import React from 'react';
import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import { ColorImpairedConsumer } from 'App/ColorImpairedContext';
import ChannelAppState from 'App/State/ChannelAppState';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import createDeepEqualSelector from 'Store/Selectors/createDeepEqualSelector';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './ChannelIndexFooter.css';

function createUnoptimizedSelector() {
  return createSelector(
    createClientSideCollectionSelector('channels', 'channelIndex'),
    (channelsState: ChannelAppState) => {
      return channelsState.items.map((channel) => {
        const { monitored, status, statistics } = channel;

        return {
          monitored,
          status,
          statistics,
        };
      });
    }
  );
}

function createChannelsSelector() {
  return createDeepEqualSelector(
    createUnoptimizedSelector(),
    (channels) => channels
  );
}

export default function ChannelIndexFooter() {
  const channels = useSelector(createChannelsSelector());
  const count = channels.length;
  let videos = 0;
  let totalVideosAllChannels = 0;
  let videoFiles = 0;
  let ended = 0;
  let continuing = 0;
  let monitored = 0;
  let totalFileSize = 0;

  channels.forEach((channel) => {
    const {
      statistics = {
        videoCount: 0,
        videoFileCount: 0,
        sizeOnDisk: 0,
        totalVideoCount: 0,
      },
    } = channel;

    const {
      videoCount = 0,
      videoFileCount = 0,
      sizeOnDisk = 0,
      totalVideoCount = 0,
    } = statistics;

    videos += videoCount;
    totalVideosAllChannels += totalVideoCount;
    videoFiles += videoFileCount;

    if (channel.status === 'ended') {
      ended++;
    } else {
      continuing++;
    }

    if (channel.monitored) {
      monitored++;
    }

    totalFileSize += sizeOnDisk;
  });

  return (
    <ColorImpairedConsumer>
      {(enableColorImpairedMode) => {
        return (
          <div className={styles.footer}>
            <div>
              <div className={styles.legendItem}>
                <div
                  className={classNames(
                    styles.continuing,
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('ChannelIndexFooterContinuing')}</div>
              </div>

              <div className={styles.legendItem}>
                <div
                  className={classNames(
                    styles.ended,
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('ChannelIndexFooterEnded')}</div>
              </div>

              <div className={styles.legendItem}>
                <div
                  className={classNames(
                    styles.missingMonitored,
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('ChannelIndexFooterMissingMonitored')}</div>
              </div>

              <div className={styles.legendItem}>
                <div
                  className={classNames(
                    styles.missingUnmonitored,
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('ChannelIndexFooterMissingUnmonitored')}</div>
              </div>

              <div className={styles.legendItem}>
                <div
                  className={classNames(
                    styles.downloading,
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('ChannelIndexFooterDownloading')}</div>
              </div>
            </div>

            <div className={styles.statistics}>
              <DescriptionList>
                <DescriptionListItem title={translate('Channels')} data={count} />

                <DescriptionListItem title={translate('Ended')} data={ended} />

                <DescriptionListItem
                  title={translate('Continuing')}
                  data={continuing}
                />
              </DescriptionList>

              <DescriptionList>
                <DescriptionListItem
                  title={translate('Monitored')}
                  data={monitored}
                />

                <DescriptionListItem
                  title={translate('Unmonitored')}
                  data={count - monitored}
                />
              </DescriptionList>

              <DescriptionList>
                <DescriptionListItem
                  title={translate('Videos')}
                  data={videos}
                />

                <DescriptionListItem
                  title={translate('ChannelIndexFooterTotalVideosAllChannels')}
                  data={totalVideosAllChannels}
                />

                <DescriptionListItem
                  title={translate('Files')}
                  data={videoFiles}
                />
              </DescriptionList>

              <DescriptionList>
                <DescriptionListItem
                  title={translate('TotalFileSize')}
                  data={formatBytes(totalFileSize)}
                />
              </DescriptionList>
            </div>
          </div>
        );
      }}
    </ColorImpairedConsumer>
  );
}
