import React, { useCallback, useEffect, useRef, useState } from 'react';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import { kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import { InputChanged } from 'typings/inputs';

interface PlexPinInputProps {
  name: string;
  value: string;
  onChange: (change: InputChanged<string> & { additionalProperties?: Record<string, unknown> }) => unknown;
}

export default function PlexPinInput({ name, onChange }: PlexPinInputProps) {
  const [code, setCode] = useState('');
  const [linkUrl, setLinkUrl] = useState('https://app.plex.tv/auth');
  const [status, setStatus] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const clearPoll = useCallback(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const pollPin = useCallback(
    (id: number, pinCode: string) => {
      clearPoll();
      timerRef.current = setInterval(() => {
        const { request } = createAjaxRequest({
          url: '/notification/action/checkPlexPin',
          method: 'POST',
          dataType: 'json',
          data: JSON.stringify({ pinId: id, code: pinCode }),
        });
        request.done((data: { authenticated?: boolean; authToken?: string }) => {
          if (data?.authenticated && data.authToken) {
            clearPoll();
            setBusy(false);
            setStatus(translate('NotificationsPlexPinSuccess'));
            onChange({
              name,
              value: 'linked',
              additionalProperties: { authToken: data.authToken },
            });
          }
        });
      }, 2500);
    },
    [clearPoll, name, onChange]
  );

  useEffect(() => () => clearPoll(), [clearPoll]);

  const startPin = useCallback(() => {
    setBusy(true);
    setStatus(translate('NotificationsPlexPinWaiting'));
    setCode('');
    clearPoll();
    const { request } = createAjaxRequest({
      url: '/notification/action/startPlexPin',
      method: 'POST',
      dataType: 'json',
      data: '{}',
    });
    request.done((data: { id?: number; code?: string; link?: string }) => {
      if (data?.id != null && data.code) {
        setCode(data.code);
        if (data.link) {
          setLinkUrl(data.link);
        }
        pollPin(data.id, data.code);
      } else {
        setBusy(false);
        setStatus(null);
      }
    });
    request.fail(() => {
      setBusy(false);
      setStatus(translate('NotificationsPlexPinStartFailed'));
    });
  }, [clearPoll, pollPin]);

  return (
    <div>
      <p className="help-text">{translate('NotificationsPlexPinHelpText')}</p>
      <Button kind={kinds.PRIMARY} isDisabled={busy} onPress={startPin}>
        {translate('NotificationsPlexPinStart')}
      </Button>
      {code ? (
        <div style={{ marginTop: '10px' }}>
          <div>
            <strong>{translate('NotificationsPlexPinCodeLabel')}</strong> {code}
          </div>
          <div style={{ marginTop: '6px' }}>
            <Link to={linkUrl}>{translate('NotificationsPlexPinOpenLink')}</Link>
          </div>
        </div>
      ) : null}
      {status ? <div style={{ marginTop: '8px' }}>{status}</div> : null}
    </div>
  );
}
