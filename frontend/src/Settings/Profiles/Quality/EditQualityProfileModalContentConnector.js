import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import {
  fetchFfmpegSettings,
  fetchQualityProfileSchema,
  saveQualityProfile,
  setQualityProfileValue
} from 'Store/Actions/settingsActions';
import createProfileInUseSelector from 'Store/Selectors/createProfileInUseSelector';
import createProviderSettingsSelector from 'Store/Selectors/createProviderSettingsSelector';
import EditQualityProfileModalContent from './EditQualityProfileModalContent';

const DEFAULT_YOUTUBE_PROFILE = {
  name: 'Standard',
  enabled: true,
  maxHeight: 1080,
  minHeight: 720,
  minFps: 24,
  maxFps: 60,
  allowHdr: true,
  allowSdr: true,
  preferSeparateStreams: true,
  allowMuxedFallback: true,
  allowedVideoCodecs: [],
  preferredVideoCodecs: [],
  allowedAudioCodecs: [],
  preferredAudioCodecs: [],
  allowedContainers: ['mp4'],
  preferredContainers: ['mp4'],
  fallbackMode: 1,
  failIfBelowMinHeight: true,
  retryForBetterFormats: false,
  retryWindowMinutes: null,
  containerPreference: 'mp4',
  compatibilityMode: 'max',
  codecPreference: 'h264',
  audioQualityMode: 'auto',
  remuxPolicy: 'auto',
  audioChannels: 'stereo',
  subtitleHandling: 'none',
  chapterHandling: 'keep',
  fileSizePreference: 'balanced',
  pixelFormat: 'auto',
  hdrHandling: 'sdr',
  frameRateMode: 'auto',
  customFfmpegArgs: '',
  selectionArgs: '',
  muxArgs: '',
  audioArgs: '',
  timeArgs: '',
  subtitleArgs: '',
  thumbnailArgs: '',
  metadataArgs: '',
  cleanupArgs: '',
  sponsorblockArgs: ''
};

const FFMPEG_REQUIRED_FLAGS = [
  '--merge-output-format',
  '--remux-video',
  '--recode-video',
  '--postprocessor-args',
  '--ppa',
  '-x',
  '--audio-format',
  '--audio-quality',
  '--download-sections',
  '--split-chapters',
  '--remove-chapters',
  '--force-keyframes-at-cuts',
  '--embed-subs',
  '--convert-thumbnails',
  '--embed-thumbnail',
  '--add-metadata',
  '--embed-metadata',
  '--embed-chapters',
  '--fixup',
  '--sponsorblock-mark',
  '--sponsorblock-remove'
];

const CODEC_ALIAS = {
  AVC: 'h264',
  H264: 'h264',
  H265: 'h265',
  HEVC: 'h265',
  VP8: 'vp8',
  VP9: 'vp9',
  AV1: 'av1',
  MP4A: 'aac',
  AAC: 'aac',
  OPUS: 'opus',
  VORBIS: 'vorbis',
  MP3: 'mp3',
  AC3: 'ac3',
  PCM: 'pcm',
  ALAC: 'alac',
  MPEG4: 'mpeg4',
  H263: 'h263',
  AMR: 'amr',
  FLAC: 'flac',
  XVID: 'xvid',
  MPEG1: 'mpeg1',
  MPEG2: 'mpeg2'
};

const REMUX_COMPATIBILITY = {
  mp4: { video: ['h264', 'h265', 'mpeg4'], audio: ['aac', 'mp3', 'ac3'] },
  mkv: { video: null, audio: null },
  webm: { video: ['vp8', 'vp9', 'av1'], audio: ['opus', 'vorbis'] },
  mov: { video: ['h264', 'h265', 'prores', 'mpeg4'], audio: ['aac', 'pcm', 'alac'] },
  avi: { video: ['mpeg4', 'xvid', 'h264'], audio: ['mp3', 'pcm', 'ac3'] },
  flv: { video: ['h264'], audio: ['aac', 'mp3'] },
  wmv: { video: [], audio: [] }, // always recode per matrix
  mpeg: { video: ['mpeg1', 'mpeg2'], audio: ['mp2', 'mp3'] },
  mpg: { video: ['mpeg1', 'mpeg2'], audio: ['mp2', 'mp3'] },
  ts: { video: ['h264', 'h265', 'mpeg2'], audio: ['aac', 'ac3', 'mp2'] },
  '3gp': { video: ['h263', 'h264'], audio: ['aac', 'amr'] },
  mp3: { video: [], audio: ['mp3'] },
  aac: { video: [], audio: ['aac'] },
  m4a: { video: [], audio: ['aac', 'alac'] },
  flac: { video: [], audio: ['flac'] },
  wav: { video: [], audio: ['pcm'] },
  ogg: { video: [], audio: ['vorbis'] },
  oga: { video: [], audio: ['vorbis'] },
  opus: { video: [], audio: ['opus'] }
};

function hasFlag(text, flag) {
  return new RegExp(`(^|\\s)${flag.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(\\s|$)`).test(String(text || ''));
}

function extractArgValue(text, flag) {
  const match = new RegExp(`${flag.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\s+([^\\s]+)`).exec(String(text || ''));
  return match ? match[1].replace(/^['"]|['"]$/g, '').toLowerCase() : null;
}

function normalizeCodecs(values) {
  if (!Array.isArray(values)) {
    return [];
  }

  return values
    .filter((x) => x != null && String(x).trim().length)
    .map((x) => CODEC_ALIAS[String(x).trim().toUpperCase()] || String(x).trim().toLowerCase());
}

function isAllowedSubset(selected, allowed) {
  if (allowed == null || selected.length === 0) {
    return true;
  }

  const set = new Set(allowed);
  return selected.every((codec) => set.has(codec));
}

function validateCompatibility(payload, isFfmpegConfigured) {
  const errors = [];
  const joinedArgs = [
    payload.selectionArgs,
    payload.muxArgs,
    payload.audioArgs,
    payload.timeArgs,
    payload.subtitleArgs,
    payload.thumbnailArgs,
    payload.metadataArgs,
    payload.cleanupArgs,
    payload.sponsorblockArgs
  ].filter(Boolean).join(' ');

  const hasAnyFlag = (flags) => flags.some((flag) => hasFlag(joinedArgs, flag));

  if (!isFfmpegConfigured && hasAnyFlag(FFMPEG_REQUIRED_FLAGS)) {
    errors.push('Some selected options require FFmpeg, but FFmpeg is not configured.');
  }

  if ((hasFlag(joinedArgs, '--audio-format') || hasFlag(joinedArgs, '--audio-quality')) && !hasFlag(joinedArgs, '-x')) {
    errors.push('Audio format/quality options require "Extract audio only (-x)".');
  }

  if (hasFlag(joinedArgs, '--format-sort-force') && !hasAnyFlag(['-f', '--format', '-S', '--format-sort'])) {
    errors.push('"--format-sort-force" requires a format selector/sort option (-f/--format or -S/--format-sort).');
  }

  if (hasFlag(joinedArgs, '--check-formats') && !hasAnyFlag(['-f', '--format', '-S', '--format-sort'])) {
    errors.push('"--check-formats" should be used with -f/--format or -S/--format-sort.');
  }

  if ((hasFlag(joinedArgs, '--list-formats') || hasFlag(joinedArgs, '-F')) && joinedArgs.trim().split(/\s+/).length > 1) {
    errors.push('"--list-formats" / "-F" is diagnostic/standalone and should not be combined with download processing options.');
  }

  if (hasFlag(joinedArgs, '--post-overwrites') && hasFlag(joinedArgs, '--no-post-overwrites')) {
    errors.push('Select only one overwrite policy: "--post-overwrites" or "--no-post-overwrites".');
  }

  if (hasFlag(joinedArgs, '--no-sponsorblock') && hasAnyFlag(['--sponsorblock-mark', '--sponsorblock-remove'])) {
    errors.push('"--no-sponsorblock" cannot be combined with SponsorBlock mark/remove.');
  }

  if (hasFlag(joinedArgs, '--sponsorblock-chapter-title') && !hasAnyFlag(['--sponsorblock-mark', '--sponsorblock-remove'])) {
    errors.push('"--sponsorblock-chapter-title" requires "--sponsorblock-mark" or "--sponsorblock-remove".');
  }

  if (hasFlag(joinedArgs, '--remux-video') && hasFlag(joinedArgs, '--recode-video')) {
    errors.push('Choose one: remux or recode, not both.');
  }

  const remuxTarget = extractArgValue(joinedArgs, '--remux-video');
  const outputContainer = (
    payload.preferredContainers?.[0] ||
    payload.allowedContainers?.[0] ||
    remuxTarget ||
    ''
  ).toLowerCase();
  const remuxRule = REMUX_COMPATIBILITY[remuxTarget || outputContainer];

  if (remuxTarget && remuxRule) {
    if (Array.isArray(remuxRule.video) && remuxRule.video.length === 0 && Array.isArray(remuxRule.audio) && remuxRule.audio.length === 0) {
      errors.push(`"${remuxTarget}" requires recode according to codecompat.csv; remux is not supported.`);
    } else {
      const effectiveVideo = normalizeCodecs((payload.preferredVideoCodecs?.length ? payload.preferredVideoCodecs : payload.allowedVideoCodecs) || []);
      const effectiveAudio = normalizeCodecs((payload.preferredAudioCodecs?.length ? payload.preferredAudioCodecs : payload.allowedAudioCodecs) || []);

      if (!isAllowedSubset(effectiveVideo, remuxRule.video)) {
        errors.push(`Selected video codecs are not remux-compatible with "${remuxTarget}" per codecompat.csv.`);
      }

      if (!isAllowedSubset(effectiveAudio, remuxRule.audio)) {
        errors.push(`Selected audio codecs are not remux-compatible with "${remuxTarget}" per codecompat.csv.`);
      }
    }
  }

  return errors;
}

function parseCurrentModel(item, defaults) {
  const get = (name, fallback) => {
    const field = item?.[name];
    if (field != null && typeof field === 'object' && Object.prototype.hasOwnProperty.call(field, 'value')) {
      return field.value ?? fallback;
    }

    return field ?? fallback;
  };

  return {
    minHeight: get('minHeight', defaults.minHeight),
    maxHeight: get('maxHeight', defaults.maxHeight),
    containerPreference: get('containerPreference', defaults.containerPreference),
    compatibilityMode: get('compatibilityMode', defaults.compatibilityMode),
    codecPreference: get('codecPreference', defaults.codecPreference),
    audioQualityMode: get('audioQualityMode', defaults.audioQualityMode),
    remuxPolicy: get('remuxPolicy', defaults.remuxPolicy),
    audioChannels: get('audioChannels', defaults.audioChannels),
    subtitleHandling: get('subtitleHandling', defaults.subtitleHandling),
    chapterHandling: get('chapterHandling', defaults.chapterHandling),
    fileSizePreference: get('fileSizePreference', defaults.fileSizePreference),
    pixelFormat: get('pixelFormat', defaults.pixelFormat),
    hdrHandling: get('hdrHandling', defaults.hdrHandling),
    frameRateMode: get('frameRateMode', defaults.frameRateMode),
    customFfmpegArgs: get('customFfmpegArgs', defaults.customFfmpegArgs)
  };
}

function buildFriendlyDerivedSettings(model, defaults) {
  const selectionParts = [];
  const muxParts = [];
  const audioParts = [];
  const timeParts = [];
  const subtitleParts = [];
  const metadataParts = [];
  const cleanupParts = [];
  const sponsorblockParts = [];

  const hasAdvancedChanges =
    model.audioChannels !== defaults.audioChannels ||
    model.subtitleHandling !== defaults.subtitleHandling ||
    model.chapterHandling !== defaults.chapterHandling ||
    model.fileSizePreference !== defaults.fileSizePreference;

  const hasPowerUserChanges =
    model.pixelFormat !== defaults.pixelFormat ||
    model.hdrHandling !== defaults.hdrHandling ||
    model.frameRateMode !== defaults.frameRateMode ||
    String(model.customFfmpegArgs ?? '').trim().length > 0;

  const minHeight = model.minHeight;
  const maxHeight = model.maxHeight;

  if (hasAdvancedChanges) {
    if (model.fileSizePreference === 'best') {
      selectionParts.push('-S "res,fps,vbr,abr"');
    } else if (model.fileSizePreference === 'small') {
      selectionParts.push('-S "size,res,fps"');
    } else {
      selectionParts.push('-S "res,fps,size"');
    }
  }

  const preferredContainers = model.containerPreference === 'auto' ? [] : [model.containerPreference];
  const allowedContainers = model.containerPreference === 'auto' ? [] : [model.containerPreference];

  const preferredVideoCodecs = [];
  const allowedVideoCodecs = [];
  const preferredAudioCodecs = [];
  const allowedAudioCodecs = [];

  if (model.compatibilityMode === 'max') {
    preferredVideoCodecs.push('AVC');
    allowedVideoCodecs.push('AVC');
    preferredAudioCodecs.push('MP4A');
    allowedAudioCodecs.push('MP4A');
    cleanupParts.push('--fixup warn', '--prefer-ffmpeg');
  } else if (model.compatibilityMode === 'balanced') {
    preferredVideoCodecs.push('AVC', 'VP9');
    allowedVideoCodecs.push('AVC', 'VP9', 'AV1');
    preferredAudioCodecs.push('MP4A');
    allowedAudioCodecs.push('MP4A', 'OPUS');
  }

  if (model.codecPreference === 'h264') {
    preferredVideoCodecs.splice(0, preferredVideoCodecs.length, 'AVC');
  } else if (model.codecPreference === 'h265') {
    preferredVideoCodecs.splice(0, preferredVideoCodecs.length, 'H265');
  } else if (model.codecPreference === 'vp9av1') {
    preferredVideoCodecs.splice(0, preferredVideoCodecs.length, 'VP9', 'AV1');
  }

  if (model.audioQualityMode === 'low') {
    audioParts.push('--audio-quality 9');
  } else if (model.audioQualityMode === 'medium') {
    audioParts.push('--audio-quality 5');
  } else if (model.audioQualityMode === 'high') {
    audioParts.push('--audio-quality 2');
  } else if (model.audioQualityMode === 'original') {
    audioParts.push('--audio-format best');
  }

  if (model.remuxPolicy === 'never' && model.containerPreference !== 'auto') {
    muxParts.push(`--remux-video ${model.containerPreference}`);
  } else if (model.remuxPolicy === 'always' && model.containerPreference !== 'auto') {
    muxParts.push(`--recode-video ${model.containerPreference}`);
  }

  if (hasAdvancedChanges) {
    if (model.audioChannels === 'stereo') {
      muxParts.push('--ppa "ffmpeg:-ac 2"');
    } else if (model.audioChannels === 'surround') {
      muxParts.push('--ppa "ffmpeg:-ac 6"');
    }
  }

  if (hasAdvancedChanges) {
    if (model.subtitleHandling === 'embed') {
      subtitleParts.push('--write-subs', '--embed-subs');
    } else if (model.subtitleHandling === 'burn') {
      subtitleParts.push('--write-subs');
      muxParts.push('--ppa "ffmpeg:-vf subtitles"');
    } else if (model.subtitleHandling === 'external') {
      subtitleParts.push('--write-subs');
    }
  }

  if (hasAdvancedChanges) {
    if (model.chapterHandling === 'keep') {
      metadataParts.push('--embed-chapters');
    } else if (model.chapterHandling === 'split') {
      timeParts.push('--split-chapters');
    } else if (model.chapterHandling === 'remove') {
      timeParts.push('--remove-chapters ".*"');
    }
  }

  if (hasPowerUserChanges && model.pixelFormat === 'yuv420p') {
    muxParts.push('--ppa "ffmpeg:-pix_fmt yuv420p"');
  }

  const allowHdr = model.hdrHandling !== 'sdr';
  const allowSdr = true;

  if (hasPowerUserChanges && model.frameRateMode === 'cfr') {
    muxParts.push('--ppa "ffmpeg:-fps_mode cfr"');
  }

  if (hasPowerUserChanges && model.customFfmpegArgs && String(model.customFfmpegArgs).trim().length) {
    const raw = String(model.customFfmpegArgs).trim();
    muxParts.push(raw.startsWith('--ppa ') ? raw : `--ppa "${raw}"`);
  }

  return {
    minHeight,
    maxHeight,
    allowHdr,
    allowSdr,
    allowedContainers,
    preferredContainers,
    allowedVideoCodecs,
    preferredVideoCodecs,
    allowedAudioCodecs,
    preferredAudioCodecs,
    selectionArgs: selectionParts.join(' ').trim(),
    muxArgs: muxParts.join(' ').trim(),
    audioArgs: audioParts.join(' ').trim(),
    timeArgs: timeParts.join(' ').trim(),
    subtitleArgs: subtitleParts.join(' ').trim(),
    metadataArgs: metadataParts.join(' ').trim(),
    cleanupArgs: cleanupParts.join(' ').trim(),
    sponsorblockArgs: sponsorblockParts.join(' ').trim()
  };
}

function createMapStateToProps() {
  return createSelector(
    (state, props) => props.id,
    createProviderSettingsSelector('qualityProfiles'),
    createProfileInUseSelector('qualityProfileId'),
    (state) => state.settings?.ffmpeg,
    (id, qualityProfile, isInUse, ffmpegSection) => {
      const { item: rawItem, schema, isFetching, isPopulated, error, isSaving, saveError } = qualityProfile;
      const schemaData = schema && typeof schema === 'object' && !Array.isArray(schema) ? schema : {};
      const isSchemaLike = rawItem && rawItem.fallbackModes != null && rawItem.videoCodecs != null;
      const baseItem = !id && (isSchemaLike || !rawItem || !rawItem.name) ? { ...DEFAULT_YOUTUBE_PROFILE, ...rawItem } : rawItem;
      const item = baseItem || DEFAULT_YOUTUBE_PROFILE;
      const ffmpegItem = ffmpegSection?.item ?? {};
      const ffmpegPending = ffmpegSection?.pendingChanges ?? {};
      const ffmpegPath = ffmpegPending.executablePath ?? ffmpegItem.executablePath ?? '';
      const ffmpegEnabled = ffmpegPending.enabled ?? ffmpegItem.enabled ?? true;
      const isFfmpegConfigured = Boolean(ffmpegEnabled && String(ffmpegPath).trim().length);

      return {
        id,
        item,
        schema: schemaData,
        isFetching,
        isPopulated,
        error,
        isSaving,
        saveError,
        isInUse,
        isFfmpegConfigured,
        isFfmpegPopulated: Boolean(ffmpegSection?.isPopulated)
      };
    }
  );
}

const mapDispatchToProps = {
  fetchFfmpegSettings,
  fetchQualityProfileSchema,
  setQualityProfileValue,
  saveQualityProfile
};

class EditQualityProfileModalContentConnector extends Component {

  state = {
    compatibilityErrors: []
  };

  componentDidMount() {
    const { schema, isFfmpegPopulated } = this.props;
    const needsSchema = !schema?.fallbackModes?.length;
    if (needsSchema) {
      this.props.fetchQualityProfileSchema();
    }

    if (!isFfmpegPopulated) {
      this.props.fetchFfmpegSettings();
    }
  }

  componentDidUpdate(prevProps) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }
  }

  onInputChange = ({ name, value }) => {
    if (this.state.compatibilityErrors.length) {
      this.setState({ compatibilityErrors: [] });
    }

    const presetFieldToBucketMap = {
      selectionArgsPresets: 'selectionArgs',
      muxArgsPresets: 'muxArgs',
      audioArgsPresets: 'audioArgs',
      timeArgsPresets: 'timeArgs',
      subtitleArgsPresets: 'subtitleArgs',
      thumbnailArgsPresets: 'thumbnailArgs',
      metadataArgsPresets: 'metadataArgs',
      cleanupArgsPresets: 'cleanupArgs',
      sponsorblockArgsPresets: 'sponsorblockArgs'
    };

    if (presetFieldToBucketMap[name]) {
      const selectedPresets = Array.isArray(value) ? value : [];
      const mappedName = presetFieldToBucketMap[name];
      const mappedValue = selectedPresets.join(' ').trim();
      this.props.setQualityProfileValue({ name: mappedName, value: mappedValue });
      return;
    }

    if (name === 'outputContainer') {
      const selectedContainer = value ? [value] : [];
      this.props.setQualityProfileValue({ name: 'allowedContainers', value: selectedContainer });
      this.props.setQualityProfileValue({ name: 'preferredContainers', value: selectedContainer });
      return;
    }

    this.props.setQualityProfileValue({ name, value });
  };

  onSavePress = () => {
    const toValue = (field, fallback) => {
      if (field != null && typeof field === 'object' && Object.prototype.hasOwnProperty.call(field, 'value')) {
        return field.value;
      }

      return field ?? fallback;
    };

    const defaults = DEFAULT_YOUTUBE_PROFILE;
    const item = this.props.item ?? defaults;
    const defaultAllowedContainers = this.props.id ? [] : defaults.allowedContainers;
    const defaultPreferredContainers = this.props.id ? [] : defaults.preferredContainers;
    const profileName = String(toValue(item.name, defaults.name) ?? '').trim();

    if (!profileName.length) {
      this.setState({ compatibilityErrors: ['Name is required.'] });
      return;
    }

    const friendlyModel = parseCurrentModel(item, defaults);
    const derived = buildFriendlyDerivedSettings(friendlyModel, defaults);

    const payload = {
      id: this.props.id,
      name: profileName,
      enabled: toValue(item.enabled, defaults.enabled),
      maxHeight: toValue(item.maxHeight, derived.maxHeight),
      minHeight: toValue(item.minHeight, derived.minHeight),
      minFps: toValue(item.minFps, defaults.minFps),
      maxFps: toValue(item.maxFps, defaults.maxFps),
      allowHdr: toValue(item.allowHdr, derived.allowHdr),
      allowSdr: toValue(item.allowSdr, derived.allowSdr),
      allowedVideoCodecs: toValue(item.allowedVideoCodecs, derived.allowedVideoCodecs),
      preferredVideoCodecs: toValue(item.preferredVideoCodecs, derived.preferredVideoCodecs),
      allowedAudioCodecs: toValue(item.allowedAudioCodecs, derived.allowedAudioCodecs),
      preferredAudioCodecs: toValue(item.preferredAudioCodecs, derived.preferredAudioCodecs),
      allowedContainers: toValue(item.allowedContainers, derived.allowedContainers.length ? derived.allowedContainers : defaultAllowedContainers),
      preferredContainers: toValue(item.preferredContainers, derived.preferredContainers.length ? derived.preferredContainers : defaultPreferredContainers),
      preferSeparateStreams: toValue(item.preferSeparateStreams, defaults.preferSeparateStreams),
      allowMuxedFallback: toValue(item.allowMuxedFallback, defaults.allowMuxedFallback),
      fallbackMode: toValue(item.fallbackMode, defaults.fallbackMode),
      failIfBelowMinHeight: toValue(item.failIfBelowMinHeight, defaults.failIfBelowMinHeight),
      retryForBetterFormats: toValue(item.retryForBetterFormats, defaults.retryForBetterFormats),
      retryWindowMinutes: toValue(item.retryWindowMinutes, defaults.retryWindowMinutes),
      containerPreference: friendlyModel.containerPreference,
      compatibilityMode: friendlyModel.compatibilityMode,
      codecPreference: friendlyModel.codecPreference,
      audioQualityMode: friendlyModel.audioQualityMode,
      remuxPolicy: friendlyModel.remuxPolicy,
      audioChannels: friendlyModel.audioChannels,
      subtitleHandling: friendlyModel.subtitleHandling,
      chapterHandling: friendlyModel.chapterHandling,
      fileSizePreference: friendlyModel.fileSizePreference,
      pixelFormat: friendlyModel.pixelFormat,
      hdrHandling: friendlyModel.hdrHandling,
      frameRateMode: friendlyModel.frameRateMode,
      customFfmpegArgs: friendlyModel.customFfmpegArgs,
      selectionArgs: toValue(item.selectionArgs, derived.selectionArgs),
      muxArgs: toValue(item.muxArgs, derived.muxArgs),
      audioArgs: toValue(item.audioArgs, derived.audioArgs),
      timeArgs: toValue(item.timeArgs, derived.timeArgs),
      subtitleArgs: toValue(item.subtitleArgs, derived.subtitleArgs),
      thumbnailArgs: toValue(item.thumbnailArgs, defaults.thumbnailArgs),
      metadataArgs: toValue(item.metadataArgs, derived.metadataArgs),
      cleanupArgs: toValue(item.cleanupArgs, derived.cleanupArgs),
      sponsorblockArgs: toValue(item.sponsorblockArgs, derived.sponsorblockArgs)
    };

    const compatibilityErrors = validateCompatibility(payload, this.props.isFfmpegConfigured);
    if (compatibilityErrors.length) {
      this.setState({ compatibilityErrors });
      return;
    }

    this.props.saveQualityProfile(payload);
  };

  render() {
    return (
      <EditQualityProfileModalContent
        {...this.props}
        compatibilityErrors={this.state.compatibilityErrors}
        onSavePress={this.onSavePress}
        onInputChange={this.onInputChange}
        onDeleteQualityProfilePress={this.props.onDeleteQualityProfilePress}
      />
    );
  }
}

EditQualityProfileModalContentConnector.propTypes = {
  id: PropTypes.number,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  compatibilityErrors: PropTypes.arrayOf(PropTypes.string),
  item: PropTypes.object.isRequired,
  schema: PropTypes.object,
  isFfmpegConfigured: PropTypes.bool.isRequired,
  isFfmpegPopulated: PropTypes.bool.isRequired,
  setQualityProfileValue: PropTypes.func.isRequired,
  fetchFfmpegSettings: PropTypes.func.isRequired,
  fetchQualityProfileSchema: PropTypes.func.isRequired,
  saveQualityProfile: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onDeleteQualityProfilePress: PropTypes.func
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditQualityProfileModalContentConnector);
