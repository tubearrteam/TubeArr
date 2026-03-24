import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

class AddTagModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      label: ''
    };
  }

  //
  // Listeners

  onLabelChange = ({ value }) => {
    this.setState({ label: value });
  };

  onSavePress = () => {
    const { label } = this.state;
    const { onSave } = this.props;

    if (!label.trim()) {
      return;
    }

    onSave({ label: label.trim() });
  };

  //
  // Render

  render() {
    const { onModalClose } = this.props;
    const { label } = this.state;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {translate('AddTag')}
        </ModalHeader>

        <ModalBody>
          <Form>
            <FormGroup>
              <FormLabel>{translate('Label')}</FormLabel>
              <FormInputGroup
                type={inputTypes.TEXT}
                name="label"
                value={label}
                onChange={this.onLabelChange}
                placeholder={translate('TagNamePlaceholder')}
              />
            </FormGroup>
          </Form>
        </ModalBody>

        <ModalFooter>
          <Button
            onPress={onModalClose}
          >
            {translate('Cancel')}
          </Button>
          <Button
            onPress={this.onSavePress}
            disabled={!label.trim()}
          >
            {translate('Add')}
          </Button>
        </ModalFooter>
      </ModalContent>
    );
  }
}

AddTagModalContent.propTypes = {
  onSave: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default AddTagModalContent;
