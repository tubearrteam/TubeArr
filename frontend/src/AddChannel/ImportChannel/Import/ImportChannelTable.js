import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import VirtualTable from 'Components/Table/VirtualTable';
import VirtualTableRow from 'Components/Table/VirtualTableRow';
import ImportChannelHeader from './ImportChannelHeader';
import ImportChannelRowConnector from './ImportChannelRowConnector';
import { normalizeChannelSearchItem } from 'Utilities/Channel/normalizeChannelSearchItem';

class ImportChannelTable extends Component {

  //
  // Control

  applyUnmappedFolder(unmappedFolder) {
    const {
      rootFolderPath,
      defaultMonitor,
      defaultQualityProfileId,
      defaultChannelType,
      defaultPlaylistFolder,
      onChannelLookup,
      onSetImportChannelValue
    } = this.props;

    const values = {
      monitor: defaultMonitor,
      qualityProfileId: defaultQualityProfileId,
      channelType: defaultChannelType,
      playlistFolder: defaultPlaylistFolder
    };

    const id = unmappedFolder.name;
    const resolveTerm = unmappedFolder.resolveInputTried || id;
    const prefill = unmappedFolder.resolveSuccess === true && unmappedFolder.suggestedChannel;

    if (prefill) {
      const ch = normalizeChannelSearchItem(unmappedFolder.suggestedChannel);
      onSetImportChannelValue({
        id,
        path: unmappedFolder.path,
        relativePath: unmappedFolder.relativePath,
        rootFolderPath,
        term: resolveTerm,
        isFetching: false,
        isPopulated: true,
        isQueued: false,
        error: null,
        items: ch ? [ch] : [],
        selectedChannel: ch,
        ...values
      });
    } else {
      onChannelLookup(id, unmappedFolder.path, unmappedFolder.relativePath, resolveTerm);
      onSetImportChannelValue({
        id,
        rootFolderPath,
        ...values
      });
    }
  }

  //
  // Lifecycle

  componentDidMount() {
    (this.props.unmappedFolders || []).forEach((unmappedFolder) => {
      this.applyUnmappedFolder(unmappedFolder);
    });
  }

  // This isn't great, but it's the most reliable way to ensure the items
  // are checked off even if they aren't actually visible since the cells
  // are virtualized.

  componentDidUpdate(prevProps) {
    const {
      items,
      selectedState,
      onSelectedChange,
      onRemoveSelectedStateItem,
      unmappedFolders
    } = this.props;

    if (prevProps.unmappedFolders !== unmappedFolders) {
      const prevNames = new Set((prevProps.unmappedFolders || []).map((f) => f.name));
      const added = (unmappedFolders || []).filter((f) => !prevNames.has(f.name));

      added.forEach((folder) => {
        this.applyUnmappedFolder(folder);
      });
    }

    prevProps.items.forEach((prevItem) => {
      const {
        id
      } = prevItem;

      const item = _.find(items, { id });

      if (!item) {
        onRemoveSelectedStateItem(id);
        return;
      }

      const selectedChannel = item.selectedChannel;
      const isSelected = selectedState[id];

      const isExistingChannel = !!selectedChannel && (
        (selectedChannel.youtubeChannelId && _.some(prevProps.allChannels, { youtubeChannelId: selectedChannel.youtubeChannelId }))
      );

      // Props doesn't have a selected channel or
      // the selected channel is an existing channel.
      if ((!selectedChannel && prevItem.selectedChannel) || (isExistingChannel && !prevItem.selectedChannel)) {
        onSelectedChange({ id, value: false });

        return;
      }

      // State is selected, but a channel isn't selected or
      // the selected channel is an existing channel.
      if (isSelected && (!selectedChannel || isExistingChannel)) {
        onSelectedChange({ id, value: false });

        return;
      }

      // A channel is being selected that wasn't previously selected.
      if (selectedChannel && selectedChannel !== prevItem.selectedChannel) {
        onSelectedChange({ id, value: true });

        return;
      }
    });

    const isExistingInLibrary = (ch) =>
      !!(ch?.youtubeChannelId && _.some(this.props.allChannels, { youtubeChannelId: ch.youtubeChannelId }));

    items.forEach((item) => {
      const ch = item.selectedChannel;
      if (!ch || isExistingInLibrary(ch)) {
        return;
      }

      if (selectedState[item.id] === false || selectedState[item.id] === true) {
        return;
      }

      const prevItem = _.find(prevProps.items, { id: item.id });
      const channelJustSet = !prevItem || prevItem.selectedChannel !== ch;
      if (channelJustSet) {
        onSelectedChange({ id: item.id, value: true });
      }
    });
  }

  //
  // Control

  rowRenderer = ({ key, rowIndex, style }) => {
    const {
      rootFolderId,
      items,
      selectedState,
      onSelectedChange
    } = this.props;

    const item = items[rowIndex];

    return (
      <VirtualTableRow
        key={key}
        style={style}
      >
        <ImportChannelRowConnector
          key={item.id}
          rootFolderId={rootFolderId}
          isSelected={selectedState[item.id]}
          onSelectedChange={onSelectedChange}
          id={item.id}
        />
      </VirtualTableRow>
    );
  };

  //
  // Render

  render() {
    const {
      items,
      allSelected,
      allUnselected,
      isSmallScreen,
      scroller,
      selectedState,
      onSelectAllChange
    } = this.props;

    if (!items.length) {
      return null;
    }

    return (
      <VirtualTable
        items={items}
        scroller={scroller}
        isSmallScreen={isSmallScreen}
        rowHeight={52}
        overscanRowCount={2}
        rowRenderer={this.rowRenderer}
        header={
          <ImportChannelHeader
            allSelected={allSelected}
            allUnselected={allUnselected}
            onSelectAllChange={onSelectAllChange}
          />
        }
        selectedState={selectedState}
      />
    );
  }
}

ImportChannelTable.propTypes = {
  rootFolderId: PropTypes.number.isRequired,
  rootFolderPath: PropTypes.string,
  items: PropTypes.arrayOf(PropTypes.object),
  unmappedFolders: PropTypes.arrayOf(PropTypes.object),
  defaultMonitor: PropTypes.string.isRequired,
  defaultQualityProfileId: PropTypes.number,
  defaultChannelType: PropTypes.string.isRequired,
  defaultPlaylistFolder: PropTypes.bool.isRequired,
  allSelected: PropTypes.bool.isRequired,
  allUnselected: PropTypes.bool.isRequired,
  selectedState: PropTypes.object.isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  allChannels: PropTypes.arrayOf(PropTypes.object),
  scroller: PropTypes.instanceOf(Element).isRequired,
  onSelectAllChange: PropTypes.func.isRequired,
  onSelectedChange: PropTypes.func.isRequired,
  onRemoveSelectedStateItem: PropTypes.func.isRequired,
  onChannelLookup: PropTypes.func.isRequired,
  onSetImportChannelValue: PropTypes.func.isRequired
};

export default ImportChannelTable;
