import React, { Children, ReactElement, ReactNode } from 'react';
import { Route, Routes } from 'react-router-dom';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';

interface ExtendedRoute {
  path: string;
  addUrlBase?: boolean;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  component?: React.ComponentType<any>;
}

interface SwitchProps {
  children: ReactNode;
}

function Switch({ children }: SwitchProps) {
  return (
    <Routes>
      {Children.map(children, (child) => {
        if (!React.isValidElement<ExtendedRoute>(child)) {
          return child;
        }

        const elementChild: ReactElement<ExtendedRoute> = child;

        const {
          path: childPath,
          addUrlBase = true,
          component: Component
        } = elementChild.props;

        if (!childPath || !Component) {
          return child;
        }

        const path = addUrlBase ? getPathWithUrlBase(childPath) : childPath;

        return (
          <Route
            key={path}
            path={path}
            element={<Component />}
          />
        );
      })}
    </Routes>
  );
}

export default Switch;
