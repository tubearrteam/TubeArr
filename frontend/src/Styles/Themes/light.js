// *Arr-style light palette: neutral grays, colored accent only for interactions.
const brandColor = '#d0021b';        // red accent (secondary)
const brandAccentHover = '#b50217';  // darker red
const darkGray = '#4b4f52';
const mediumGray = '#6b7075';
const gray = '#90969c';
const black = '#000000';
const white = '#FFFFFF';
const offWhite = '#f5f7fa';
const purple = '#7a43b6';
const pink = '#ff69b4';
const lightGray = '#dde6e9';
const defaultColor = '#515253';

module.exports = {
  textColor: defaultColor,
  defaultColor,
  disabledColor: '#909293',
  dimColor: '#6b7075',
  black,
  white,
  offWhite,
  primaryColor: brandColor,
  selectedColor: '#f9be03',
  successColor: '#27c24c',
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
  themeDarkColor: '#3a3f51',
  themeLightColor: '#8a8f98',
  pageBackground: white,
  pageFooterBackground: white,

  // Labels
  inverseLabelColor: '#ddd',
  inverseLabelTextColor: defaultColor,
  disabledLabelColor: '#999',
  infoTextColor: white,

  // Links
  defaultLinkHoverColor: '#fff',
  linkColor: brandColor,
  linkHoverColor: brandAccentHover,

  // Header
  pageHeaderBackgroundColor: '#3a3f51',

  // Sidebar

  sidebarColor: '#e1e2e3',
  sidebarBackgroundColor: '#3a3f51',
  sidebarActiveBackgroundColor: '#2f3342',
  sidebarActiveColor: white,
  sidebarChildColor: '#cfd2d4',
  sidebarChildHoverColor: white,
  sidebarChildActiveColor: white,

  // Toolbar
  toolbarColor: '#e1e2e3',
  toolbarBackgroundColor: '#3a3f51',
  toolbarMenuItemBackgroundColor: '#3a3f51',
  toolbarMenuItemHoverBackgroundColor: '#2f3342',
  toolbarLabelColor: '#e1e2e3',

  // Accents
  borderColor: '#e5e5e5',
  inputBorderColor: '#dde6e9',
  inputBoxShadowColor: 'rgba(0, 0, 0, 0.075)',
  inputFocusBorderColor: '#d86a75',
  inputFocusBoxShadowColor: 'rgba(208, 2, 27, 0.25)',
  inputErrorBorderColor: '#f05050',
  inputErrorBoxShadowColor: 'rgba(240, 80, 80, 0.6)',
  inputWarningBorderColor: '#ffa500',
  inputWarningBoxShadowColor: 'rgba(255, 165, 0, 0.6)',
  colorImpairedGradient: '#ffffff',
  colorImpairedGradientDark: '#f4f5f6',
  colorImpairedDangerGradient: '#d84848',
  colorImpairedWarningGradient: '#e59400',
  colorImpairedPrimaryGradient: '#cc0000',
  colorImpairedGrayGradient: '#9b9b9b  ',

  //
  // Buttons

  defaultButtonTextColor: defaultColor,
  defaultBackgroundColor: '#fff',
  defaultBorderColor: '#eaeaea',
  defaultHoverBackgroundColor: '#f4f5f6',
  defaultHoverBorderColor: '#d6d8db',

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
  modalBackgroundColor: white,
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

  scrollbarBackgroundColor: '#c9cdd1',
  scrollbarHoverBackgroundColor: '#aeb4ba',

  //
  // Card

  cardBackgroundColor: white,
  cardShadowColor: '#e1e1e1',
  cardAlternateBackgroundColor: white,
  cardCenterBackgroundColor: white,

  //
  // Alert

  alertDangerBorderColor: '#ebccd1',
  alertDangerBackgroundColor: '#f2dede',
  alertDangerColor: '#a94442',

  alertInfoBorderColor: '#bce8f1',
  alertInfoBackgroundColor: '#d9edf7',
  alertInfoColor: '#31708f',

  alertSuccessBorderColor: '#d6e9c6',
  alertSuccessBackgroundColor: '#dff0d8',
  alertSuccessColor: '#3c763d',

  alertWarningBorderColor: '#faebcc',
  alertWarningBackgroundColor: '#fcf8e3',
  alertWarningColor: '#8a6d3b',

  //
  // Slider

  sliderAccentColor: brandColor,

  //
  // Form

  inputBackgroundColor: white,
  inputReadOnlyBackgroundColor: '#eee',
  inputHoverBackgroundColor: '#f8f8f8',
  inputSelectedBackgroundColor: '#e2e2e2',
  advancedFormLabelColor: '#ff902b',
  disabledCheckInputColor: '#ddd',
  disabledInputColor: '#808080',

  //
  // Popover

  popoverTitleBackgroundColor: '#3a3f51',
  popoverTitleBorderColor: '#2f3342',
  popoverBodyBackgroundColor: white,
  popoverShadowColor: 'rgba(0, 0, 0, 0.2)',
  popoverArrowBorderColor: '#fff',

  popoverTitleBackgroundInverseColor: black,
  popoverTitleBorderInverseColor: brandAccentHover,
  popoverShadowInverseColor: 'rgba(0, 0, 0, 0.2)',
  popoverArrowBorderInverseColor: 'rgba(58, 63, 81, 0.75)',

  //
  // Calendar

  calendarTodayBackgroundColor: '#c5c5c5',
  calendarBackgroundColor: white,
  calendarBorderColor: '#cecece',
  calendarTextDim: '#666',
  calendarTextDimAlternate: '#242424',

  calendarFullColorFilter: 'brightness(30%)',

  //
  // Table

  tableRowHoverBackgroundColor: '#f4f5f6',

  //
  // Channel

  addChannelBackgroundColor: white,
  channelBackgroundColor: white,
  searchIconContainerBackgroundColor: white,
  collapseButtonBackgroundColor: white,

  //
  // Playlist

  playlistBackgroundColor: white,
  videosBackgroundColor: white,

  //
  // misc

  progressBarFrontTextColor: white,
  progressBarBackTextColor: darkGray,
  progressBarBackgroundColor: white,
  logEventsBackgroundColor: white
};
