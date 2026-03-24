import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { kinds } from 'Helpers/Props';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import AddTagModal from './AddTag/AddTagModal';
import TagConnector from './TagConnector';
import styles from './Tags.css';

class Tags extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isAddTagModalOpen: false
    };
  }

  onAddTagPress = () => {
    this.setState({ isAddTagModalOpen: true });
  };

  onAddTagModalClose = () => {
    this.setState({ isAddTagModalOpen: false });
  };

  render() {
    const {
      items,
      ...otherProps
    } = this.props;

    const { isAddTagModalOpen } = this.state;

    return (
      <FieldSet
        legend={translate('Tags')}
      >
        <PageSectionContent
          errorMessage={translate('TagsLoadError')}
          {...otherProps}
        >
          <div className={styles.tags}>
            {
              items.map((item) => {
                return (
                  <TagConnector
                    key={item.id}
                    {...item}
                  />
                );
              })
            }

            <Card
              className={styles.addTag}
              onPress={this.onAddTagPress}
            >
              <div className={styles.center}>
                <Icon
                  name={icons.ADD}
                  size={45}
                />
              </div>
            </Card>
          </div>

          <AddTagModal
            isOpen={isAddTagModalOpen}
            onModalClose={this.onAddTagModalClose}
          />
        </PageSectionContent>
      </FieldSet>
    );
  }
}

Tags.propTypes = {
  items: PropTypes.arrayOf(PropTypes.object).isRequired
};

export default Tags;
