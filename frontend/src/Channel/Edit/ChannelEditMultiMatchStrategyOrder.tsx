import React, { useCallback, useEffect, useState } from 'react';
import FormGroup from 'Components/Form/FormGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import { icons } from 'Helpers/Props';
import styles from './EditChannelModalContent.css';

const STRATEGY_IDS = [0, 1, 2, 3] as const;

const LABEL_KEYS = [
  'PlaylistMultiMatchLatestActivity',
  'PlaylistMultiMatchAlphabetical',
  'PlaylistMultiMatchNewestAdded',
  'PlaylistMultiMatchOldestAdded',
] as const;

export function derivePlaylistMultiMatchOrderFromLegacy(legacy: number): string {
  const first = legacy >= 0 && legacy <= 3 ? legacy : 0;
  const rest = [0, 1, 2, 3].filter((x) => x !== first);
  return `${first}${rest.join('')}`;
}

function parseOrderString(raw: string | undefined | null): number[] {
  const s = (raw ?? '').trim();
  if (s.length !== 4) {
    return [...STRATEGY_IDS];
  }
  const seen = new Set<number>();
  const out: number[] = [];
  for (let i = 0; i < s.length; i++) {
    const ch = s.charCodeAt(i) - 48;
    if (ch < 0 || ch > 3 || seen.has(ch)) {
      return [...STRATEGY_IDS];
    }
    seen.add(ch);
    out.push(ch);
  }
  return seen.size === 4 ? out : [...STRATEGY_IDS];
}

function serializeOrder(ids: readonly number[]): string {
  return ids.map((n) => String(n)).join('');
}

export interface ChannelEditMultiMatchStrategyOrderProps {
  channelId: number;
  orderString: string;
  translate: (key: string, tokens?: Record<string, string | number>) => string;
  onOrderChange: (serialized: string, legacyPrimary: number) => void;
}

export default function ChannelEditMultiMatchStrategyOrder({
  channelId,
  orderString,
  translate,
  onOrderChange,
}: ChannelEditMultiMatchStrategyOrderProps) {
  const [dragIndex, setDragIndex] = useState<number | null>(null);

  const [localIds, setLocalIds] = useState<number[]>(() => parseOrderString(orderString));

  useEffect(() => {
    setLocalIds(parseOrderString(orderString));
  }, [channelId, orderString]);

  const applyReorder = useCallback(
    (from: number, to: number) => {
      if (from === to || from < 0 || to < 0) {
        return;
      }
      setLocalIds((prev) => {
        const next = [...prev];
        const [removed] = next.splice(from, 1);
        next.splice(to, 0, removed);
        const ser = serializeOrder(next);
        onOrderChange(ser, next[0] ?? 0);
        return next;
      });
    },
    [onOrderChange]
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

  return (
    <FormGroup>
      <FormLabel>{translate('PlaylistMultiMatchStrategy')}</FormLabel>
      <p className={styles.customPlaylistsHelp}>
        {translate('PlaylistMultiMatchStrategyDragHelp')}
      </p>
      <ul className={styles.strategyOrderList}>
        {localIds.map((sid, index) => (
          <li
            key={sid}
            className={
              dragIndex === index
                ? `${styles.strategyOrderRow} ${styles.strategyOrderRowDragging}`
                : styles.strategyOrderRow
            }
            draggable
            onDragStart={onDragStart(index)}
            onDragOver={onDragOver}
            onDrop={onDrop(index)}
            onDragEnd={onDragEnd}
          >
            <span className={styles.strategyOrderHandle} aria-hidden>
              <Icon name={icons.REORDER} />
            </span>
            <span className={styles.strategyOrderLabel}>
              {translate(LABEL_KEYS[sid])}
            </span>
          </li>
        ))}
      </ul>
      <p className={styles.customPlaylistsHelp}>{translate('PlaylistMultiMatchStrategyHelpText')}</p>
    </FormGroup>
  );
}
