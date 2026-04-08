import { ComponentType, lazy as reactLazy, LazyExoticComponent } from 'react';

/**
 * Like React.lazy, but triggers a full reload when webpack dev-server has emitted new chunks
 * while the browser still runs an old runtime (ChunkLoadError / stale dynamic import).
 */
export default function lazyWithChunkReload<T extends ComponentType<unknown>>(
  importFn: () => Promise<{ default: T }>
): LazyExoticComponent<T> {
  return reactLazy(() =>
    importFn().catch((error: unknown) => {
      const name = error instanceof Error ? error.name : '';
      const message = error instanceof Error ? error.message : String(error);
      if (
        name === 'ChunkLoadError' ||
        message.includes('Loading chunk') ||
        message.includes('Failed to fetch dynamically imported module')
      ) {
        window.location.reload();
        return new Promise<{ default: T }>(() => {});
      }
      throw error;
    })
  );
}
