import { get } from 'lodash';
import monitorOptions from 'Utilities/Channel/monitorOptions';

export default function migrateAddChannelDefaults(persistedState) {
  const monitor = get(persistedState, 'addChannel.defaults.monitor');

  if (!monitor) {
    return;
  }

  if (!monitorOptions.find((option) => option.key === monitor)) {
    persistedState.addChannel.defaults.monitor = monitorOptions[0].key;
  }
}
