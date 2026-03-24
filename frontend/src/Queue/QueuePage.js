import PropTypes from 'prop-types';
import React, { useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TablePager from 'Components/Table/TablePager';
import { align, icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import QueueTableRow from './QueueTableRow';

function QueuePage(props) {
  const {
    isFetching,
    isPopulated,
    error,
    items,
    columns,
    totalRecords,
    pageTitle,
    sectionTitle,
    onRefreshPress,
    onClearQueuePress,
    onStartDownloadsPress,
    onRemoveQueueItemPress,
    ...otherProps
  } = props;

  const [isClearConfirmOpen, setIsClearConfirmOpen] = useState(false);

  const handleClearQueueConfirm = () => {
    onClearQueuePress();
    setIsClearConfirmOpen(false);
  };

  return (
    <PageContent title={pageTitle}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label={translate('Refresh')}
            iconName={icons.REFRESH}
            spinningName={icons.REFRESH}
            isSpinning={isFetching}
            onPress={onRefreshPress}
          />
          {items.length > 0 && (
            <>
              <PageToolbarButton
                label={translate('StartDownloads')}
                iconName={icons.DOWNLOAD}
                onPress={onStartDownloadsPress}
              />
              <PageToolbarButton
                label={translate('ClearQueue')}
                iconName={icons.CLEAR}
                onPress={() => setIsClearConfirmOpen(true)}
              />
            </>
          )}
        </PageToolbarSection>
      </PageToolbar>

      <ConfirmModal
        isOpen={isClearConfirmOpen}
        kind={kinds.DANGER}
        title={translate('ClearQueue')}
        message={translate('ClearQueueMessage')}
        confirmLabel={translate('ClearQueue')}
        onConfirm={handleClearQueueConfirm}
        onCancel={() => setIsClearConfirmOpen(false)}
      />

      <PageContentBody>
        <FieldSet legend={sectionTitle}>
          {isFetching && !isPopulated && <LoadingIndicator />}

          {error && (
            <Alert kind={kinds.DANGER}>
              {translate('QueueLoadError')}
            </Alert>
          )}

          {isPopulated && !error && !items.length && (
            <Alert kind={kinds.INFO}>
              {translate('QueueIsEmpty')}
            </Alert>
          )}

          {isPopulated && !error && !!items.length && (
            <div>
              <Table columns={columns} {...otherProps}>
                <TableBody>
                  {items.map((item) => (
                    <QueueTableRow
                      key={item.id}
                      columns={columns}
                      onRemovePress={onRemoveQueueItemPress}
                      {...item}
                    />
                  ))}
                </TableBody>
              </Table>

              <TablePager
                totalRecords={totalRecords}
                isFetching={isFetching}
                {...otherProps}
              />
            </div>
          )}
        </FieldSet>
      </PageContentBody>
    </PageContent>
  );
}

QueuePage.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  columns: PropTypes.array.isRequired,
  totalRecords: PropTypes.number,
  pageTitle: PropTypes.string,
  sectionTitle: PropTypes.string,
  onRefreshPress: PropTypes.func.isRequired,
  onClearQueuePress: PropTypes.func.isRequired,
  onStartDownloadsPress: PropTypes.func.isRequired,
  onRemoveQueueItemPress: PropTypes.func.isRequired
};

QueuePage.defaultProps = {
  pageTitle: `${translate('Download')} ${translate('Queue')}`,
  sectionTitle: `${translate('Download')} ${translate('Queue')}`
};

export default QueuePage;
