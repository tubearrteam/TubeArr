import React, { useCallback, useState } from 'react';
import ChannelMonitoringOptionsPopoverContent from 'AddChannel/ChannelMonitoringOptionsPopoverContent';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import ButtonGroup from 'Components/Link/ButtonGroup';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import { icons, inputTypes, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './ChangeMonitoringModalContent.css';

const NO_CHANGE = 'noChange';

interface ChangeMonitoringModalContentProps {
  channelIds: number[];
  saveError?: object;
  onSavePress(monitor: string, roundRobinLatestVideoCount?: number): void;
  onModalClose(): void;
}

function ChangeMonitoringModalContent(
  props: ChangeMonitoringModalContentProps
) {
  const { channelIds, onSavePress, onModalClose, ...otherProps } = props;

  const [monitor, setMonitor] = useState(NO_CHANGE);
  const [roundRobinLatestVideoCount, setRoundRobinLatestVideoCount] = useState('');

  const onMonitorChange = useCallback(({ value }: { name?: string; value: string }) => {
    setMonitor(value);
  }, []);

  const onRoundRobinChange = useCallback(({ value }: { name?: string; value: string | number }) => {
    setRoundRobinLatestVideoCount(String(value ?? ''));
  }, []);

  const roundRobinCap = parseInt(roundRobinLatestVideoCount.trim(), 10);
  const roundRobinInvalid =
    monitor === 'roundRobin' &&
    (!Number.isFinite(roundRobinCap) || roundRobinCap <= 0);

  const onSavePressWrapper = useCallback(() => {
    if (monitor === 'roundRobin') {
      onSavePress(monitor, roundRobinCap);
      return;
    }
    onSavePress(monitor);
  }, [monitor, onSavePress, roundRobinCap]);

  const selectedCount = channelIds.length;
  const saveDisabled = monitor === NO_CHANGE || roundRobinInvalid;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('MonitorChannel')}</ModalHeader>

      <ModalBody>
        <Form {...otherProps}>
          <FormGroup>
            <FormLabel>
              {translate('Monitoring')}

              <Popover
                anchor={<Icon className={styles.labelIcon} name={icons.INFO} />}
                title={translate('MonitoringOptions')}
                body={<ChannelMonitoringOptionsPopoverContent />}
                position={tooltipPositions.RIGHT}
              />
            </FormLabel>

            <FormInputGroup
              type={inputTypes.MONITOR_VIDEOS_SELECT}
              name="monitor"
              value={monitor}
              includeNoChange={true}
              onChange={onMonitorChange}
            />
          </FormGroup>

          {
            monitor === 'roundRobin' ?
              <FormGroup>
                <FormLabel>
                  {translate('RoundRobinMonitoringLatestCount')}
                  <Popover
                    anchor={<Icon className={styles.labelIcon} name={icons.INFO} />}
                    title={translate('RoundRobinMonitoring')}
                    body={translate('RoundRobinMonitoringHelpText')}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.NUMBER}
                  name="roundRobinLatestVideoCount"
                  value={roundRobinLatestVideoCount}
                  min={1}
                  onChange={onRoundRobinChange}
                  helpText={translate('RoundRobinMonitoringLatestCountHelp')}
                />
              </FormGroup> :
              null
          }
        </Form>
      </ModalBody>

      <ModalFooter className={styles.modalFooter}>
        <div className={styles.selected}>
          {translate('CountChannelSelected', { count: selectedCount })}
        </div>

        <ButtonGroup>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <Button
            isDisabled={saveDisabled}
            onPress={onSavePressWrapper}
          >
            {translate('Save')}
          </Button>
        </ButtonGroup>
      </ModalFooter>
    </ModalContent>
  );
}

export default ChangeMonitoringModalContent;
