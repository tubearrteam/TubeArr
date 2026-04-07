import PropTypes from 'prop-types';
import React, { Component } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import Icon from 'Components/Icon';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './OrganizePreviewRow.css';

class OrganizePreviewRow extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      id,
      onSelectedChange,
      safeToApply
    } = this.props;

    if (safeToApply !== false) {
      onSelectedChange({ id, value: true });
    }
  }

  //
  // Listeners

  onSelectedChange = ({ value, shiftKey }) => {
    const {
      id,
      onSelectedChange
    } = this.props;

    onSelectedChange({ id, value, shiftKey });
  };

  //
  // Render

  render() {
    const {
      id,
      existingPath,
      newPath,
      isSelected,
      collision,
      allowCollisionRename
    } = this.props;

    const blockCollisionSelection = collision === true && !allowCollisionRename;

    return (
      <div className={styles.row}>
        <CheckInput
          containerClassName={styles.selectedContainer}
          name={id.toString()}
          value={isSelected}
          isDisabled={blockCollisionSelection}
          onChange={this.onSelectedChange}
        />

        <div>
          {
            collision ?
              <div className={styles.collisionNote}>
                <Icon
                  name={icons.WARNING}
                  kind={kinds.WARNING}
                />
                <span>{translate('OrganizeRenameCollisionHint')}</span>
              </div> :
              null
          }

          <div>
            <Icon
              name={icons.SUBTRACT}
              kind={kinds.DANGER}
            />

            <span className={styles.path}>
              {existingPath}
            </span>
          </div>

          <div>
            <Icon
              name={icons.ADD}
              kind={collision ? kinds.WARNING : kinds.SUCCESS}
            />

            <span className={styles.path}>
              {newPath}
            </span>
          </div>
        </div>
      </div>
    );
  }
}

OrganizePreviewRow.propTypes = {
  id: PropTypes.number.isRequired,
  existingPath: PropTypes.string.isRequired,
  newPath: PropTypes.string.isRequired,
  isSelected: PropTypes.bool,
  collision: PropTypes.bool,
  safeToApply: PropTypes.bool,
  allowCollisionRename: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired
};

export default OrganizePreviewRow;
