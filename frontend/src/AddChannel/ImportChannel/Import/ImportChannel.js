import { reduce } from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import Icon from 'Components/Icon';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import selectAll from 'Utilities/Table/selectAll';
import toggleSelected from 'Utilities/Table/toggleSelected';
import ImportChannelFooterConnector from './ImportChannelFooterConnector';
import ImportChannelTableConnector from './ImportChannelTableConnector';
import styles from './ImportChannel.css';

class ImportChannel extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.scrollerRef = React.createRef();

    this.state = {
      allSelected: false,
      allUnselected: false,
      lastToggled: null,
      selectedState: {}
    };
  }

  //
  // Listeners

  getSelectedIds = () => {
    return reduce(
      this.state.selectedState,
      (result, value, id) => {
        if (value) {
          result.push(id);
        }

        return result;
      },
      []
    );
  };

  onSelectAllChange = ({ value }) => {
    // Only select non-dupes
    this.setState(selectAll(this.state.selectedState, value));
  };

  onSelectedChange = ({ id, value, shiftKey = false }) => {
    this.setState((state) => {
      return toggleSelected(state, this.props.items, id, value, shiftKey);
    });
  };

  onRemoveSelectedStateItem = (id) => {
    this.setState((state) => {
      const selectedState = Object.assign({}, state.selectedState);
      delete selectedState[id];

      return {
        ...state,
        selectedState
      };
    });
  };

  onInputChange = ({ name, value }) => {
    this.props.onInputChange(this.getSelectedIds(), name, value);
  };

  onImportPress = () => {
    this.props.onImportPress(this.getSelectedIds());
  };

  //
  // Render

  render() {
    const {
      rootFolderId,
      path,
      rootFoldersFetching,
      rootFoldersPopulated,
      rootFoldersError,
      unmappedFolders,
      onScanUnmonitoredFoldersPress,
      isScanningUnmonitoredFolders = false
    } = this.props;

    const {
      allSelected,
      allUnselected,
      selectedState
    } = this.state;

    return (
      <PageContent title={translate('ImportChannel')}>
        <PageContentBody ref={this.scrollerRef} >
          {
            rootFoldersFetching && !rootFoldersPopulated ?
              <LoadingIndicator /> :
              null
          }

          {
            !rootFoldersFetching && !!rootFoldersError ?
              <Alert kind={kinds.DANGER}>
                {translate('RootFoldersLoadError')}
              </Alert> :
              null
          }

          {
            !rootFoldersError &&
            rootFoldersPopulated &&
            path &&
            onScanUnmonitoredFoldersPress ?
              <div className={styles.scanBar}>
                <Button
                  kind={kinds.DEFAULT}
                  isDisabled={isScanningUnmonitoredFolders}
                  onPress={onScanUnmonitoredFoldersPress}
                >
                  <Icon
                    className={styles.scanButtonIcon}
                    name={icons.SEARCH}
                    isSpinning={isScanningUnmonitoredFolders}
                  />

                  {translate('LibraryImportScanUnmonitoredFolders')}
                </Button>
              </div> :
              null
          }

          {
            isScanningUnmonitoredFolders ?
              <LoadingIndicator /> :
              null
          }

          {
            !rootFoldersError &&
            rootFoldersPopulated &&
            !rootFoldersFetching &&
            !isScanningUnmonitoredFolders &&
            !unmappedFolders.length ?
              <Alert kind={kinds.INFO}>
                {translate('AllChannelInRootFolderHaveBeenImported', { path })}
              </Alert> :
              null
          }

          {
            !rootFoldersError &&
            rootFoldersPopulated &&
            !!unmappedFolders.length ?
              <Alert
                kind={kinds.INFO}
                className={styles.importHint}
              >
                {translate('LibraryImportSelectRowsToAdd')}
              </Alert> :
              null
          }

          {
            !rootFoldersError &&
            rootFoldersPopulated &&
            !!unmappedFolders.length &&
            this.scrollerRef.current ?
              <ImportChannelTableConnector
                rootFolderId={rootFolderId}
                rootFolderPath={path}
                unmappedFolders={unmappedFolders}
                allSelected={allSelected}
                allUnselected={allUnselected}
                selectedState={selectedState}
                scroller={this.scrollerRef.current}
                onSelectAllChange={this.onSelectAllChange}
                onSelectedChange={this.onSelectedChange}
                onRemoveSelectedStateItem={this.onRemoveSelectedStateItem}
              /> :
              null
          }
        </PageContentBody>

        {
          !rootFoldersError &&
          rootFoldersPopulated &&
          !!unmappedFolders.length ?
            <ImportChannelFooterConnector
              selectedIds={this.getSelectedIds()}
              onInputChange={this.onInputChange}
              onImportPress={this.onImportPress}
            /> :
            null
        }
      </PageContent>
    );
  }
}

ImportChannel.propTypes = {
  rootFolderId: PropTypes.number.isRequired,
  path: PropTypes.string,
  rootFoldersFetching: PropTypes.bool.isRequired,
  rootFoldersPopulated: PropTypes.bool.isRequired,
  rootFoldersError: PropTypes.object,
  unmappedFolders: PropTypes.arrayOf(PropTypes.object),
  items: PropTypes.arrayOf(PropTypes.object),
  isScanningUnmonitoredFolders: PropTypes.bool,
  onScanUnmonitoredFoldersPress: PropTypes.func,
  onInputChange: PropTypes.func.isRequired,
  onImportPress: PropTypes.func.isRequired
};

ImportChannel.defaultProps = {
  unmappedFolders: []
};

export default ImportChannel;
