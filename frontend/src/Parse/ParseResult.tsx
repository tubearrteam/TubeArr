import React from 'react';
import { ParseModel } from 'App/State/ParseAppState';
import FieldSet from 'Components/FieldSet';
import VideoFormats from 'Video/VideoFormats';
import ChannelTitleLink from 'Channel/ChannelTitleLink';
import translate from 'Utilities/String/translate';
import ParseResultItem from './ParseResultItem';
import styles from './ParseResult.css';

interface ParseResultProps {
  item: ParseModel;
}

function ParseResult(props: ParseResultProps) {
  const { item } = props;
  const {
    customFormats,
    customFormatScore,
    videos,
    languages,
    parsedVideoInfo,
    channel,
  } = item;

  const {
    releaseTitle,
    channelTitle,
    channelTitleInfo,
    releaseGroup,
    releaseHash,
    playlistNumber,
    videoNumbers,
    absoluteVideoNumbers,
    special,
    fullPlaylist,
    isMultiPlaylist,
    isPartialPlaylist,
    isDaily,
    airDate,
    quality,
  } = parsedVideoInfo;

  const finalLanguages = languages ?? parsedVideoInfo.languages;

  return (
    <div>
      <FieldSet legend={translate('Release')}>
        <ParseResultItem
          title={translate('ReleaseTitle')}
          data={releaseTitle}
        />

        <ParseResultItem title={translate('ChannelTitle')} data={channelTitle} />

        <ParseResultItem
          title={translate('Year')}
          data={channelTitleInfo.year > 0 ? channelTitleInfo.year : '-'}
        />

        <ParseResultItem
          title={translate('AllTitles')}
          data={
            channelTitleInfo.allTitles?.length > 0
              ? channelTitleInfo.allTitles.join(', ')
              : '-'
          }
        />

        <ParseResultItem
          title={translate('ReleaseGroup')}
          data={releaseGroup ?? '-'}
        />

        <ParseResultItem
          title={translate('ReleaseHash')}
          data={releaseHash ? releaseHash : '-'}
        />
      </FieldSet>

      <FieldSet legend={translate('VideoInfo')}>
        <div className={styles.container}>
          <div className={styles.column}>
            <ParseResultItem
              title={translate('PlaylistNumber')}
              data={
                playlistNumber === 0 && absoluteVideoNumbers.length
                  ? '-'
                  : playlistNumber
              }
            />

            <ParseResultItem
              title={translate('VideoNumbers')}
              data={videoNumbers.join(', ') || '-'}
            />

            <ParseResultItem
              title={translate('AbsoluteVideoNumbers')}
              data={
                absoluteVideoNumbers.length
                  ? absoluteVideoNumbers.join(', ')
                  : '-'
              }
            />

            <ParseResultItem
              title={translate('Daily')}
              data={isDaily ? 'True' : 'False'}
            />

            <ParseResultItem
              title={translate('AirDate')}
              data={airDate ?? '-'}
            />
          </div>

          <div className={styles.column}>
            <ParseResultItem
              title={translate('Special')}
              data={special ? translate('True') : translate('False')}
            />

            <ParseResultItem
              title={translate('FullPlaylist')}
              data={fullPlaylist ? translate('True') : translate('False')}
            />

            <ParseResultItem
              title={translate('MultiPlaylist')}
              data={isMultiPlaylist ? translate('True') : translate('False')}
            />

            <ParseResultItem
              title={translate('PartialPlaylist')}
              data={isPartialPlaylist ? translate('True') : translate('False')}
            />
          </div>
        </div>
      </FieldSet>

      <FieldSet legend={translate('Quality')}>
        <div className={styles.container}>
          <div className={styles.column}>
            <ParseResultItem
              title={translate('Quality')}
              data={quality.quality.name}
            />
            <ParseResultItem
              title={translate('Proper')}
              data={
                quality.revision.version > 1 && !quality.revision.isRepack
                  ? translate('True')
                  : '-'
              }
            />

            <ParseResultItem
              title={translate('Repack')}
              data={quality.revision.isRepack ? translate('True') : '-'}
            />
          </div>

          <div className={styles.column}>
            <ParseResultItem
              title={translate('Version')}
              data={
                quality.revision.version > 1 ? quality.revision.version : '-'
              }
            />

            <ParseResultItem
              title={translate('Real')}
              data={quality.revision.real ? translate('True') : '-'}
            />
          </div>
        </div>
      </FieldSet>

      <FieldSet legend={translate('Languages')}>
        <ParseResultItem
          title={translate('Languages')}
          data={finalLanguages.map((l) => l.name).join(', ')}
        />
      </FieldSet>

      <FieldSet legend={translate('Details')}>
        <ParseResultItem
          title={translate('MatchedToChannel')}
          data={
            channel ? (
              <ChannelTitleLink
                titleSlug={channel.titleSlug}
                title={channel.title}
              />
            ) : (
              '-'
            )
          }
        />

        <ParseResultItem
          title={translate('MatchedToPlaylist')}
          data={videos.length ? videos[0].playlistNumber : '-'}
        />

        <ParseResultItem
          title={translate('MatchedToVideos')}
          data={
            videos.length ? (
              <div>
                {videos.map((e) => {
                  return (
                    <div key={e.id}>
                      {e.videoNumber}
                      {channel?.channelType === 'episodic' && e.absoluteVideoNumber
                        ? ` (${e.absoluteVideoNumber})`
                        : ''}{' '}
                      {` - ${e.title}`}
                    </div>
                  );
                })}
              </div>
            ) : (
              '-'
            )
          }
        />

        <ParseResultItem
          title={translate('CustomFormats')}
          data={
            customFormats?.length ? (
              <VideoFormats formats={customFormats} />
            ) : (
              '-'
            )
          }
        />

        <ParseResultItem
          title={translate('CustomFormatScore')}
          data={customFormatScore}
        />
      </FieldSet>
    </div>
  );
}

export default ParseResult;
