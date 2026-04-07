import PropTypes from 'prop-types';
import React, { useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TablePager from 'Components/Table/TablePager';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import HistoryTableRow from './HistoryTableRow';
import MetadataHistorySection from './MetadataHistorySection';
import OpsHistorySection from './OpsHistorySection';

function HistoryPage(props) {
  const {
    isFetching,
    isPopulated,
    error,
    items,
    columns,
    totalRecords,
    onRefreshPress,
    ...otherProps
  } = props;

  const [opsHistoryNonce, setOpsHistoryNonce] = useState(0);

  const handleRefreshPress = () => {
    onRefreshPress();
    setOpsHistoryNonce((n) => n + 1);
  };

  return (
    <PageContent title={translate('History')}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label={translate('Refresh')}
            iconName={icons.REFRESH}
            spinningName={icons.REFRESH}
            isSpinning={isFetching}
            onPress={handleRefreshPress}
          />
        </PageToolbarSection>
      </PageToolbar>

      <PageContentBody>
        <FieldSet legend={translate('History')}>
          {isFetching && !isPopulated && <LoadingIndicator />}

          {error && (
            <Alert kind={kinds.DANGER}>
              {translate('HistoryLoadError')}
            </Alert>
          )}

          {isPopulated && !error && !items.length && (
            <Alert kind={kinds.INFO}>
              {translate('NoHistoryFound')}
            </Alert>
          )}

          {isPopulated && !error && !!items.length && (
            <div>
              <Table columns={columns} {...otherProps}>
                <TableBody>
                  {items.map((item) => (
                    <HistoryTableRow
                      key={item.id}
                      columns={columns}
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

        <MetadataHistorySection refreshNonce={opsHistoryNonce} />
        <OpsHistorySection
          refreshNonce={opsHistoryNonce}
          apiPath="/file-ops-history"
          legendKey="FileOpsHistorySection"
          emptyKey="NoFileOpsHistoryFound"
        />
        <OpsHistorySection
          refreshNonce={opsHistoryNonce}
          apiPath="/db-ops-history"
          legendKey="DbOpsHistorySection"
          emptyKey="NoDbOpsHistoryFound"
        />
      </PageContentBody>
    </PageContent>
  );
}

HistoryPage.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  columns: PropTypes.array.isRequired,
  totalRecords: PropTypes.number,
  onRefreshPress: PropTypes.func.isRequired
};

export default HistoryPage;
