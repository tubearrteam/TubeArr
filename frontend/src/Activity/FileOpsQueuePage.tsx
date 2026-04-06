import React, { useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import { icons, kinds } from 'Helpers/Props';
import { cancelCommand, fetchCommands } from 'Store/Actions/commandActions';
import QueuedTasks from 'System/Tasks/Queued/QueuedTasks';
import translate from 'Utilities/String/translate';
import { isActiveFileOpsQueueItem } from 'Utilities/Command/fileOpsQueueFilter';
import Command from 'Commands/Command';

const columns = [
  {
    name: 'trigger',
    label: '',
    isVisible: true,
  },
  {
    name: 'commandName',
    label: () => translate('Name'),
    isVisible: true,
  },
  {
    name: 'acquisitionMethods',
    label: () => translate('Method'),
    isVisible: true,
  },
  {
    name: 'started',
    label: () => translate('Started'),
    isVisible: true,
  },
  {
    name: 'duration',
    label: () => translate('Duration'),
    isVisible: true,
  },
  {
    name: 'actions',
    isVisible: true,
  },
];

function isClearableFileOpsQueueItem(item: Command) {
  return isActiveFileOpsQueueItem(item);
}

export default function FileOpsQueuePage() {
  const dispatch = useDispatch();
  const isFetching = useSelector((state: AppState) => state.commands.isFetching);
  const commandItems = useSelector((state: AppState) => state.commands.items ?? []);
  const title = translate('FileOperationsQueue');
  const [isClearConfirmOpen, setIsClearConfirmOpen] = useState(false);
  const [isClearing, setIsClearing] = useState(false);

  const clearableCommandIds = useMemo(() => {
    return commandItems.filter(isClearableFileOpsQueueItem).map((item) => item.id);
  }, [commandItems]);

  const handleClearQueueConfirm = async () => {
    if (!clearableCommandIds.length) {
      setIsClearConfirmOpen(false);
      return;
    }

    setIsClearing(true);

    try {
      await Promise.all(clearableCommandIds.map((id) => dispatch(cancelCommand({ id }))));
    } finally {
      setIsClearing(false);
      setIsClearConfirmOpen(false);
    }
  };

  return (
    <PageContent title={title}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label={translate('Refresh')}
            iconName={icons.REFRESH}
            spinningName={icons.REFRESH}
            isSpinning={isFetching}
            onPress={() => dispatch(fetchCommands())}
          />

          {clearableCommandIds.length > 0 && (
            <PageToolbarButton
              label={translate('ClearQueue')}
              iconName={icons.CLEAR}
              isSpinning={isClearing}
              onPress={() => setIsClearConfirmOpen(true)}
            />
          )}
        </PageToolbarSection>
      </PageToolbar>

      <ConfirmModal
        isOpen={isClearConfirmOpen}
        kind={kinds.DANGER}
        title={translate('ClearQueue')}
        message={translate('ClearFileOpsQueueMessage')}
        confirmLabel={translate('ClearQueue')}
        onConfirm={handleClearQueueConfirm}
        onCancel={() => setIsClearConfirmOpen(false)}
      />

      <PageContentBody>
        <QueuedTasks
          legend={title}
          emptyMessage={translate('QueueIsEmpty')}
          filterPredicate={isActiveFileOpsQueueItem}
          columns={columns}
          rowVariant="metadata"
        />
      </PageContentBody>
    </PageContent>
  );
}
