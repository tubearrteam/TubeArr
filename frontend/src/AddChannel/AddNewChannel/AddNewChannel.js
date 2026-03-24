import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { icons, kinds } from 'Helpers/Props';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import AddNewChannelSearchResultConnector from './AddNewChannelSearchResultConnector';
import styles from './AddNewChannel.css';

class AddNewChannel extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      term: props.term || '',
      isFetching: false
    };
  }

  componentDidMount() {
    const term = this.state.term;

    if (term) {
      this.props.onChannelLookupChange(term);
    }
  }

  componentDidUpdate(prevProps) {
    const {
      term,
      isFetching
    } = this.props;

    if (term && term !== prevProps.term) {
      this.setState({
        term,
        isFetching: true
      });
      this.props.onChannelLookupChange(term);
    } else if (isFetching !== prevProps.isFetching) {
      this.setState({
        isFetching
      });
    }
  }

  getStatusMessage = () => {
    const { resolveStatus } = this.props;
    if (resolveStatus === 'resolving') {
      return translate('AddNewChannelResolving');
    }
    if (resolveStatus === 'searchPending' || resolveStatus === 'searching') {
      return translate('AddNewChannelSearching');
    }
    if (resolveStatus === 'resolvedDirect' || resolveStatus === 'resolvedHttp') {
      return translate('AddNewChannelResolvedFromUrl');
    }
    if (resolveStatus === 'failed') {
      return translate('AddNewChannelNoDirectMatchSearching');
    }
    return null;
  };

  //
  // Listeners

  onSearchInputChange = ({ value }) => {
    const hasValue = !!value.trim();

    this.setState({ term: value, isFetching: hasValue }, () => {
      if (hasValue) {
        this.props.onChannelLookupChange(value);
      } else {
        this.props.onClearChannelLookup();
      }
    });
  };

  onClearChannelLookupPress = () => {
    this.setState({ term: '' });
    this.props.onClearChannelLookup();
  };

  //
  // Render

  render() {
    const {
      error,
      items,
      hasExistingChannels,
      resolveStatus
    } = this.props;

    const term = this.state.term;
    const isFetching = this.state.isFetching;
    const statusMessage = this.getStatusMessage();

    return (
      <PageContent title={translate('AddNewChannel')}>
        <PageContentBody>
          <div className={styles.searchContainer}>
            <div className={styles.searchIconContainer}>
              <Icon
                name={icons.SEARCH}
                size={20}
              />
            </div>

            <TextInput
              className={styles.searchInput}
              name="channelLookup"
              value={term}
              placeholder={translate('AddNewChannelLookupPlaceholder')}
              autoFocus={true}
              onChange={this.onSearchInputChange}
            />

            <Button
              className={styles.clearLookupButton}
              onPress={this.onClearChannelLookupPress}
            >
              <Icon
                name={icons.REMOVE}
                size={20}
              />
            </Button>
          </div>

          {
            isFetching &&
              <>
                <LoadingIndicator />
                {statusMessage && <div className={styles.statusText}>{statusMessage}</div>}
              </>
          }

          {
            !isFetching && !!error ?
              <div className={styles.message}>
                <div className={styles.helpText}>
                  {translate('AddNewChannelError')}
                </div>

                <Alert kind={kinds.DANGER}>{getErrorMessage(error)}</Alert>
              </div> : null
          }

          {
            !isFetching && !error && !!items.length &&
              <div className={styles.searchResults}>
                {
                  items.map((item) => {
                    return (
                      <AddNewChannelSearchResultConnector
                        key={item.youtubeChannelId || item.id}
                        {...item}
                      />
                    );
                  })
                }
              </div>
          }

          {
            !isFetching && !error && !items.length && !!term &&
              <div className={styles.message}>
                <div className={styles.noResults}>{translate('CouldNotFindResults', { term })}</div>
                <div>{translate('SearchByChannelId')}</div>
              </div>
          }

          {
            term ?
              null :
              <div className={styles.message}>
                <div className={styles.helpText}>
                  {translate('AddNewChannelHelpText')}
                </div>
                <div>{translate('SearchByChannelId')}</div>
              </div>
          }

          {
            !term && !hasExistingChannels ?
              <div className={styles.message}>
                <div className={styles.noChannelsText}>
                  {translate('NoChannelHaveBeenAdded')}
                </div>
                <div>
                  <Button
                    to="/add/import"
                    kind={kinds.PRIMARY}
                  >
                    {translate('ImportExistingChannel')}
                  </Button>
                </div>
              </div> :
              null
          }

          <div />
        </PageContentBody>
      </PageContent>
    );
  }
}

AddNewChannel.propTypes = {
  term: PropTypes.string,
  isFetching: PropTypes.bool.isRequired,
  resolveStatus: PropTypes.string,
  error: PropTypes.object,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  hasExistingChannels: PropTypes.bool.isRequired,
  onChannelLookupChange: PropTypes.func.isRequired,
  onClearChannelLookup: PropTypes.func.isRequired
};

export default AddNewChannel;
