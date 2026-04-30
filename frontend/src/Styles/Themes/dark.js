// *Arr-style dark palette: neutral grays, colored accent only for interactions.
const brandColor = '#d0021b';        // red accent
const brandAccentHover = '#b50217';  // darker red
const darkGray = '#9aa1a7';
const mediumGray = '#80868c';
const gray = '#676d73';
const black = '#000000';
const white = '#FFFFFF';
const offWhite = '#f5f7fa';
const purple = '#7a43b6';
const pink = '#ff69b4';
const lightGray = '#ddd';

module.exports = {
  textColor: white,
  defaultColor: white,
  disabledColor: '#999',
  dimColor: '#555',
  black,
  white,
  offWhite,
  primaryColor: brandColor,
  selectedColor: '#f9be03',
  successColor: '#00853d',
  dangerColor: '#f05050',
  warningColor: '#ffa500',
  infoColor: brandColor,
  purple,
  pink,
  brandColor,
  helpTextColor: '#909293',
  darkGray,
  gray,
  lightGray,
  mediumGray,

  // Theme Colors

  // Keep naming compatible with existing components (legacy "blue" token becomes our accent).
  themeBlue: brandColor,
  themeAlternateBlue: brandAccentHover,
  themeRed: '#f05050',
  themeDarkColor: '#1f1f1f',
  themeLightColor: '#3a3f51',
  pageBackground: '#2b2b2b',
  pageFooterBackground: '#1f1f1f',

  // Labels
  inverseLabelColor: '#ddd',
  inverseLabelTextColor: '#333',
  disabledLabelColor: '#838383',
  infoTextColor: white,

  // Links
  defaultLinkHoverColor: '#fff',
  linkColor: brandColor,
  linkHoverColor: brandAccentHover,

  // Header
  pageHeaderBackgroundColor: '#1f1f1f',

  // Sidebar

  sidebarColor: white,
  sidebarBackgroundColor: '#1f1f1f',
  sidebarActiveBackgroundColor: '#2f3342',
  sidebarActiveColor: white,
  sidebarChildColor: '#E5E5E5',
  sidebarChildHoverColor: white,
  sidebarChildActiveColor: white,

  // Toolbar
  toolbarColor: '#e1e2e3',
  toolbarBackgroundColor: '#1f1f1f',
  toolbarMenuItemBackgroundColor: '#1f1f1f',
  toolbarMenuItemHoverBackgroundColor: '#2f3342',
  toolbarLabelColor: '#e1e2e3',

  // Accents
  borderColor: '#3c3c3c',
  inputBorderColor: '#dde6e9',
  inputBoxShadowColor: 'rgba(0, 0, 0, 0.075)',
  inputFocusBorderColor: '#d86a75',
  inputFocusBoxShadowColor: 'rgba(208, 2, 27, 0.25)',
  inputErrorBorderColor: '#f05050',
  inputErrorBoxShadowColor: 'rgba(240, 80, 80, 0.6)',
  inputWarningBorderColor: '#ffa500',
  inputWarningBoxShadowColor: 'rgba(255, 165, 0, 0.6)',
  colorImpairedGradient: '#707070',
  colorImpairedGradientDark: '#424242',
  colorImpairedDangerGradient: '#d84848',
  colorImpairedWarningGradient: '#e59400',
  colorImpairedPrimaryGradient: brandAccentHover,
  colorImpairedGrayGradient: '#9b9b9b',

  //
  // Buttons

  defaultButtonTextColor: '#eee',
  defaultBackgroundColor: '#2b2b2b',
  defaultBorderColor: '#3c3c3c',
  defaultHoverBackgroundColor: '#333333',
  defaultHoverBorderColor: '#454545',

  primaryBackgroundColor: brandColor,
  primaryBorderColor: brandColor,
  primaryHoverBackgroundColor: brandAccentHover,
  primaryHoverBorderColor: brandAccentHover,

  successBackgroundColor: '#27c24c',
  successBorderColor: '#26be4a',
  successHoverBackgroundColor: '#24b145',
  successHoverBorderColor: '#1f9c3d',

  warningBackgroundColor: '#ff902b',
  warningBorderColor: '#ff8d26',
  warningHoverBackgroundColor: '#ff8517',
  warningHoverBorderColor: '#fc7800',

  dangerBackgroundColor: '#f05050',
  dangerBorderColor: '#f04b4b',
  dangerHoverBackgroundColor: '#ee3d3d',
  dangerHoverBorderColor: '#ec2626',

  iconButtonDisabledColor: '#7a7a7a',
  iconButtonHoverColor: '#666',
  iconButtonHoverLightColor: '#ccc',

  //
  // Modal

  modalBackdropBackgroundColor: 'rgba(0, 0, 0, 0.6)',
  modalBackgroundColor: '#2b2b2b',
  modalCloseButtonHoverColor: '#888',

  //
  // Menu
  menuItemColor: '#e1e2e3',
  menuItemHoverColor: white,
  menuItemHoverBackgroundColor: brandColor,

  //
  // Toolbar

  toobarButtonHoverColor: brandColor,
  toobarButtonSelectedColor: brandColor,

  //
  // Scroller

  scrollbarBackgroundColor: '#707070',
  scrollbarHoverBackgroundColor: '#606060',

  //
  // Card

  cardBackgroundColor: black,
  cardShadowColor: '#111',
  cardAlternateBackgroundColor: black,
  cardCenterBackgroundColor: black,

  //
  // Alert

  alertDangerBorderColor: '#a94442',
  alertDangerBackgroundColor: 'rgba(255,0,0,0.1)',
  alertDangerColor: '#ccc',

  alertInfoBorderColor: '#31708f',
  alertInfoBackgroundColor: 'rgba(0,0,255,0.1)',
  alertInfoColor: '#ccc',

  alertSuccessBorderColor: '#3c763d',
  alertSuccessBackgroundColor: 'rgba(0,255,0,0.1)',
  alertSuccessColor: '#ccc',

  alertWarningBorderColor: '#8a6d3b',
  alertWarningBackgroundColor: 'rgba(255,255,0,0.1)',
  alertWarningColor: '#ccc',

  //
  // Slider

  sliderAccentColor: brandColor,

  //
  // Form

  inputBackgroundColor: '#333333',
  inputReadOnlyBackgroundColor: '#2b2b2b',
  inputHoverBackgroundColor: 'rgba(255, 255, 255, 0.20)',
  inputSelectedBackgroundColor: 'rgba(255, 255, 255, 0.05)',
  advancedFormLabelColor: '#ff902b',
  disabledCheckInputColor: '#ddd',
  disabledInputColor: '#808080',

  //
  // Popover

  popoverTitleBackgroundColor: '#1f1f1f',
  popoverTitleBorderColor: '#3c3c3c',
  popoverBodyBackgroundColor: '#2b2b2b',
  popoverShadowColor: 'rgba(0, 0, 0, 0.2)',
  popoverArrowBorderColor: '#2a2a2a',

  popoverTitleBackgroundInverseColor: '#595959',
  popoverTitleBorderInverseColor: '#707070',
  popoverShadowInverseColor: 'rgba(0, 0, 0, 0.2)',
  popoverArrowBorderInverseColor: 'rgba(58, 63, 81, 0.75)',

  //
  // Calendar

  calendarTodayBackgroundColor: '#3e3e3e',
  calendarBackgroundColor: '#2b2b2b',
  calendarBorderColor: '#3c3c3c',
  calendarTextDim: '#eee',
  calendarTextDimAlternate: '#fff',

  calendarFullColorFilter: 'grayscale(90%) contrast(200%) saturate(50%)',

  //
  // Table

  tableRowHoverBackgroundColor: 'rgba(255, 255, 255, 0.06)',

  //
  // Channel

  addChannelBackgroundColor: '#2b2b2b',
  channelBackgroundColor: '#2b2b2b',
  searchIconContainerBackgroundColor: '#2b2b2b',
  collapseButtonBackgroundColor: '#2b2b2b',

  //
  // Playlist

  playlistBackgroundColor: '#2b2b2b',
  videosBackgroundColor: '#2b2b2b',

  //
  // misc

  progressBarFrontTextColor: white,
  progressBarBackTextColor: white,
  progressBarBackgroundColor: '#3c3c3c',
  logEventsBackgroundColor: '#2b2b2b'
};
