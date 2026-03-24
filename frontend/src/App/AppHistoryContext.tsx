import type { History } from 'history';
import React from 'react';

export const AppHistoryContext = React.createContext<History | null>(null);
