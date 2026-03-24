import React, { useCallback, useState } from 'react';
import FieldSet from 'Components/FieldSet';
import SelectInput from 'Components/Form/SelectInput';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, sizes } from 'Helpers/Props';
import NamingConfig from 'typings/Settings/NamingConfig';
import translate from 'Utilities/String/translate';
import NamingOption from './NamingOption';
import TokenCase from './TokenCase';
import TokenSeparator from './TokenSeparator';
import styles from './NamingModal.css';

const separatorOptions: { key: TokenSeparator; value: string }[] = [
  {
    key: ' ',
    get value() {
      return `${translate('Space')} ( )`;
    },
  },
  {
    key: '.',
    get value() {
      return `${translate('Period')} (.)`;
    },
  },
  {
    key: '_',
    get value() {
      return `${translate('Underscore')} (_)`;
    },
  },
  {
    key: '-',
    get value() {
      return `${translate('Dash')} (-)`;
    },
  },
];

const caseOptions: { key: TokenCase; value: string }[] = [
  {
    key: 'title',
    get value() {
      return translate('DefaultCase');
    },
  },
  {
    key: 'lower',
    get value() {
      return translate('Lowercase');
    },
  },
  {
    key: 'upper',
    get value() {
      return translate('Uppercase');
    },
  },
];

const fileNameTokens = [
  {
    token: '{Upload Date} - {Video Title} [{Video Id}]',
    example: '2026-03-14 - My Cool Video [dQw4w9WgXcQ]',
  },
  {
    token: '{Channel Name} - {Video Title} [{Quality Full}]',
    example: "Sample Channel - My Cool Video [WEBRip-1080p]",
  },
  {
    token: '{Playlist Index:00} - {Video Title}',
    example: '01 - My Cool Video',
  },
];

const fileNameDailyTokens = [
  {
    token: '{Upload Date} - {Video Title}',
    example: '2026-03-14 - My Cool Video',
  },
];

const fileNameEpisodicTokens = fileNameTokens;

const channelTokens = [
  { token: '{Channel Name}', example: "Sample Channel", footNote: true },
  { token: '{Channel Id}', example: 'UCxxxxxxxxxxxxxxxxxxxx', footNote: true },
];

const channelIdTokens = [
  { token: '{Channel Id}', example: 'UCxxxxxxxxxxxxxxxxxxxx' },
];

const playlistTokens = [
  { token: '{Playlist Title}', example: 'Sample Playlist' },
  { token: '{Playlist Id}', example: 'PL1234567890' },
  { token: '{Playlist Index}', example: '1' },
  { token: '{Playlist Index:00}', example: '01' },
];

const videoTokens: { token: string; example: string; footNote?: boolean }[] =
  [];

const airDateTokens = [
  { token: '{Upload Date}', example: '2026-03-14' },
  { token: '{Upload Year}', example: '2026' },
  { token: '{Upload Month}', example: '03' },
  { token: '{Upload Day}', example: '14' },
];

const absoluteTokens: { token: string; example: string }[] = [];

const videoTitleTokens = [
  { token: '{Video Title}', example: "My Cool Video", footNote: true },
];

const qualityTokens = [
  { token: '{Quality Full}', example: 'WEBDL-1080p Proper' },
  { token: '{Quality Title}', example: 'WEBDL-1080p' },
];

const mediaInfoTokens: { token: string; example: string; footNote?: boolean }[] =
[
  { token: '{MediaInfo Codec}', example: 'avc1' },
  { token: '{MediaInfo Audio Codec}', example: 'aac' },
  { token: '{MediaInfo Resolution}', example: '1080p' },
  { token: '{MediaInfo Framerate}', example: '30' },
  { token: '{MediaInfo HDR/SDR}', example: 'SDR' },
  { token: '{MediaInfo Audio Channels}', example: '2' },
  { token: '{MediaInfo Bitrate}', example: '4500k' },
  { token: '{MediaInfo Container}', example: 'mkv' },
];

const originalTokens = [
  {
    token: '{Original Title}',
    example: "Sample.Channel.Title's!.WEBRip.1080p.x264-GROUP",
  },
  {
    token: '{Original Filename}',
    example: 'sample.channel.title.s01e01.webrip.1080p.x264-GROUP',
  },
];

interface NamingModalProps {
  isOpen: boolean;
  name: keyof Pick<
    NamingConfig,
    | 'standardVideoFormat'
    | 'dailyVideoFormat'
    | 'episodicVideoFormat'
    | 'streamingVideoFormat'
    | 'channelFolderFormat'
    | 'playlistFolderFormat'
    | 'specialsFolderFormat'
  >;
  value: string;
  playlist?: boolean;
  video?: boolean;
  daily?: boolean;
  anime?: boolean;
  additional?: boolean;
  onInputChange: ({ name, value }: { name: string; value: string }) => void;
  onModalClose: () => void;
}

function NamingModal(props: NamingModalProps) {
  const {
    isOpen,
    name,
    value,
    playlist = false,
    video = false,
    daily = false,
    anime = false,
    additional = false,
    onInputChange,
    onModalClose,
  } = props;

  const [tokenSeparator, setTokenSeparator] = useState<TokenSeparator>(' ');
  const [tokenCase, setTokenCase] = useState<TokenCase>('title');
  const [selectionStart, setSelectionStart] = useState<number | null>(null);
  const [selectionEnd, setSelectionEnd] = useState<number | null>(null);

  const handleTokenSeparatorChange = useCallback(
    ({ value }: { value: TokenSeparator }) => {
      setTokenSeparator(value);
    },
    [setTokenSeparator]
  );

  const handleTokenCaseChange = useCallback(
    ({ value }: { value: TokenCase }) => {
      setTokenCase(value);
    },
    [setTokenCase]
  );

  const handleInputSelectionChange = useCallback(
    (selectionStart: number | null, selectionEnd: number | null) => {
      setSelectionStart(selectionStart);
      setSelectionEnd(selectionEnd);
    },
    [setSelectionStart, setSelectionEnd]
  );

  const handleOptionPress = useCallback(
    ({
      isFullFilename,
      tokenValue,
    }: {
      isFullFilename: boolean;
      tokenValue: string;
    }) => {
      if (isFullFilename) {
        onInputChange({ name, value: tokenValue });
      } else if (selectionStart == null || selectionEnd == null) {
        onInputChange({
          name,
          value: `${value}${tokenValue}`,
        });
      } else {
        const start = value.substring(0, selectionStart);
        const end = value.substring(selectionEnd);
        const newValue = `${start}${tokenValue}${end}`;

        onInputChange({ name, value: newValue });

        setSelectionStart(newValue.length - 1);
        setSelectionEnd(newValue.length - 1);
      }
    },
    [name, value, selectionEnd, selectionStart, onInputChange]
  );

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {video
            ? translate('FileNameTokens')
            : translate('FolderNameTokens')}
        </ModalHeader>

        <ModalBody>
          <div className={styles.namingSelectContainer}>
            <SelectInput
              className={styles.namingSelect}
              name="separator"
              value={tokenSeparator}
              values={separatorOptions}
              onChange={handleTokenSeparatorChange}
            />

            <SelectInput
              className={styles.namingSelect}
              name="case"
              value={tokenCase}
              values={caseOptions}
              onChange={handleTokenCaseChange}
            />
          </div>

          {video ? (
            <FieldSet legend={translate('FileNames')}>
              <div className={styles.groups}>
                {daily
                  ? fileNameDailyTokens.map(({ token, example }) => (
                      <NamingOption
                        key={token}
                        token={token}
                        example={example}
                        isFullFilename={true}
                        tokenSeparator={tokenSeparator}
                        tokenCase={tokenCase}
                        size={sizes.LARGE}
                        onPress={handleOptionPress}
                      />
                    ))
                  : null}

                {anime
                  ? fileNameEpisodicTokens.map(({ token, example }) => (
                      <NamingOption
                        key={token}
                        token={token}
                        example={example}
                        isFullFilename={true}
                        tokenSeparator={tokenSeparator}
                        tokenCase={tokenCase}
                        size={sizes.LARGE}
                        onPress={handleOptionPress}
                      />
                    ))
                  : null}

                {fileNameTokens.map(({ token, example }) => (
                  <NamingOption
                    key={token}
                    token={token}
                    example={example}
                    isFullFilename={true}
                    tokenSeparator={tokenSeparator}
                    tokenCase={tokenCase}
                    size={sizes.LARGE}
                    onPress={handleOptionPress}
                  />
                ))}
              </div>
            </FieldSet>
          ) : null}

          <FieldSet legend={translate('Channels')}>
            <div className={styles.groups}>
              {channelTokens.map(({ token, example, footNote }) => (
                <NamingOption
                  key={token}
                  token={token}
                  example={example}
                  footNote={footNote}
                  tokenSeparator={tokenSeparator}
                  tokenCase={tokenCase}
                  onPress={handleOptionPress}
                />
              ))}
            </div>

            <div className={styles.footNote}>
              <Icon className={styles.icon} name={icons.FOOTNOTE} />
              <InlineMarkdown data={translate('ChannelFootNote')} />
            </div>
          </FieldSet>

          <FieldSet legend={translate('ChannelID')}>
            <div className={styles.groups}>
              {channelIdTokens.map(({ token, example }) => (
                <NamingOption
                  key={token}
                  token={token}
                  example={example}
                  tokenSeparator={tokenSeparator}
                  tokenCase={tokenCase}
                  onPress={handleOptionPress}
                />
              ))}
            </div>
          </FieldSet>

          {playlist ? (
            <FieldSet legend={translate('Playlist')}>
              <div className={styles.groups}>
                {playlistTokens.map(({ token, example }) => (
                  <NamingOption
                    key={token}
                    token={token}
                    example={example}
                    tokenSeparator={tokenSeparator}
                    tokenCase={tokenCase}
                    onPress={handleOptionPress}
                  />
                ))}
              </div>
            </FieldSet>
          ) : null}

          {video ? (
            <div>
              <FieldSet legend={translate('Video')}>
                <div className={styles.groups}>
                  {videoTokens.map(({ token, example }) => (
                    <NamingOption
                      key={token}
                      token={token}
                      example={example}
                      tokenSeparator={tokenSeparator}
                      tokenCase={tokenCase}
                      onPress={handleOptionPress}
                    />
                  ))}
                </div>
              </FieldSet>

              <FieldSet legend={translate('AirDate')}>
                <div className={styles.groups}>
                  {airDateTokens.map(({ token, example }) => (
                    <NamingOption
                      key={token}
                      token={token}
                      example={example}
                      tokenSeparator={tokenSeparator}
                      tokenCase={tokenCase}
                      onPress={handleOptionPress}
                    />
                  ))}
                </div>
              </FieldSet>

              {anime ? (
                <FieldSet legend={translate('AbsoluteVideoNumber')}>
                  <div className={styles.groups}>
                    {absoluteTokens.map(({ token, example }) => (
                      <NamingOption
                        key={token}
                        token={token}
                        example={example}
                        tokenSeparator={tokenSeparator}
                        tokenCase={tokenCase}
                        onPress={handleOptionPress}
                      />
                    ))}
                  </div>
                </FieldSet>
              ) : null}
            </div>
          ) : null}

          {additional ? (
            <div>
              <FieldSet legend={translate('VideoTitle')}>
                <div className={styles.groups}>
                  {videoTitleTokens.map(({ token, example, footNote }) => (
                    <NamingOption
                      key={token}
                      token={token}
                      example={example}
                      footNote={footNote}
                      tokenSeparator={tokenSeparator}
                      tokenCase={tokenCase}
                      onPress={handleOptionPress}
                    />
                  ))}
                </div>
                <div className={styles.footNote}>
                  <Icon className={styles.icon} name={icons.FOOTNOTE} />
                  <InlineMarkdown data={translate('VideoTitleFootNote')} />
                </div>
              </FieldSet>

              <FieldSet legend={translate('Quality')}>
                <div className={styles.groups}>
                  {qualityTokens.map(({ token, example }) => (
                    <NamingOption
                      key={token}
                      token={token}
                      example={example}
                      tokenSeparator={tokenSeparator}
                      tokenCase={tokenCase}
                      onPress={handleOptionPress}
                    />
                  ))}
                </div>
              </FieldSet>

              <FieldSet legend={translate('MediaInfo')}>
                <div className={styles.groups}>
                  {mediaInfoTokens.map(({ token, example, footNote }) => (
                    <NamingOption
                      key={token}
                      token={token}
                      example={example}
                      footNote={footNote}
                      tokenSeparator={tokenSeparator}
                      tokenCase={tokenCase}
                      onPress={handleOptionPress}
                    />
                  ))}
                </div>

                <div className={styles.footNote}>
                  <Icon className={styles.icon} name={icons.FOOTNOTE} />
                  <InlineMarkdown data={translate('MediaInfoFootNote')} />
                </div>
              </FieldSet>

              <FieldSet legend={translate('Original')}>
                <div className={styles.groups}>
                  {originalTokens.map(({ token, example }) => (
                    <NamingOption
                      key={token}
                      token={token}
                      example={example}
                      tokenSeparator={tokenSeparator}
                      tokenCase={tokenCase}
                      size={sizes.LARGE}
                      onPress={handleOptionPress}
                    />
                  ))}
                </div>
              </FieldSet>
            </div>
          ) : null}
        </ModalBody>

        <ModalFooter>
          <TextInput
            name={name}
            value={value}
            onChange={onInputChange}
            onSelectionChange={handleInputSelectionChange}
          />

          <Button onPress={onModalClose}>{translate('Close')}</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default NamingModal;
