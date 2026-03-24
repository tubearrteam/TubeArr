import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Card from 'Components/Card';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Tooltip from 'Components/Tooltip/Tooltip';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditQualityProfileModalConnector from './EditQualityProfileModalConnector';
import styles from './QualityProfile.css';

const FALLBACK_MODE_NAMES = {
  0: 'Strict',
  1: 'Next Best (Within Ceiling)',
  2: 'Degrade Resolution',
  3: 'Next Best'
};

function toChipText(values) {
  if (!Array.isArray(values) || !values.length) {
    return 'Any';
  }

  return values.join(' / ');
}

function toResolutionRange(minHeight, maxHeight) {
  if (minHeight == null && maxHeight == null) {
    return 'Any';
  }

  if (minHeight != null && maxHeight != null) {
    return `${minHeight}p - ${maxHeight}p`;
  }

  if (minHeight != null) {
    return `>= ${minHeight}p`;
  }

  return `<= ${maxHeight}p`;
}

function toHdrSdrLabel(allowHdr, allowSdr, hdrHandling) {
  if (hdrHandling === 'sdr') {
    return 'SDR Preferred';
  }

  if (allowHdr && allowSdr) {
    return 'HDR + SDR';
  }

  if (allowHdr) {
    return 'HDR only';
  }

  if (allowSdr) {
    return 'SDR only';
  }

  return 'Disabled';
}

class QualityProfile extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isEditQualityProfileModalOpen: false,
      isDeleteQualityProfileModalOpen: false
    };
  }

  //
  // Listeners

  onEditQualityProfilePress = () => {
    this.setState({ isEditQualityProfileModalOpen: true });
  };

  onEditQualityProfileModalClose = () => {
    this.setState({ isEditQualityProfileModalOpen: false });
  };

  onDeleteQualityProfilePress = () => {
    this.setState({
      isEditQualityProfileModalOpen: false,
      isDeleteQualityProfileModalOpen: true
    });
  };

  onDeleteQualityProfileModalClose = () => {
    this.setState({ isDeleteQualityProfileModalOpen: false });
  };

  onConfirmDeleteQualityProfile = () => {
    this.props.onConfirmDeleteQualityProfile(this.props.id);
  };

  onCloneQualityProfilePress = () => {
    const {
      id,
      onCloneQualityProfilePress
    } = this.props;

    onCloneQualityProfilePress(id);
  };

  //
  // Render

  render() {
    const {
      id,
      name,
      upgradeAllowed,
      cutoff,
      items = [],
      maxHeight,
      minHeight,
      preferredVideoCodecs,
      allowedVideoCodecs,
      fallbackMode,
      allowHdr,
      allowSdr,
      hdrHandling,
      isDeleting
    } = this.props;

    const hasLegacyQualities = Array.isArray(items) && items.length > 0 && items.some((i) => i.quality != null || i.allowed != null);
    const isYouTubeShape = !hasLegacyQualities;
    const fallbackModeLabel = FALLBACK_MODE_NAMES[fallbackMode] || 'Unknown';
    const hdrSdrLabel = toHdrSdrLabel(allowHdr, allowSdr, hdrHandling);

    return (
      <Card
        className={styles.qualityProfile}
        overlayContent={true}
        onPress={this.onEditQualityProfilePress}
      >
        <div className={styles.nameContainer}>
          <div className={styles.name}>
            {name}
          </div>

          <IconButton
            className={styles.cloneButton}
            title={translate('CloneProfile')}
            name={icons.CLONE}
            onPress={this.onCloneQualityProfilePress}
          />
        </div>

        <div className={styles.qualities}>
          {isYouTubeShape ? (
            <>
              <Label kind={kinds.DEFAULT}>
                Resolution range: {toResolutionRange(minHeight, maxHeight)}
              </Label>

              <Label kind={kinds.DEFAULT}>
                Preferred quality: {toChipText(preferredVideoCodecs)}
              </Label>

              <Label kind={kinds.DEFAULT}>
                Allowed quality: {toChipText(allowedVideoCodecs)}
              </Label>

              <Label kind={kinds.DEFAULT}>
                Fallback mode: {fallbackModeLabel}
              </Label>

              <Label kind={kinds.DEFAULT}>
                HDR/SDR: {hdrSdrLabel}
              </Label>
            </>
          ) : (
            (items || []).map((item) => {
              if (!item.allowed) {
                return null;
              }

              if (item.quality) {
                const isCutoff = upgradeAllowed && item.quality.id === cutoff;

                return (
                  <Label
                    key={item.quality.id}
                    kind={isCutoff ? kinds.INFO : kinds.DEFAULT}
                    title={isCutoff ? translate('UpgradeUntilThisQualityIsMetOrExceeded') : null}
                  >
                    {item.quality.name}
                  </Label>
                );
              }

              const isCutoff = upgradeAllowed && item.id === cutoff;
              const groupItems = item.items || [];

              return (
                <Tooltip
                  key={item.id}
                  className={styles.tooltipLabel}
                  anchor={
                    <Label
                      kind={isCutoff ? kinds.INFO : kinds.DEFAULT}
                      title={isCutoff ? translate('Cutoff') : null}
                    >
                      {item.name}
                    </Label>
                  }
                  tooltip={
                    <div>
                      {
                        groupItems.map((groupItem) => {
                          return (
                            <Label
                              key={groupItem.quality?.id ?? groupItem.id}
                              kind={isCutoff ? kinds.INFO : kinds.DEFAULT}
                              title={isCutoff ? translate('Cutoff') : null}
                            >
                              {groupItem.quality?.name ?? groupItem.name}
                            </Label>
                          );
                        })
                      }
                    </div>
                  }
                  kind={kinds.INVERSE}
                  position={tooltipPositions.TOP}
                />
              );
            })
          )}
        </div>

        <EditQualityProfileModalConnector
          id={id}
          isOpen={this.state.isEditQualityProfileModalOpen}
          onModalClose={this.onEditQualityProfileModalClose}
          onDeleteQualityProfilePress={this.onDeleteQualityProfilePress}
        />

        <ConfirmModal
          isOpen={this.state.isDeleteQualityProfileModalOpen}
          kind={kinds.DANGER}
          title={translate('DeleteQualityProfile')}
          message={translate('DeleteQualityProfileMessageText', { name })}
          confirmLabel={translate('Delete')}
          isSpinning={isDeleting}
          onConfirm={this.onConfirmDeleteQualityProfile}
          onCancel={this.onDeleteQualityProfileModalClose}
        />
      </Card>
    );
  }
}

QualityProfile.propTypes = {
  id: PropTypes.number.isRequired,
  name: PropTypes.string.isRequired,
  upgradeAllowed: PropTypes.bool,
  cutoff: PropTypes.number,
  items: PropTypes.arrayOf(PropTypes.object),
  maxHeight: PropTypes.number,
  minHeight: PropTypes.number,
  preferredVideoCodecs: PropTypes.arrayOf(PropTypes.string),
  allowedVideoCodecs: PropTypes.arrayOf(PropTypes.string),
  fallbackMode: PropTypes.number,
  allowHdr: PropTypes.bool,
  allowSdr: PropTypes.bool,
  hdrHandling: PropTypes.string,
  isDeleting: PropTypes.bool.isRequired,
  onConfirmDeleteQualityProfile: PropTypes.func.isRequired,
  onCloneQualityProfilePress: PropTypes.func.isRequired
};

export default QualityProfile;
