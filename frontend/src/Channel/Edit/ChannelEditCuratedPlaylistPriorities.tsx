import React, { useCallback, useEffect, useMemo, useState } from 'react';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import type { Playlist } from 'Channel/Channel';
import { icons } from 'Helpers/Props';
import styles from './EditChannelModalContent.css';

export interface ChannelEditCuratedPlaylistPrioritiesProps {
  channelId: number;
  playlists: Playlist[];
  translate: (key: string, tokens?: Record<string, string | number>) => string;
  onEditsChange: (edits: Record<string, number>) => void;
}

/** Stable key: native YouTube playlist vs custom rule playlist (IDs are in different tables). */
export function playlistPriorityRowKey(p: Playlist): string | null {
  if (p.playlistNumber <= 1) {
    return null;
  }
  if (p.isCustom && p.customPlaylistId != null && p.customPlaylistId > 0) {
    return `c:${p.customPlaylistId}`;
  }
  if (!p.isCustom && p.playlistId != null && p.playlistId > 0) {
    return `yt:${p.playlistId}`;
  }
  return null;
}

function sortPlaylistRows(playlists: Playlist[]): Playlist[] {
  const rows = (playlists ?? []).filter((p) => playlistPriorityRowKey(p) != null) as Playlist[];
  return [...rows].sort((a, b) => {
    const pa = a.priority ?? 0;
    const pb = b.priority ?? 0;
    if (pa !== pb) {
      return pa - pb;
    }
    if (Boolean(a.isCustom) !== Boolean(b.isCustom)) {
      return a.isCustom ? 1 : -1;
    }
    return (a.title ?? '').localeCompare(b.title ?? '', undefined, { sensitivity: 'base' });
  });
}

export default function ChannelEditCuratedPlaylistPriorities({
  channelId,
  playlists,
  translate,
  onEditsChange,
}: ChannelEditCuratedPlaylistPrioritiesProps) {
  const rows = useMemo(() => sortPlaylistRows(playlists ?? []), [playlists]);

  const [orderedKeys, setOrderedKeys] = useState<string[]>([]);
  const [dragIndex, setDragIndex] = useState<number | null>(null);

  useEffect(() => {
    setOrderedKeys(rows.map((r) => playlistPriorityRowKey(r)!).filter(Boolean));
  }, [channelId, rows]);

  const pushEdits = useCallback(
    (nextKeys: string[]) => {
      const edits: Record<string, number> = {};
      nextKeys.forEach((key, index) => {
        edits[key] = index;
      });
      onEditsChange(edits);
    },
    [onEditsChange]
  );

  const applyReorder = useCallback(
    (from: number, to: number) => {
      if (from === to || from < 0 || to < 0) {
        return;
      }
      setOrderedKeys((prev) => {
        const next = [...prev];
        const [removed] = next.splice(from, 1);
        next.splice(to, 0, removed);
        pushEdits(next);
        return next;
      });
    },
    [pushEdits]
  );

  const onDragStart = useCallback((index: number) => (e: React.DragEvent) => {
    setDragIndex(index);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', String(index));
  }, []);

  const onDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (dropIndex: number) => (e: React.DragEvent) => {
      e.preventDefault();
      const from = parseInt(e.dataTransfer.getData('text/plain'), 10);
      setDragIndex(null);
      if (!Number.isFinite(from)) {
        return;
      }
      applyReorder(from, dropIndex);
    },
    [applyReorder]
  );

  const onDragEnd = useCallback(() => {
    setDragIndex(null);
  }, []);

  const keyToRow = useMemo(() => {
    const m = new Map<string, Playlist>();
    rows.forEach((r) => {
      const k = playlistPriorityRowKey(r);
      if (k) {
        m.set(k, r);
      }
    });
    return m;
  }, [rows]);

  if (rows.length === 0) {
    return null;
  }

  return (
    <div className={styles.curatedPlaylistPrioritiesSection}>
      <FormLabel>{translate('CuratedPlaylistPriority')}</FormLabel>
      <p className={styles.customPlaylistsHelp}>{translate('CuratedPlaylistOrderHelp')}</p>
      <ul className={styles.curatedPlaylistDragList}>
        {orderedKeys.map((key, index) => {
          const p = keyToRow.get(key);
          if (!p) {
            return null;
          }
          const isCustom = Boolean(p.isCustom);
          return (
            <li
              key={key}
              className={
                dragIndex === index
                  ? `${styles.curatedPlaylistDragRow} ${styles.curatedPlaylistDragRowDragging}`
                  : styles.curatedPlaylistDragRow
              }
              draggable
              onDragStart={onDragStart(index)}
              onDragOver={onDragOver}
              onDrop={onDrop(index)}
              onDragEnd={onDragEnd}
            >
              <span className={styles.curatedPlaylistDragHandle} aria-hidden>
                <Icon name={icons.REORDER} />
              </span>
              <span className={styles.curatedPlaylistDragTitle}>{p.title ?? ''}</span>
              {isCustom ? (
                <span className={styles.curatedPlaylistKindTag}>{translate('PlaylistKindCustom')}</span>
              ) : null}
            </li>
          );
        })}
      </ul>
    </div>
  );
}

export function buildPlaylistsPayloadForSave(
  playlists: Playlist[] | undefined,
  priorityEdits: Record<string, number>
) {
  return (playlists ?? []).map((p) => {
    const key = p.isCustom
      ? `c:${p.customPlaylistId ?? ''}`
      : `yt:${p.playlistId ?? ''}`;
    const priority =
      priorityEdits[key] !== undefined ? priorityEdits[key]! : (p.priority ?? 0);
    return {
      playlistNumber: p.playlistNumber,
      monitored: p.monitored,
      isCustom: p.isCustom ?? false,
      customPlaylistId: p.customPlaylistId ?? null,
      playlistId: p.playlistId ?? null,
      priority,
    };
  });
}
