import { push } from 'redux-first-history';
import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { useParams } from 'react-router-dom';
import { createSelector } from 'reselect';
import NotFound from 'Components/NotFound';
import createAllChannelsSelector from 'Store/Selectors/createAllChannelSelector';
import translate from 'Utilities/String/translate';
import ChannelDetailsConnector from './ChannelDetailsConnector';

function createMapStateToProps() {
  return createSelector(
    (state, ownProps) => ownProps.titleSlug,
    createAllChannelsSelector(),
    (titleSlug, allChannels) => {
      if (!titleSlug) {
        return {};
      }

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
  push: PropTypes.func.isRequired
};

const ConnectedChannelDetailsPageConnector = connect(
  createMapStateToProps,
  mapDispatchToProps
)(ChannelDetailsPageConnector);

function ChannelDetailsPageConnectorWithParams(props) {
  const { titleSlug } = useParams();

  return (
    <ConnectedChannelDetailsPageConnector
      {...props}
      titleSlug={titleSlug}
    />
  );
}

export default ChannelDetailsPageConnectorWithParams;
