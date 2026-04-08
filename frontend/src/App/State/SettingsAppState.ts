import AppSectionState, {
  AppSectionDeleteState,
  AppSectionItemSchemaState,
  AppSectionItemState,
  AppSectionSaveState,
} from 'App/State/AppSectionState';
import Language from 'Language/Language';
import CustomFormat from 'typings/CustomFormat';
import Notification from 'typings/Notification';
import QualityProfile from 'typings/QualityProfile';
import General from 'typings/Settings/General';
import NamingConfig from 'typings/Settings/NamingConfig';
import NamingExample from 'typings/Settings/NamingExample';
import UiSettings from 'typings/Settings/UiSettings';
import YtDlpSettings from 'typings/Settings/YtDlpSettings';
import SlskdSettings from 'typings/Settings/SlskdSettings';
import FFmpegSettings from 'typings/Settings/FFmpegSettings';
import YouTubeSettings from 'typings/Settings/YouTubeSettings';
import MetadataAppState from './MetadataAppState';

export interface GeneralAppState
  extends AppSectionItemState<General>,
    AppSectionSaveState {}

export interface NamingAppState
  extends AppSectionItemState<NamingConfig>,
    AppSectionSaveState {}

export type NamingExamplesAppState = AppSectionItemState<NamingExample>;

export interface NotificationAppState
  extends AppSectionState<Notification>,
    AppSectionDeleteState {}

export interface QualityProfilesAppState
  extends AppSectionState<QualityProfile>,
    AppSectionItemSchemaState<QualityProfile> {}

export interface CustomFormatAppState
  extends AppSectionState<CustomFormat>,
    AppSectionDeleteState,
    AppSectionSaveState {}

export type LanguageSettingsAppState = AppSectionState<Language>;
export type UiSettingsAppState = AppSectionItemState<UiSettings>;

export interface YtDlpSettingsAppState extends AppSectionItemState<YtDlpSettings>, AppSectionSaveState {
  isTesting?: boolean;
  testMessage?: string | null;
  testSuccess?: boolean | null;
}

export interface SlskdSettingsAppState extends AppSectionItemState<SlskdSettings>, AppSectionSaveState {
  isTesting?: boolean;
  testMessage?: string | null;
  testSuccess?: boolean | null;
}

export interface FFmpegSettingsAppState extends AppSectionItemState<FFmpegSettings>, AppSectionSaveState {
  isTesting?: boolean;
  testMessage?: string | null;
  testSuccess?: boolean | null;
}

export type YouTubeSettingsAppState = AppSectionItemState<YouTubeSettings> & AppSectionSaveState;

interface SettingsAppState {
  advancedSettings: boolean;
  customFormats: CustomFormatAppState;
  general: GeneralAppState;
  languages: LanguageSettingsAppState;
  metadata: MetadataAppState;
  naming: NamingAppState;
  namingExamples: NamingExamplesAppState;
  notifications: NotificationAppState;
  qualityProfiles: QualityProfilesAppState;
  ui: UiSettingsAppState;
  ytdlp: YtDlpSettingsAppState;
  slskd: SlskdSettingsAppState;
  ffmpeg: FFmpegSettingsAppState;
  youtube: YouTubeSettingsAppState;
}

export default SettingsAppState;
