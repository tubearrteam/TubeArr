import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import {
  clearAddChannel,
  resolveChannel,
  runSearchFallback,
  setAddChannelResolveStatus,
  SEARCH_FALLBACK_DEBOUNCE_MS
} from 'Store/Actions/addChannelActions';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import { classifyInput, isResolvableWithoutSearch } from 'Utilities/Channel/channelInputClassifier';
import parseUrl from 'Utilities/String/parseUrl';
import AddNewChannel from './AddNewChannel';

function createMapStateToProps() {
  return createSelector(
    (state) => state.addChannel,
    (state) => state.channels.items.length,
    (state) => state.router.location,
    (addChannel, existingChannelCount, location) => {
      const { params } = parseUrl(location.search);

      return {
        ...addChannel,
        term: params.term,
        hasExistingChannels: existingChannelCount > 0
      };
    }
  );
}

const mapDispatchToProps = {
  resolveChannel,
  runSearchFallback,
  setAddChannelResolveStatus,
  clearAddChannel,
  fetchRootFolders
};

class AddNewChannelConnector extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._fallbackTimeout = null;
    this._resolveTimeout = null;
    this._inputIdCounter = 0;
  }

  componentDidMount() {
    this.props.fetchRootFolders();
  }

  componentWillUnmount() {
    if (this._fallbackTimeout) {
      clearTimeout(this._fallbackTimeout);
      this._fallbackTimeout = null;
    }
    if (this._resolveTimeout) {
      clearTimeout(this._resolveTimeout);
      this._resolveTimeout = null;
    }

    this.props.clearAddChannel();
  }

  //
  // Listeners

  onChannelLookupChange = (term) => {
    if (this._fallbackTimeout) {
      clearTimeout(this._fallbackTimeout);
      this._fallbackTimeout = null;
    }
    if (this._resolveTimeout) {
      clearTimeout(this._resolveTimeout);
      this._resolveTimeout = null;
    }

    const trimmed = term.trim();
    if (trimmed === '') {
      this.props.clearAddChannel();
      return;
    }

    const classification = classifyInput(term);
    const inputId = String(++this._inputIdCounter);

    // Resolvable input (channel ID, @handle, URL): resolve only (but debounce so we don't resolve on every keystroke)
    if (isResolvableWithoutSearch(classification)) {
      this.props.setAddChannelResolveStatus({ resolveStatus: 'typing', pendingInputId: inputId });
      this._resolveTimeout = setTimeout(() => {
        this._resolveTimeout = null;
        this.props.resolveChannel({ input: trimmed, inputId });
      }, SEARCH_FALLBACK_DEBOUNCE_MS);
      return;
    }

    if (classification.kind === 'SearchTerm' || classification.kind === 'Unknown') {
      this.props.setAddChannelResolveStatus({ resolveStatus: 'searchPending', pendingInputId: inputId });
      this._fallbackTimeout = setTimeout(() => {
        this._fallbackTimeout = null;
        this.props.runSearchFallback({ term: trimmed, inputId });
      }, SEARCH_FALLBACK_DEBOUNCE_MS);
    }
  };

  onClearChannelLookup = () => {
    if (this._fallbackTimeout) {
      clearTimeout(this._fallbackTimeout);
      this._fallbackTimeout = null;
    }
    if (this._resolveTimeout) {
      clearTimeout(this._resolveTimeout);
      this._resolveTimeout = null;
    }
    this.props.clearAddChannel();
  };

  //
  // Render

  render() {
    const {
      term,
      ...otherProps
    } = this.props;

    return (
      <AddNewChannel
        term={term}
        {...otherProps}
        onChannelLookupChange={this.onChannelLookupChange}
        onClearChannelLookup={this.onClearChannelLookup}
      />
    );
  }
}

AddNewChannelConnector.propTypes = {
  term: PropTypes.string,
  resolveChannel: PropTypes.func.isRequired,
  runSearchFallback: PropTypes.func.isRequired,
  setAddChannelResolveStatus: PropTypes.func.isRequired,
  clearAddChannel: PropTypes.func.isRequired,
  fetchRootFolders: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewChannelConnector);
