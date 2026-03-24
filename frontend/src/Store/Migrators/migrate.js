import migrateAddChannelDefaults from './migrateAddChannelDefaults';

export default function migrate(persistedState) {
  if (!persistedState) {
    return;
  }

  migrateAddChannelDefaults(persistedState);
}
