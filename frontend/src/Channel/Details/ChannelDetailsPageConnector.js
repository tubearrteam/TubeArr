import { push } from 'connected-react-router';
import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import NotFound from 'Components/NotFound';
import createAllChannelsSelector from 'Store/Selectors/createAllChannelSelector';
import translate from 'Utilities/String/translate';
import ChannelDetailsConnector from './ChannelDetailsConnector';

function createMapStateToProps() {
  return createSelector(
    (state, { match }) => match,
    createAllChannelsSelector(),
    (match, allChannels) => {
      const titleSlug = match.params.titleSlug;
      const channelIndex = _.findIndex(allChannels, { titleSlug });

      if (channelIndex > -1) {
        return {
          titleSlug
        };
      }

      return {};
    }
  );
}

const mapDispatchToProps = {
  push
};

class ChannelDetailsPageConnector extends Component {

  //
  // Lifecycle

  componentDidUpdate(prevProps) {
    if (!this.props.titleSlug) {
      this.props.push(`${window.TubeArr.urlBase}/`);
      return;
    }
  }

  //
  // Render

  render() {
    const {
      titleSlug
    } = this.props;

    if (!titleSlug) {
      return (
        <NotFound
          message={translate('ChannelCannotBeFound')}
        />
      );
    }

    return (
      <ChannelDetailsConnector
        titleSlug={titleSlug}
      />
    );
  }
}

ChannelDetailsPageConnector.propTypes = {
  titleSlug: PropTypes.string,
  match: PropTypes.shape({ params: PropTypes.shape({ titleSlug: PropTypes.string.isRequired }).isRequired }).isRequired,
  push: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ChannelDetailsPageConnector);
