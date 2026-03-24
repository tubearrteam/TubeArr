import React from 'react';
import getLanguageName from 'Utilities/String/getLanguageName';
import translate from 'Utilities/String/translate';
import useVideoFile from './useVideoFile';

function formatLanguages(languages: string | undefined) {
  if (!languages) {
    return null;
  }

  const splitLanguages = [...new Set(languages.split('/'))].map((l) => {
    const simpleLanguage = l.split('_')[0];

    if (simpleLanguage === 'und') {
      return translate('Unknown');
    }

    return getLanguageName(simpleLanguage);
  });

  if (splitLanguages.length > 3) {
    return (
      <span title={splitLanguages.join(', ')}>
        {splitLanguages.slice(0, 2).join(', ')}, {splitLanguages.length - 2}{' '}
        more
      </span>
    );
  }

  return <span>{splitLanguages.join(', ')}</span>;
}

export type MediaInfoType =
  | 'audio'
  | 'audioLanguages'
  | 'subtitles'
  | 'video'
  | 'videoDynamicRangeType';

interface MediaInfoProps {
  videoFileId?: number;
  type: MediaInfoType;
}

function MediaInfo({ videoFileId, type }: MediaInfoProps) {
  const videoFile = useVideoFile(videoFileId);

  if (!videoFile?.mediaInfo) {
    return null;
  }

  const {
    audioChannels,
    audioCodec,
    audioLanguages,
    subtitles,
    videoCodec,
    videoDynamicRangeType,
  } = videoFile.mediaInfo;

  if (type === 'audio') {
    return (
      <span>
        {audioCodec ? audioCodec : ''}

        {audioCodec && audioChannels ? ' - ' : ''}

        {audioChannels ? audioChannels.toFixed(1) : ''}
      </span>
    );
  }

  if (type === 'audioLanguages') {
    return formatLanguages(audioLanguages);
  }

  if (type === 'subtitles') {
    return formatLanguages(subtitles);
  }

  if (type === 'video') {
    return <span>{videoCodec}</span>;
  }

  if (type === 'videoDynamicRangeType') {
    return <span>{videoDynamicRangeType}</span>;
  }

  return null;
}

export default MediaInfo;
