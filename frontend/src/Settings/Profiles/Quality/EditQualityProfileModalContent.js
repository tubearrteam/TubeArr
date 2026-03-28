import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputButton from 'Components/Form/FormInputButton';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Measure from 'Components/Measure';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, inputTypes, kinds, sizes } from 'Helpers/Props';
import dimensions from 'Styles/Variables/dimensions';
import translate from 'Utilities/String/translate';
import styles from './EditQualityProfileModalContent.css';

const FALLBACK_MODE_HELP_KEYS = [
  { name: 'Strict', helpKey: 'YouTubeQualityFallbackStrictHelp' },
  { name: 'NextBestWithinCeiling', helpKey: 'YouTubeQualityFallbackNextBestWithinCeilingHelp' },
  { name: 'DegradeResolution', helpKey: 'YouTubeQualityFallbackDegradeResolutionHelp' },
  { name: 'NextBest', helpKey: 'YouTubeQualityFallbackNextBestHelp' }
];

const MODAL_BODY_PADDING = parseInt(dimensions.modalBodyPadding, 10);

const HEIGHT_OPTIONS = [
  { key: 144, value: '144p' },
  { key: 240, value: '240p' },
  { key: 360, value: '360p' },
  { key: 480, value: '480p' },
  { key: 720, value: '720p' },
  { key: 1080, value: '1080p' },
  { key: 1440, value: '1440p' },
  { key: 2160, value: '2160p' },
  { key: 4320, value: '4320p' },
];

const FPS_OPTIONS = [
  { key: 24, value: '24' },
  { key: 25, value: '25' },
  { key: 30, value: '30' },
  { key: 50, value: '50' },
  { key: 60, value: '60' }
];

const CONTAINERS_REQUIRING_FFMPEG = new Set(['mp4', 'm4a', '3gp']);

const SELECTION_ARG_PRESETS = [
  { key: '-f "bv*+ba/b"', value: 'Best video+audio fallback chain (-f)' },
  { key: '--format "bv*+ba/b"', value: 'Best video+audio fallback chain (--format)' },
  { key: '-S "res,fps,codec:avc:m4a"', value: 'Prefer resolution/fps then AVC+MP4A (-S)' },
  { key: '--format-sort "res,fps,codec:avc:m4a"', value: 'Prefer resolution/fps then AVC+MP4A (--format-sort)' },
  { key: '--format-sort-force', value: 'Force custom format sort order' },
  { key: '-F', value: 'List available formats (-F, diagnostic)' },
  { key: '--list-formats', value: 'List available formats (diagnostic)' },
  { key: '--check-formats', value: 'Check selected format availability' }
];

const MUX_ARG_PRESETS = [
  { key: '--merge-output-format mp4', value: 'Merge output container: MP4', requiresFfmpeg: true },
  { key: '--merge-output-format webm', value: 'Merge output container: WebM', requiresFfmpeg: true },
  { key: '--remux-video mp4', value: 'Remux into MP4 (no re-encode)', requiresFfmpeg: true },
  { key: '--recode-video mp4', value: 'Recode into MP4 (transcode)', requiresFfmpeg: true },
  { key: '--postprocessor-args "ffmpeg:-movflags +faststart"', value: 'Postprocessor args (--postprocessor-args)', requiresFfmpeg: true },
  { key: '--ppa "ffmpeg:-movflags +faststart"', value: 'FFmpeg postprocessor args (faststart)', requiresFfmpeg: true }
];

const AUDIO_ARG_PRESETS = [
  { key: '-x', value: 'Extract audio only', requiresFfmpeg: true },
  { key: '--audio-format mp3', value: 'Convert extracted audio to MP3', requiresFfmpeg: true },
  { key: '--audio-format m4a', value: 'Convert extracted audio to M4A', requiresFfmpeg: true },
  { key: '--audio-quality 0', value: 'Audio quality best (0)', requiresFfmpeg: true }
];

const TIME_ARG_PRESETS = [
  { key: '--download-sections "*00:00:00-00:10:00"', value: 'Download first 10 minutes', requiresFfmpeg: true },
  { key: '--split-chapters', value: 'Split output by chapter', requiresFfmpeg: true },
  { key: '--remove-chapters "intro|outro"', value: 'Remove matching chapters', requiresFfmpeg: true },
  { key: '--force-keyframes-at-cuts', value: 'Force keyframes at cut points', requiresFfmpeg: true }
];

const SUBTITLE_ARG_PRESETS = [
  { key: '--write-subs', value: 'Write subtitles' },
  { key: '--write-auto-subs', value: 'Write auto-generated subtitles' },
  { key: '--sub-langs "en.*,live_chat"', value: 'Subtitle languages: English + live chat' },
  { key: '--sub-format "best"', value: 'Subtitle format: best available' },
  { key: '--embed-subs', value: 'Embed subtitles into media file', requiresFfmpeg: true }
];

const THUMBNAIL_ARG_PRESETS = [
  { key: '--write-thumbnail', value: 'Write primary thumbnail' },
  { key: '--write-all-thumbnails', value: 'Write all available thumbnails' },
  { key: '--convert-thumbnails jpg', value: 'Convert thumbnails to JPG', requiresFfmpeg: true },
  { key: '--embed-thumbnail', value: 'Embed thumbnail in output media', requiresFfmpeg: true }
];

const METADATA_ARG_PRESETS = [
  { key: '--add-metadata', value: 'Add metadata tags', requiresFfmpeg: true },
  { key: '--embed-metadata', value: 'Embed metadata into file', requiresFfmpeg: true },
  { key: '--embed-chapters', value: 'Embed chapter markers', requiresFfmpeg: true },
  { key: '--parse-metadata "title:%(title)s"', value: 'Parse/transform metadata fields' },
  { key: '--replace-in-metadata title "\\\\|" "-"', value: 'Replace metadata text in title field' },
  { key: '--xattrs', value: 'Write metadata to filesystem xattrs' }
];

const CLEANUP_ARG_PRESETS = [
  { key: '--fixup warn', value: 'Container fixup strategy: warn', requiresFfmpeg: true },
  { key: '--prefer-ffmpeg', value: 'Prefer FFmpeg postprocessing backend' },
  { key: '--ffmpeg-location "/path/to/ffmpeg"', value: 'Set FFmpeg location explicitly', requiresFfmpeg: true },
  { key: '--post-overwrites', value: 'Allow postprocessor overwrites' },
  { key: '--no-post-overwrites', value: 'Prevent postprocessor overwrites' },
  { key: '--keep-video', value: 'Keep intermediate source video files' }
];

const SPONSORBLOCK_ARG_PRESETS = [
  { key: '--sponsorblock-mark all', value: 'Mark SponsorBlock segments as chapters', requiresFfmpeg: true },
  { key: '--sponsorblock-remove all', value: 'Remove SponsorBlock segments', requiresFfmpeg: true },
  { key: '--sponsorblock-chapter-title "[SponsorBlock] %(category_names)s"', value: 'Custom SponsorBlock chapter title' },
  { key: '--no-sponsorblock', value: 'Disable SponsorBlock processing' }
];

const CONTAINER_PREFERENCE_OPTIONS = [
  { key: 'auto', value: 'Auto (recommended)' },
  { key: 'mp4', value: 'MP4 (compatibility)' },
  { key: 'mkv', value: 'MKV (no limits)' },
  { key: 'webm', value: 'WebM (YouTube native)' }
];

const COMPATIBILITY_MODE_OPTIONS = [
  { key: 'max', value: 'Maximum Compatibility (H264 + AAC + yuv420p)' },
  { key: 'balanced', value: 'Balanced (allow H265/VP9 where safe)' },
  { key: 'original', value: 'Original Quality (direct streams)' }
];

const CODEC_PREFERENCE_OPTIONS = [
  { key: 'auto', value: 'Auto' },
  { key: 'h264', value: 'H264 (most compatible)' },
  { key: 'h265', value: 'H265 (smaller files)' },
  { key: 'vp9av1', value: 'VP9 / AV1 (best quality, lower compatibility)' }
];

const AUDIO_QUALITY_OPTIONS = [
  { key: 'auto', value: 'Auto' },
  { key: 'low', value: 'Low (~96k)' },
  { key: 'medium', value: 'Medium (~160k)' },
  { key: 'high', value: 'High (~192k+)' },
  { key: 'original', value: 'Original' }
];

const REMUX_POLICY_OPTIONS = [
  { key: 'auto', value: 'Auto (recommended)' },
  { key: 'never', value: 'Never Re-encode' },
  { key: 'always', value: 'Always Convert to Selected Format' }
];

const AUDIO_CHANNEL_OPTIONS = [
  { key: 'auto', value: 'Auto' },
  { key: 'stereo', value: 'Stereo (recommended)' },
  { key: 'surround', value: 'Surround (5.1)' }
];

const SUBTITLE_HANDLING_OPTIONS = [
  { key: 'none', value: 'None' },
  { key: 'embed', value: 'Embed if Available' },
  { key: 'burn', value: 'Burn into Video' },
  { key: 'external', value: 'External File' }
];

const CHAPTER_HANDLING_OPTIONS = [
  { key: 'keep', value: 'Keep' },
  { key: 'split', value: 'Split into Files' },
  { key: 'remove', value: 'Remove' }
];

const FILE_SIZE_PREFERENCE_OPTIONS = [
  { key: 'best', value: 'Best Quality' },
  { key: 'balanced', value: 'Balanced' },
  { key: 'small', value: 'Smaller Files' }
];

const PIXEL_FORMAT_OPTIONS = [
  { key: 'auto', value: 'Auto (yuv420p)' },
  { key: 'yuv420p', value: 'yuv420p' }
];

const HDR_HANDLING_OPTIONS = [
  { key: 'keep', value: 'Keep HDR' },
  { key: 'sdr', value: 'Convert to SDR (recommended)' }
];

const FRAME_RATE_MODE_OPTIONS = [
  { key: 'auto', value: 'Auto' },
  { key: 'cfr', value: 'Constant (CFR)' }
];

class EditQualityProfileModalContent extends Component {

  constructor(props, context) {
    super(props, context);
    this.state = {
      headerHeight: 0,
      bodyHeight: 0,
      footerHeight: 0,
      isFallbackHelpOpen: false,
      isPreferStreamsHelpOpen: false,
      isAdvancedOpen: false,
      isPowerUserOpen: false
    };
  }

  componentDidUpdate(prevProps, prevState) {
    const { headerHeight, bodyHeight, footerHeight } = this.state;
    if (
      headerHeight > 0 &&
      bodyHeight > 0 &&
      footerHeight > 0 &&
      (headerHeight !== prevState.headerHeight ||
        bodyHeight !== prevState.bodyHeight ||
        footerHeight !== prevState.footerHeight)
    ) {
      this.props.onContentHeightChange(
        headerHeight + bodyHeight + footerHeight + MODAL_BODY_PADDING * 2
      );
    }
  }

  onHeaderMeasure = ({ height }) => {
    if (height > this.state.headerHeight) this.setState({ headerHeight: height });
  };

  onBodyMeasure = ({ height }) => {
    if (height > this.state.bodyHeight) this.setState({ bodyHeight: height });
  };

  onFooterMeasure = ({ height }) => {
    if (height > this.state.footerHeight) this.setState({ footerHeight: height });
  };

  onFallbackHelpPress = () => {
    this.setState({ isFallbackHelpOpen: true });
  };

  onFallbackHelpModalClose = () => {
    this.setState({ isFallbackHelpOpen: false });
  };

  onPreferStreamsHelpPress = () => {
    this.setState({ isPreferStreamsHelpOpen: true });
  };

  onPreferStreamsHelpModalClose = () => {
    this.setState({ isPreferStreamsHelpOpen: false });
  };

  onPowerUserTogglePress = () => {
    this.setState((prevState) => ({ isPowerUserOpen: !prevState.isPowerUserOpen }));
  };

  onAdvancedTogglePress = () => {
    this.setState((prevState) => ({ isAdvancedOpen: !prevState.isAdvancedOpen }));
  };

  render() {
    const {
      isFetching,
      error,
      isSaving,
      saveError,
      compatibilityErrors,
      item,
      isInUse,
      isFfmpegConfigured,
      schema,
      onInputChange,
      onSavePress,
      onModalClose,
      onDeleteQualityProfilePress,
      isReadOnly
    } = this.props;

    const formLocked = Boolean(isReadOnly);

    const fallbackModes = schema?.fallbackModes ?? [];
    const videoCodecs = schema?.videoCodecs ?? ['AV1', 'VP9', 'AVC'];
    const audioCodecs = schema?.audioCodecs ?? ['OPUS', 'MP4A'];
    const containers = schema?.containers ?? ['mp4', 'webm'];

    const fallbackModeValues = fallbackModes.map((m) => ({
      key: m.value,
      value: translate(`YouTubeQualityFallback${m.name}`)
    }));

    const id = item?.id;
    const name = item?.name ?? { value: '' };
    const enabled = item?.enabled ?? { value: true };
    const maxHeight = item?.maxHeight ?? { value: 1080 };
    const minHeight = item?.minHeight ?? { value: 720 };
    const minFps = item?.minFps ?? { value: 24 };
    const maxFps = item?.maxFps ?? { value: 60 };
    const allowHdr = item?.allowHdr ?? { value: true };
    const allowSdr = item?.allowSdr ?? { value: true };
    const preferSeparateStreams = item?.preferSeparateStreams ?? { value: true };
    const allowMuxedFallback = item?.allowMuxedFallback ?? { value: true };
    const fallbackMode = item?.fallbackMode ?? { value: 1 };
    const failIfBelowMinHeight = item?.failIfBelowMinHeight ?? { value: true };
    const retryForBetterFormats = item?.retryForBetterFormats ?? { value: false };
    const retryWindowMinutes = item?.retryWindowMinutes ?? { value: null };
    const preferredContainers = item?.preferredContainers ?? { value: [] };
    const allowedContainers = item?.allowedContainers ?? { value: [] };
    const selectionArgs = item?.selectionArgs ?? { value: '' };
    const muxArgs = item?.muxArgs ?? { value: '' };
    const audioArgs = item?.audioArgs ?? { value: '' };
    const timeArgs = item?.timeArgs ?? { value: '' };
    const subtitleArgs = item?.subtitleArgs ?? { value: '' };
    const thumbnailArgs = item?.thumbnailArgs ?? { value: '' };
    const metadataArgs = item?.metadataArgs ?? { value: '' };
    const cleanupArgs = item?.cleanupArgs ?? { value: '' };
    const sponsorblockArgs = item?.sponsorblockArgs ?? { value: '' };
    const containerPreference = item?.containerPreference ?? { value: 'mp4' };
    const compatibilityMode = item?.compatibilityMode ?? { value: 'max' };
    const codecPreference = item?.codecPreference ?? { value: 'h264' };
    const audioQualityMode = item?.audioQualityMode ?? { value: 'auto' };
    const remuxPolicy = item?.remuxPolicy ?? { value: 'auto' };
    const audioChannels = item?.audioChannels ?? { value: 'stereo' };
    const subtitleHandling = item?.subtitleHandling ?? { value: 'none' };
    const chapterHandling = item?.chapterHandling ?? { value: 'keep' };
    const fileSizePreference = item?.fileSizePreference ?? { value: 'balanced' };
    const pixelFormat = item?.pixelFormat ?? { value: 'auto' };
    const hdrHandling = item?.hdrHandling ?? { value: 'sdr' };
    const frameRateMode = item?.frameRateMode ?? { value: 'auto' };
    const customFfmpegArgs = item?.customFfmpegArgs ?? { value: '' };

    const nameProp = typeof name === 'object' && name !== null ? name : { value: name ?? '' };
    const getNumProp = (v) => (typeof v === 'object' && v !== null ? v : { value: v });
    const getBoolProp = (v) => (typeof v === 'object' && v !== null ? v : { value: !!v });
    const getArrayValue = (v) => {
      if (Array.isArray(v)) {
        return v;
      }

      if (v != null && typeof v === 'object' && Array.isArray(v.value)) {
        return v.value;
      }

      return [];
    };
    const preferredContainerList = getArrayValue(preferredContainers);
    const allowedContainerList = getArrayValue(allowedContainers);
    const outputContainerValue = preferredContainerList[0] ?? allowedContainerList[0] ?? containers[0] ?? 'mp4';

    const getSelectedPresetKeys = (rawValue, options) => {
      const raw = String(typeof rawValue === 'object' && rawValue !== null ? rawValue.value ?? '' : rawValue ?? '');
      return options.filter((option) => raw.includes(option.key)).map((option) => option.key);
    };

    const selectionPresetValue = getSelectedPresetKeys(selectionArgs, SELECTION_ARG_PRESETS);
    const muxPresetValue = getSelectedPresetKeys(muxArgs, MUX_ARG_PRESETS);
    const audioPresetValue = getSelectedPresetKeys(audioArgs, AUDIO_ARG_PRESETS);
    const timePresetValue = getSelectedPresetKeys(timeArgs, TIME_ARG_PRESETS);
    const subtitlePresetValue = getSelectedPresetKeys(subtitleArgs, SUBTITLE_ARG_PRESETS);
    const thumbnailPresetValue = getSelectedPresetKeys(thumbnailArgs, THUMBNAIL_ARG_PRESETS);
    const metadataPresetValue = getSelectedPresetKeys(metadataArgs, METADATA_ARG_PRESETS);
    const cleanupPresetValue = getSelectedPresetKeys(cleanupArgs, CLEANUP_ARG_PRESETS);
    const sponsorblockPresetValue = getSelectedPresetKeys(sponsorblockArgs, SPONSORBLOCK_ARG_PRESETS);
    const outputContainerOptions = containers.map((container) => {
      const requiresFfmpeg = CONTAINERS_REQUIRING_FFMPEG.has(String(container).toLowerCase());
      return {
        key: container,
        value: requiresFfmpeg
          ? translate('YouTubeQualityOutputContainerRequiresFfmpeg', { container: String(container).toUpperCase() })
          : String(container).toUpperCase(),
        isDisabled: requiresFfmpeg && !isFfmpegConfigured
      };
    });
    const toSelectableOptions = (options) => {
      return options.map((option) => ({
        ...option,
        key: option.key,
        value: option.requiresFfmpeg && !isFfmpegConfigured
          ? `${option.value} (requires FFmpeg)`
          : option.value,
        isDisabled: Boolean(option.requiresFfmpeg && !isFfmpegConfigured)
      }));
    };
    const applyGuardedDisable = (options, guards = []) => {
      return options.map((option) => {
        const guard = guards.find((g) => g.key === option.key && g.when());
        if (!guard) {
          return option;
        }

        return {
          ...option,
          value: `${option.value} (${guard.reason})`,
          isDisabled: true
        };
      });
    };

    const selectionOptions = applyGuardedDisable(
      toSelectableOptions(SELECTION_ARG_PRESETS, selectionPresetValue),
      [
        {
          key: '--format-sort-force',
          reason: 'requires format selector/sort',
          when: () => !selectionPresetValue.some((x) => x.startsWith('-f ') || x.startsWith('--format ') || x.startsWith('-S ') || x.startsWith('--format-sort '))
        },
        {
          key: '--check-formats',
          reason: 'requires format selector/sort',
          when: () => !selectionPresetValue.some((x) => x.startsWith('-f ') || x.startsWith('--format ') || x.startsWith('-S ') || x.startsWith('--format-sort '))
        },
        {
          key: '--list-formats',
          reason: 'standalone diagnostic',
          when: () => selectionPresetValue.some((x) => x !== '--list-formats' && x !== '-F')
        },
        {
          key: '-F',
          reason: 'standalone diagnostic',
          when: () => selectionPresetValue.some((x) => x !== '--list-formats' && x !== '-F')
        }
      ]
    );

    const audioOptions = applyGuardedDisable(
      toSelectableOptions(AUDIO_ARG_PRESETS, audioPresetValue),
      [
        {
          key: '--audio-format mp3',
          reason: 'requires -x',
          when: () => !audioPresetValue.includes('-x')
        },
        {
          key: '--audio-format m4a',
          reason: 'requires -x',
          when: () => !audioPresetValue.includes('-x')
        },
        {
          key: '--audio-quality 0',
          reason: 'requires -x',
          when: () => !audioPresetValue.includes('-x')
        }
      ]
    );

    const cleanupOptions = applyGuardedDisable(
      toSelectableOptions(CLEANUP_ARG_PRESETS, cleanupPresetValue),
      [
        {
          key: '--post-overwrites',
          reason: 'conflicts with --no-post-overwrites',
          when: () => cleanupPresetValue.includes('--no-post-overwrites')
        },
        {
          key: '--no-post-overwrites',
          reason: 'conflicts with --post-overwrites',
          when: () => cleanupPresetValue.includes('--post-overwrites')
        }
      ]
    );

    const sponsorblockOptions = applyGuardedDisable(
      toSelectableOptions(SPONSORBLOCK_ARG_PRESETS, sponsorblockPresetValue),
      [
        {
          key: '--no-sponsorblock',
          reason: 'conflicts with mark/remove',
          when: () => sponsorblockPresetValue.includes('--sponsorblock-mark all') || sponsorblockPresetValue.includes('--sponsorblock-remove all')
        },
        {
          key: '--sponsorblock-mark all',
          reason: 'conflicts with --no-sponsorblock',
          when: () => sponsorblockPresetValue.includes('--no-sponsorblock')
        },
        {
          key: '--sponsorblock-remove all',
          reason: 'conflicts with --no-sponsorblock',
          when: () => sponsorblockPresetValue.includes('--no-sponsorblock')
        },
        {
          key: '--sponsorblock-chapter-title "[SponsorBlock] %(category_names)s"',
          reason: 'requires mark/remove',
          when: () => !sponsorblockPresetValue.includes('--sponsorblock-mark all') && !sponsorblockPresetValue.includes('--sponsorblock-remove all')
        }
      ]
    );

    return (
      <>
        <ModalContent onModalClose={onModalClose}>
        <Measure whitelist={['height']} includeMargin={false} onMeasure={this.onHeaderMeasure}>
          <div>
            <ModalHeader>
              {id ? (formLocked ? translate('ShowQualityProfile') : translate('EditQualityProfile')) : translate('AddQualityProfile')}
            </ModalHeader>
          </div>
        </Measure>

        <ModalBody>
          <Measure whitelist={['height']} onMeasure={this.onBodyMeasure}>
            <div>
              {isFetching && <LoadingIndicator />}
              {!isFetching && !!error && (
                <Alert kind={kinds.DANGER}>{translate('AddQualityProfileError')}</Alert>
              )}
              {!isFetching && !error && (
                <Form>
                  {compatibilityErrors?.length ? (
                    <Alert kind={kinds.DANGER}>
                      {compatibilityErrors.map((message) => (
                        <div key={message}>{message}</div>
                      ))}
                    </Alert>
                  ) : null}
                  {formLocked ? (
                    <Alert kind={kinds.INFO}>{translate('BuiltInQualityProfileReadOnly')}</Alert>
                  ) : null}
                  <div className={styles.formFullWidth}>
                  <div className={styles.fourColumnLayout}>
                    {/* Core */}
                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('Name')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.TEXT}
                        name="name"
                        {...nameProp}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('Enabled')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.CHECK}
                        name="enabled"
                        {...getBoolProp(enabled)}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityMinHeight')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup type={inputTypes.SELECT} name="minHeight" values={HEIGHT_OPTIONS} {...getNumProp(minHeight)} onChange={onInputChange} isDisabled={formLocked} />
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityMaxHeight')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup type={inputTypes.SELECT} name="maxHeight" values={HEIGHT_OPTIONS} {...getNumProp(maxHeight)} onChange={onInputChange} isDisabled={formLocked} />
                    </FormGroup>

                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityMinFps')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="minFps"
                        values={FPS_OPTIONS}
                        {...getNumProp(minFps)}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityMaxFps')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="maxFps"
                        values={FPS_OPTIONS}
                        {...getNumProp(maxFps)}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityAllowHdr')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.CHECK}
                        name="allowHdr"
                        {...getBoolProp(allowHdr)}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityContainerPreference')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="containerPreference"
                        values={outputContainerOptions.length ? [
                          CONTAINER_PREFERENCE_OPTIONS[0],
                          ...CONTAINER_PREFERENCE_OPTIONS.slice(1).map((o) => ({
                            ...o,
                            isDisabled: o.key !== 'auto' && outputContainerOptions.find((c) => c.key === o.key)?.isDisabled
                          }))
                        ] : CONTAINER_PREFERENCE_OPTIONS}
                        {...(typeof containerPreference === 'object' && containerPreference !== null ? containerPreference : { value: containerPreference })}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityCompatibilityMode')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="compatibilityMode"
                        values={COMPATIBILITY_MODE_OPTIONS}
                        {...(typeof compatibilityMode === 'object' && compatibilityMode !== null ? compatibilityMode : { value: compatibilityMode })}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityCodecPreference')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="codecPreference"
                        values={CODEC_PREFERENCE_OPTIONS}
                        {...(typeof codecPreference === 'object' && codecPreference !== null ? codecPreference : { value: codecPreference })}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityAudioQuality')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup type={inputTypes.SELECT} name="audioQualityMode" values={AUDIO_QUALITY_OPTIONS} {...(typeof audioQualityMode === 'object' && audioQualityMode !== null ? audioQualityMode : { value: audioQualityMode })} onChange={onInputChange} isDisabled={formLocked} />
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                      <FormLabel>{translate('YouTubeQualityRemuxPolicy')}</FormLabel>
                    </FormGroup>
                    <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="remuxPolicy"
                        values={REMUX_POLICY_OPTIONS}
                        {...(typeof remuxPolicy === 'object' && remuxPolicy !== null ? remuxPolicy : { value: remuxPolicy })}
                        onChange={onInputChange}
                        isDisabled={formLocked}
                      />
                    </FormGroup>

                    <div className={styles.sectionToggleRow}>
                      <button
                        type="button"
                        className={styles.sectionToggleButton}
                        title={this.state.isAdvancedOpen ? translate('HideAdvanced') : translate('ShowAdvanced')}
                        onClick={this.onAdvancedTogglePress}
                        disabled={formLocked}
                      >
                        <Icon name={this.state.isAdvancedOpen ? icons.COLLAPSE : icons.EXPAND} />
                      </button>
                    </div>
                    <div className={styles.sectionDescription}>
                      <strong>{translate('YouTubeQualityAdvancedSectionTitle')}</strong>: {translate('YouTubeQualityAdvancedSectionDescription')}
                    </div>

                    {this.state.isAdvancedOpen ? (
                      <>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                          <FormLabel>{translate('YouTubeQualityAudioChannels')}</FormLabel>
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                          <FormInputGroup type={inputTypes.SELECT} name="audioChannels" values={AUDIO_CHANNEL_OPTIONS} {...(typeof audioChannels === 'object' && audioChannels !== null ? audioChannels : { value: audioChannels })} onChange={onInputChange} isDisabled={formLocked} />
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                          <FormLabel>{translate('YouTubeQualitySubtitleHandling')}</FormLabel>
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                          <FormInputGroup type={inputTypes.SELECT} name="subtitleHandling" values={SUBTITLE_HANDLING_OPTIONS} {...(typeof subtitleHandling === 'object' && subtitleHandling !== null ? subtitleHandling : { value: subtitleHandling })} onChange={onInputChange} isDisabled={formLocked} />
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                          <FormLabel>{translate('YouTubeQualityChapterHandling')}</FormLabel>
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                          <FormInputGroup type={inputTypes.SELECT} name="chapterHandling" values={CHAPTER_HANDLING_OPTIONS} {...(typeof chapterHandling === 'object' && chapterHandling !== null ? chapterHandling : { value: chapterHandling })} onChange={onInputChange} isDisabled={formLocked} />
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                          <FormLabel>{translate('YouTubeQualityFileSizePreference')}</FormLabel>
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                          <FormInputGroup type={inputTypes.SELECT} name="fileSizePreference" values={FILE_SIZE_PREFERENCE_OPTIONS} {...(typeof fileSizePreference === 'object' && fileSizePreference !== null ? fileSizePreference : { value: fileSizePreference })} onChange={onInputChange} isDisabled={formLocked} />
                        </FormGroup>
                      </>
                    ) : null}

                    <div className={styles.sectionToggleRow}>
                      <button
                        type="button"
                        className={styles.sectionToggleButton}
                        title={this.state.isPowerUserOpen ? translate('HidePowerUser') : translate('ShowPowerUser')}
                        onClick={this.onPowerUserTogglePress}
                        disabled={formLocked}
                      >
                        <Icon name={this.state.isPowerUserOpen ? icons.COLLAPSE : icons.EXPAND} />
                      </button>
                    </div>
                    <div className={styles.sectionDescription}>
                      <strong>{translate('YouTubeQualityPowerUserSectionTitle')}</strong>: {translate('YouTubeQualityPowerUserSectionDescription')}
                    </div>

                    {this.state.isPowerUserOpen ? (
                      <>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                          <FormLabel>{translate('YouTubeQualityPixelFormat')}</FormLabel>
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                          <FormInputGroup type={inputTypes.SELECT} name="pixelFormat" values={PIXEL_FORMAT_OPTIONS} {...(typeof pixelFormat === 'object' && pixelFormat !== null ? pixelFormat : { value: pixelFormat })} onChange={onInputChange} isDisabled={formLocked} />
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                          <FormLabel>{translate('YouTubeQualityHdrHandling')}</FormLabel>
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                          <FormInputGroup type={inputTypes.SELECT} name="hdrHandling" values={HDR_HANDLING_OPTIONS} {...(typeof hdrHandling === 'object' && hdrHandling !== null ? hdrHandling : { value: hdrHandling })} onChange={onInputChange} isDisabled={formLocked} />
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                          <FormLabel>{translate('YouTubeQualityFrameRateMode')}</FormLabel>
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                          <FormInputGroup type={inputTypes.SELECT} name="frameRateMode" values={FRAME_RATE_MODE_OPTIONS} {...(typeof frameRateMode === 'object' && frameRateMode !== null ? frameRateMode : { value: frameRateMode })} onChange={onInputChange} isDisabled={formLocked} />
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridLabel}>
                          <FormLabel>{translate('YouTubeQualityCustomFfmpegArguments')}</FormLabel>
                        </FormGroup>
                        <FormGroup size={sizes.SMALL} compact className={styles.gridContent}>
                          <FormInputGroup type={inputTypes.TEXT} name="customFfmpegArgs" {...(typeof customFfmpegArgs === 'object' && customFfmpegArgs !== null ? customFfmpegArgs : { value: customFfmpegArgs })} onChange={onInputChange} isDisabled={formLocked} />
                        </FormGroup>
                      </>
                    ) : null}
                  </div>
                  </div>
                </Form>
              )}
            </div>
          </Measure>
        </ModalBody>

        <Measure whitelist={['height']} includeMargin={false} onMeasure={this.onFooterMeasure}>
          <div>
            <ModalFooter>
            {id && !formLocked ? (
              <div
                className={styles.deleteButtonContainer}
                title={isInUse ? translate('QualityProfileInUseChannelListCollection') : undefined}
              >
                <Button kind={kinds.DANGER} isDisabled={isInUse} onPress={onDeleteQualityProfilePress}>
                  {translate('Delete')}
                </Button>
              </div>
            ) : null}
            <Button onPress={onModalClose}>{translate('Cancel')}</Button>
            <SpinnerErrorButton isSpinning={isSaving} error={saveError} onPress={onSavePress} isDisabled={formLocked}>
              {translate('Save')}
            </SpinnerErrorButton>
            </ModalFooter>
          </div>
        </Measure>
        </ModalContent>

        <Modal
          isOpen={this.state.isFallbackHelpOpen}
          onModalClose={this.onFallbackHelpModalClose}
        >
          <ModalContent onModalClose={this.onFallbackHelpModalClose}>
            <ModalHeader>{translate('YouTubeQualityFallbackModeHelpModalTitle')}</ModalHeader>
            <ModalBody>
              <ul className={styles.fallbackHelpList}>
                {FALLBACK_MODE_HELP_KEYS.map(({ name, helpKey }) => (
                  <li key={name}>
                    <strong>{translate(`YouTubeQualityFallback${name}`)}</strong>
                    <span className={styles.fallbackHelpDescription}>
                      {translate(helpKey)}
                    </span>
                  </li>
                ))}
              </ul>
            </ModalBody>
            <ModalFooter>
              <Button onPress={this.onFallbackHelpModalClose}>{translate('Close')}</Button>
            </ModalFooter>
          </ModalContent>
        </Modal>

        <Modal
          isOpen={this.state.isPreferStreamsHelpOpen}
          onModalClose={this.onPreferStreamsHelpModalClose}
        >
          <ModalContent onModalClose={this.onPreferStreamsHelpModalClose}>
            <ModalHeader>{translate('YouTubeQualityPreferSeparateStreams')}</ModalHeader>
            <ModalBody>
              <p className={styles.helpModalParagraph}>
                {translate('YouTubeQualityPreferSeparateStreamsHelpText')}
              </p>
            </ModalBody>
            <ModalFooter>
              <Button onPress={this.onPreferStreamsHelpModalClose}>{translate('Close')}</Button>
            </ModalFooter>
          </ModalContent>
        </Modal>
      </>
    );
  }
}

EditQualityProfileModalContent.propTypes = {
  isReadOnly: PropTypes.bool,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  compatibilityErrors: PropTypes.arrayOf(PropTypes.string),
  item: PropTypes.object.isRequired,
  schema: PropTypes.object,
  isInUse: PropTypes.bool.isRequired,
  isFfmpegConfigured: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onContentHeightChange: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onDeleteQualityProfilePress: PropTypes.func
};

EditQualityProfileModalContent.defaultProps = {
  isReadOnly: false
};

export default EditQualityProfileModalContent;
