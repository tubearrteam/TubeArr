import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { addChannel, setAddChannelDefault } from 'Store/Actions/addChannelActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createSystemStatusSelector from 'Store/Selectors/createSystemStatusSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import AddNewChannelModalContent from './AddNewChannelModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.addChannel,
    createDimensionsSelector(),
    createSystemStatusSelector(),
    (addChannelState, dimensions, systemStatus) => {
      const {
        isAdding,
        addError,
        defaults
      } = addChannelState;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(defaults, {}, addError);

      return {
        isAdding,
        addError,
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        isWindows: systemStatus.isWindows,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  setAddChannelDefault,
  addChannel
};

class AddNewChannelModalContentConnector extends Component {

  constructor(props) {
    super(props);
    this.state = {
      diskFolderName: ''
    };
    this._abortFolderPreview = null;
  }

  componentDidMount() {
    this.fetchDiskFolderPreview();
  }

  componentDidUpdate(prevProps) {
    if (
      prevProps.youtubeChannelId !== this.props.youtubeChannelId ||
      prevProps.title !== this.props.title ||
      prevProps.titleSlug !== this.props.titleSlug
    ) {
      this.fetchDiskFolderPreview();
    }
  }

  componentWillUnmount() {
    if (this._abortFolderPreview) {
      this._abortFolderPreview();
      this._abortFolderPreview = null;
    }
  }

  fetchDiskFolderPreview = () => {
    const { youtubeChannelId, title, titleSlug } = this.props;
    if (this._abortFolderPreview) {
      this._abortFolderPreview();
      this._abortFolderPreview = null;
    }
    const yt = (youtubeChannelId || '').trim();
    if (!yt) {
      this.setState({ diskFolderName: '' });
      return;
    }
    const { request, abortRequest } = createAjaxRequest({
      url: '/channels/folder-preview',
      data: {
        youtubeChannelId: yt,
        title: title || '',
        titleSlug: titleSlug || ''
      }
    });
    this._abortFolderPreview = abortRequest;
    request
      .done((data) => {
        const folder = data && (data.folder != null ? data.folder : data.Folder);
        this.setState({
          diskFolderName: typeof folder === 'string' ? folder.trim() : ''
        });
      })
      .fail(() => {
        this.setState({ diskFolderName: (titleSlug || '').trim() });
      });
  };

  onInputChange = ({ name, value }) => {
    this.props.setAddChannelDefault({ [name]: value });
  };

  onAddChannelPress = (channelType) => {
    const {
      youtubeChannelId,
      rootFolderPath,
      monitor,
      roundRobinLatestVideoCount,
      qualityProfileId,
      playlistFolder,
      searchForMissingVideos,
      searchForCutoffUnmetVideos,
      filterOutShorts,
      filterOutLivestreams,
      tags
    } = this.props;

    const rrVal = roundRobinLatestVideoCount && roundRobinLatestVideoCount.value;
    const rrParsed = rrVal !== '' && rrVal != null ? parseInt(String(rrVal), 10) : NaN;

    this.props.addChannel({
      youtubeChannelId,
      rootFolderPath: rootFolderPath.value,
      monitor: monitor.value,
      ...(monitor.value === 'roundRobin' && Number.isFinite(rrParsed) && rrParsed > 0
        ? { roundRobinLatestVideoCount: rrParsed }
        : {}),
      qualityProfileId: qualityProfileId.value,
      channelType,
      playlistFolder: playlistFolder.value,
      searchForMissingVideos: searchForMissingVideos.value,
      searchForCutoffUnmetVideos: searchForCutoffUnmetVideos.value,
      filterOutShorts: filterOutShorts.value,
      filterOutLivestreams: filterOutLivestreams.value,
      tags: tags.value
    });
  };

  render() {
    return (
      <AddNewChannelModalContent
        {...this.props}
        diskFolderName={this.state.diskFolderName}
        onInputChange={this.onInputChange}
        onAddChannelPress={this.onAddChannelPress}
      />
    );
  }
}

AddNewChannelModalContentConnector.propTypes = {
  youtubeChannelId: PropTypes.string.isRequired,
  title: PropTypes.string.isRequired,
  titleSlug: PropTypes.string,
  rootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  channelType: PropTypes.object.isRequired,
  playlistFolder: PropTypes.object.isRequired,
  searchForMissingVideos: PropTypes.object.isRequired,
  searchForCutoffUnmetVideos: PropTypes.object.isRequired,
  filterOutShorts: PropTypes.object.isRequired,
  filterOutLivestreams: PropTypes.object.isRequired,
  tags: PropTypes.object.isRequired,
  onModalClose: PropTypes.func.isRequired,
  setAddChannelDefault: PropTypes.func.isRequired,
  addChannel: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewChannelModalContentConnector);
