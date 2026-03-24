import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import EditChannelModal from './EditChannelModal';

const mapDispatchToProps = {
  clearPendingChanges
};

class EditChannelModalConnector extends Component {

  //
  // Listeners

  onModalClose = () => {
    this.props.clearPendingChanges({ section: 'channels' });
    this.props.onModalClose();
  };

  //
  // Render

  render() {
    return (
      <EditChannelModal
        {...this.props}
        onModalClose={this.onModalClose}
      />
    );
  }
}

EditChannelModalConnector.propTypes = {
  ...EditChannelModal.propTypes,
  onModalClose: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired
};

export default connect(undefined, mapDispatchToProps)(EditChannelModalConnector);
