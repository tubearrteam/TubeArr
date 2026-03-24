import PropTypes from 'prop-types';
import React, { Component } from 'react';
import ChannelPoster from 'Channel/ChannelPoster';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import { icons, kinds, sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import AddNewChannelModal from './AddNewChannelModal';
import styles from './AddNewChannelSearchResult.css';

class AddNewChannelSearchResult extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isNewAddChannelModalOpen: false
    };
  }

  componentDidUpdate(prevProps) {
    if (!prevProps.isExistingChannel && this.props.isExistingChannel) {
      this.onAddChannelModalClose();
    }
  }

  //
  // Listeners

  onPress = () => {
    this.setState({ isNewAddChannelModalOpen: true });
  };

  onAddChannelModalClose = () => {
    this.setState({ isNewAddChannelModalOpen: false });
  };

  //
  // Render

  render() {
    const {
      youtubeChannelId,
      title,
      titleSlug,
      description,
      thumbnailUrl,
      isExistingChannel,
      isSmallScreen
    } = this.props;

    const {
      isNewAddChannelModalOpen
    } = this.state;

    const linkProps = isExistingChannel ? { to: `/channels/${titleSlug}` } : { onPress: this.onPress };

    const images = thumbnailUrl
      ? [{ coverType: 'poster', url: thumbnailUrl, remoteUrl: thumbnailUrl }]
      : [];

    return (
      <div className={styles.searchResult}>
        <Link
          className={styles.underlay}
          {...linkProps}
        />

        <div className={styles.overlay}>
          {
            isSmallScreen ?
              null :
              <ChannelPoster
                className={styles.poster}
                images={images}
                size={250}
                overflow={true}
                lazy={false}
              />
          }

          <div className={styles.content}>
            <div className={styles.titleRow}>
              <div className={styles.titleContainer}>
                <div className={styles.title}>
                  {title}
                </div>
              </div>

              <div className={styles.icons}>
                {
                  isExistingChannel ?
                    <Icon
                      className={styles.alreadyExistsIcon}
                      name={icons.CHECK_CIRCLE}
                      size={36}
                      title={translate('AlreadyInYourLibrary')}
                    /> :
                    null
                }
              </div>
            </div>

              <div>
                <Label size={sizes.LARGE}>{youtubeChannelId}</Label>

                {
                  !isExistingChannel ?
                    <Label
                      kind={kinds.SUCCESS}
                      size={sizes.LARGE}
                    >
                      {translate('AddNewChannel')}
                    </Label> :
                    null
                }
              </div>

            <div className={styles.overview}>
                {description}
            </div>
          </div>
        </div>

        <AddNewChannelModal
          isOpen={isNewAddChannelModalOpen && !isExistingChannel}
          youtubeChannelId={youtubeChannelId}
          title={title}
          titleSlug={titleSlug}
          description={description}
          thumbnailUrl={thumbnailUrl}
          onModalClose={this.onAddChannelModalClose}
        />
      </div>
    );
  }
}

AddNewChannelSearchResult.propTypes = {
  youtubeChannelId: PropTypes.string.isRequired,
  title: PropTypes.string.isRequired,
  titleSlug: PropTypes.string.isRequired,
    description: PropTypes.string,
    thumbnailUrl: PropTypes.string,
  isExistingChannel: PropTypes.bool.isRequired,
  isSmallScreen: PropTypes.bool.isRequired
};

export default AddNewChannelSearchResult;
