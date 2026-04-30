import classNames from 'classnames';
import React from 'react';
import styles from './ButtonGroup.css';

type Align = 'left' | 'right' | 'spaceBetween';

export interface ButtonGroupProps {
  className?: string;
  align?: Align;
  children: React.ReactNode;
}

export default function ButtonGroup({
  className,
  align = 'right',
  children,
}: ButtonGroupProps) {
  return (
    <div className={classNames(styles.group, styles[align], className)}>
      {children}
    </div>
  );
}

