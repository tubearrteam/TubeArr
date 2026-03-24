import React from 'react';
import { useNavigationType } from 'react-router-dom';
import scrollPositions from 'Store/scrollPositions';

interface WrappedComponentProps {
  initialScrollTop: number;
}

function withScrollPosition(
  WrappedComponent: React.FC<WrappedComponentProps>,
  scrollPositionKey: string
) {
  function ScrollPosition(props: Record<string, unknown>) {
    const navigationType = useNavigationType();

    const initialScrollTop =
      navigationType === 'POP' ? scrollPositions[scrollPositionKey] : 0;

    return (
      <WrappedComponent
        {...props}
        initialScrollTop={initialScrollTop}
      />
    );
  }

  return ScrollPosition;
}

export default withScrollPosition;
