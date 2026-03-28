import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import Link from 'Components/Link/Link';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import translate from 'Utilities/String/translate';

const API_PRIORITY_ITEMS = [
  {
    key: 'channelSearch',
    labelKey: 'ApiPriorityChannelSearch',
    unitCostKey: 'ApiPriorityChannelSearchUnitCost'
  },
  {
    key: 'channelResolve',
    labelKey: 'ApiPriorityChannelResolve',
    unitCostKey: 'ApiPriorityChannelResolveUnitCost'
  },
  {
    key: 'channelMetadata',
    labelKey: 'ApiPriorityChannelMetadata',
    unitCostKey: 'ApiPriorityChannelMetadataUnitCost'
  },
  {
    key: 'videoListing',
    labelKey: 'ApiPriorityVideoListing',
    unitCostKey: 'ApiPriorityVideoListingUnitCost'
  },
  {
    key: 'videoDetails',
    labelKey: 'ApiPriorityVideoDetails',
    unitCostKey: 'ApiPriorityVideoDetailsUnitCost'
  },
  {
    key: 'livestreamIdentification',
    labelKey: 'ApiPriorityLivestreamIdentification',
    unitCostKey: 'ApiPriorityLivestreamIdentificationUnitCost'
  }
];

class YouTubeSettings extends Component {

  onPriorityItemChange = ({ name, value }) => {
    const { settings, onInputChange } = this.props;
    const current = (settings.apiPriorityMetadataItems.value || []).slice();

    if (value) {
      if (!current.includes(name)) {
        current.push(name);
      }
    } else {
      const idx = current.indexOf(name);
      if (idx !== -1) {
        current.splice(idx, 1);
      }
    }

    onInputChange({ name: 'apiPriorityMetadataItems', value: current });
  };

  render() {
    const {
      isFetching,
      error,
      settings,
      hasSettings,
      onInputChange,
      onSavePress,
      ...otherProps
    } = this.props;

    const useYouTubeApi = hasSettings && settings.useYouTubeApi ? settings.useYouTubeApi.value : false;
    const priorityItems = hasSettings && settings.apiPriorityMetadataItems ? (settings.apiPriorityMetadataItems.value || []) : [];

    return (
      <PageContent title={translate('YouTubeSettings')}>
        <SettingsToolbarConnector
          {...otherProps}
          onSavePress={onSavePress}
        />

        <PageContentBody>
          {
            isFetching ?
              <LoadingIndicator /> :
              null
          }

          {
            !isFetching && error ?
              <Alert kind={kinds.DANGER}>
                {translate('YouTubeSettingsLoadError')}
              </Alert> :
              null
          }

          {
            hasSettings && !isFetching && !error ?
              <Form
                id="youtubeSettings"
                {...otherProps}
              >
                <FieldSet legend={translate('YouTubeApiKey')}>
                  <Alert kind={kinds.INFO}>
                    {translate('YouTubeApiKeyOptionalPerformanceNote')}
                    <Link to="/settings/tools/ytdlp">
                      {translate('YouTubeApiKeyOptionalPerformanceNoteToolsLink')}
                    </Link>
                  </Alert>

                  <FormGroup>
                    <FormLabel>{translate('ApiKey')}</FormLabel>
                    <FormInputGroup
                      type={inputTypes.PASSWORD}
                      name="apiKey"
                      placeholder={translate('YouTubeApiKeyPlaceholder')}
                      helpText={translate('YouTubeApiKeyHelpText')}
                      onChange={onInputChange}
                      {...settings.apiKey}
                    />
                  </FormGroup>

                  <FormGroup size={sizes.MEDIUM}>
                    <FormLabel>{translate('UseYouTubeApi')}</FormLabel>
                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="useYouTubeApi"
                      helpText={translate('UseYouTubeApiHelpText')}
                      onChange={onInputChange}
                      {...settings.useYouTubeApi}
                    />
                  </FormGroup>
                </FieldSet>

                {
                  useYouTubeApi ?
                    <FieldSet legend={translate('ApiPriorityMetadataItems')}>
                      <Alert kind={kinds.INFO}>
                        {translate('ApiPriorityMetadataItemsHelpText')}
                      </Alert>

                      {
                        API_PRIORITY_ITEMS.map(({ key, labelKey, unitCostKey }) => (
                          <React.Fragment key={key}>
                            <FormGroup size={sizes.MEDIUM}>
                              <FormLabel>{translate(labelKey)}</FormLabel>
                              <FormInputGroup
                                type={inputTypes.CHECK}
                                name={key}
                                value={priorityItems.includes(key)}
                                onChange={this.onPriorityItemChange}
                              />
                            </FormGroup>

                            {
                              priorityItems.includes(key) ?
                                <Alert kind={kinds.WARNING}>
                                  <div>{translate('YouTubeApiUnitCostsWarningByItem', { operation: translate(labelKey), cost: translate(unitCostKey) })}</div>

                                  {
                                    key === 'channelSearch' || key === 'channelResolve' ?
                                      <div>{translate('YouTubeApiSearchResolveRecommendation')}</div> :
                                      null
                                  }
                                </Alert> :
                                null
                            }
                          </React.Fragment>
                        ))
                      }
                    </FieldSet> :
                    null
                }
              </Form>
              : null
          }
        </PageContentBody>
      </PageContent>
    );
  }
}

YouTubeSettings.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired
};

export default YouTubeSettings;
