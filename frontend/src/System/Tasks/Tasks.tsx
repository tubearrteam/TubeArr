import React from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import translate from 'Utilities/String/translate';
import ScheduledTaskHistory from './Scheduled/ScheduledTaskHistory';
import ScheduledTasks from './Scheduled/ScheduledTasks';

function Tasks() {
  return (
    <PageContent title={translate('Tasks')}>
      <PageContentBody>
        <ScheduledTasks />
        <ScheduledTaskHistory />
      </PageContentBody>
    </PageContent>
  );
}

export default Tasks;
