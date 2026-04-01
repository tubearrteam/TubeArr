import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Button from 'Components/Link/Button';
import FormGroup from 'Components/Form/FormGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import { icons, inputTypes } from 'Helpers/Props';
import FormInputGroup from 'Components/Form/FormInputGroup';
import type { ChannelCustomPlaylist } from 'Channel/Channel';
import styles from './EditChannelModalContent.css';

export type CustomPlaylistDraftRule = {
  field: string;
  operator: string;
  value: string;
};

export type CustomPlaylistDraft = {
  id?: number;
  name: string;
  enabled: boolean;
  priority: number;
  matchType: number;
  rules: CustomPlaylistDraftRule[];
};

const ruleFields = [
  'title',
  'description',
  'sourcePlaylistId',
  'sourcePlaylistName',
  'publishedAt',
  'durationSeconds',
];

const ruleOperators = [
  'contains',
  'notContains',
  'equals',
  'notEquals',
  'startsWith',
  'endsWith',
  'in',
  'notIn',
  'gt',
  'gte',
  'lt',
  'lte',
];

function ruleValueToString(value: unknown): string {
  if (value === undefined || value === null) {
    return '';
  }
  if (typeof value === 'string') {
    return value;
  }
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

function fromApi(cp: ChannelCustomPlaylist): CustomPlaylistDraft {
  return {
    id: cp.id,
    name: cp.name,
    enabled: cp.enabled,
    priority: cp.priority,
    matchType: cp.matchType,
    rules: (cp.rules ?? []).map((r) => ({
      field: r.field,
      operator: r.operator,
      value: ruleValueToString(r.value),
    })),
  };
}

function parseRuleValue(raw: string, operator: string, field: string): unknown {
  const t = raw.trim();
  if (t === '') {
    return null;
  }
  if (operator === 'in' || operator === 'notIn') {
    if (t.startsWith('[')) {
      try {
        return JSON.parse(t);
      } catch {
        return t.split(',').map((s) => s.trim()).filter(Boolean);
      }
    }
    return t.split(',').map((s) => s.trim()).filter(Boolean);
  }
  if (field === 'durationSeconds') {
    const n = Number(t);
    return Number.isFinite(n) ? n : t;
  }
  if (field === 'publishedAt') {
    return t;
  }
  const num = Number(t);
  if (Number.isFinite(num) && String(num) === t && !t.includes('-')) {
    return num;
  }
  return t;
}

export function toSaveCustomPlaylistsPayload(drafts: CustomPlaylistDraft[]) {
  return drafts.map((d) => ({
    id: d.id != null && d.id > 0 ? d.id : null,
    name: d.name.trim(),
    enabled: d.enabled,
    priority: Number.isFinite(d.priority) ? d.priority : 0,
    matchType: d.matchType === 1 ? 1 : 0,
    rules: d.rules.map((r) => {
      const op = r.operator.trim();
      const field = r.field.trim();
      const v = parseRuleValue(r.value, op, field);
      return {
        field,
        operator: op,
        value: v,
      };
    }),
  }));
}

export interface ChannelEditCustomPlaylistsProps {
  translate: (key: string, tokens?: Record<string, string | number>) => string;
  source: ChannelCustomPlaylist[] | undefined;
  onDraftChange: (drafts: CustomPlaylistDraft[]) => void;
  /** When true, section is shown inside a dedicated modal (no top border / extra margin). */
  embeddedInModal?: boolean;
}

export default function ChannelEditCustomPlaylists({
  translate,
  source,
  onDraftChange,
  embeddedInModal = false,
}: ChannelEditCustomPlaylistsProps) {
  const [drafts, setDrafts] = useState<CustomPlaylistDraft[]>([]);

  const normalizedSource = useMemo(
    () => (Array.isArray(source) ? source.map(fromApi) : []),
    [source]
  );

  useEffect(() => {
    setDrafts(normalizedSource);
  }, [normalizedSource]);

  useEffect(() => {
    onDraftChange(drafts);
  }, [drafts, onDraftChange]);

  const notify = useCallback((next: CustomPlaylistDraft[]) => {
    setDrafts(next);
  }, []);

  const addPlaylist = useCallback(() => {
    notify([
      ...drafts,
      {
        name: translate('CustomPlaylistNewName'),
        enabled: true,
        priority: drafts.length,
        matchType: 0,
        rules: [],
      },
    ]);
  }, [drafts, notify, translate]);

  const removePlaylist = useCallback(
    (index: number) => {
      notify(drafts.filter((_, i) => i !== index));
    },
    [drafts, notify]
  );

  const updatePlaylist = useCallback(
    (index: number, patch: Partial<CustomPlaylistDraft>) => {
      const next = drafts.map((d, i) => (i === index ? { ...d, ...patch } : d));
      notify(next);
    },
    [drafts, notify]
  );

  const addRule = useCallback(
    (pi: number) => {
      const pl = drafts[pi];
      if (!pl || pl.rules.length >= 5) {
        return;
      }
      const next = drafts.map((d, i) =>
        i === pi
          ? {
              ...d,
              rules: [...d.rules, { field: 'title', operator: 'contains', value: '' }],
            }
          : d
      );
      notify(next);
    },
    [drafts, notify]
  );

  const updateRule = useCallback(
    (pi: number, ri: number, patch: Partial<CustomPlaylistDraftRule>) => {
      const pl = drafts[pi];
      if (!pl) {
        return;
      }
      const rules = pl.rules.map((r, j) => (j === ri ? { ...r, ...patch } : r));
      updatePlaylist(pi, { rules });
    },
    [drafts, updatePlaylist]
  );

  const removeRule = useCallback(
    (pi: number, ri: number) => {
      const pl = drafts[pi];
      if (!pl) {
        return;
      }
      updatePlaylist(pi, { rules: pl.rules.filter((_, j) => j !== ri) });
    },
    [drafts, updatePlaylist]
  );

  const fieldOptions = useMemo(
    () => ruleFields.map((f) => ({ key: f, value: translate(`CustomPlaylistField_${f}`) })),
    [translate]
  );

  const opOptions = useMemo(
    () => ruleOperators.map((o) => ({ key: o, value: translate(`CustomPlaylistOperator_${o}`) })),
    [translate]
  );

  const matchOptions = useMemo(
    () => [
      { key: 0, value: translate('CustomPlaylistMatchAll') },
      { key: 1, value: translate('CustomPlaylistMatchAny') },
    ],
    [translate]
  );

  return (
    <div
      className={
        embeddedInModal
          ? `${styles.customPlaylistsSection} ${styles.customPlaylistsInModal}`
          : styles.customPlaylistsSection
      }
    >
      <div className={styles.customPlaylistsHeader}>
        <FormLabel>{translate('CustomPlaylists')}</FormLabel>
        <Button kind="default" onPress={addPlaylist}>
          <Icon name={icons.ADD} />
          <span>{translate('AddCustomPlaylist')}</span>
        </Button>
      </div>

      <p className={styles.customPlaylistsHelp}>{translate('CustomPlaylistsHelp')}</p>

      {drafts.map((pl, pi) => (
        <div key={pl.id ?? `new-${pi}`} className={styles.customPlaylistCard}>
          <div className={styles.customPlaylistCardHeader}>
            <FormInputGroup
              type={inputTypes.TEXT}
              name={`customPlName-${pi}`}
              value={pl.name}
              placeholder={translate('Name')}
              onChange={({ value }: { value: string }) => updatePlaylist(pi, { name: value })}
            />
            <label className={styles.customPlaylistEnabled}>
              <input
                type="checkbox"
                checked={pl.enabled}
                onChange={(e) => updatePlaylist(pi, { enabled: e.target.checked })}
              />
              {translate('Enabled')}
            </label>
            <Button kind="danger" onPress={() => removePlaylist(pi)}>
              {translate('Remove')}
            </Button>
          </div>

          <div className={styles.customPlaylistRow}>
            <FormGroup>
              <FormLabel>{translate('Priority')}</FormLabel>
              <FormInputGroup
                type={inputTypes.TEXT}
                name={`customPlPriority-${pi}`}
                value={String(pl.priority)}
                onChange={({ value }: { value: string }) => {
                  const n = parseInt(String(value), 10);
                  updatePlaylist(pi, { priority: Number.isFinite(n) ? n : 0 });
                }}
              />
            </FormGroup>
            <FormGroup>
              <FormLabel>{translate('CustomPlaylistMatching')}</FormLabel>
              <FormInputGroup
                type={inputTypes.SELECT}
                name={`customPlMatch-${pi}`}
                values={matchOptions}
                value={pl.matchType}
                onChange={({ value }: { value: number }) => updatePlaylist(pi, { matchType: value === 1 ? 1 : 0 })}
              />
            </FormGroup>
          </div>

          <div className={styles.customPlaylistRules}>
            <div className={styles.customPlaylistRulesHeader}>
              <span>{translate('CustomPlaylistRules')}</span>
              <Button
                isDisabled={pl.rules.length >= 5}
                kind="default"
                onPress={() => addRule(pi)}
              >
                {translate('AddRule')}
              </Button>
            </div>
            {pl.rules.map((r, ri) => (
              <div key={ri} className={styles.customPlaylistRuleRow}>
                <FormInputGroup
                  type={inputTypes.SELECT}
                  name={`ruleField-${pi}-${ri}`}
                  values={fieldOptions}
                  value={r.field}
                  onChange={({ value }: { value: string }) => updateRule(pi, ri, { field: value })}
                />
                <FormInputGroup
                  type={inputTypes.SELECT}
                  name={`ruleOp-${pi}-${ri}`}
                  values={opOptions}
                  value={r.operator}
                  onChange={({ value }: { value: string }) => updateRule(pi, ri, { operator: value })}
                />
                <FormInputGroup
                  type={inputTypes.TEXT}
                  name={`ruleVal-${pi}-${ri}`}
                  value={r.value}
                  placeholder={translate('CustomPlaylistRuleValuePlaceholder')}
                  onChange={({ value }: { value: string }) => updateRule(pi, ri, { value })}
                />
                <Button kind="danger" onPress={() => removeRule(pi, ri)}>
                  ×
                </Button>
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
