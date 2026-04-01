import classNames from 'classnames';
import { throttle } from 'lodash';
import React, {
  ComponentProps,
  ForwardedRef,
  forwardRef,
  MutableRefObject,
  ReactNode,
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
} from 'react';
import { ScrollDirection } from 'Helpers/Props/scrollDirections';
import styles from './Scroller.css';

export interface OnScroll {
  scrollLeft: number;
  scrollTop: number;
}

interface ScrollerProps {
  className?: string;
  scrollDirection?: ScrollDirection;
  autoFocus?: boolean;
  autoScroll?: boolean;
  scrollTop?: number;
  initialScrollTop?: number;
  children?: ReactNode;
  style?: ComponentProps<'div'>['style'];
  onScroll?: (payload: OnScroll) => void;
}

function assignRef<T>(ref: ForwardedRef<T>, value: T | null) {
  if (ref == null) {
    return;
  }
  if (typeof ref === 'function') {
    ref(value);
  } else {
    (ref as MutableRefObject<T | null>).current = value;
  }
}

const Scroller = forwardRef<HTMLDivElement, ScrollerProps>(function Scroller(props, forwardedRef) {
  const {
    className,
    autoFocus = false,
    autoScroll = true,
    scrollDirection = 'vertical',
    children,
    scrollTop,
    initialScrollTop,
    onScroll,
    ...otherProps
  } = props;

  const internalRef = useRef<HTMLDivElement | null>(null);
  const forwardedRefLatest = useRef(forwardedRef);
  forwardedRefLatest.current = forwardedRef;

  const setDivRef = useCallback((node: HTMLDivElement | null) => {
    internalRef.current = node;
    assignRef(forwardedRefLatest.current, node);
  }, []);

  useLayoutEffect(() => {
    const el = internalRef.current;
    if (el != null && initialScrollTop != null) {
      el.scrollTop = initialScrollTop;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- run once on mount when node exists
  }, []);

  useEffect(() => {
    const el = internalRef.current;
    if (el == null) {
      return;
    }
    if (scrollTop != null) {
      el.scrollTop = scrollTop;
    }

    if (autoFocus && scrollDirection !== 'none') {
      el.focus({ preventScroll: true });
    }
  }, [autoFocus, scrollDirection, scrollTop]);

  useLayoutEffect(() => {
    const div = internalRef.current;
    if (div == null) {
      return;
    }

    const handleScroll = throttle(() => {
      onScroll?.({ scrollLeft: div.scrollLeft, scrollTop: div.scrollTop });
    }, 10);

    div.addEventListener('scroll', handleScroll);

    return () => {
      handleScroll.cancel();
      div.removeEventListener('scroll', handleScroll);
    };
  }, [onScroll]);

  return (
    <div
      {...otherProps}
      ref={setDivRef}
      className={classNames(
        className,
        styles.scroller,
        styles[scrollDirection],
        autoScroll && styles.autoScroll
      )}
      tabIndex={-1}
    >
      {children}
    </div>
  );
});

export default Scroller;
